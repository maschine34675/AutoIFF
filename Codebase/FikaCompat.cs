using System;
using System.Runtime.CompilerServices;
using EFT;
using Fika.Core.Main.Players;
using HarmonyLib;

namespace AutoIFF.Codebase
{
    internal static class FikaCompat
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void Probe()
        {
            if (AccessTools.Field(typeof(FikaPlayer), nameof(FikaPlayer.IsObservedAI)) == null)
                throw new MissingFieldException(nameof(FikaPlayer), nameof(FikaPlayer.IsObservedAI));
            if (AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyClientShot)) == null)
                throw new MissingMethodException(nameof(ObservedPlayer), nameof(ObservedPlayer.ApplyClientShot));
            if (AccessTools.Method(typeof(ObservedPlayer), nameof(ObservedPlayer.ApplyDamageInfo)) == null)
                throw new MissingMethodException(nameof(ObservedPlayer), nameof(ObservedPlayer.ApplyDamageInfo));
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool IsObserved(Player target)
        {
            return target is ObservedPlayer;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool TryClassify(Player localPlayer, Player target, bool selfTraitor, out ETargetStance stance, out string roleLabel)
        {
            stance = ETargetStance.Wary;
            roleLabel = null;

            if (!(target is ObservedPlayer observed))
                return false;

            if (!observed.IsObservedAI)
            {
                stance = ETargetStance.Friendly;
                string nickname = observed.Profile?.Info?.Nickname;
                roleLabel = nickname != null ? $"Player ({nickname})" : "Player";
                return true;
            }

            var settings = observed.Profile?.Info?.Settings;
            if (settings == null)
                return false;

            roleLabel = IdentifierManager.GetBotRoleLabel(observed);
            stance = localPlayer.Side == EPlayerSide.Savage
                ? ClassifyBotForScav(settings.Role, selfTraitor)
                : ClassifyBotForPmc(settings.Role, localPlayer.Side);
            return true;
        }
        private static ETargetStance ClassifyBotForPmc(WildSpawnType role, EPlayerSide localSide)
        {
            switch (role)
            {
                case WildSpawnType.gifter:
                case WildSpawnType.peacefullZryachiyEvent:
                    return ETargetStance.Friendly;
                case WildSpawnType.exUsec:
                    return localSide == EPlayerSide.Usec ? ETargetStance.Wary : ETargetStance.Hostile;

                case WildSpawnType.shooterBTR:
                    return ETargetStance.Wary;

                default:
                    return ETargetStance.Hostile;
            }
        }
        private static ETargetStance ClassifyBotForScav(WildSpawnType role, bool selfTraitor)
        {
            if (selfTraitor)
                return ETargetStance.Hostile;

            switch (role)
            {
                case WildSpawnType.assault:
                case WildSpawnType.assaultGroup:
                case WildSpawnType.cursedAssault:
                case WildSpawnType.marksman:
                case WildSpawnType.crazyAssaultEvent:
                case WildSpawnType.gifter:
                case WildSpawnType.peacefullZryachiyEvent:
                    return ETargetStance.Friendly;
                case WildSpawnType.sectantWarrior:
                case WildSpawnType.sectantPredvestnik:
                case WildSpawnType.sectantPrizrak:
                case WildSpawnType.sectantOni:
                case WildSpawnType.sectantPriest:
                case WildSpawnType.sectactPriestEvent:
                case WildSpawnType.bossKilla:
                case WildSpawnType.bossKillaAgro:
                case WildSpawnType.bossKojaniy:
                case WildSpawnType.followerBigPipe:
                case WildSpawnType.followerBirdEye:
                case WildSpawnType.bossZryachiy:
                case WildSpawnType.followerZryachiy:
                case WildSpawnType.ravangeZryachiyEvent:
                case WildSpawnType.infectedAssault:
                case WildSpawnType.infectedPmc:
                case WildSpawnType.infectedCivil:
                case WildSpawnType.infectedLaborant:
                case WildSpawnType.infectedTagilla:
                    return ETargetStance.Hostile;
                default:
                    return ETargetStance.Wary;
            }
        }
    }
}
