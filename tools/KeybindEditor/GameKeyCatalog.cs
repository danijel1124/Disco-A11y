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

        new("CycleForward", "Cycle category forward", "In Kategorie weiter", "Period|False|False|False", "PageDown|False|False|False", "PageDown|False|False|False"),
        new("CycleBackward", "Cycle category backward", "In Kategorie zurück", "Period|False|False|True", "PageUp|False|False|False", "PageUp|False|False|False"),
        new("NavigateToSelected", "Walk to selected object", "Zum ausgewählten Objekt gehen", "Comma|False|False|False", "Home|True|False|False", "Home|True|False|False"),
        new("StopMovement", "Stop movement", "Bewegung stoppen", "Slash|False|False|False", "Space|False|False|False", "Space|False|False|False"),

        new("ToggleDialogReading", "Toggle dialog reading mode", "Dialog-Lesemodus umschalten", "Minus|False|False|False", "KeypadDivide|False|False|False", "F12|False|False|False"),
        new("RepeatDialogue", "Repeat last dialogue line", "Letzte Dialogzeile wiederholen", "R|False|False|False", "R|False|False|False", "R|False|False|False"),
        new("ToggleOrbAnnouncements", "Toggle orb announcements", "Orb-Ansagen umschalten", "Alpha0|False|False|False", "Alpha0|False|False|False", "Alpha0|False|False|False"),
        new("ToggleSpeechInterrupt", "Toggle speech interrupt", "Sprachunterbrechung umschalten", "Alpha8|False|False|False", "F11|False|False|False", "F11|False|False|False"),
        new("ToggleDiagnostics", "Toggle encoding diagnostics", "Encoding-Diagnose umschalten", "Alpha9|True|False|False", "B|True|False|False", "B|True|False|False"),

        new("AnnounceStatus", "Announce status (health/morale)", "Status ansagen (Gesundheit/Moral)", "H|False|False|False", "H|False|False|False", "H|False|False|False"),
        new("AnnounceStats", "Announce stats (time/money/experience)", "Werte ansagen (Zeit/Geld/Erfahrung)", "X|False|False|False", "X|False|False|False", "X|False|False|False"),
        new("AnnounceOfficerProfile", "Announce officer profile", "Beamtenprofil ansagen", "O|False|False|False", "O|False|False|False", "O|False|False|False"),
        new("ReadSkillDescription", "Read skill description", "Skill-Beschreibung vorlesen", "N|False|False|False", "N|False|False|False", "N|False|False|False"),
        new("AnnounceKimStatus", "Announce Kim dialogue status", "Kim-Dialogstatus ansagen", "K|False|False|False", "K|False|False|False", "K|False|False|False"),
    };
}
