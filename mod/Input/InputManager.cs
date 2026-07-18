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

            // Thought splash exit (bug #57b). The research-result splash is modal, its
            // close button is mouse-only, and NOTHING else closes it from the keyboard -
            // physical Enter, EventSystem submit and a synthetic click all bounced off in
            // live testing (17.07.2026). This check must run FIRST (PR review findings
            // 2+4): before the dialogue gate, because the splash can open while
            // IsConversationActive is still true (Disco runs many interactions as
            // conversations) and the player must never be trapped without a keyboard
            // exit - and before the interact dispatch below, because otherwise the
            // interact key would first start an autowalk to some world object behind
            // the modal and only then close the splash.
            if (KeyBindings.IsPressed(GameKey.CloseSplash)
                || KeyBindings.IsPressed(GameKey.InteractWithSelected))
            {
                if (TryCloseThoughtSplash())
                {
                    return; // the key was consumed by the splash - don't also interact
                }
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

            // Inventory tab switching (Ctrl+Tab / Ctrl+Shift+Tab). Both share the Tab base
            // key and IsPressed tolerates extra modifiers, so the Shift variant must be
            // checked first. Only acts while the inventory screen is actually open.
            if (KeyBindings.IsPressed(GameKey.InventoryPrevTab))
            {
                if (Inventory.InventoryNavigationHandler.IsInventoryViewOpen)
                    Inventory.InventoryNavigationHandler.Instance.SwitchTab(backward: true);
            }
            else if (KeyBindings.IsPressed(GameKey.InventoryNextTab))
            {
                if (Inventory.InventoryNavigationHandler.IsInventoryViewOpen)
                    Inventory.InventoryNavigationHandler.Instance.SwitchTab(backward: false);
            }

            // Healing keys (Ctrl+H health, Shift+H morale - digits are off limits, the
            // game reads them in dialogue regardless of Ctrl; see KeyBindings). Both
            // share base key H with the plain-H status announcement, which yields to
            // them in HandleDialogSafeKeys (specific-binding-first rule). The else-if
            // also breaks the Ctrl+Shift+H tie in favour of health.
            if (KeyBindings.IsPressed(GameKey.HealHealth))
            {
                Patches.HealingKeyActions.HealHealth();
            }
            else if (KeyBindings.IsPressed(GameKey.HealMorale))
            {
                Patches.HealingKeyActions.HealMorale();
            }

            HandleDialogSafeKeys();

            // Handle Thought Cabinet specific input
            ThoughtCabinetNavigationHandler.HandleThoughtCabinetInput();
        }

        private float lastDialogBlockHint;

        /// <summary>
        /// Closes the thought-research splash if it is the current view. Returns true
        /// when the splash was there (and the close request was sent), false when there
        /// is no splash - so the caller knows whether the key press is consumed.
        /// </summary>
        private static bool TryCloseThoughtSplash()
        {
            try
            {
                var view = Il2CppSunshine.Views.ViewController.GetCurrentView();
                if (view == null || view.GetViewType() != Il2CppSunshine.Views.ViewType.THOUGHTSPLASHSCREEN)
                {
                    return false;
                }

                var splash = view.TryCast<Il2CppSunshine.Views.ThoughtSplashScreenView>();
                if (splash == null) return false;

                // Two steps, both needed (all single-call paths failed live 17.07.2026:
                // physical Enter, EventSystem submit, synthetic click and Escape all
                // bounce off; OnControllerButtonToClosePressed NREs without a gamepad):
                //  1. SetThoughtStateAndGoBack = the accept bookkeeping (fixes the
                //     completed thought into its slot, same as the mouse accept).
                //     Despite the name, its "go back" does NOT change the view.
                //  2. SwitchToView(CLEAR) = actually leave the modal - the one exit
                //     that verifiably works without a mouse.
                try
                {
                    // The accept bookkeeping: whatever the game wired onto its close
                    // button (SetThoughtStateAndGoBack is private and NOT exposed by the
                    // interop assembly, so the button's own click event is the way in).
                    splash.buttonClose?.onClick?.Invoke();
                }
                catch (System.Exception inner)
                {
                    // Bookkeeping failed (no project set, artificial states, ...) - the
                    // exit below must still run so the player is NEVER trapped.
                    MelonLogger.Warning($"[THOUGHT] buttonClose invoke failed: {inner.Message}");
                }

                // Only FORCE the exit if the button's own handler did not already leave
                // the splash. Live-verified, the button click alone keeps the view on the
                // splash (that is why the forced switch exists) - but if a future/other
                // flow (e.g. the splash opened from inside the thought cabinet) navigates
                // back on its own, forcing CLEAR here would override that and eject the
                // player to gameplay. So: re-check the view, and only push CLEAR if we are
                // still stuck on the splash.
                var after = Il2CppSunshine.Views.ViewController.GetCurrentView();
                if (after == null || after.GetViewType() != Il2CppSunshine.Views.ViewType.THOUGHTSPLASHSCREEN)
                {
                    // The button's own close already took us somewhere - leave it be.
                    MelonLogger.Msg("[THOUGHT] Splash closed by its own button");
                    return true;
                }

                // Still on the splash: force the exit through the ViewController INSTANCE,
                // exactly like the dev bridge's working "view CLEAR" command. Calling
                // View.SwitchToView(ViewType) on the splash itself does nothing - that is
                // the callback the controller fires, not the initiator.
                var controller = UnityEngine.Object.FindObjectOfType<Il2CppSunshine.Views.ViewController>();
                var clearView = controller?.GetViewByType(Il2CppSunshine.Views.ViewType.CLEAR);
                if (controller == null || clearView == null)
                {
                    MelonLogger.Warning("[THOUGHT] ViewController/CLEAR view not found - cannot close splash");
                    return true; // key consumed anyway; better silent than a stray interact
                }
                controller.SwitchToView(clearView, false, false,
                    Il2CppSunshine.Views.VIEW_STACK_OPERATION.STACK_PREVIOUS);
                MelonLogger.Msg("[THOUGHT] Splash closed via keyboard");
                return true;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error($"Error closing thought splash: {ex}");
                return false;
            }
        }

        /// <summary>True when a key was pressed whose action is suppressed while the dialogue UI is up.</summary>
        private bool IsAnyWorldNavigationKeyPressed() =>
            KeyBindings.IsPressed(GameKey.SelectNpcs) || KeyBindings.IsPressed(GameKey.SelectLocations)
            || KeyBindings.IsPressed(GameKey.SelectLoot) || KeyBindings.IsPressed(GameKey.SelectEverything)
            || KeyBindings.IsPressed(GameKey.CycleForward) || KeyBindings.IsPressed(GameKey.CycleBackward)
            || KeyBindings.IsPressed(GameKey.CycleCategoryForward) || KeyBindings.IsPressed(GameKey.CycleCategoryBackward)
            || KeyBindings.IsPressed(GameKey.NavigateToSelected) || KeyBindings.IsPressed(GameKey.InteractWithSelected)
            || KeyBindings.IsPressed(GameKey.CreateWaypoint) || KeyBindings.IsPressed(GameKey.FocusWaypoints)
            || KeyBindings.IsPressed(GameKey.DeleteWaypoint) || KeyBindings.IsPressed(GameKey.ToggleSortingMode)
            || KeyBindings.IsPressed(GameKey.ScanSceneByDistance)
            // Healing is world-only too (the HUD with its plus buttons is gone during
            // dialogue) - listing it here gives a blocked Ctrl+H/Shift+H the same spoken
            // "in dialogue" hint instead of silence.
            || KeyBindings.IsPressed(GameKey.HealHealth) || KeyBindings.IsPressed(GameKey.HealMorale);

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

            // Character status announcement. Plain H shares its base key with the two
            // healing chords (Ctrl+H / Shift+H) and IsPressed tolerates extra held
            // modifiers - so status only speaks when NO healing binding matches,
            // otherwise one keypress would heal AND announce (specific-binding-first
            // rule, same convention as the other shared-base-key chains).
            if (KeyBindings.IsPressed(GameKey.AnnounceStatus)
                && !KeyBindings.IsPressed(GameKey.HealHealth)
                && !KeyBindings.IsPressed(GameKey.HealMorale))
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

            // "What even is a Glastara?" - the item's own description on demand, so the scan
            // announcements can stay short for players who do not want it after every item.
            if (KeyBindings.IsPressed(GameKey.DescribeItem))
            {
                DescribeSelectedItem();
            }

            // The debugger window - also under the debug-mode umbrella, for the same reason
            // as the name-sources key: nobody opens it while actually playing.
            if (KeyBindings.IsPressed(GameKey.OpenModDebugger))
            {
                if (AccessibilityPreferences.GetDebugMode())
                {
                    Utils.ModDebuggerLauncher.Open();
                }
                else
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("DebugModeOff"), true);
                }
            }
        }

        private void DescribeSelectedItem()
        {
            var selected = navigationSystem.StateManager.GetCurrentSelectedObject();
            if (selected == null)
            {
                TolkScreenReader.Instance.Speak(Loc.Get("ItemNoSelection"), true);
                return;
            }

            string name = Utils.ObjectNameCleaner.GetBetterObjectName(selected);
            string description = Utils.ObjectNameCleaner.GetPickupItemDescription(selected);

            TolkScreenReader.Instance.Speak(
                string.IsNullOrEmpty(description)
                    ? Loc.Get("ItemNoDescription", name)
                    : $"{name}. {description}",
                true);
        }

        private void AnnounceCurrentSelection()
        {
            try
            {
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    var currentSelection = eventSystem.currentSelectedGameObject;

                    // In the inventory the focused object is usually an InventoryHighlighter
                    // slot, which the GENERIC UI formatter cannot read - only the inventory
                    // path can. Without this, pressing "announce current selection" over an
                    // item said "Current selection has no text" and, being interrupting,
                    // beheaded the tab/count announcement (bug #2). Try the inventory
                    // resolver first whenever the inventory screen is open. Its three-way
                    // answer routes three ways (PR review finding 6, Jana's decision):
                    //   text  -> a real item / equipment slot: read it.
                    //   ""    -> an EMPTY grid slot: say where we are (tab + count).
                    //   null  -> NOT an inventory slot (a button, a header): fall through
                    //            to the generic UI reader below, which knows how to read
                    //            it - the tab summary would describe the wrong thing.
                    if (Inventory.InventoryNavigationHandler.IsInventoryViewOpen)
                    {
                        string itemText = currentSelection == null
                            ? null
                            : Patches.InventoryHighlighterHelper.GetSelectionText(currentSelection);

                        if (!string.IsNullOrEmpty(itemText))
                        {
                            TolkScreenReader.Instance.Speak(itemText, true);
                            MelonLogger.Msg($"[ON-DEMAND] Inventory selection: {itemText}");
                            return;
                        }

                        if (itemText == "" || currentSelection == null)
                        {
                            // Empty grid slot / nothing focused yet: say where we are, and
                            // do it NON-interrupting (Danijel's option 2) so it never cuts
                            // off the tab announcement. "keine Objekte" when the tab is
                            // empty.
                            TolkScreenReader.Instance.Speak(
                                Inventory.InventoryNavigationHandler.DescribeCurrentTab(), false);
                            return;
                        }
                        // itemText == null with a focused object: not ours - fall through.
                    }

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
