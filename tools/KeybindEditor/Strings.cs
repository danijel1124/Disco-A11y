using System.Globalization;

namespace KeybindEditor;

public enum Language
{
    English,
    German,
}

/// <summary>
/// Minimal localization: a flat key -> per-language string table. Not resx-based since
/// the app is small; keeps every user-facing string in one reviewable place.
/// </summary>
public static class Strings
{
    public static Language Current { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? Language.German : Language.English;

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        ["WindowTitle"] = ("Disco Elysium - Accessibility Mod Configurator", "Disco Elysium – Accessibility-Mod-Konfigurator"),
        ["GamePathLabel"] = ("Game folder:", "Spielordner:"),
        ["GamePathAccessible"] = ("Path to the Disco Elysium game folder", "Pfad zum Disco-Elysium-Spielordner"),
        ["Browse"] = ("Browse...", "Durchsuchen..."),
        ["BrowseDialogTitle"] = ("Select the Disco Elysium game folder", "Disco-Elysium-Spielordner auswählen"),
        ["BindingsListAccessible"] = ("List of key bindings", "Liste der Tastenbelegungen"),
        ["ColumnHeader"] = ("Action — Key", "Aktion — Taste"),
        ["Rebind"] = ("Rebind...", "Neu belegen..."),
        ["CancelRebind"] = ("Cancel rebind", "Neubelegung abbrechen"),
        ["ResetSelected"] = ("Reset to default", "Auf Standard zurücksetzen"),
        ["PresetDefault"] = ("Load preset: Default (US)", "Preset: US-Standard laden"),
        ["PresetSafe"] = ("Load preset: With numpad", "Preset: Mit Ziffernblock laden"),
        ["PresetStardew"] = ("Load preset: Without numpad", "Preset: Ohne Ziffernblock laden"),
        ["GeneralGroup"] = ("Other settings", "Weitere Einstellungen"),
        ["DialogModeLabel"] = ("Dialog reading mode:", "Dialog-Lesemodus:"),
        ["DialogModeOff"] = ("Off", "Aus"),
        ["DialogModeFull"] = ("Full text", "Volltext"),
        ["DialogModeSpeakerOnly"] = ("Speaker name only", "Nur Sprechername"),
        ["OrbAnnouncements"] = ("Orb announcements on", "Orb-Ansagen aktiv"),
        ["SpeechInterrupt"] = ("Speech interrupt on", "Sprachunterbrechung aktiv"),
        ["SpeakAudioCaptions"] = ("Speak sound captions", "Geräusch-Untertitel vorlesen"),
        ["DialogAutoAdvance"] = ("Auto-advance dialog after reading (autoread)", "Dialog nach dem Vorlesen automatisch weiterschalten (Autoread)"),
        ["AutoInteract"] = ("Auto-interact on arrival", "Bei Ankunft automatisch interagieren"),
        ["Save"] = ("Save", "Speichern"),
        ["StatusAccessible"] = ("Status", "Status"),
        ["LanguageLabel"] = ("Language:", "Sprache:"),

        ["StatusGamePathMissing"] = ("Game folder not found. Please use 'Browse...' to select it.", "Spielordner nicht gefunden. Bitte über 'Durchsuchen...' auswählen."),
        ["StatusConfigLoaded"] = ("Existing configuration loaded.", "Bestehende Konfiguration geladen."),
        ["StatusConfigNotFound"] = ("No existing configuration found - showing defaults (not saved yet).", "Keine bestehende Konfiguration gefunden - Standardwerte werden angezeigt (noch nicht gespeichert)."),
        ["StatusSelectFirst"] = ("Please select an action in the list first.", "Bitte zuerst eine Aktion in der Liste auswählen."),
        ["StatusPressKey"] = ("Now press the desired key (optionally with Ctrl/Alt/Shift), or use 'Cancel rebind'.", "Bitte jetzt die gewünschte Taste (optional mit Strg/Alt/Umschalt) drücken, oder 'Neubelegung abbrechen' nutzen."),
        ["StatusRebindCancelled"] = ("Rebind cancelled.", "Neubelegung abgebrochen."),
        ["StatusKeyUnsupported"] = ("Key '{0}' is not currently supported. Please choose a different key.", "Taste '{0}' wird aktuell nicht unterstützt. Bitte eine andere Taste wählen."),
        ["StatusRebindConflict"] = ("{0} is already used by '{1}'. Please choose a different key.", "{0} wird bereits von '{1}' verwendet. Bitte eine andere Taste wählen."),
        ["StatusRebound"] = ("'{0}' set to {1}. Don't forget to save.", "'{0}' auf {1} gesetzt. Nicht vergessen zu speichern."),
        ["StatusReset"] = ("'{0}' reset to default key.", "'{0}' auf Standardtaste zurückgesetzt."),
        ["StatusSafePreset"] = ("Numpad preset loaded (F-keys + numpad, avoids all game key collisions, layout-independent). Don't forget to save.", "Ziffernblock-Preset geladen (F-Tasten + Ziffernblock, vermeidet alle Kollisionen mit Spieltasten, layoutunabhängig). Nicht vergessen zu speichern."),
        ["StatusStardewPreset"] = ("No-numpad preset loaded (F-keys only, for keyboards without a numpad, layout-independent). Don't forget to save.", "Preset ohne Ziffernblock geladen (nur F-Tasten, für Tastaturen ohne Ziffernblock, layoutunabhängig). Nicht vergessen zu speichern."),
        ["StatusDefaultPreset"] = ("Default (US) preset loaded (the mod's original bindings). Don't forget to save.", "US-Standard-Preset geladen (Originalbelegung des Mods). Nicht vergessen zu speichern."),
        ["StatusSaved"] = ("Saved. Changes take effect on the next game launch.", "Gespeichert. Änderungen gelten ab dem nächsten Spielstart."),
        ["StatusSaveError"] = ("Error while saving: {0}", "Fehler beim Speichern: {0}"),
        ["SaveDialogTitle"] = ("Key Bindings", "Tastenbelegung"),

        ["ModCtrl"] = ("Ctrl+", "Strg+"),
        ["ModAlt"] = ("Alt+", "Alt+"),
        ["ModShift"] = ("Shift+", "Umschalt+"),
        ["NumpadPrefix"] = ("Numpad ", "Ziffernblock "),
    };

    public static string Get(string key) => Current == Language.German ? Table[key].De : Table[key].En;

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
