namespace KeybindEditor;

/// <summary>
/// A single remappable action. Must match the GameKey enum names and default/preset
/// bindings in mod/Settings/KeyBindings.cs exactly - Name is used as-is as the cfg key.
/// SafeBinding mirrors NumpadSafePreset, StardewBinding mirrors StardewPreset (which
/// uses no numpad key at all).
/// </summary>
public sealed record ActionInfo(string Name, string LabelEn, string LabelDe, string DefaultBinding, string SafeBinding, string StardewBinding)
{
    public string Label => Strings.Current == Language.German ? LabelDe : LabelEn;
}

public static class GameKeyCatalog
{
    // Binding format: "UnityKeyCodeName|RequireCtrl|RequireAlt|RequireShift"
    public static readonly ActionInfo[] Actions =
    {
        new("AnnounceCurrentSelection", "Announce current selection", "Aktuelle Auswahl ansagen", "BackQuote|False|False|False", "F6|False|False|False", "F6|False|False|False"),
        new("ToggleSortingMode", "Toggle sorting mode (distance/direction)", "Sortiermodus umschalten (Distanz/Richtung)", "Semicolon|False|False|False", "F7|False|False|False", "F7|False|False|False"),
        new("ScanSceneByDistance", "Scan scene by distance", "Szene nach Distanz abtasten", "Quote|False|False|False", "F8|False|False|False", "F8|False|False|False"),

        new("SelectNpcs", "Category: select NPCs", "Kategorie: Personen auswählen", "LeftBracket|False|False|False", "F2|False|False|False", "F2|False|False|False"),
        new("SelectLocations", "Category: select locations", "Kategorie: Orte auswählen", "RightBracket|False|False|False", "F3|False|False|False", "F3|False|False|False"),
        new("SelectLoot", "Category: select loot/containers", "Kategorie: Beute/Behälter auswählen", "Backslash|False|False|False", "F4|False|False|False", "F4|False|False|False"),
        new("SelectEverything", "Category: select everything", "Kategorie: Alles auswählen", "Equals|False|False|False", "Keypad0|False|False|False", "F10|False|False|False"),

        new("FocusWaypoints", "Focus waypoints", "Wegpunkte fokussieren", "LeftBracket|True|False|False", "F2|True|False|False", "F2|True|False|False"),
        new("CreateWaypoint", "Create waypoint", "Wegpunkt erstellen", "LeftBracket|False|True|False", "F2|False|True|False", "F2|False|True|False"),
        new("DeleteWaypoint", "Delete waypoint", "Wegpunkt löschen", "RightBracket|False|True|False", "F3|False|True|False", "F3|False|True|False"),

        new("CycleForward", "Cycle objects forward", "Objekte weiter", "Period|False|False|False", "PageDown|False|False|False", "PageDown|False|False|False"),
        new("CycleBackward", "Cycle objects backward", "Objekte zurück", "Period|False|False|True", "PageUp|False|False|False", "PageUp|False|False|False"),
        new("CycleCategoryForward", "Next category", "Nächste Kategorie", "PageDown|True|False|False", "PageDown|True|False|False", "PageDown|True|False|False"),
        new("CycleCategoryBackward", "Previous category", "Vorherige Kategorie", "PageUp|True|False|False", "PageUp|True|False|False", "PageUp|True|False|False"),
        new("NavigateToSelected", "Walk to selected object", "Zum ausgewählten Objekt gehen", "Comma|False|False|False", "Home|True|False|False", "Home|True|False|False"),
        new("InteractWithSelected", "Interact with selected object", "Mit ausgewähltem Objekt interagieren", "F|False|False|False", "F|False|False|False", "F|False|False|False"),
        new("ToggleAutoInteract", "Toggle auto-interact on arrival", "Auto-Interaktion bei Ankunft umschalten", "F|True|False|False", "F|True|False|False", "F|True|False|False"),
        new("StopMovement", "Stop movement", "Bewegung stoppen", "Slash|False|False|False", "Space|False|False|False", "Space|False|False|False"),

        new("ToggleDialogReading", "Toggle dialog reading mode", "Dialog-Lesemodus umschalten", "Minus|False|False|False", "KeypadDivide|False|False|False", "F12|False|False|False"),
        new("ToggleDialogAutoAdvance", "Toggle dialog auto-advance (autoread)", "Automatisches Weiterschalten im Dialog umschalten (Autoread)", "G|False|False|False", "G|False|False|False", "G|False|False|False"),
        new("RepeatDialogue", "Repeat last dialogue line", "Letzte Dialogzeile wiederholen", "R|False|False|False", "R|False|False|False", "R|False|False|False"),
        new("ToggleOrbAnnouncements", "Toggle orb announcements", "Orb-Ansagen umschalten", "Alpha0|False|False|False", "Alpha0|False|False|False", "Alpha0|False|False|False"),
        new("ToggleSpeechInterrupt", "Toggle speech interrupt", "Sprachunterbrechung umschalten", "Alpha8|False|False|False", "F11|False|False|False", "F11|False|False|False"),
        new("ToggleDiagnostics", "Toggle encoding diagnostics", "Encoding-Diagnose umschalten", "Alpha9|True|False|False", "B|True|False|False", "B|True|False|False"),

        new("AnnounceStatus", "Announce status (health/morale)", "Status ansagen (Gesundheit/Moral)", "H|False|False|False", "H|False|False|False", "H|False|False|False"),
        new("AnnounceStats", "Announce stats (time/money/experience)", "Werte ansagen (Zeit/Geld/Erfahrung)", "X|False|False|False", "X|False|False|False", "X|False|False|False"),
        new("AnnounceOfficerProfile", "Announce officer profile", "Beamtenprofil ansagen", "O|False|False|False", "O|False|False|False", "O|False|False|False"),
        new("ReadSkillDescription", "Read skill description", "Skill-Beschreibung vorlesen", "N|False|False|False", "N|False|False|False", "N|False|False|False"),
        new("AnnounceKimStatus", "Announce Kim dialogue status", "Kim-Dialogstatus ansagen", "K|False|False|False", "K|False|False|False", "K|False|False|False"),
        new("AnnounceNameSources", "Announce where the selected object's name comes from (diagnostics)", "Namensquellen des ausgewählten Objekts ansagen (Diagnose)", "N|True|False|False", "N|True|False|False", "N|True|False|False"),
        new("DescribeArea", "Describe the area you are in", "Beschreibung des aktuellen Bereichs", "U|False|False|False", "U|False|False|False", "U|False|False|False"),
        new("DescribeAreaFull", "Full introduction of the area (what kind of place is this?)", "Ausführliche Einführung des Bereichs (was ist das hier für ein Ort?)", "U|True|False|False", "U|True|False|False", "U|True|False|False"),
        new("DescribeItem", "Describe the selected object (what even is this?)", "Das ausgewählte Objekt beschreiben (was ist das überhaupt?)", "B|False|False|False", "B|False|False|False", "B|False|False|False"),
        new("OpenModDebugger", "Open the mod debugger window (debug mode only)", "Mod-Debugger-Fenster öffnen (nur im Debug-Modus)", "Y|True|False|False", "Y|True|False|False", "Y|True|False|False"),

        new("InventoryNextTab", "Inventory: next tab", "Inventar: nächster Tab", "Tab|True|False|False", "Tab|True|False|False", "Tab|True|False|False"),
        new("InventoryPrevTab", "Inventory: previous tab", "Inventar: vorheriger Tab", "Tab|True|False|True", "Tab|True|False|True", "Tab|True|False|True"),
        // Healing shares base key H with AnnounceStatus on purpose (H = the "how are
        // health and morale?" key); digits are off limits - the game reads them in
        // dialogue regardless of Ctrl and would commit a dialogue option.
        new("HealHealth", "Use a health healing charge", "Gesundheits-Heilladung verwenden", "H|True|False|False", "H|True|False|False", "H|True|False|False"),
        new("HealMorale", "Use a morale healing charge", "Moral-Heilladung verwenden", "H|False|False|True", "H|False|False|True", "H|False|False|True"),
        new("CloseSplash", "Close the thought research screen", "Forschungsergebnis-Bildschirm schließen", "Return|False|False|False", "Return|False|False|False", "Return|False|False|False"),
    };
}
