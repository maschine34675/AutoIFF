using System;
using System.Runtime.CompilerServices;
using AutoIFF.Codebase;
using AutoIFF.Patches;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using EFT;
using SPT.Reflection.Patching;
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

    public enum EBotNameDisplay
    {
        Off,
        Teammates,
        All
    }

    [BepInPlugin("com.maschine.AutoIFF", "maschine-AutoIFF", PluginVersion)]
    [BepInDependency("Light.LightsAutomaticIdentiier", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(FikaGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginVersion = "2.1.0";
        public const string FikaGuid = "com.fika.core";

        public static ManualLogSource Log;
        public static bool FikaPresent { get; private set; }

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
        public static ConfigEntry<EBotNameDisplay> ShowBotName;
        public static ConfigEntry<bool> ShowTargetHealth;
        public static ConfigEntry<bool> ShowTraitorWarning;
        public static ConfigEntry<float> TraitorAlertDuration;

        private const string OldModGuid = "Light.LightsAutomaticIdentiier"; // typo is intentional — that's the original GUID

        private static Plugin instance;
        private static LocalPlayerWatcher playerWatcher;
        public static void StartWatchingLocalPlayer()
        {
            if (instance == null)
                return;

            if (playerWatcher == null)
                playerWatcher = instance.gameObject.AddComponent<LocalPlayerWatcher>();

            playerWatcher.Reset();
            playerWatcher.enabled = true;
        }
        public static void NoteLocalPlayerActivated(Player player)
        {
            if (playerWatcher != null)
                playerWatcher.Track(player);
        }
        public static void StopWatchingLocalPlayer()
        {
            if (playerWatcher != null)
                playerWatcher.enabled = false;
        }

        private void Awake()
        {
            Log = Logger;
            instance = this;

            if (Chainloader.PluginInfos.ContainsKey(OldModGuid))
            {
                Log.LogError("[AutoIFF] ════════════════════════════════════════════");
                Log.LogError("[AutoIFF] CONFLICT: Original LightsAutomaticIdentifier detected!");
                Log.LogError("[AutoIFF] Both mods running simultaneously will cause issues.");
                Log.LogError("[AutoIFF] Please remove the old DLL from BepInEx/plugins/.");
                Log.LogError($"[AutoIFF] AutoIFF v{PluginVersion} has NOT been activated.");
                Log.LogError("[AutoIFF] ════════════════════════════════════════════");
                gameObject.AddComponent<ConflictWarningGui>();
                return;
            }
            ActivationMode = Config.Bind("General", "ActivationMode", EActivationMode.Automatic,
                Tagged("When To Identify Targets", 30,
                    "Automatic = only active when playing as Scav. AlwaysOn = active in every raid. AlwaysOff = disabled. Hotkey = toggle via keybind."));

            ActivationHotkey = Config.Bind("General", "ActivationHotkey", KeyboardShortcut.Empty,
                Tagged("Toggle Key (Hotkey Mode Only)", 20,
                    "Keybind to toggle the mod on/off when ActivationMode is set to Hotkey. Not assigned by default."));

            FriendlyOnly = Config.Bind("General", "FriendlyOnly", false,
                Tagged("Show Friendlies Only (No Delay)", 10,
                    "Only show friendly targets, identified instantly. Hostile targets show no label, and the identification time settings do not apply. Useful for preventing friendly fire in any raid type."));

            BaseIdentificationTime = Config.Bind("Identification", "BaseIdentificationTime", 0.7f,
                Tagged("Base Identification Time (s)", 40,
                    "Base time in seconds to identify a target.",
                    new AcceptableValueRange<float>(0.05f, 5f)));

            DistanceMultiplier = Config.Bind("Identification", "DistanceMultiplier", 0.1f,
                Tagged("Extra Time For Distant Targets", 30,
                    "How strongly distance increases identification time. 0 = distance has no effect.",
                    new AcceptableValueRange<float>(0f, 1f)));

            IdentificationRange = Config.Bind("Identification", "IdentificationRange", 100f,
                Tagged("Max Identification Range (m)", 20,
                    "Maximum identification range in meters. Beyond it, targets show as too far.",
                    new AcceptableValueRange<float>(10f, 500f)));

            MemoryDuration = Config.Bind("Identification", "MemoryDuration", 60f,
                Tagged("Remember Identified Targets (s)", 10,
                    "How long (seconds) an identified target stays in memory.",
                    new AcceptableValueRange<float>(5f, 300f)));

            UseSkillScaling = Config.Bind("Skills", "UseSkillScaling", true,
                Tagged("Improve With Character Skills", 0,
                    "Scale identification time and range based on Attention, Perception, and Search skills."));

            ShowBotRole = Config.Bind("Display", "ShowBotRole", true,
                Tagged("Show Target Type", 60,
                    "Show what the target is (PMC, Scav, Boss; Player for coop players) below the label."));

            ShowBotName = Config.Bind("Display", "ShowBotName", EBotNameDisplay.Teammates,
                Tagged("Show Target Name", 50,
                    "Show the target's name on its own line below the label. Teammates = squad mates registered by team mods (e.g. PitFireTeam) and coop players; without such mods nothing changes. All = every identified target."));

            ShowTargetHealth = Config.Bind("Display", "ShowTargetHealth", false,
                Tagged("Show Target Health", 40,
                    "Show the target's current and maximum health (all body parts combined) next to the Friendly/Hostile label."));

            ShowDistance = Config.Bind("Display", "ShowDistance", false,
                Tagged("Show Distance To Target", 30,
                    "Show the distance to the identified target."));

            ShowTraitorWarning = Config.Bind("Display", "ShowTraitorWarning", true,
                Tagged("Show Scav Traitor Warning", 20,
                    "Show a warning in the bottom-right corner when you are marked as a Scav traitor."));

            TraitorAlertDuration = Config.Bind("Display", "TraitorAlertDuration", 5f,
                Tagged("Traitor Warning Duration (s)", 10,
                    "How long (seconds) the traitor warning stays on screen per alert. Each newly alerted Scav group resets the timer.",
                    new AcceptableValueRange<float>(1f, 15f)));

            FikaPresent = DetectFika();

            bool identificationReady = TryEnable(new MatchStartedPatchLAI());
            TryEnable(new MatchEndedPatchLAI());
            TryEnable(new TraitorDetectionPatch());

            if (!identificationReady)
                Log.LogError("[AutoIFF] The raid-start patch could not be applied — no targets will be identified this session.");

            if (FikaPresent && !EnableFikaPatches())
            {
                FikaPresent = false;
                Log.LogWarning("[AutoIFF] Fika detected, but the Fika patches failed to apply — Fika support disabled.");
            }

            Log.LogInfo($"AutoIFF v{PluginVersion} loaded.");
        }

        private static bool DetectFika()
        {
            if (!Chainloader.PluginInfos.ContainsKey(FikaGuid))
                return false;

            try
            {
                FikaCompat.Probe();
                Log.LogInfo("[AutoIFF] Fika detected — coop IFF support enabled.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[AutoIFF] Fika detected, but its types do not match this AutoIFF build — Fika support disabled. ({ex.GetType().Name})");
                return false;
            }
        }

        private static ConfigDescription Tagged(string displayName, int order, string description)
        {
            return Tagged(displayName, order, description, null);
        }

        private static ConfigDescription Tagged(string displayName, int order, string description,
            AcceptableValueBase acceptableValues)
        {
            return new ConfigDescription(
                description,
                acceptableValues,
                new ConfigurationManagerAttributes { DispName = displayName, Order = order });
        }
        private static bool TryEnable(ModulePatch patch)
        {
            try
            {
                patch.Enable();
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"[AutoIFF] Patch {patch.GetType().Name} could not be applied: {ex.Message}");
                return false;
            }
        }
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool EnableFikaPatches()
        {
            var shotPatch = new FikaObservedShotPatch();
            if (!TryEnable(shotPatch))
                return false;

            if (TryEnable(new FikaObservedDamageInfoPatch()))
                return true;
            try
            {
                shotPatch.Disable();
            }
            catch (Exception ex)
            {
                Log.LogWarning($"[AutoIFF] Could not roll back the observed-shot patch: {ex.Message}");
            }

            return false;
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
