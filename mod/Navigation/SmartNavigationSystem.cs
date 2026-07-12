using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Il2Cpp;
using Il2CppFortressOccident;
using AccessibilityMod.Utils;
using AccessibilityMod.Settings;
using UnityEngine.SceneManagement;
using MelonLoader;

namespace AccessibilityMod.Navigation
{
    public enum NavigationFocus
    {
        ObjectCategories,
        Waypoints
    }

    public class SmartNavigationSystem
    {
        private readonly NavigationStateManager stateManager;
        private readonly MovementController movementController;
        private readonly WaypointManager waypointManager;

        private NavigationFocus currentFocus = NavigationFocus.ObjectCategories;
        private WaypointNamingSession namingSession;
        private bool isCategorySelectionActive;
        private Vector3 pendingWaypointPosition;
        private string pendingWaypointName;
        private string pendingWaypointScene;

        public NavigationStateManager StateManager => stateManager;
        public MovementController MovementController => movementController;
        public WaypointManager WaypointManager => waypointManager;
        public bool IsWaypointNamingActive => namingSession?.IsActive ?? false;
        public bool IsWaypointFocus => currentFocus == NavigationFocus.Waypoints;
        public bool IsCategorySelectionActive => isCategorySelectionActive;

        // True while the current auto-walk targets a world object (as opposed to a
        // waypoint, which is a bare position with nothing to interact with).
        private bool lastNavigationTargetWasObject;

        /// <summary>Current selection snapshot, exposed for companion mods (AI dev bridge).</summary>
        public NavigationInfo GetNavigationInfo() =>
            stateManager.GetCurrentNavigationInfo(GameObjectUtils.GetPlayerPosition());

        public SmartNavigationSystem()
        {
            stateManager = new NavigationStateManager();
            movementController = new MovementController();
            waypointManager = new WaypointManager();
            movementController.OnMovementCompleted += OnArrived;
        }

        private void OnArrived(string _)
        {
            if (!lastNavigationTargetWasObject) return;
            if (!AccessibilityPreferences.GetAutoInteract()) return;

            InteractWithSelectedObject();
        }

        public void SelectCategory(ObjectCategory category)
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            currentFocus = NavigationFocus.ObjectCategories;

            try
            {
                MelonLogger.Msg($"[SMART NAV] Selecting category: {category}");
                
                // Get current objects from registry
                var registry = MouseOverHighlight.registry;
                if (registry == null || registry.Count == 0)
                {
                    TolkScreenReader.Instance.Speak("No objects available for selection.", true);
                    return;
                }
                
                // Find player position
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (playerPos == Vector3.zero)
                {
                    TolkScreenReader.Instance.Speak("Could not find player position", true);
                    return;
                }
                
                // Update categorized objects and switch to category
                stateManager.UpdateCategorizedObjects(playerPos, category);
                
                // Announce category contents
                var navInfo = stateManager.GetCurrentNavigationInfo(playerPos);
                string announcement = $"{category}: " + navInfo.FormatAnnouncement();
                
                MelonLogger.Msg($"[SMART NAV] {announcement}");
                TolkScreenReader.Instance.Speak(announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Error selecting category: {ex}");
                TolkScreenReader.Instance.Speak($"Category selection failed: {ex.Message}", true);
            }
        }

        public void FocusWaypoints()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            if (currentFocus == NavigationFocus.Waypoints)
            {
                currentFocus = NavigationFocus.ObjectCategories;
                TolkScreenReader.Instance.Speak("Waypoint mode off. Press [ for NPCs, ] for locations, or \\ for loot.", true);
                return;
            }

            currentFocus = NavigationFocus.Waypoints;

            string sceneKey = GetActiveSceneKey();
            waypointManager.ClearCategoryFilter(sceneKey);

            if (!waypointManager.HasWaypointsInScene(sceneKey))
            {
                string message = waypointManager.HasAnyWaypoints
                    ? $"No waypoints saved for this area. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one here."
                    : $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.";
                TolkScreenReader.Instance.Speak(message, true);
                return;
            }

