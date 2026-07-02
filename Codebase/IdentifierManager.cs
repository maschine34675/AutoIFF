using Comfort.Common;
using EFT;
using EFT.CameraControl;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AutoIFF.Codebase
{
    public class IdentifierManager : MonoBehaviour
    {
        private Player player;
        private Camera playerCamera;

        private BotOwner currentTarget;
        private float identificationStartTime;
        private bool isIdentifying;
        private float lastSeenTime;

        private float durationCombined;
        private float distanceMultCombined;
        private float rangeCombined;
        private bool isAttentionElite;
        private bool isPerceptionElite;

        private readonly Dictionary<string, float> identifiedBots = new Dictionary<string, float>();

        private GUIStyle labelStyle;
        private string displayText = "";
        private Color displayColor = Color.white;

        private bool hotkeyActive;

        private float traitorAlertUntil;
        private int traitorAlertCount;
        private GUIStyle traitorStyle;

        private const float GracePeriod = 0.5f;
        private const float MemoryCleanupInterval = 30f;
        private float nextMemoryCleanup;

        public static bool isRaidOver;

        static readonly int LayerMaskBots = LayerMask.GetMask("Player", "Foliage", "HighPolyCollider", "Terrain")
                                            & ~(1 << LayerMask.NameToLayer("Ignore Raycast"));

        private void Awake()
        {
            player = Singleton<GameWorld>.Instance.MainPlayer;
            playerCamera = Singleton<PlayerCameraController>.Instance.Camera;

            labelStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = Color.white },
                alignment = TextAnchor.MiddleCenter
            };

            traitorStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = new Color(1f, 0.45f, 0f) },
                alignment = TextAnchor.MiddleCenter
            };

            if (player == null) Plugin.Log.LogError("[AutoIFF] MainPlayer is null on Awake.");
            if (playerCamera == null) Plugin.Log.LogError("[AutoIFF] PlayerCamera is null on Awake.");

            ReloadConfig();
            Plugin.Log.LogInfo("[AutoIFF] IdentifierManager attached.");
        }

        public void ReloadConfig()
        {
            durationCombined = Plugin.BaseIdentificationTime.Value;
            distanceMultCombined = Plugin.DistanceMultiplier.Value;
            rangeCombined = Plugin.IdentificationRange.Value;
        }

        public void ApplySkillScaling(int attentionLevel, int perceptionLevel, int searchLevel,
            bool attentionElite, bool perceptionElite, bool searchElite)
        {
            if (!Plugin.UseSkillScaling.Value) return;

            isAttentionElite = attentionElite;
            isPerceptionElite = perceptionElite;

            durationCombined = Plugin.BaseIdentificationTime.Value - (attentionLevel / 100f);
            durationCombined = Mathf.Max(0.05f, durationCombined);

            distanceMultCombined = Plugin.DistanceMultiplier.Value - (perceptionLevel / 750f);
            distanceMultCombined = Mathf.Max(0f, distanceMultCombined);

            rangeCombined = Plugin.IdentificationRange.Value + (searchLevel * 2f);
            if (searchElite) rangeCombined *= 1.5f;

            Plugin.Log.LogInfo($"[AutoIFF] Skill scaling applied — duration: {durationCombined:F2}s, distMult: {distanceMultCombined:F3}, range: {rangeCombined:F0}m");
        }

        private void Update()
        {
            if (isRaidOver || player == null || playerCamera == null) return;
            if (player.HandsController == null) { ResetIdentification(); return; }

            if (Plugin.ActivationMode.Value == EActivationMode.Hotkey)
            {
                if (Plugin.ActivationHotkey.Value.IsDown())
                    hotkeyActive = !hotkeyActive;
                if (!hotkeyActive) { ResetIdentification(); return; }
            }

            CleanupMemoryIfNeeded();

            if (!player.HandsController.IsAiming)
            {
                ResetIdentification();
                return;
            }

            var ray = new Ray(playerCamera.transform.position, AdjustedAimDirection());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMaskBots))
            {
                HandleNoHit();
                return;
            }

            GameObject hitObject = hit.collider.gameObject;
            BotOwner bot = hitObject.GetComponentInParent<BotOwner>();

            if (bot == null)
            {
                string layerName = LayerMask.LayerToName(hitObject.layer).ToLower();
                if (layerName == "foliage" && hit.distance < rangeCombined)
                {
                    displayText = "Obscured by foliage";
                    displayColor = Color.yellow;
                    labelStyle.fontSize = 16;
                    return;
                }
                HandleNoHit();
                return;
            }

            if (bot.IsDead)
            {
                HandleNoHit();
                return;
            }

            float distance = Vector3.Distance(playerCamera.transform.position, bot.Position);
            if (distance > rangeCombined)
            {
                displayText = $"Target too far ({distance:F0}m)";
                displayColor = Color.magenta;
                return;
            }

            LocalPlayer botPlayer = bot.GetComponent<LocalPlayer>();
            if (botPlayer == null)
            {
                HandleNoHit();
                return;
            }

            string botId = botPlayer.AccountId;
            lastSeenTime = Time.time;
            labelStyle.fontSize = 22;

            if (Plugin.FriendlyOnly.Value)
            {
                ShowFriendlyOnly(bot, distance);
                return;
            }

            if (identifiedBots.TryGetValue(botId, out float lastTime) &&
                Time.time - lastTime < Plugin.MemoryDuration.Value)
            {
                ShowIdentification(bot, distance);
                return;
            }

            if (currentTarget != bot)
            {
                currentTarget = bot;
                identificationStartTime = Time.time;
                isIdentifying = true;
                displayText = "Identifying...";
                displayColor = Color.white;
                return;
            }

            if (isIdentifying)
            {
                float required = CalcRequiredTime(distance);
                if (Time.time - identificationStartTime >= required)
                {
                    identifiedBots[botId] = Time.time;
                    ShowIdentification(bot, distance);
                    isIdentifying = false;
                }
            }
        }

        private void HandleNoHit()
        {
            if (Time.time - lastSeenTime > GracePeriod)
            {
                ResetIdentification();
                return;
            }
            displayText = "Losing target...";
            displayColor = Color.yellow;
        }

        private float CalcRequiredTime(float distance)
        {
            if (distance <= 15f)
            {
                if (isAttentionElite) return 0.01f;
                if (isPerceptionElite) return durationCombined / 2f + (distance * distanceMultCombined) / 12f;
                return (durationCombined + (distance * distanceMultCombined) / 6f) / 2f;
            }
            if (isPerceptionElite) return durationCombined + (distance * distanceMultCombined) / 12f;
            return durationCombined + (distance * distanceMultCombined) / 6f;
        }

        private void ShowIdentification(BotOwner bot, float distance)
        {
            var enemyInfos = bot.EnemiesController?.EnemyInfos;
            bool isHostile = enemyInfos != null && enemyInfos.ContainsKey(player);

            string role = GetBotRoleLabel(bot);
            string distLabel = Plugin.ShowDistance.Value ? $"  ({distance:F0}m)" : "";
            string roleLabel = Plugin.ShowBotRole.Value && role != null ? $"\n{role}" : "";

            displayText = (isHostile ? "Hostile" : "Friendly") + distLabel + roleLabel;
            displayColor = isHostile ? Color.red : Color.green;
        }

        private void ShowFriendlyOnly(BotOwner bot, float distance)
        {
            var enemyInfos = bot.EnemiesController?.EnemyInfos;
            bool isHostile = enemyInfos != null && enemyInfos.ContainsKey(player);

            if (isHostile)
            {
                ResetIdentification();
                return;
            }

            string role = GetBotRoleLabel(bot);
            string distLabel = Plugin.ShowDistance.Value ? $"  ({distance:F0}m)" : "";
            string roleLabel = Plugin.ShowBotRole.Value && role != null ? $"\n{role}" : "";

            displayText = "Friendly" + distLabel + roleLabel;
            displayColor = Color.green;
        }

        private static string GetBotRoleLabel(BotOwner bot)
        {
            var settings = bot.Profile?.Info?.Settings;
            if (settings == null) return null;

            switch (settings.Role)
            {
                case WildSpawnType.pmcBEAR: return "PMC (BEAR)";
                case WildSpawnType.pmcUSEC: return "PMC (USEC)";
                case WildSpawnType.pmcBot: return "Raider";
                case WildSpawnType.assault:
                case WildSpawnType.assaultGroup: return "Scav";
                case WildSpawnType.marksman: return "Sniper Scav";
                case WildSpawnType.cursedAssault: return "Scav (Cursed)";
                case WildSpawnType.crazyAssaultEvent: return "Scav (Event)";
                case WildSpawnType.exUsec: return "Rogue";
                case WildSpawnType.arenaFighter:
                case WildSpawnType.arenaFighterEvent: return "Arena Fighter";
                case WildSpawnType.sectantWarrior:
                case WildSpawnType.sectantPredvestnik:
                case WildSpawnType.sectantPrizrak:
                case WildSpawnType.sectantOni: return "Cultist";
                case WildSpawnType.sectantPriest: return "Cultist Priest";
                case WildSpawnType.sectactPriestEvent: return "Cultist Priest (Event)";
                case WildSpawnType.bossTest: return "Boss (Test)";
                case WildSpawnType.followerTest: return "Boss Follower (Test)";
                case WildSpawnType.test: return "Test Bot";
                case WildSpawnType.bossKilla:
                case WildSpawnType.bossKillaAgro: return "Boss (Killa)";
                case WildSpawnType.bossBully: return "Boss (Reshala)";
                case WildSpawnType.bossGluhar: return "Boss (Gluhar)";
                case WildSpawnType.bossSanitar: return "Boss (Sanitar)";
                case WildSpawnType.bossTagilla:
                case WildSpawnType.bossTagillaAgro:
                case WildSpawnType.tagillaHelperAgro: return "Boss (Tagilla)";
                case WildSpawnType.bossKnight: return "Boss (Knight)";
                case WildSpawnType.bossZryachiy: return "Boss (Zryachiy)";
                case WildSpawnType.peacefullZryachiyEvent: return "Boss (Zryachiy, Peaceful)";
                case WildSpawnType.ravangeZryachiyEvent: return "Boss (Zryachiy, Event)";
                case WildSpawnType.bossBoar: return "Boss (Kaban)";
                case WildSpawnType.bossBoarSniper: return "Kaban Sniper";
                case WildSpawnType.bossKojaniy: return "Boss (Shturman)";
                case WildSpawnType.bossKolontay: return "Boss (Kolontay)";
                case WildSpawnType.bossPartisan: return "Boss (Partisan)";
                case WildSpawnType.followerBully: return "Reshala Guard";
                case WildSpawnType.followerKojaniy: return "Shturman Guard";
                case WildSpawnType.followerGluharAssault:
                case WildSpawnType.followerGluharSecurity:
                case WildSpawnType.followerGluharScout:
                case WildSpawnType.followerGluharSnipe: return "Gluhar Guard";
                case WildSpawnType.followerSanitar: return "Sanitar Guard";
                case WildSpawnType.followerTagilla: return "Tagilla Guard";
                case WildSpawnType.followerBigPipe: return "Boss (Big Pipe)";
                case WildSpawnType.followerBirdEye: return "Boss (Birdeye)";
                case WildSpawnType.followerZryachiy: return "Zryachiy Guard";
                case WildSpawnType.followerBoar:
                case WildSpawnType.followerBoarClose1:
                case WildSpawnType.followerBoarClose2: return "Kaban Guard";
                case WildSpawnType.followerKolontayAssault:
                case WildSpawnType.followerKolontaySecurity: return "Kolontay Guard";
                case WildSpawnType.shooterBTR: return "BTR Gunner";
                case WildSpawnType.gifter: return "Santa";
                case WildSpawnType.spiritWinter:
                case WildSpawnType.spiritSpring: return "Spirit";
                case WildSpawnType.peacemaker: return "Peacemaker";
                case WildSpawnType.skier: return "Skier";
                case WildSpawnType.infectedAssault:
                case WildSpawnType.infectedPmc:
                case WildSpawnType.infectedCivil:
                case WildSpawnType.infectedLaborant:
                case WildSpawnType.infectedTagilla: return "Infected";
                default: return settings.Role.ToString();
            }
        }

        private Vector3 AdjustedAimDirection()
        {
            Vector3 aim = playerCamera.transform.forward;
            aim.y -= 0.0043f;
            return aim;
        }

        private void ResetIdentification()
        {
            currentTarget = null;
            isIdentifying = false;
            displayText = "";
        }

        public void SetTraitor()
        {
            traitorAlertCount++;
            traitorAlertUntil = Time.time + Plugin.TraitorAlertDuration.Value;
            Plugin.Log.LogInfo($"[AutoIFF] Scav traitor alert #{traitorAlertCount}.");
        }

        private void CleanupMemoryIfNeeded()
        {
            if (Time.time < nextMemoryCleanup) return;
            nextMemoryCleanup = Time.time + MemoryCleanupInterval;

            float expiryTime = Plugin.MemoryDuration.Value;
            var toRemove = new List<string>();
            foreach (var kv in identifiedBots)
            {
                if (Time.time - kv.Value >= expiryTime)
                    toRemove.Add(kv.Key);
            }
            foreach (var key in toRemove)
                identifiedBots.Remove(key);
        }

        private void OnGUI()
        {
            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            if (Plugin.ShowTraitorWarning.Value && Time.time < traitorAlertUntil)
            {
                string label = traitorAlertCount > 1
                    ? $"MARKED AS SCAV TRAITOR ×{traitorAlertCount}"
                    : "MARKED AS SCAV TRAITOR";
                float w = 240f;
                float h = 35f;
                float margin = 20f;
                GUI.Label(new Rect(Screen.width - w - margin, Screen.height - h - margin, w, h), label, traitorStyle);
            }

            if (!string.IsNullOrEmpty(displayText))
            {
                labelStyle.normal.textColor = displayColor;
                GUI.Label(new Rect(cx - 150f, cy + 100f, 300f, 60f), displayText, labelStyle);
            }
        }

        private void OnDestroy()
        {
            player = null;
            playerCamera = null;
            currentTarget = null;
            isRaidOver = true;
            hotkeyActive = false;
            traitorAlertUntil = 0f;
            traitorAlertCount = 0;
            identifiedBots.Clear();
            Plugin.Log.LogInfo("[AutoIFF] IdentifierManager destroyed.");
        }
    }
}
