using System;
using System.Linq;
using MelonLoader;
using HarmonyLib;
using UnityEngine;
using AccessibilityMod.Navigation;
using AccessibilityMod.Input;
using AccessibilityMod.UI;
using AccessibilityMod.Inventory;
using AccessibilityMod.Settings;
using AccessibilityMod.Audio;

[assembly: MelonInfo(typeof(AccessibilityMod.AccessibilityMod), "Disco Elysium Accessibility Mod", "1.0.0", "YourName")]
[assembly: MelonGame("ZAUM Studio", "Disco Elysium")]

namespace AccessibilityMod
{
    public class AccessibilityMod : MelonMod
    {
        private SmartNavigationSystem navigationSystem;
        private InputManager inputManager;
        private UINavigationHandler uiNavigationHandler;
        private InventoryNavigationHandler inventoryHandler;

        /// <summary>
        /// The live navigation system, exposed for companion mods (the AI dev bridge in
        /// tools/DevBridge drives the mod through this instead of simulating keys).
        /// Null until OnInitializeMelon has run.
        /// </summary>
        public static SmartNavigationSystem NavigationSystem { get; private set; }

        /// <summary>Release tag this build was packed as (v0.2.0 / nightly-... / dev), embedded at build time.</summary>
        public static string ModVersion =>
            System.Reflection.Assembly.GetExecutingAssembly()
                .GetCustomAttributes(typeof(System.Reflection.AssemblyMetadataAttribute), false)
                .OfType<System.Reflection.AssemblyMetadataAttribute>()
                .FirstOrDefault(a => a.Key == "ModVersion")?.Value ?? "dev";

        private string lastAnnouncedScene;
        private float lastSceneCheck;

        /// <summary>
        /// Announces entering a new area. Runs from OnUpdate rather than
        /// OnSceneWasLoaded: a scene only counts as a real play area once the player
        /// character exists in it, which silently skips boot/menu/internal scenes in
        /// any game without naming them.
        /// </summary>
        private void AnnounceAreaIfChanged()
        {
            if (Time.unscaledTime - lastSceneCheck < 1f) return;
            lastSceneCheck = Time.unscaledTime;

            var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName == lastAnnouncedScene) return;
            if (Utils.GameObjectUtils.GetPlayerPosition() == Vector3.zero) return;

            bool isFirstArea = lastAnnouncedScene == null;
            lastAnnouncedScene = sceneName;
            // The very first area after loading a save is where the player already
            // knows they are - only transitions get announced.
            if (!isFirstArea)
            {
                TolkScreenReader.Instance.Speak(
                    Loc.Get("AreaEntered", sceneName.Replace("-", " ")), false, AnnouncementCategory.Queueable);
            }
        }

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"Accessibility Mod initializing... (build {ModVersion})");

            // Initialize preferences
            AccessibilityPreferences.Initialize();
            KeyBindings.Initialize();
            TutorialGuide.Initialize();
            UpdateNotifier.Initialize(ModVersion);

            // Initialize Harmony patches
            try
            {
                var harmony = new HarmonyLib.Harmony("com.accessibility.discoelysium");
                harmony.PatchAll();
                LoggerInstance.Msg("Harmony patches applied successfully");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to apply Harmony patches: {ex}");
            }

            // Initialize Tolk screen reader
            if (TolkScreenReader.Instance.Initialize())
            {
                LoggerInstance.Msg("Tolk initialized successfully!");
                
                string detectedReader = TolkScreenReader.Instance.DetectScreenReader();
                if (!string.IsNullOrEmpty(detectedReader))
                {
                    LoggerInstance.Msg($"Detected screen reader: {detectedReader}");
                }
                else
                {
                    LoggerInstance.Msg("No screen reader detected, using SAPI fallback");
                }
                
                if (TolkScreenReader.Instance.HasSpeech())
                {
                    LoggerInstance.Msg("Speech output available");
                    // Version included so an update is verifiable by ear - "did the
                    // upgrade actually take?" was undiagnosable before.
                    TolkScreenReader.Instance.Speak($"Disco Elysium Accessibility Mod {ModVersion} loaded", true);
                }
                
                if (TolkScreenReader.Instance.HasBraille())
                {
                    LoggerInstance.Msg("Braille output available");
                }
            }
            else
            {
                LoggerInstance.Warning("Failed to initialize Tolk - falling back to console logging");
            }

            // Initialize audio-aware announcement manager
            try
            {
                var audioManager = AudioAwareAnnouncementManager.Instance;
                LoggerInstance.Msg("AudioAwareAnnouncementManager initialized successfully");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Failed to initialize AudioAwareAnnouncementManager: {ex}");
            }

            // Initialize modular systems
            navigationSystem = new SmartNavigationSystem();
            NavigationSystem = navigationSystem;
            inputManager = new InputManager(navigationSystem);
            uiNavigationHandler = new UINavigationHandler();
            inventoryHandler = InventoryNavigationHandler.Instance;
            inventoryHandler.Initialize();
            
            LoggerInstance.Msg("All accessibility systems initialized successfully");
        }
        
        public override void OnApplicationQuit()
        {
            navigationSystem?.WaypointManager.SaveAllWaypoints();

            // Clean up Tolk when the game exits
            TolkScreenReader.Instance.Cleanup();
            LoggerInstance.Msg("Tolk cleaned up");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg($"Scene loaded: {sceneName} (Index: {buildIndex})");

            // Area-change announcements happen in OnUpdate once the player character
            // exists in the new scene - a global signal that holds for any scene,
            // instead of maintaining a list of this game's boot/menu scene names.
            // Re-detect RTL on scene load in case language changed in settings
            Utils.RTLHelper.ClearCache();
            // The game only generates sound-effect captions while its caption system is on
            Patches.AudioCaptionPatches.EnsureCaptionsEnabled();
            // Instance IDs get recycled across scenes - a stale entry would name an object
            // after whatever held its ID in the previous scene.
            Utils.ObjectNameCleaner.ClearPickupNameCache();
            // Retries until the game's InControl action set exists, then runs once
            GameKeybindConflictChecker.RunOnce();
        }
        
        public override void OnUpdate()
        {
            try
            {
                // Update audio-aware announcement manager
                AudioAwareAnnouncementManager.Instance.Update();

                // Handle input through the centralized input manager
                inputManager.HandleInput();

                // Auto-advance dialogue once the current line finished speaking
                DialogAutoAdvance.Update();

                // Contextual one-shot tutorial tips
                TutorialGuide.Update();

                AnnounceAreaIfChanged();

                UpdateNotifier.Update();
                UI.ScreenAnnouncer.Update();
                UI.ResponseListAnnouncer.Update();
                UI.DialogEndAnnouncer.Update();

                // Update movement monitoring
                navigationSystem.UpdateMovement();

                // Update UI navigation
                uiNavigationHandler.UpdateUINavigation();

                // Update inventory navigation
                inventoryHandler.Update();

                // Update thought cabinet cache
                ThoughtCabinetNavigationHandler.UpdateThoughtCache();
            }
            catch (Exception ex)
            {
                LoggerInstance.Error($"Error in OnUpdate: {ex}");
            }
        }
    }
}
