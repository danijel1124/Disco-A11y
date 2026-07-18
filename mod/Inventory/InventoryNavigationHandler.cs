using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using MelonLoader;
using Il2CppDiscoPages.Elements.Inventory;
using Il2CppSunshine.Metric;
using Il2CppSunshine.Views;
using Il2CppPages.Gameplay.Inventory;
using Il2Cpp;
using AccessibilityMod.UI;
using AccessibilityMod.Utils;

namespace AccessibilityMod.Inventory
{
    public class InventoryNavigationHandler
    {
        private static InventoryNavigationHandler _instance;
        public static InventoryNavigationHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new InventoryNavigationHandler();
                }
                return _instance;
            }
        }

        private bool isInventoryOpen = false;
        private InventoryItemSlot lastSelectedSlot = null;
        private string lastAnnounced = "";
        private float lastAnnouncementTime = 0f;
        private const float ANNOUNCEMENT_COOLDOWN = 0.2f;

        // Track current tab
        private ItemTabGroup currentTab = ItemTabGroup.TOOLS;
        private PageSystemInventoryTabPanel lastTabPanel = null;

        public void Initialize()
        {
            MelonLogger.Msg("InventoryNavigationHandler initialized");
        }

        public void Update()
        {
            try
            {
                // Check if inventory is open
                CheckInventoryState();

                if (isInventoryOpen)
                {
                    // Check for selected inventory items
                    CheckSelectedInventoryItem();
                    
                    // Check for tab changes
                    CheckTabChanges();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error in InventoryNavigationHandler.Update: {ex}");
            }
        }

        private void CheckInventoryState()
        {
            try
            {
                // One source of truth for "is the inventory open?": the ViewController
                // (PR review cleanup - this poll used to run three FindObjectOfType scene
                // scans per frame AND could disagree with IsInventoryViewOpen, which the
                // key handling already trusted).
                bool wasOpen = isInventoryOpen;
                isInventoryOpen = IsInventoryViewOpen;

                // Announce state changes
                if (isInventoryOpen && !wasOpen)
                {
                    OnInventoryOpened();
                }
                else if (!isInventoryOpen && wasOpen)
                {
                    OnInventoryClosed();
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking inventory state: {ex}");
            }
        }

        private void CheckSelectedInventoryItem()
        {
            try
            {
                // Check EventSystem for selected GameObject
                var eventSystem = EventSystem.current;
                if (eventSystem == null) return;

                var selected = eventSystem.currentSelectedGameObject;
                if (selected == null) return;

                // Check if it's an inventory slot
                var inventorySlot = selected.GetComponent<InventoryItemSlot>();
                if (inventorySlot == null)
                {
                    // Check parent for inventory slot
                    inventorySlot = selected.GetComponentInParent<InventoryItemSlot>();
                }

                if (inventorySlot != null && inventorySlot != lastSelectedSlot)
                {
                    lastSelectedSlot = inventorySlot;
                    AnnounceInventoryItem(inventorySlot);
                }

                // Also check for equipment slots
                var equipmentSlot = selected.GetComponent<InventoryEquipmentSlot>();
                if (equipmentSlot == null)
                {
                    equipmentSlot = selected.GetComponentInParent<InventoryEquipmentSlot>();
                }

                if (equipmentSlot != null)
                {
                    AnnounceEquipmentSlot(equipmentSlot);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking selected inventory item: {ex}");
            }
        }

        private void CheckTabChanges()
        {
            try
            {
                // Find active tab panel
                var tabPanel = UnityEngine.Object.FindObjectOfType<PageSystemInventoryTabPanel>();
                if (tabPanel == null) return;

                if (tabPanel != lastTabPanel)
                {
                    lastTabPanel = tabPanel;
                    // Tab panel changed, check current tab
                    CheckCurrentTab(tabPanel);
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking tab changes: {ex}");
            }
        }

        private void CheckCurrentTab(PageSystemInventoryTabPanel tabPanel)
        {
            try
            {
                // Check for active tab buttons
                var tabButtons = tabPanel.GetComponentsInChildren<PageSystemInventoryTabButton>();
                foreach (var button in tabButtons)
                {
                    if (button != null && button.gameObject.activeInHierarchy)
                    {
                        // Check if this button is selected
                        var selectable = button.GetComponent<UnityEngine.UI.Selectable>();
                        if (selectable != null && EventSystem.current != null && 
                            EventSystem.current.currentSelectedGameObject == button.gameObject)
                        {
                            // Get tab name from button
                            var text = UIElementFormatter.ExtractTextFromGameObject(button.gameObject);
                            if (!string.IsNullOrEmpty(text))
                            {
                                AnnounceText($"Tab: {text}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error checking current tab: {ex}");
            }
        }

        private void AnnounceInventoryItem(InventoryItemSlot slot)
        {
            try
            {
                if (slot == null) return;

                string announcement = "";

                // Get item information
                if (slot.item != null)
                {
                    var item = slot.item;
                    
                    // Get item name
                    string itemName = GetItemDisplayName(item);
                    if (string.IsNullOrEmpty(itemName))
                    {
                        itemName = slot.itemName;
                    }

                    // Get item description
                    string description = GetItemDescription(item);

                    // Get item type and group
                    string itemType = GetItemTypeString(item);

                    // Build announcement
                    announcement = itemName;
                    
                    if (!string.IsNullOrEmpty(itemType))
                    {
                        announcement += $", {itemType}";
                    }

                    if (item.substance)
                    {
                        announcement += ", Consumable";
                        if (item.substanceActive)
                        {
                            announcement += " (Active)";
                        }

                        // Add number of uses for multi-use items
                        if (item.substanceUses > 0)
                        {
                            announcement += $", {item.substanceUses} use{(item.substanceUses != 1 ? "s" : "")} remaining";
                        }

                        // Add substance effects (bonuses/penalties when consumed)
                        string substanceEffects = GetSubstanceEffects(item);
                        if (!string.IsNullOrEmpty(substanceEffects))
                        {
                            announcement += $" - Effects: {substanceEffects}";
                        }
                    }

                    if (item.cursed)
                    {
                        announcement += ", Cursed";
                    }

                    // Add description if available
                    if (!string.IsNullOrEmpty(description) && description != itemName)
                    {
                        announcement += $". {description}";
                    }
                }
                else if (!string.IsNullOrEmpty(slot.itemName))
                {
                    // Slot has name but no item
                    announcement = slot.itemName;
                }
                else
                {
                    // Empty slot
                    announcement = "Empty slot";
                }

                AnnounceText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing inventory item: {ex}");
            }
        }

        private void AnnounceEquipmentSlot(InventoryEquipmentSlot slot)
        {
            try
            {
                if (slot == null) return;

                string announcement = "";

                // Get slot type
                string slotType = GetEquipmentSlotType(slot);
                
                // Check if slot has an item by looking at the item name
                if (!string.IsNullOrEmpty(slot.prevItemName))
                {
                    announcement = $"{slotType}: {slot.prevItemName}";
                }
                else
                {
                    announcement = $"{slotType}: Empty";
                }

                AnnounceText(announcement);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error announcing equipment slot: {ex}");
            }
        }

        private string GetItemDisplayName(InventoryItem item)
        {
            try
            {
                if (item == null) return "";

                // Try display name property
                string name = item.displayName;
                if (!string.IsNullOrEmpty(name)) return RTLHelper.FixForScreenReader(name);

                // Try list name
                name = item.listName;
                if (!string.IsNullOrEmpty(name)) return RTLHelper.FixForScreenReader(name);

                // Try getting from game object name
                return item.gameObject.name.Replace("_", " ");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting item display name: {ex}");
                return "";
            }
        }

        private string GetItemDescription(InventoryItem item)
        {
            try
            {
                if (item == null) return "";
                return RTLHelper.FixForScreenReader(item.description);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting item description: {ex}");
                return "";
            }
        }

        private string GetSubstanceEffects(InventoryItem item)
        {
            try
            {
                if (item == null || item.substanceBuffs == null || item.substanceBuffs.Count == 0)
                    return null;

                var effectStrings = new List<string>();

                // Iterate through substance buffs
                foreach (var buff in item.substanceBuffs)
                {
                    if (buff == null || buff.effects == null)
                        continue;

                    // Each buff contains an array of CharacterEffects
                    foreach (var effect in buff.effects)
                    {
                        if (effect == null)
                            continue;

                        // Format the effect using the same method as thoughts
                        string effectDesc = FormatCharacterEffect(effect);
                        if (!string.IsNullOrEmpty(effectDesc))
                        {
                            effectStrings.Add(effectDesc);
                        }
                    }
                }

                return effectStrings.Count > 0 ? string.Join(", ", effectStrings) : null;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting substance effects: {ex}");
                return null;
            }
        }

        private string FormatCharacterEffect(CharacterEffect effect)
        {
            try
            {
                if (effect == null)
                    return null;

                // Use the game's own EffectName method to get the formatted effect string
                string effectName = RTLHelper.FixForScreenReader(effect.EffectName(false, false, false, true));

                // Filter out empty, null, or technical effects
                if (
                    string.IsNullOrEmpty(effectName)
                    || effectName.Contains("LUA_")
                    || effectName.Contains("COMMAND")
                    || effectName.Trim() == "0"
                    || effectName.Contains("+0 ")
                    || effectName.Contains("-0 ")
                )
                {
                    return null;
                }

                return effectName.Trim();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error formatting character effect: {ex}");
                return null;
            }
        }

        private string GetItemTypeString(InventoryItem item)
        {
            try
            {
                if (item == null) return "";

                // Get item type
                string typeStr = item.type.ToString();
                
                // Get item group
                string groupStr = item.group.ToString();

                // Combine if different
                if (typeStr != groupStr && !string.IsNullOrEmpty(groupStr))
                {
                    return $"{typeStr}, {groupStr}";
                }
                
                return typeStr;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error getting item type: {ex}");
                return "";
            }
        }

        private string GetEquipmentSlotType(InventoryEquipmentSlot slot)
        {
            try
            {
                // Try to get slot type from the slot itself
                return slot.slotType.ToString().Replace("_", " ");
            }
            catch (Exception ex)
            {
                try
                {
                    // Try to get from UI text as fallback
                    var text = UIElementFormatter.ExtractTextFromGameObject(slot.gameObject);
                    if (!string.IsNullOrEmpty(text))
                    {
                        return text;
                    }
                }
                catch (Exception ex2)
                {
                    MelonLogger.Error($"Error getting equipment slot text: {ex2}");
                }

                MelonLogger.Error($"Error getting equipment slot type: {ex}");
                return "Equipment slot";
            }
        }

        private void OnInventoryOpened()
        {
            // No announcement needed - it's obvious from context
            lastSelectedSlot = null;
        }

        private void OnInventoryClosed()
        {
            // No announcement needed - it's obvious from context
            lastSelectedSlot = null;
            lastTabPanel = null;
        }

        private void AnnounceText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (text == lastAnnounced && Time.time - lastAnnouncementTime < ANNOUNCEMENT_COOLDOWN) return;

            TolkScreenReader.Instance.Speak(text, true);
            lastAnnounced = text;
            lastAnnouncementTime = Time.time;
        }

        public void OnInventoryViewOpened(InventoryView view)
        {
            // Called from patch
            isInventoryOpen = true;
            OnInventoryOpened();
        }

        public void OnInventoryViewClosed()
        {
            // Called from patch
            isInventoryOpen = false;
            OnInventoryClosed();
        }

        public void OnInventoryItemSelected(InventoryItemSlot slot)
        {
            // Called from patch
            if (slot != lastSelectedSlot)
            {
                lastSelectedSlot = slot;
                AnnounceInventoryItem(slot);
            }
        }

        public void OnTabChanged(ItemTabGroup newTab)
        {
            // Called from patch
            if (newTab != currentTab)
            {
                currentTab = newTab;
                AnnounceText(DescribeTab(newTab));
            }
        }

        // One-shot suppression of the auto-select announcement after a tab switch.
        // The game auto-selects the new tab's first cell right after SwitchTab, and its
        // OnSelect announcement would collide with the tab announcement, which already
        // carries the first item's name (both interrupt -> the player hears only 40 ms
        // of one of them, verified live 17.07.2026). A 0.6 s TIME WINDOW sat here
        // before and swallowed EVERY announcement inside it - including the user's own
        // fast arrow moves right after the switch (PR review finding 5; Jana's pick:
        // one-shot flag). The timestamp is only a staleness backstop: should the
        // auto-select ever not fire, an armed flag must not lie in wait and swallow the
        // user's next real move much later.
        private static bool suppressNextSelectAnnouncement;
        private static float suppressArmedTime;

        /// <summary>Arm the one-shot suppression - called by SwitchTab only.</summary>
        private static void ArmTabSwitchSuppression()
        {
            suppressNextSelectAnnouncement = true;
            suppressArmedTime = UnityEngine.Time.unscaledTime;
        }

        /// <summary>
        /// True exactly once per tab switch: for the first OnSelect announcement after
        /// arming (the game's auto-select). A stale flag (no auto-select within 1 s)
        /// reports false, so a late real move is never swallowed.
        /// </summary>
        public static bool ConsumeTabSwitchSuppression()
        {
            if (!suppressNextSelectAnnouncement) return false;
            suppressNextSelectAnnouncement = false;
            return UnityEngine.Time.unscaledTime - suppressArmedTime < 1f;
        }

        /// <summary>
        /// THE tab order - the panel's left-to-right order, which is also how the game's
        /// InventoryManager.CurrentTab int counts. Single source of truth: SwitchTab,
        /// DescribeCurrentTab and the CurrentTab->enum mapping in InventoryPatches all
        /// derive from this one array (it used to be encoded four times; PR review
        /// cleanup).
        /// </summary>
        internal static readonly ItemTabGroup[] TabOrder =
            { ItemTabGroup.TOOLS, ItemTabGroup.CLOTHES, ItemTabGroup.PAWNABLES, ItemTabGroup.READING };

        /// <summary>The game's CurrentTab int as an ItemTabGroup, clamped to a valid tab.</summary>
        internal static ItemTabGroup TabFromIndex(int index) =>
            TabOrder[index >= 0 && index < TabOrder.Length ? index : 0];

        /// <summary>
        /// Public entry for "where am I in the inventory?" - the current tab and how many
        /// items it holds, "keine Objekte" when empty. Used by the on-demand announce key
        /// as a non-interrupting fallback when no item is focused (bug #2).
        /// </summary>
        public static string DescribeCurrentTab()
        {
            try
            {
                var manager = Il2CppSunshine.InventoryManager.Singleton;
                return DescribeTab(TabFromIndex(manager != null ? manager.CurrentTab : 0));
            }
            catch (Exception ex)
            {
                // Say honestly that the tab is unreadable - "no items" here would sell an
                // interop error as an empty inventory (PR review cleanup).
                MelonLogger.Warning($"[Inventory] DescribeCurrentTab failed: {ex.Message}");
                return Settings.Loc.Get("InvTabReadError");
            }
        }

        /// <summary>
        /// Localized tab name plus how many items it holds ("Tab Kleidung: 4 Gegenstände.") -
        /// the count is what tells a blind player whether the tab is worth walking through.
        /// If the tab has items, the auto-selected first one is appended ("Ausgewählt: ...")
        /// because its own OnSelect announcement is suppressed during a tab switch (see
        /// LastTabSwitchTime).
        /// </summary>
        private static string DescribeTab(ItemTabGroup tab)
        {
            string name = Settings.Loc.Get("InvTab_" + tab);
            int count = 0;
            bool countReadable = false;
            string firstItem = null;
            try
            {
                var data = Il2CppSunshine.Metric.InventoryViewData.Singleton;
                var tabs = data?.tabContents;
                if (tabs != null && tabs.ContainsKey(tab) && tabs[tab] != null)
                {
                    count = tabs[tab].Count;
                    countReadable = true;

                    // Slot indices are contiguous from 0 (the game compacts them), so
                    // slot 0 is normally the item the game auto-selects after a tab
                    // switch - but guard against a hole at 0 by falling back to the
                    // lowest occupied slot (PR review cleanup).
                    if (count > 0)
                    {
                        int firstSlot = int.MaxValue;
                        if (tabs[tab].ContainsKey(0))
                        {
                            firstSlot = 0;
                        }
                        else
                        {
                            foreach (var slotIndex in tabs[tab].Keys)
                            {
                                if (slotIndex < firstSlot) firstSlot = slotIndex;
                            }
                        }
                        if (firstSlot != int.MaxValue)
                        {
                            // The library resolves the internal key ("gloves_garden") to
                            // the localized display name ("Gelbe Gartenhandschuhe"), with
                            // the shared listName/raw-key fallback chain.
                            firstItem = Patches.InventoryHighlighterHelper.GetItemDisplayName(
                                tabs[tab][firstSlot], data);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Logged, and the announcement stays NEUTRAL below: claiming "no items"
                // on an interop error would sell a full tab as empty (PR review cleanup).
                MelonLogger.Warning($"[Inventory] DescribeTab({tab}) failed: {ex.Message}");
            }

            // "keine Objekte" for an empty tab (Danijel's option 2), a real count when we
            // have one, only the tab name when the count could not be read.
            string text = !countReadable
                ? Settings.Loc.Get("InvTabNoCount", name)
                : count == 0
                    ? Settings.Loc.Get("InvTabEmpty", name)
                    : Settings.Loc.Get(count == 1 ? "InvTabWithCountOne" : "InvTabWithCount", name, count);
            if (!string.IsNullOrEmpty(firstItem))
            {
                text += Settings.Loc.Get("InvTabFirstItem", firstItem);
            }
            return text;
        }

        /// <summary>
        /// Ctrl+Tab / Ctrl+Shift+Tab: cycle the inventory tabs (Tools, Clothes, Pawnables,
        /// Reading) with wrap-around. The game's tab buttons are mouse-only, so without
        /// this key every item outside the currently shown tab is simply unreachable for
        /// a keyboard player (bug #55: "my gloves and tape reel are gone").
        ///
        /// IMPORTANT - which tab API is the real one: the game ships TWO inventory UI
        /// systems. PageSystemInventoryTabPanel (DiscoPages) is DEAD CODE on PC -
        /// FindObjectOfType returns null even with the inventory wide open (verified
        /// live 17.07.2026; same finding as the ScreenAnnouncer counter bug). The PC
        /// inventory is driven by Il2CppSunshine.InventoryManager: CurrentTab (int,
        /// writable) plus UpdateCurrentlySelectedTab(), which repaints the panel and -
        /// through our existing Harmony patch on it - triggers the OnTabChanged
        /// announcement. So this method only writes the game's own state and pokes the
        /// game's own refresh; no UI simulation anywhere.
        /// </summary>
        public void SwitchTab(bool backward)
        {
            try
            {
                // Singleton exists for the whole session; the VIEW gate (is the inventory
                // actually open?) sits in InputManager, not here. So a null manager with
                // the inventory OPEN is an internal error - the message says
                // "unavailable", not "open the inventory first" (which would be wrong
                // advice in the only state this line can play; PR review cleanup).
                var manager = Il2CppSunshine.InventoryManager.Singleton;
                if (manager == null)
                {
                    MelonLogger.Warning("[Inventory] SwitchTab: InventoryManager.Singleton is null with the inventory open");
                    TolkScreenReader.Instance.Speak(Settings.Loc.Get("InvTabUnavailable"), true);
                    return;
                }

                // Tab indices count along TabOrder (the panel's left-to-right order).
                // Adding (length - 1) instead of subtracting 1 keeps the modulo positive.
                int tabCount = TabOrder.Length;
                int next = (manager.CurrentTab + (backward ? tabCount - 1 : 1)) % tabCount;

                manager.CurrentTab = next;
                // Arm the one-shot suppression BEFORE the refresh below: the game's
                // auto-select of the new tab's first item fires inside
                // UpdateCurrentlySelectedTab, and its OnSelect announcement must stay
                // silent (the tab announcement already carries the item's name).
                ArmTabSwitchSuppression();
                // The game's own refresh: repaints the grid for the new tab and fires our
                // UpdateCurrentlySelectedTab patch, which announces "Tab Kleidung: 4
                // Gegenstände. Ausgewählt: ..." - so there is no separate announcement here.
                manager.UpdateCurrentlySelectedTab();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"Error switching inventory tab: {ex}");
            }
        }

        /// <summary>True while the inventory screen is the current view - the tab keys only make sense there.</summary>
        public static bool IsInventoryViewOpen
        {
            get
            {
                try
                {
                    var view = Il2CppSunshine.Views.ViewController.GetCurrentView();
                    if (view == null) return false;
                    var type = view.GetViewType();
                    return type == Il2CppSunshine.Views.ViewType.INVENTORY
                        || type == Il2CppSunshine.Views.ViewType.INVENTORY_PAWN;
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// True while the PAWN SHOP variant of the inventory is the current view (it has
        /// no tabs; everything sits in the PAWNABLES group). Same single source of truth
        /// as IsInventoryViewOpen.
        /// </summary>
        public static bool IsPawnShopOpen
        {
            get
            {
                try
                {
                    var view = Il2CppSunshine.Views.ViewController.GetCurrentView();
                    return view != null
                        && view.GetViewType() == Il2CppSunshine.Views.ViewType.INVENTORY_PAWN;
                }
                catch
                {
                    return false;
                }
            }
        }

        public ItemTabGroup GetCurrentTab()
        {
            // Read current tab from game's state instead of tracking it ourselves
            Il2Cpp.ItemTabGroup gameTab = Il2Cpp.ItemTabGroup.TOOLS; // fallback

            // Try PageSystem first (the active inventory system)
            var pageSystemPanel = UnityEngine.Object.FindObjectOfType<Il2CppDiscoPages.Elements.Inventory.PageSystemInventoryTabPanel>();
            if (pageSystemPanel != null)
            {
                gameTab = pageSystemPanel.CurrentItemTabGroup;
            }
            else
            {
                // Fallback to singleton if PageSystem not available
                var inventoryTabPanel = Il2Cpp.InventoryTabPanel.Singleton;
                if (inventoryTabPanel != null)
                {
                    gameTab = inventoryTabPanel.CurrentItemTabGroup;
                }
            }

            return gameTab;
        }
    }
}