            waypointManager.EnsureSelection(sceneKey);
            AnnounceWaypointSelection(sceneKey, includeCategoryIntro: true);
        }

        public void SelectWaypointCategory(WaypointCategory? category)
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            string sceneKey = GetActiveSceneKey();
            waypointManager.SetCategoryFilter(sceneKey, category);
            waypointManager.EnsureSelection(sceneKey);

            if (!waypointManager.HasWaypointsInScene(sceneKey))
            {
                string label = GetWaypointCategoryLabel(category);
                string message = label == "all"
                    ? "No waypoints saved for this area."
                    : $"No {label} waypoints in this area.";
                TolkScreenReader.Instance.Speak(message, true);
                return;
            }

            AnnounceWaypointSelection(sceneKey, includeCategoryIntro: true);
        }

        public void ConfirmWaypointCategory(WaypointCategory category)
        {
            if (!isCategorySelectionActive) return;

            isCategorySelectionActive = false;
            waypointManager.AddWaypoint(pendingWaypointPosition, pendingWaypointName, pendingWaypointScene, category);

            WaypointCategory? filterToSet = category == WaypointCategory.General
                ? (WaypointCategory?)null
                : category;
            waypointManager.SetCategoryFilter(pendingWaypointScene, filterToSet);

            currentFocus = NavigationFocus.Waypoints;
            string prefix = $"Waypoint {pendingWaypointName} saved.";
            AnnounceWaypointSelection(pendingWaypointScene, includeCategoryIntro: true, prefix: prefix);
        }

        public void CancelCategorySelection()
        {
            if (!isCategorySelectionActive) return;
            isCategorySelectionActive = false;
            TolkScreenReader.Instance.Speak("Waypoint creation cancelled.", true);
        }

        public void StartWaypointCreation()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Already naming a waypoint. Press Enter to confirm or Escape to cancel.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
            if (playerPos == Vector3.zero)
            {
                TolkScreenReader.Instance.Speak("Could not capture player position for waypoint.", true);
                return;
            }

            string sceneKey = GetActiveSceneKey();
            string defaultName = waypointManager.GetDefaultName(sceneKey);
            namingSession = new WaypointNamingSession(
                playerPos,
                defaultName,
                sceneKey,
                OnWaypointNamingCompleted,
                OnWaypointNamingCancelled);

            TolkScreenReader.Instance.Speak($"Creating waypoint. Type a name, then press Enter to save. Press Escape to cancel. Default is {defaultName}.", true);
        }

        public void HandleWaypointNamingInput(string inputCharacters)
        {
            namingSession?.HandleInput(inputCharacters);
        }

        public void ConfirmWaypointNaming()
        {
            namingSession?.Confirm();
        }

        public void CancelWaypointNaming()
        {
            namingSession?.Cancel();
        }

        public void DeleteCurrentWaypoint()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            string sceneKey = GetActiveSceneKey();

            if (currentFocus != NavigationFocus.Waypoints)
            {
                if (!waypointManager.HasAnyWaypoints)
                {
                    string message = $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.";
                    TolkScreenReader.Instance.Speak(message, true);
                }
                else
                {
                    TolkScreenReader.Instance.Speak($"Focus waypoints first with {KeyBindings.SpeakableName(GameKey.FocusWaypoints)}, then press {KeyBindings.SpeakableName(GameKey.DeleteWaypoint)} to delete.", true);
                }
                return;
            }

            if (!waypointManager.TryGetSelection(sceneKey, out var waypoint, out _, out _))
            {
                string message = waypointManager.HasAnyWaypoints
                    ? $"No waypoints saved for this area. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one here."
                    : $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.";
                TolkScreenReader.Instance.Speak(message, true);
                return;
            }

            string deletedName = waypoint.Name;
            if (!waypointManager.RemoveWaypoint(sceneKey, waypoint))
            {
                MelonLogger.Error("[WAYPOINTS] Failed to delete waypoint.");
                TolkScreenReader.Instance.Speak("Could not delete waypoint.", true);
                return;
            }

            MelonLogger.Msg($"[WAYPOINTS] Deleted waypoint {deletedName}");

            if (!waypointManager.HasWaypointsInScene(sceneKey))
            {
                TolkScreenReader.Instance.Speak($"Deleted waypoint {deletedName}. No waypoints saved for this area.", true);
                return;
            }

            waypointManager.EnsureSelection(sceneKey);
            AnnounceWaypointSelection(sceneKey, includeCategoryIntro: true, prefix: $"Deleted waypoint {deletedName}.");
        }

        public void CycleWithinCategory(bool backward = false)
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first.", true);
                return;
            }

            if (isCategorySelectionActive)
            {
                TolkScreenReader.Instance.Speak("Finish selecting a category first.", true);
                return;
            }

            try
            {
                if (currentFocus == NavigationFocus.Waypoints)
                {
                    string sceneKey = GetActiveSceneKey();
                    if (!waypointManager.HasWaypointsInScene(sceneKey))
                    {
                        WaypointCategory? filter = waypointManager.GetCategoryFilter(sceneKey);
                        string label = GetWaypointCategoryLabel(filter);
                        string message = filter.HasValue
                            ? $"No {label} waypoints in this area."
                            : (waypointManager.HasAnyWaypoints
                                ? $"No waypoints saved for this area. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one here."
                                : $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.");
                        TolkScreenReader.Instance.Speak(message, true);
                        return;
                    }

                    if (backward)
                        waypointManager.SelectPrevious(sceneKey);
                    else
                        waypointManager.SelectNext(sceneKey);

                    AnnounceWaypointSelection(sceneKey, includeCategoryIntro: false);
                    return;
                }

                if (!stateManager.HasSelection)
                {
                    TolkScreenReader.Instance.Speak("No objects in current category. Press [ for NPCs, ] for locations, \\ for containers, = for everything, or Ctrl plus [ for waypoints.", true);
                    return;
                }

                // Cycle to next or previous object based on direction
                if (backward)
                    stateManager.CycleToPreviousObject();
                else
                    stateManager.CycleToNextObject();

                // Find player position for announcement
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (playerPos == Vector3.zero) return;

                // Announce selected object
                var navInfo = stateManager.GetCurrentNavigationInfo(playerPos);
                string announcement = navInfo.FormatAnnouncement();

                MelonLogger.Msg($"[SMART NAV] {announcement}");
                TolkScreenReader.Instance.Speak(announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Error cycling objects: {ex}");
                TolkScreenReader.Instance.Speak($"Cycling failed: {ex.Message}", true);
            }
        }

        /// <summary>
        /// stardew-access-style category cycling (Ctrl+PageDown/PageUp): steps through
        /// NPCs, Locations, Loot, Everything with wrap-around, announcing the new
        /// category's first object. In waypoint focus it steps through the waypoint
        /// filters (NPCs, Locations, all) instead.
        /// </summary>
        public void CycleCategory(bool backward)
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            try
            {
                if (currentFocus == NavigationFocus.Waypoints)
                {
                    string sceneKey = GetActiveSceneKey();
                    WaypointCategory? filter = waypointManager.GetCategoryFilter(sceneKey);
                    // Cycle order: all -> NPCs -> Locations -> all (General acts as "all")
                    WaypointCategory? next = (filter, backward) switch
                    {
                        (null, false) => WaypointCategory.NPCs,
                        (WaypointCategory.NPCs, false) => WaypointCategory.Locations,
                        (WaypointCategory.Locations, false) => null,
                        (null, true) => WaypointCategory.Locations,
                        (WaypointCategory.Locations, true) => WaypointCategory.NPCs,
                        (WaypointCategory.NPCs, true) => null,
                        _ => null,
                    };
                    SelectWaypointCategory(next);
                    return;
                }

                var order = new[] { ObjectCategory.NPCs, ObjectCategory.Locations, ObjectCategory.Loot, ObjectCategory.Everything };
                int index = Array.IndexOf(order, stateManager.CurrentCategory);
                if (index < 0) index = 0;
                int nextIndex = (index + (backward ? -1 : 1) + order.Length) % order.Length;
                SelectCategory(order[nextIndex]);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Error cycling categories: {ex}");
            }
        }

        /// <summary>
        /// Keyboard equivalent of clicking the currently selected object. The game's own
        /// E-Interact only acts on its controller-driven interactable selection, which
        /// keyboard-only play never populates - so this drives the click pipeline
        /// (MouseOverHighlight.InteractFirstActive) directly on the mod's selection.
        /// </summary>
        public void InteractWithSelectedObject()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first. Press Enter to save or Escape to cancel.", true);
                return;
            }

            if (currentFocus == NavigationFocus.Waypoints)
            {
                TolkScreenReader.Instance.Speak($"Waypoints are positions, not objects. Press {KeyBindings.SpeakableName(GameKey.NavigateToSelected)} to walk there instead.", true);
                return;
            }

            try
            {
                var selectedObject = stateManager.GetCurrentSelectedObject();
                if (selectedObject == null)
                {
                    TolkScreenReader.Instance.Speak($"No object selected. Select a category first, then use {KeyBindings.SpeakableName(GameKey.CycleForward)} to cycle.", true);
                    return;
                }

                string objectName = ObjectNameCleaner.GetBetterObjectName(selectedObject);

                var entity = selectedObject.GetFirstActive();
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (entity != null && playerPos != Vector3.zero && !entity.IsWithinInteractionRadius(playerPos))
                {
                    TolkScreenReader.Instance.Speak($"{objectName} is too far away. Press {KeyBindings.SpeakableName(GameKey.NavigateToSelected)} to walk there first.", true);
                    return;
                }

                bool interacted = selectedObject.InteractFirstActive(new Interactable.ClickEventData());
                MelonLogger.Msg($"[SMART NAV] InteractFirstActive on {objectName}: {interacted}");
                if (!interacted)
                {
                    TolkScreenReader.Instance.Speak($"Cannot interact with {objectName} right now.", true);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Error interacting with selected object: {ex}");
                TolkScreenReader.Instance.Speak("Interaction failed.", true);
            }
        }

        public void NavigateToSelectedObject()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first.", true);
                return;
            }

            try
            {
                if (currentFocus == NavigationFocus.Waypoints)
                {
                    string sceneKey = GetActiveSceneKey();
                    if (!waypointManager.TryGetSelection(sceneKey, out var waypoint, out _, out _))
                    {
                        string message = waypointManager.HasAnyWaypoints
                            ? $"No waypoints saved for this area. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one here."
                            : $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.";
                        TolkScreenReader.Instance.Speak(message, true);
                        return;
                    }

                    MelonLogger.Msg($"[WAYPOINTS] Navigating to waypoint {waypoint.Name}");
                    TolkScreenReader.Instance.Speak($"Calculating path to waypoint {waypoint.Name}...", true);

                    lastNavigationTargetWasObject = false;
                    movementController.TryNavigateToPosition(waypoint.Position, $"waypoint {waypoint.Name}");
                    return;
                }

                var selectedObject = stateManager.GetCurrentSelectedObject();
                if (selectedObject == null || selectedObject.transform == null)
                {
                    TolkScreenReader.Instance.Speak($"No object selected. Select a category first, then use {KeyBindings.SpeakableName(GameKey.CycleForward)} to cycle.", true);
                    return;
                }

                Vector3 destination = selectedObject.transform.position;
                string objectName = ObjectNameCleaner.GetBetterObjectName(selectedObject);

                MelonLogger.Msg($"[SMART NAV] Attempting to navigate to {objectName}");
                TolkScreenReader.Instance.Speak($"Calculating path to {objectName}...", true);

                // Try automated movement
                lastNavigationTargetWasObject = true;
                movementController.TryNavigateToPosition(destination, objectName);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Navigation error: {ex}");
                TolkScreenReader.Instance.Speak($"Navigation failed: {ex.Message}", true);
            }
        }

        public void StopMovement()
        {
            movementController.StopMovement();
        }

        public void UpdateMovement()
        {
            movementController.UpdateMovementProgress();
        }

        public void ToggleSortingMode()
        {
            if (IsWaypointNamingActive)
            {
                TolkScreenReader.Instance.Speak("Finish naming your waypoint first.", true);
                return;
            }

            if (currentFocus == NavigationFocus.Waypoints)
            {
                TolkScreenReader.Instance.Speak("Sorting only applies to object categories. Press [ to switch back to NPCs or Ctrl plus [ for waypoints.", true);
                return;
            }

            try
            {
                stateManager.ToggleSortingMode();

                // Re-sort current category with new mode
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (playerPos != Vector3.zero && stateManager.HasSelection)
                {
                    // Update objects with new sorting
                    stateManager.UpdateCategorizedObjects(playerPos, stateManager.CurrentCategory);
                }

                string modeName = stateManager.CurrentSortingMode == SortingMode.Directional
                    ? "directional (clockwise)"
                    : "distance";

                string announcement = $"Sorting mode changed to {modeName}";
                MelonLogger.Msg($"[SMART NAV] {announcement}");
                TolkScreenReader.Instance.Speak(announcement, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[SMART NAV] Error toggling sorting mode: {ex}");
            }
        }

        public void TestRegistryAccess()
        {
            try
            {
                MelonLogger.Msg("[REGISTRY TEST] Starting scene-wide object scan...");
                
                // Test 1: Access the MouseOverHighlight registry
                var registry = MouseOverHighlight.registry;
                if (registry == null)
                {
                    MelonLogger.Error("[REGISTRY TEST] Registry is null!");
                    TolkScreenReader.Instance.Speak("Registry test failed: registry is null", true);
                    return;
                }
                
                int totalObjects = registry.Count;
                MelonLogger.Msg($"[REGISTRY TEST] Found {totalObjects} objects in registry");
                
                // Test 2: Find player position
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (playerPos == Vector3.zero)
                {
                    MelonLogger.Error("[REGISTRY TEST] Could not find player character!");
                    TolkScreenReader.Instance.Speak("Registry test failed: no player found", true);
                    return;
                }
                
                MelonLogger.Msg($"[REGISTRY TEST] Player position: {playerPos}");
                
                // Test 3: Scan all objects for distances
                float maxDistance = 0;
                float minDistance = float.MaxValue;
                string furthestObject = "";
                string nearestObject = "";
                int objectsOver20m = 0;
                
                foreach (var obj in registry)
                {
                    if (obj == null || obj.transform == null) continue;
                    
                    float distance = Vector3.Distance(playerPos, obj.transform.position);
                    string name = ObjectNameCleaner.GetBetterObjectName(obj);
                    
                    if (distance > maxDistance)
                    {
                        maxDistance = distance;
                        furthestObject = name;
                    }

                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        nearestObject = name;
                    }

                    if (distance > 20f)
                        objectsOver20m++;
                }
                
                // Check interactable selection manager
                var selectionManager = UnityEngine.Object.FindObjectOfType<CharacterAnalogueControl>();
                int nearbyOnly = 0;
                if (selectionManager != null && selectionManager.m_interactableSelectionManager != null)
                {
                    nearbyOnly = selectionManager.m_interactableSelectionManager.m_availableInteractables.Count;
                }
                
                // Announce results
                string result = $"Registry scan complete! Found {totalObjects} total objects in scene. " +
                               $"Nearest: {nearestObject} at {minDistance:F1} meters. " +
                               $"Furthest: {furthestObject} at {maxDistance:F1} meters. " +
                               $"{objectsOver20m} objects beyond 20 meters. " +
                               $"Selection manager only sees {nearbyOnly} nearby objects.";
                
                MelonLogger.Msg($"[REGISTRY TEST] {result}");
                TolkScreenReader.Instance.Speak(result, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[REGISTRY TEST] Error: {ex}");
                TolkScreenReader.Instance.Speak($"Registry test failed with error: {ex.Message}", true);
            }
        }

        public void ScanSceneByDistance()
        {
            try
            {
                MelonLogger.Msg("[DISTANCE SCAN] Starting distance-based scene scan...");
                
                // Access the registry
                var registry = MouseOverHighlight.registry;
                if (registry == null || registry.Count == 0)
                {
                    TolkScreenReader.Instance.Speak("No objects found in scene", true);
                    return;
                }
                
                // Find player position
                Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
                if (playerPos == Vector3.zero)
                {
                    TolkScreenReader.Instance.Speak("Could not find player position", true);
                    return;
                }
                
                // Group objects by distance
                var immediate = new List<string>();  // 0-5m
                var nearby = new List<string>();     // 5-15m
                var shortWalk = new List<string>();  // 15-30m
                var mediumDist = new List<string>(); // 30-50m
                int distantCount = 0;                // 50m+
                
                foreach (var obj in registry)
                {
                    if (obj == null || obj.transform == null) continue;
                    
                    float distance = Vector3.Distance(playerPos, obj.transform.position);
                    string name = ObjectNameCleaner.GetBetterObjectName(obj);
                    
                    if (distance <= 5f)
                        immediate.Add($"{name} ({distance:F0}m)");
                    else if (distance <= 15f)
                        nearby.Add($"{name} ({distance:F0}m)");
                    else if (distance <= 30f)
                        shortWalk.Add($"{name} ({distance:F0}m)");
                    else if (distance <= 50f)
                        mediumDist.Add($"{name} ({distance:F0}m)");
                    else
                        distantCount++;
                }
                
                // Build report
                string report = $"Scene scan: {registry.Count} objects found.";
                
                if (immediate.Count > 0)
                    report += $" Right here: {string.Join(", ", immediate.Take(5).ToArray())}" + 
                             (immediate.Count > 5 ? $" and {immediate.Count - 5} more." : ".");
                             
                if (nearby.Count > 0)
                    report += $" Nearby: {string.Join(", ", nearby.Take(8).ToArray())}" + 
                             (nearby.Count > 8 ? $" and {nearby.Count - 8} more." : ".");
                             
                if (shortWalk.Count > 0)
                    report += $" Short walk: {string.Join(", ", shortWalk.Take(5).ToArray())}" + 
                             (shortWalk.Count > 5 ? $" and {shortWalk.Count - 5} more." : ".");
                             
                if (mediumDist.Count > 0)
                    report += $" Medium distance: {string.Join(", ", mediumDist.Take(3).ToArray())}" + 
                             (mediumDist.Count > 3 ? $" and {mediumDist.Count - 3} more." : ".");
                             
                if (distantCount > 0)
                    report += $" {distantCount} distant objects beyond 50 meters.";
                
                MelonLogger.Msg($"[DISTANCE SCAN] {report}");
                TolkScreenReader.Instance.Speak(report, true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DISTANCE SCAN] Error: {ex}");
                TolkScreenReader.Instance.Speak($"Distance scan failed: {ex.Message}", true);
            }
        }

        private string GetActiveSceneKey()
        {
            try
            {
                var player = GameObjectUtils.GetPlayerCharacter();
                if (player != null)
                {
                    string playerKey = BuildPlayerSceneKey(player);
                    if (!string.IsNullOrEmpty(playerKey))
                        return playerKey;
                }

                var activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                {
                    if (!string.IsNullOrEmpty(activeScene.path))
                        return activeScene.path;
                    if (!string.IsNullOrEmpty(activeScene.name))
                        return activeScene.name;
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[WAYPOINTS] Failed to determine active scene key: {ex}");
            }

            return "UnknownScene";
        }

        private string BuildPlayerSceneKey(Character player)
        {
            try
            {
                var scene = player.gameObject.scene;
                string baseKey = scene.IsValid()
                    ? (!string.IsNullOrEmpty(scene.path) ? scene.path : scene.name)
                    : null;

                string locationHint = TryGetLocationHint(player.transform);
                if (!string.IsNullOrEmpty(locationHint))
                {
                    if (string.IsNullOrEmpty(baseKey))
                        return locationHint;
                    return $"{baseKey}|{locationHint}";
                }

                return baseKey;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[WAYPOINTS] Failed to build player scene key: {ex}");
                return null;
            }
        }

        private string TryGetLocationHint(Transform transform)
        {
            if (transform == null)
                return null;

            var candidates = new List<string>();
            Transform current = transform.parent;
            int depth = 0;

            while (current != null && depth < 8)
            {
                string name = current.name;
                if (!string.IsNullOrWhiteSpace(name) && !IsGenericTransformName(name))
                {
                    if (IsLocationNameCandidate(name))
                    {
                        return name;
                    }

                    if (!candidates.Contains(name))
                    {
                        candidates.Add(name);
                    }
                }

                current = current.parent;
                depth++;
            }

            return candidates.Count > 0 ? candidates[0] : null;
        }

        private bool IsLocationNameCandidate(string name)
        {
            string lower = name.ToLowerInvariant();
            string[] keywords =
            {
                "interior", "exterior", "room", "shop", "store", "book", "whirling", "church",
                "kitchen", "office", "station", "dock", "apartment", "basement", "yard", "hall", "gallery"
            };

            return keywords.Any(lower.Contains) || lower.EndsWith("_int") || lower.EndsWith("_ext");
        }

        private bool IsGenericTransformName(string name)
        {
            string lower = name.ToLowerInvariant();
            string[] generic =
            {
                "root", "game", "scene", "characters", "character", "player", "harry", "armature", "skeleton", "hips"
            };

            if (generic.Any(g => lower == g))
                return true;

            return lower.StartsWith("char_") || lower.StartsWith("animation");
        }

        private string GetWaypointCategoryLabel(WaypointCategory? category) => category switch
        {
            WaypointCategory.NPCs => "NPC",
            WaypointCategory.Locations => "location",
            WaypointCategory.General => "general",
            null => "all",
            _ => "waypoint"
        };

        private void AnnounceWaypointSelection(string sceneKey, bool includeCategoryIntro, string prefix = null)
        {
            if (!waypointManager.TryGetSelection(sceneKey, out var waypoint, out int selectedIndex, out int totalCount))
            {
                string message = waypointManager.HasAnyWaypoints
                    ? $"No waypoints saved for this area. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one here."
                    : $"No waypoints saved yet. Press {KeyBindings.SpeakableName(GameKey.CreateWaypoint)} to create one.";
                TolkScreenReader.Instance.Speak(message, true);
                return;
            }

            Vector3 playerPos = GameObjectUtils.GetPlayerPosition();
            string controlsInstruction = $"Press {KeyBindings.SpeakableName(GameKey.CycleForward)} to cycle, {KeyBindings.SpeakableName(GameKey.NavigateToSelected)} to navigate, {KeyBindings.SpeakableName(GameKey.DeleteWaypoint)} to delete.";
            string announcement;

            if (playerPos != Vector3.zero)
            {
                float distance = Vector3.Distance(playerPos, waypoint.Position);
                string direction = DirectionCalculator.GetCardinalDirection(playerPos, waypoint.Position);
                announcement = $"{waypoint.Name} {distance:F0} meters {direction}, {selectedIndex + 1} of {totalCount}. {controlsInstruction}";
            }
            else
            {
                announcement = $"{waypoint.Name}, {selectedIndex + 1} of {totalCount}. {controlsInstruction}";
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                announcement = $"{prefix} {announcement}";
            }

            if (includeCategoryIntro)
            {
                WaypointCategory? filter = waypointManager.GetCategoryFilter(sceneKey);
                string label = GetWaypointCategoryLabel(filter);
                string intro = label == "all" ? "Waypoints" : $"{char.ToUpper(label[0])}{label.Substring(1)} waypoints";
                announcement = $"{intro}: {announcement}";
            }

            MelonLogger.Msg($"[WAYPOINTS] {announcement}");
            TolkScreenReader.Instance.Speak(announcement, true);
        }

        private void OnWaypointNamingCompleted(string finalName, Vector3 position, string sceneName, bool usedDefault)
        {
            namingSession = null;
            pendingWaypointPosition = position;
            pendingWaypointName = finalName;
            pendingWaypointScene = sceneName;
            isCategorySelectionActive = true;

            TolkScreenReader.Instance.Speak(
                $"Waypoint {finalName} named. Select category: 1 NPC, 2 location, 3 general, or press Enter for general.",
                true);
        }

        private void OnWaypointNamingCancelled()
        {
            namingSession = null;
            TolkScreenReader.Instance.Speak("Waypoint creation cancelled.", true);
        }
    }
}

