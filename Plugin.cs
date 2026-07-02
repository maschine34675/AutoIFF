using AutoIFF.Patches;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace AutoIFF
{
    public enum EActivationMode
    {
        Automatic,
        AlwaysOn,
        AlwaysOff,
        Hotkey
    }

    [BepInPlugin("com.maschine.AutoIFF", "maschine-AutoIFF", "1.1.0")]
    [BepInDependency("Light.LightsAutomaticIdentiier", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Log;

        public static ConfigEntry<EActivationMode> ActivationMode;
        public static ConfigEntry<KeyboardShortcut> ActivationHotkey;
        public static ConfigEntry<bool> FriendlyOnly;
        public static ConfigEntry<float> BaseIdentificationTime;
        public static ConfigEntry<float> IdentificationRange;
        public static ConfigEntry<float> DistanceMultiplier;
        public static ConfigEntry<float> MemoryDuration;
        public static ConfigEntry<bool> UseSkillScaling;
        public static ConfigEntry<bool> ShowDistance;
        public static ConfigEntry<bool> ShowBotRole;
        public static ConfigEntry<bool> ShowTraitorWarning;
        public static ConfigEntry<float> TraitorAlertDuration;

        private const string OldModGuid = "Light.LightsAutomaticIdentiier"; // typo is intentional — that's the original GUID

        private void Awake()
        {
            Log = Logger;

            if (Chainloader.PluginInfos.ContainsKey(OldModGuid))
            {
                Log.LogError("[AutoIFF] ════════════════════════════════════════════");
                Log.LogError("[AutoIFF] CONFLICT: Original LightsAutomaticIdentifier detected!");
                Log.LogError("[AutoIFF] Both mods running simultaneously will cause issues.");
                Log.LogError("[AutoIFF] Please remove the old DLL from BepInEx/plugins/.");
                Log.LogError("[AutoIFF] AutoIFF v1.1.0 has NOT been activated.");
                Log.LogError("[AutoIFF] ════════════════════════════════════════════");
                gameObject.AddComponent<ConflictWarningGui>();
                return;
            }

            ActivationMode = Config.Bind("General", "ActivationMode", EActivationMode.Automatic,
                "Automatic = only active when playing as Scav. AlwaysOn = active in every raid. AlwaysOff = disabled. Hotkey = toggle via keybind.");

            ActivationHotkey = Config.Bind("General", "ActivationHotkey", KeyboardShortcut.Empty,
                "Keybind to toggle the mod on/off when ActivationMode is set to Hotkey. Not assigned by default.");

            FriendlyOnly = Config.Bind("General", "FriendlyOnly", false,
                "Only show friendly targets with instant identification (no delay). Useful for preventing friendly fire in any raid type.");

            BaseIdentificationTime = Config.Bind("Identification", "BaseIdentificationTime", 0.7f,
                new ConfigDescription("Base time in seconds to identify a target.", new AcceptableValueRange<float>(0.05f, 5f)));

            IdentificationRange = Config.Bind("Identification", "IdentificationRange", 100f,
                new ConfigDescription("Maximum identification range in meters.", new AcceptableValueRange<float>(10f, 500f)));

            DistanceMultiplier = Config.Bind("Identification", "DistanceMultiplier", 0.1f,
                new ConfigDescription("How much distance increases identification time.", new AcceptableValueRange<float>(0f, 1f)));

            MemoryDuration = Config.Bind("Identification", "MemoryDuration", 60f,
                new ConfigDescription("How long (seconds) an identified target stays in memory.", new AcceptableValueRange<float>(5f, 300f)));

            UseSkillScaling = Config.Bind("Skills", "UseSkillScaling", true,
                "Scale identification time and range based on Attention, Perception, and Search skills.");

            ShowDistance = Config.Bind("Display", "ShowDistance", false,
                "Show the distance to the identified target.");

            ShowBotRole = Config.Bind("Display", "ShowBotRole", true,
                "Show the bot's role (PMC, Scav, Boss, etc.).");

            ShowTraitorWarning = Config.Bind("Display", "ShowTraitorWarning", true,
                "Show a warning in the bottom-right corner when you are marked as a Scav traitor.");

            TraitorAlertDuration = Config.Bind("Display", "TraitorAlertDuration", 5f,
                new ConfigDescription("How long (seconds) the traitor warning stays on screen per alert. Each newly alerted Scav group resets the timer.", new AcceptableValueRange<float>(1f, 15f)));

            new MatchStartedPatchLAI().Enable();
            new MatchEndedPatchLAI().Enable();
            new TraitorDetectionPatch().Enable();

            Log.LogInfo("AutoIFF v1.1.0 loaded.");
        }
    }

    internal class ConflictWarningGui : MonoBehaviour
    {
        private GUIStyle style;

        private void Awake()
        {
            style = new GUIStyle
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                normal = new GUIStyleState { textColor = Color.red },
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private void OnGUI()
        {
            float w = 580f;
            float h = 44f;
            GUI.Label(
                new Rect(Screen.width / 2f - w / 2f, 16f, w, h),
                "CONFLICT: Remove the original LightsAutomaticIdentifier.dll from BepInEx/plugins/ — AutoIFF is inactive!",
                style
            );
        }
    }
}
