using AutoIFF.Codebase;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPT.Reflection.Patching;
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
            Plugin.StartWatchingLocalPlayer();

            Player player = Singleton<GameWorld>.Instance.MainPlayer;

            if (player == null)
            {
                Plugin.Log.LogInfo("[AutoIFF] No local player in this GameWorld (headless host?) — skipping activation.");
                return;
            }

            AutoIffActivation.ActivateFor(player, "raid started");
            Plugin.NoteLocalPlayerActivated(player);
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
            if (iPlayer == null || !iPlayer.IsYourPlayer) return;
            if (!RaidIsEnding())
            {
                Plugin.Log.LogInfo("[AutoIFF] Local player unregistered while the raid continues (respawn?) — waiting for the replacement.");
                return;
            }

            IdentifierManager.isRaidOver = true;
            Plugin.StopWatchingLocalPlayer();

            Player player = Singleton<GameWorld>.Instance?.MainPlayer;
            IdentifierManager manager = player?.GetComponent<IdentifierManager>();

            if (manager != null)
            {
                UnityEngine.Object.Destroy(manager);
            }

            Plugin.Log.LogInfo("[AutoIFF] Raid ended, IdentifierManager removal requested.");
        }
        private static bool RaidIsEnding()
        {
            var game = Singleton<AbstractGame>.Instantiated ? Singleton<AbstractGame>.Instance : null;
            if (game == null)
                return true;

            return game.Status == GameStatus.Stopping
                || game.Status == GameStatus.SoftStopping
                || game.Status == GameStatus.Stopped;
        }
    }
}
