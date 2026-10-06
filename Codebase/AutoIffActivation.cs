using EFT;

namespace AutoIFF.Codebase
{
    public static class AutoIffActivation
    {
        public static void ActivateFor(Player player, string reason)
        {
            if (player == null)
                return;

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

            Plugin.Log.LogInfo($"[AutoIFF] Active as {(isScav ? "Scav" : "PMC")} ({reason}). Attention {attentionLevel}, Perception {perceptionLevel}, Search {searchLevel}.");
        }
    }
}
