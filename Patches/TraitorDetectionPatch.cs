using AutoIFF.Codebase;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
using System.Reflection;

namespace AutoIFF.Patches
{
    internal class TraitorDetectionPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return AccessTools.Method(typeof(BotsGroup), nameof(BotsGroup.AddEnemy),
                new[] { typeof(IPlayer), typeof(EBotEnemyCause) });
        }

        private static bool failureLogged;

        [PatchPrefix]
        private static void Prefix(BotsGroup __instance, IPlayer person, out bool __state)
        {
            __state = person != null && __instance.Enemies != null && __instance.Enemies.ContainsKey(person);
        }

        [PatchPostfix]
        private static void Postfix(BotsGroup __instance, IPlayer person, EBotEnemyCause cause, bool __result, bool __state)
        {
            if (!__result || __state) return;

            try
            {
                var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
                if (mainPlayer == null) return;
                if (mainPlayer.Side != EPlayerSide.Savage) return;
                if (!ReferenceEquals(person, mainPlayer)) return;

                WildSpawnType role = __instance.InitialBotType;
                bool scavGroup = IsScavRole(role);
                bool arrival = IsArrivalCause(cause);
                bool traitor = scavGroup && !arrival;
                Plugin.Log.LogDebug($"[AutoIFF] Group of {role} listed the player as an enemy (cause {cause}): "
                    + (traitor ? "counted as traitor alert." : scavGroup ? "ignored, hostile on arrival." : "ignored, not a Scav group."));

                if (traitor)
                    mainPlayer.GetComponent<IdentifierManager>()?.SetTraitor();
            }
            catch (System.Exception ex)
            {
                if (!failureLogged)
                {
                    failureLogged = true;
                    Plugin.Log.LogWarning($"[AutoIFF] Traitor check failed (logged once): {ex}");
                }
            }
        }

        private static bool IsScavRole(WildSpawnType role)
        {
            switch (role)
            {
                case WildSpawnType.assault:
                case WildSpawnType.assaultGroup:
                case WildSpawnType.marksman:
                case WildSpawnType.cursedAssault:
                case WildSpawnType.crazyAssaultEvent:
                    return true;
                default:
                    return false;
            }
        }
        private static bool IsArrivalCause(EBotEnemyCause cause)
        {
            switch (cause)
            {
                case EBotEnemyCause.initial:
                case EBotEnemyCause.AddNewMember:
                case EBotEnemyCause.addPlayer:
                case EBotEnemyCause.addPlayerToBoss:
                case EBotEnemyCause.addCauseGroup:
                    return true;
                default:
                    return false;
            }
        }
    }
}
