using Comfort.Common;
using EFT;
using EFT.CameraControl;
using EFT.HealthSystem;
using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace AutoIFF.Codebase
{
    public enum ETargetStance
    {
        Friendly,
        Hostile,
        Wary
    }
    public struct TargetClassification
    {
        public ETargetStance Stance;
        public string RoleLabel;
        public string Nickname;
        public bool IsTeammate;
        public bool HasHealth;
        public float HealthCurrent;
        public float HealthMax;
    }

    public class IdentifierManager : MonoBehaviour
    {
        private Player player;
        private Camera playerCamera;

        private Player currentTarget;
        private Player lastLoggedTarget;
        private Player nameCacheTarget;
        private string nameCache;
        private static bool nameLookupWarned;
        private static bool healthLookupDisabled;
        private readonly GUIContent labelContent = new GUIContent();
        private float identificationStartTime;
        private bool isIdentifying;
        private float lastSeenTime;
        private bool selfTraitor;

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
            if (isRaidOver || player == null) return;
            if (!IsCurrentLocalPlayer()) { ResetIdentification(); return; }
            if (playerCamera == null)
            {
                playerCamera = Singleton<PlayerCameraController>.Instance?.Camera;
                if (playerCamera == null) return;
            }

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

            labelStyle.fontSize = 22;

            var ray = new Ray(playerCamera.transform.position, AdjustedAimDirection());
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, LayerMaskBots))
            {
                HandleNoHit();
                return;
            }

            GameObject hitObject = hit.collider.gameObject;
            Player target = hitObject.GetComponentInParent<Player>();

            if (target == null || ReferenceEquals(target, player))
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

            if (target.HealthController == null || !target.HealthController.IsAlive)
            {
                HandleNoHit();
                return;
            }

            float distance = Vector3.Distance(playerCamera.transform.position, target.Position);
            if (distance > rangeCombined)
            {
                displayText = Plugin.ShowDistance.Value
                    ? $"Target too far ({distance:F0}m)"
                    : "Target too far";
                displayColor = Color.magenta;
                return;
            }

            if (!CanClassify(target))
            {
                HandleNoHit();
                return;
            }

            string botId = target.ProfileId;
            lastSeenTime = Time.time;

            if (Plugin.FriendlyOnly.Value)
            {
                ShowFriendlyOnly(target, distance);
                return;
            }

            if (identifiedBots.TryGetValue(botId, out float lastTime) &&
                Time.time - lastTime < Plugin.MemoryDuration.Value)
            {
                ShowIdentification(target, distance);
                return;
            }

            if (currentTarget != target)
            {
                currentTarget = target;
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
                    ShowIdentification(target, distance);
                    isIdentifying = false;
                }
            }
        }
        private static BotOwner GetBotOwner(Player target)
        {
            return target.AIData?.BotOwner ?? target.GetComponent<BotOwner>();
        }

        private bool IsCurrentLocalPlayer()
        {
            var world = Singleton<GameWorld>.Instantiated ? Singleton<GameWorld>.Instance : null;
            return world != null
                && ReferenceEquals(world.MainPlayer, player)
                && player.HealthController != null
                && player.HealthController.IsAlive;
        }
        private void GetBotRelation(BotOwner bot, Player target, out bool ally, out bool hostile)
        {
            ally = false;
            bool groupEnemy = false;

            var group = bot.BotsGroup;
            if (group != null)
            {
                var allies = group.Allies;
                for (int i = 0; allies != null && i < allies.Count; i++)
                {
                    if (allies[i] != null && allies[i].Id == player.Id)
                    {
                        ally = true;
                        break;
                    }
                }

                groupEnemy = group.IsEnemy(player);
            }

            var enemyInfos = bot.EnemiesController?.EnemyInfos;
            bool botEnemy = enemyInfos != null && enemyInfos.ContainsKey(player);
            if (!ReferenceEquals(target, lastLoggedTarget))
            {
                lastLoggedTarget = target;
                Plugin.Log.LogDebug($"[AutoIFF] Stance for {target.ProfileId}: ally={ally} groupEnemy={groupEnemy} botEnemy={botEnemy}");
            }

            hostile = !ally && (groupEnemy || botEnemy);
        }
        private static string GetDisplayName(Player target)
        {
            var profile = target.Profile;
            var info = profile?.Info;
            if (info == null)
                return null;

            try
            {
                if (profile.Side == EPlayerSide.Savage && !string.IsNullOrEmpty(info.MainProfileNickname))
                    return info.MainProfileNickname;

                return profile.GetCorrectedNickname();
            }
            catch (Exception ex)
            {
                if (!nameLookupWarned)
                {
                    nameLookupWarned = true;
                    Plugin.Log.LogWarning($"[AutoIFF] Name lookup failed, falling back to the raw nickname: {ex.GetType().Name}");
                }
                return info.Nickname;
            }
        }

        private bool CanClassify(Player target)
        {
            if (GetBotOwner(target) != null) return true;
            return Plugin.FikaPresent && FikaCompat.IsObserved(target);
        }

        private bool TryClassify(Player target, out TargetClassification result)
        {
            result = default;
            bool known;

            BotOwner bot = GetBotOwner(target);
            if (bot != null)
            {
                GetBotRelation(bot, target, out bool ally, out bool hostile);
                result.Stance = hostile ? ETargetStance.Hostile : ETargetStance.Friendly;
                result.RoleLabel = GetBotRoleLabel(target);
                result.IsTeammate = ally;
                known = true;
            }
            else if (Plugin.FikaPresent)
            {
                known = FikaCompat.TryClassify(player, target, selfTraitor, out result);
            }
            else
            {
                result.Stance = ETargetStance.Wary;
                return false;
            }

            if (known)
            {
                result.Nickname = ResolveName(target, result.IsTeammate);
                if (Plugin.ShowTargetHealth.Value)
                    result.HasHealth = TryGetHealth(target, out result.HealthCurrent, out result.HealthMax);
            }
            return known;
        }
        private static bool TryGetHealth(Player target, out float current, out float max)
        {
            current = 0f;
            max = 0f;

            if (healthLookupDisabled)
                return false;

            var health = target.HealthController;
            if (health == null)
                return false;

            try
            {
                ValueStruct total = health.GetBodyPartHealth(EBodyPart.Common, true);
                current = total.Current;
                max = total.Maximum;
                return max > 0f;
            }
            catch (Exception ex)
            {
                healthLookupDisabled = true;
                Plugin.Log.LogWarning($"[AutoIFF] Health lookup failed, health stays hidden for this session: {ex.GetType().Name}");
                return false;
            }
        }
        private string ResolveName(Player target, bool isTeammate)
        {
            switch (Plugin.ShowBotName.Value)
            {
                case EBotNameDisplay.All:
                    break;
                case EBotNameDisplay.Teammates:
                    if (!isTeammate) return null;
                    break;
                default:
                    return null;
            }

            if (!ReferenceEquals(target, nameCacheTarget))
            {
                nameCacheTarget = target;
                nameCache = GetDisplayName(target);
            }

            return nameCache;
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

        private void ShowIdentification(Player target, float distance)
        {
            if (!TryClassify(target, out TargetClassification c))
            {
                HandleNoHit();
                return;
            }

            switch (c.Stance)
            {
                case ETargetStance.Hostile:
                    displayText = ComposeLabel("Hostile", distance, c);
                    displayColor = Color.red;
                    break;
                case ETargetStance.Wary:
                    displayText = ComposeLabel("Wary", distance, c);
                    displayColor = new Color(1f, 0.45f, 0f);
                    break;
                default:
                    displayText = ComposeLabel("Friendly", distance, c);
                    displayColor = Color.green;
                    break;
            }
        }

        private void ShowFriendlyOnly(Player target, float distance)
        {
            if (!TryClassify(target, out TargetClassification c) || c.Stance != ETargetStance.Friendly)
            {
                ResetIdentification();
                return;
            }

            displayText = ComposeLabel("Friendly", distance, c);
            displayColor = Color.green;
        }

        private static string ComposeLabel(string stanceText, float distance, in TargetClassification c)
        {
            string text = stanceText;
            if (Plugin.ShowDistance.Value)
                text += $"  ({distance:F0}m)";
            if (c.HasHealth)
                text += $"  HP {c.HealthCurrent:F0}/{c.HealthMax:F0}";
            if (Plugin.ShowBotRole.Value && c.RoleLabel != null)
                text += "\n" + c.RoleLabel;
            if (!string.IsNullOrEmpty(c.Nickname))
                text += "\n" + c.Nickname;
            return text;
        }

        internal static string GetBotRoleLabel(Player target)
        {
            var settings = target.Profile?.Info?.Settings;
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
        public void SetSelfTraitor()
        {
            if (selfTraitor) return;
            selfTraitor = true;
            SetTraitor();
            Plugin.Log.LogInfo("[AutoIFF] Local scav damaged an innocent scav — assuming traitor status (Fika client heuristic).");
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
            if (isRaidOver || player == null || !IsCurrentLocalPlayer()) return;

            float cx = Screen.width / 2f;
            float cy = Screen.height / 2f;

            if (Plugin.ShowTraitorWarning.Value && Time.time < traitorAlertUntil)
            {
                string label = traitorAlertCount > 1
                    ? $"MARKED AS SCAV TRAITOR ×{traitorAlertCount}"
                    : "MARKED AS SCAV TRAITOR";
                Vector2 size = traitorStyle.CalcSize(new GUIContent(label));
                float margin = 20f;
                GUI.Label(new Rect(Screen.width - size.x - margin, Screen.height - size.y - margin, size.x, size.y), label, traitorStyle);
            }

            if (!string.IsNullOrEmpty(displayText))
            {
                labelStyle.normal.textColor = displayColor;
                labelContent.text = displayText;
                Vector2 size = labelStyle.CalcSize(labelContent);
                float w = Mathf.Max(300f, size.x + 20f);
                float h = Mathf.Max(60f, size.y);
                GUI.Label(new Rect(cx - w / 2f, cy + 100f, w, h), labelContent, labelStyle);
            }
        }

        private void OnDestroy()
        {
            player = null;
            playerCamera = null;
            currentTarget = null;
            nameCacheTarget = null;
            nameCache = null;
            hotkeyActive = false;
            selfTraitor = false;
            traitorAlertUntil = 0f;
            traitorAlertCount = 0;
            identifiedBots.Clear();
            Plugin.Log.LogInfo("[AutoIFF] IdentifierManager destroyed.");
        }
    }
}
