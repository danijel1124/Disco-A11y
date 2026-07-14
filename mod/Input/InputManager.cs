using UnityEngine;
using UnityEngine.EventSystems;
using AccessibilityMod.Navigation;
using AccessibilityMod.UI;
using AccessibilityMod.Patches;
using AccessibilityMod.Settings;
using MelonLoader;

namespace AccessibilityMod.Input
{
    public class InputManager
    {
        private readonly SmartNavigationSystem navigationSystem;

        public InputManager(SmartNavigationSystem navigationSystem)
        {
            this.navigationSystem = navigationSystem;
        }

        public void HandleInput()
        {
            if (navigationSystem.IsWaypointNamingActive)
            {
                string typedCharacters = UnityEngine.Input.inputString;
                if (!string.IsNullOrEmpty(typedCharacters))
                {
                    navigationSystem.HandleWaypointNamingInput(typedCharacters);
                }

                bool confirm = UnityEngine.Input.GetKeyDown(KeyCode.Return) || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter);
                bool cancel = UnityEngine.Input.GetKeyDown(KeyCode.Escape);

                if (confirm)
                {
                    navigationSystem.ConfirmWaypointNaming();
                }
                else if (cancel)
                {
                    navigationSystem.CancelWaypointNaming();
                }

                Il2CppInControl.InputManager.ClearInputState();
                UnityEngine.Input.ResetInputAxes();
                return;
            }

