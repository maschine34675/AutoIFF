using AutoIFF.Codebase;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Reflection.Utils;
using System.Reflection;

namespace AutoIFF.Patches
{
    internal class MatchStartedPatchLAI : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.OnGameStarted));
        }

        [PatchPostfix]
        private static void Postfix(GameWorld __instance)
        {
            if (__instance is HideoutGameWorld) return;
            if (__instance.LocationId?.ToLower() == "hideout") return;

            Player player = Singleton<GameWorld>.Instance.MainPlayer;

            bool isScav = player.Side == EPlayerSide.Savage;
            bool shouldActivate = Plugin.ActivationMode.Value switch
            {
                EActivationMode.AlwaysOn  => true,
                EActivationMode.AlwaysOff => false,
                EActivationMode.Hotkey    => true,
                _                         => isScav
            };

            if (!shouldActivate)
            {
                Plugin.Log.LogInfo($"[AutoIFF] Skipping activation (mode={Plugin.ActivationMode.Value}, side={player.Side}).");
                return;
            }

            IdentifierManager manager = player.GetOrAddComponent<IdentifierManager>();
            IdentifierManager.isRaidOver = false;
            manager.ReloadConfig();

            int attentionLevel = player.Skills.Attention.Level;
            int perceptionLevel = player.Skills.Perception.Level;
            int searchLevel = player.Skills.Search.Level;
            bool isAttentionElite = player.Skills.Attention.IsEliteLevel;
            bool isPerceptionElite = player.Skills.Perception.IsEliteLevel;
            bool isSearchElite = player.Skills.Search.IsEliteLevel;

            manager.ApplySkillScaling(attentionLevel, perceptionLevel, searchLevel, isAttentionElite, isPerceptionElite, isSearchElite);

            Plugin.Log.LogInfo($"[AutoIFF] Raid started as {(isScav ? "Scav" : "PMC")}. Attention {attentionLevel}, Perception {perceptionLevel}, Search {searchLevel}.");
        }
    }

    internal class MatchEndedPatchLAI : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(GameWorld), nameof(GameWorld.UnregisterPlayer));
        }

        [PatchPostfix]
        private static void Postfix(GameWorld __instance, ref IPlayer iPlayer)
        {
            if (__instance is HideoutGameWorld) return;
            if (__instance.LocationId?.ToLower() == "hideout") return;
            if (IdentifierManager.isRaidOver) return;

            string localProfileId = ClientAppUtils.GetClientApp().GetClientBackEndSession().Profile.ProfileId;
            if (iPlayer.ProfileId != localProfileId) return;

            IdentifierManager.isRaidOver = true;
            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            IdentifierManager manager = player?.GetComponent<IdentifierManager>();

            if (manager != null)
            {
                UnityEngine.Object.Destroy(manager);
            }

            Plugin.Log.LogInfo("[AutoIFF] Raid ended, IdentifierManager removal requested.");
        }
    }
}
