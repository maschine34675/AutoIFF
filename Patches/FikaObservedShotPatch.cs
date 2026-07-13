using System;
using System.Reflection;
using AutoIFF.Codebase;
using Comfort.Common;
using EFT;
using Fika.Core.Main.Players;
using HarmonyLib;
using SPT.Reflection.Patching;

namespace AutoIFF.Patches
{
    internal static class FikaSelfTraitorDetector
    {
        public static void HandlePlayerDamage(ObservedPlayer victim, DamageInfoStruct damageInfo)
        {
            try
            {
                if (!victim.IsObservedAI) return;

                Player mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
                if (mainPlayer == null || mainPlayer.Side != EPlayerSide.Savage) return;
                if (!ReferenceEquals(damageInfo.Player?.iPlayer, mainPlayer)) return;

                var settings = victim.Profile?.Info?.Settings;
                if (settings == null || settings.Role.IsHostileToEverybody()) return;

                mainPlayer.GetComponent<IdentifierManager>()?.SetSelfTraitor();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogDebug($"[AutoIFF] Self-traitor check failed: {ex}");
            }
        }
    }

    internal class FikaObservedShotPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyClientShot));
        }

        [PatchPostfix]
        private static void Postfix(ObservedPlayer __instance, DamageInfoStruct damageInfo)
        {
            FikaSelfTraitorDetector.HandlePlayerDamage(__instance, damageInfo);
        }
    }

    internal class FikaObservedDamageInfoPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyDamageInfo));
        }
        [PatchPostfix]
        private static void Postfix(ObservedPlayer __instance, DamageInfoStruct DamageInfo)
        {
            FikaSelfTraitorDetector.HandlePlayerDamage(__instance, DamageInfo);
        }
    }
}