            if (navigationSystem.IsCategorySelectionActive)
            {
                if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha1))
                    navigationSystem.ConfirmWaypointCategory(WaypointCategory.NPCs);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha2))
                    navigationSystem.ConfirmWaypointCategory(WaypointCategory.Locations);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Alpha3)
                      || UnityEngine.Input.GetKeyDown(KeyCode.Return)
                      || UnityEngine.Input.GetKeyDown(KeyCode.KeypadEnter))
                    navigationSystem.ConfirmWaypointCategory(WaypointCategory.General);
                else if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                    navigationSystem.CancelCategorySelection();

                Il2CppInControl.InputManager.ClearInputState();
                UnityEngine.Input.ResetInputAxes();
                return;
            }

            // On-demand current selection announcement
            if (KeyBindings.IsPressed(GameKey.AnnounceCurrentSelection))
            {
                AnnounceCurrentSelection();
            }

            // While the dialogue UI is up, world navigation must not run - auto-walking
            // around mid-conversation (while the game waits for Enter) is disorienting.
            // Dialog-related keys (reading mode, autoread, repeat, announcements) stay
            // active; a blocked key speaks a hint instead of silently doing nothing.
            if (DialogStateManager.IsDialogUiActive)
            {
                if (IsAnyWorldNavigationKeyPressed()
                    && UnityEngine.Time.unscaledTime - lastDialogBlockHint > 3f)
                {
                    lastDialogBlockHint = UnityEngine.Time.unscaledTime;
                    TolkScreenReader.Instance.Speak(
                        "In dialogue. Navigation is paused - press Enter to continue or choose a response.", true);
                }
                HandleDialogSafeKeys();
                return;
            }

            // With the journal's map tab open, the cycling keys move through the travel
            // destinations instead of world objects (which they could not reach from
            // inside a menu anyway), and the interact key travels.
            if (UI.MapNavigationHandler.IsMapOpen)
            {
                if (KeyBindings.IsPressed(GameKey.CycleForward))
                {
                    UI.MapNavigationHandler.CycleDestination(false);
                    return;
                }
                if (KeyBindings.IsPressed(GameKey.CycleBackward))
                {
                    UI.MapNavigationHandler.CycleDestination(true);
                    return;
                }
                if (KeyBindings.IsPressed(GameKey.InteractWithSelected))
                {
                    UI.MapNavigationHandler.TravelToSelected();
                    return;
                }
            }

            // Toggle sorting mode - toggles between distance and directional sorting
            if (KeyBindings.IsPressed(GameKey.ToggleSortingMode))
            {
                navigationSystem.ToggleSortingMode();
            }

            // Distance-based scene scanner
            if (KeyBindings.IsPressed(GameKey.ScanSceneByDistance))
            {
                navigationSystem.ScanSceneByDistance();
            }

            // Category selection keys + modifiers for waypoints. Several GameKeys can
            // share the same physical key (e.g. SelectNpcs / FocusWaypoints / CreateWaypoint
            // all default to LeftBracket) - KeyBindings.IsPressed only returns true for the
            // one whose modifier requirement exactly matches what's currently held, so no
            // explicit priority order is needed here.
            if (KeyBindings.IsPressed(GameKey.CreateWaypoint))
            {
                navigationSystem.StartWaypointCreation();
            }
            else if (KeyBindings.IsPressed(GameKey.FocusWaypoints))
            {
                navigationSystem.FocusWaypoints();
            }
            else if (KeyBindings.IsPressed(GameKey.SelectNpcs))
            {
                if (navigationSystem.IsWaypointFocus)
                    navigationSystem.SelectWaypointCategory(WaypointCategory.NPCs);
                else
                    navigationSystem.SelectCategory(ObjectCategory.NPCs);
            }
            else if (KeyBindings.IsPressed(GameKey.DeleteWaypoint))
            {
                navigationSystem.DeleteCurrentWaypoint();
            }
            else if (KeyBindings.IsPressed(GameKey.SelectLocations))
            {
                if (navigationSystem.IsWaypointFocus)
                    navigationSystem.SelectWaypointCategory(WaypointCategory.Locations);
                else
                    navigationSystem.SelectCategory(ObjectCategory.Locations);
            }
            else if (KeyBindings.IsPressed(GameKey.SelectLoot))
            {
                if (navigationSystem.IsWaypointFocus)
                    TolkScreenReader.Instance.Speak($"Press {KeyBindings.SpeakableName(GameKey.SelectNpcs)} for NPC waypoints, {KeyBindings.SpeakableName(GameKey.SelectLocations)} for locations, or {KeyBindings.SpeakableName(GameKey.SelectEverything)} for all.", true);
                else
                    navigationSystem.SelectCategory(ObjectCategory.Loot);
            }
            else if (KeyBindings.IsPressed(GameKey.SelectEverything))
            {
                if (navigationSystem.IsWaypointFocus)
                    navigationSystem.SelectWaypointCategory(null);  // null = all
                else
                    navigationSystem.SelectCategory(ObjectCategory.Everything);
            }

            // Cycle categories / objects. All four share base keys in the layout-safe
            // presets (PageUp/PageDown with and without Ctrl) and KeyBindings.IsPressed
            // tolerates extra modifiers, so the more specific bindings (more required
            // modifiers: Ctrl category cycling, Shift backward-cycling) must be checked
            // before the plain ones in one else-if chain.
            if (KeyBindings.IsPressed(GameKey.CycleCategoryBackward))
            {
                navigationSystem.CycleCategory(backward: true);
            }
            else if (KeyBindings.IsPressed(GameKey.CycleCategoryForward))
            {
                navigationSystem.CycleCategory(backward: false);
            }
            else if (KeyBindings.IsPressed(GameKey.CycleBackward))
            {
                navigationSystem.CycleWithinCategory(backward: true);
            }
            else if (KeyBindings.IsPressed(GameKey.CycleForward))
            {
                navigationSystem.CycleWithinCategory(backward: false);
            }

            // Navigate to selected object
            if (KeyBindings.IsPressed(GameKey.NavigateToSelected))
            {
                navigationSystem.NavigateToSelectedObject();
            }

            // Interact with the selected object (keyboard equivalent of clicking it -
            // the game's own E-Interact only works with controller selection).
            // ToggleAutoInteract shares the base key (Ctrl+F vs F), so the more
            // specific binding is checked first in the same chain.
            if (KeyBindings.IsPressed(GameKey.ToggleAutoInteract))
            {
                bool newValue = !AccessibilityPreferences.GetAutoInteract();
                AccessibilityPreferences.SetAutoInteract(newValue);
                TolkScreenReader.Instance.Speak(newValue
                    ? "Auto interact enabled: arriving at an object interacts with it."
                    : "Auto interact disabled", true);
            }
            else if (KeyBindings.IsPressed(GameKey.InteractWithSelected))
            {
                navigationSystem.InteractWithSelectedObject();
            }

            // Stop automated movement
            if (KeyBindings.IsPressed(GameKey.StopMovement))
            {
                navigationSystem.StopMovement();
            }

            HandleDialogSafeKeys();

            // Handle Thought Cabinet specific input
            ThoughtCabinetNavigationHandler.HandleThoughtCabinetInput();
        }

        private float lastDialogBlockHint;

        /// <summary>True when a key was pressed whose action is suppressed while the dialogue UI is up.</summary>
        private bool IsAnyWorldNavigationKeyPressed() =>
            KeyBindings.IsPressed(GameKey.SelectNpcs) || KeyBindings.IsPressed(GameKey.SelectLocations)
            || KeyBindings.IsPressed(GameKey.SelectLoot) || KeyBindings.IsPressed(GameKey.SelectEverything)
            || KeyBindings.IsPressed(GameKey.CycleForward) || KeyBindings.IsPressed(GameKey.CycleBackward)
            || KeyBindings.IsPressed(GameKey.CycleCategoryForward) || KeyBindings.IsPressed(GameKey.CycleCategoryBackward)
            || KeyBindings.IsPressed(GameKey.NavigateToSelected) || KeyBindings.IsPressed(GameKey.InteractWithSelected)
            || KeyBindings.IsPressed(GameKey.CreateWaypoint) || KeyBindings.IsPressed(GameKey.FocusWaypoints)
            || KeyBindings.IsPressed(GameKey.DeleteWaypoint) || KeyBindings.IsPressed(GameKey.ToggleSortingMode)
            || KeyBindings.IsPressed(GameKey.ScanSceneByDistance);

        /// <summary>
        /// Keys that make sense both in the world and while the dialogue UI is up:
        /// dialog reading/autoread controls and pure announcements.
        /// </summary>
        private void HandleDialogSafeKeys()
        {
            // Toggle dialog reading mode
            if (KeyBindings.IsPressed(GameKey.ToggleDialogReading))
            {
                DialogStateManager.ToggleDialogReading();
            }

            // Toggle dialog auto-advance ("autoread")
            if (KeyBindings.IsPressed(GameKey.ToggleDialogAutoAdvance))
            {
                DialogAutoAdvance.Toggle();
            }

            // Repeat last dialogue line
            if (KeyBindings.IsPressed(GameKey.RepeatDialogue))
            {
                string lastDialogue = DialogSystemPatches.GetLastDialogueLine();
                TolkScreenReader.Instance.Speak(lastDialogue, true, AnnouncementCategory.Immediate);
            }

            // Toggle orb announcements
            if (KeyBindings.IsPressed(GameKey.ToggleOrbAnnouncements))
            {
                OrbTextVocalizationPatches.ToggleOrbAnnouncements();
            }

            // Character status announcement
            if (KeyBindings.IsPressed(GameKey.AnnounceStatus))
            {
                Patches.CharacterStatusAnnouncement.AnnounceFullStatus();
            }

            // Character stats announcement (time, money, experience)
            if (KeyBindings.IsPressed(GameKey.AnnounceStats))
            {
                Patches.CharacterStatsAnnouncement.AnnounceCharacterStats();
            }

            // Toggle speech interrupt mode
            if (KeyBindings.IsPressed(GameKey.ToggleSpeechInterrupt))
            {
                TolkScreenReader.Instance.ToggleGlobalInterrupt();
            }

            // Toggle encoding diagnostic logging
            if (KeyBindings.IsPressed(GameKey.ToggleDiagnostics))
            {
                bool newState = !TextExtractor.DiagnosticLogging;
                TextExtractor.DiagnosticLogging = newState;
                TolkScreenReader.Instance.DiagnosticLogging = newState;
                string status = newState ? "enabled" : "disabled";
                TolkScreenReader.Instance.Speak($"Encoding diagnostics {status}", true);
                MelonLogger.Msg($"[DIAG] Encoding diagnostic logging {status}");
            }

            // Officer profile announcement
            if (KeyBindings.IsPressed(GameKey.AnnounceOfficerProfile))
            {
                Patches.OfficerProfileAnnouncement.AnnounceOfficerProfile();
            }

            // Read skill description in character sheet
            if (KeyBindings.IsPressed(GameKey.ReadSkillDescription))
            {
                SkillDescriptionReader.ReadSelectedSkillDescription();
            }

            // Check Kim dialogue status
            if (KeyBindings.IsPressed(GameKey.AnnounceKimStatus))
            {
                AnnounceKimDialogueStatus();
            }

            // Diagnostics: where the selected object's name comes from. Reports only - a
            // player who cannot see the object has no other way to tell a wrong name from
            // a merely odd one.
            // Under the debug-mode umbrella: it reports where a name comes from, which is a
            // question you only ask while working on the mod, not while playing.
            if (KeyBindings.IsPressed(GameKey.AnnounceNameSources))
            {
                if (AccessibilityPreferences.GetDebugMode())
                {
                    Utils.NameSourceReporter.AnnounceForSelected(navigationSystem);
                }
                else
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("DebugModeOff"), true);
                }
            }

            // Describe the area again - what a sighted player can simply look at twice.
            // The Ctrl variant must be checked first: IsPressed tolerates extra modifiers,
            // so plain U would also fire while Ctrl is held.
            if (KeyBindings.IsPressed(GameKey.DescribeAreaFull))
            {
                AccessibilityMod.SpeakAreaIntro();
            }
            else if (KeyBindings.IsPressed(GameKey.DescribeArea))
            {
                AccessibilityMod.SpeakAreaDescription(onDemand: true);
            }
        }

        private void AnnounceCurrentSelection()
        {
            try
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    var currentSelection = eventSystem.currentSelectedGameObject;
                    if (currentSelection != null)
                    {
                        string speechText = UIElementFormatter.FormatUIElementForSpeech(currentSelection);
                        if (!string.IsNullOrEmpty(speechText))
                        {
                            TolkScreenReader.Instance.Speak(speechText, true); // Interrupt for on-demand announcements
                            MelonLogger.Msg($"[ON-DEMAND] Current selection: {speechText}");
                        }
                        else
                        {
                            TolkScreenReader.Instance.Speak("Current selection has no text", true);
                            MelonLogger.Msg($"[ON-DEMAND] Current selection: {currentSelection.name} (no formatted text)");
                        }
                    }
                    else
                    {
                        TolkScreenReader.Instance.Speak("No UI element selected", true);
                        MelonLogger.Msg("[ON-DEMAND] No UI element currently selected");
                    }
                }
                else
                {
                    TolkScreenReader.Instance.Speak("No event system active", true);
                    MelonLogger.Msg("[ON-DEMAND] No EventSystem found");
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error announcing current selection: {ex}");
                TolkScreenReader.Instance.Speak("Error getting current selection", true);
            }
        }

        private void AnnounceKimDialogueStatus()
        {
            try
            {
                bool hasDialogue = PortraitNotificationPatches.IsKimDialogueAvailable();
                string message = hasDialogue
                    ? "Kim has dialogue available"
                    : "No new dialogue from Kim";
                TolkScreenReader.Instance.Speak(message, true);
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error checking Kim dialogue status: {ex}");
                TolkScreenReader.Instance.Speak("Error checking Kim status", true);
            }
        }
    }
}
