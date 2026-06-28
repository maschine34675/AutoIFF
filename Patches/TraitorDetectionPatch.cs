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

        [PatchPostfix]
        private static void Postfix(BotsGroup __instance, IPlayer person, bool __result)
        {
            if (!__result) return;
            if (__instance.Side != EPlayerSide.Savage) return;

            var mainPlayer = Singleton<GameWorld>.Instance?.MainPlayer;
            if (mainPlayer == null) return;
            if (mainPlayer.Side != EPlayerSide.Savage) return;
            if (!ReferenceEquals(person, mainPlayer)) return;

            mainPlayer.GetComponent<IdentifierManager>()?.SetTraitor();
        }
    }
}
