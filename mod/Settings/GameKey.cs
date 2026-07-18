namespace AccessibilityMod.Settings
{
    /// <summary>
    /// Every remappable action in the mod. Each maps to exactly one physical button
    /// (KeyCode + required modifiers) via <see cref="KeyBindings"/>. Contextual meaning
    /// (e.g. "select NPC category" vs. "select NPC waypoint filter" while in waypoint
    /// focus) is still resolved by InputManager based on game state - GameKey only
    /// identifies which physical button was pressed.
    /// </summary>
    public enum GameKey
    {
        AnnounceCurrentSelection,
        ToggleSortingMode,
        ScanSceneByDistance,

        SelectNpcs,
        SelectLocations,
        SelectLoot,
        SelectEverything,

        FocusWaypoints,
        CreateWaypoint,
        DeleteWaypoint,

        CycleForward,
        CycleBackward,
        CycleCategoryForward,
        CycleCategoryBackward,
        NavigateToSelected,
        InteractWithSelected,
        ToggleAutoInteract,
        StopMovement,

        ToggleDialogReading,
        ToggleDialogAutoAdvance,
        RepeatDialogue,
        ToggleOrbAnnouncements,
        ToggleSpeechInterrupt,
        ToggleDiagnostics,

        AnnounceStatus,
        AnnounceStats,
        AnnounceOfficerProfile,
        ReadSkillDescription,
        AnnounceKimStatus,

        /// <summary>Reports every name the selected object has, from every source. Diagnostic only - changes nothing.</summary>
        AnnounceNameSources,

        /// <summary>Repeats the description of the area you are standing in.</summary>
        DescribeArea,

        /// <summary>Repeats the long first-visit introduction of the area (what kind of place this is).</summary>
        DescribeAreaFull,

        /// <summary>Reads the game's own description of the selected object - "what even is a Glastara?".</summary>
        DescribeItem,

        /// <summary>Opens the mod debugger in its own window (live transcript + comments).</summary>
        OpenModDebugger,

        /// <summary>Next inventory tab (Tools, Clothes, Pawnables, Reading). The game's tab
        /// buttons are mouse-only - without this key, items in other tabs are unreachable.</summary>
        InventoryNextTab,

        /// <summary>Previous inventory tab.</summary>
        InventoryPrevTab,

        /// <summary>Consume a health healing charge - the keyboard equivalent of clicking
        /// the plus button on the health bar, which is mouse-only in the game.</summary>
        HealHealth,

        /// <summary>Consume a morale healing charge (the plus button on the morale bar).</summary>
        HealMorale,

        /// <summary>Close the thought-research splash screen (whose own close button is
        /// mouse-only). Was hardcoded Enter at first - made remappable because EVERY mod
        /// hotkey must be a GameKey, visible to the configurator (PR review finding 10).</summary>
        CloseSplash,
    }
}
