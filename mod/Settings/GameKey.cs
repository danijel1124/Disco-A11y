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
    }
}
