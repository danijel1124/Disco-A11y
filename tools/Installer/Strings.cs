using System.Globalization;

namespace Installer;

public enum Language
{
    English,
    German,
}

public static class Strings
{
    public static Language Current { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de" ? Language.German : Language.English;

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        ["WindowTitle"] = ("Disco Elysium Accessibility Mod - Installer", "Disco Elysium Accessibility Mod – Installer"),
        ["LanguageLabel"] = ("Language:", "Sprache:"),
        ["GamePathLabel"] = ("Game folder:", "Spielordner:"),
        ["Browse"] = ("Browse...", "Durchsuchen..."),
        ["BrowseDialogTitle"] = ("Select the Disco Elysium game folder", "Disco-Elysium-Spielordner auswählen"),
        ["Install"] = ("Install / Update", "Installieren / Aktualisieren"),
        ["OpenKeybindEditor"] = ("Open Mod Configurator", "Mod-Konfigurator öffnen"),
        ["PrereleaseCheck"] = ("Install latest prerelease (nightly)", "Aktuellstes Prerelease laden (Nightly)"),
        ["DevBridgeCheck"] = ("Enable AI dev bridge", "AI-Dev-Bridge aktivieren"),
        ["StepDevBridgeInstalled"] = ("AI dev bridge installed (Mods/DevBridge.dll). Command channel: UserData/DevBridge/.", "AI-Dev-Bridge installiert (Mods/DevBridge.dll). Kommandokanal: UserData/DevBridge/."),
        ["StepDevBridgeRemoved"] = ("AI dev bridge removed.", "AI-Dev-Bridge entfernt."),
        ["StepDevBridgeMissing"] = ("DevBridge.dll not found next to this installer - AI dev bridge skipped.", "DevBridge.dll liegt nicht neben diesem Installer - AI-Dev-Bridge übersprungen."),
        ["LogAccessible"] = ("Installation log", "Installationsprotokoll"),

        ["StatusGamePathMissing"] = ("Game folder not found. Please use 'Browse...' to select it.", "Spielordner nicht gefunden. Bitte über 'Durchsuchen...' auswählen."),
        ["StatusGameNotFoundAuto"] = ("Could not find Disco Elysium automatically. Please select the folder manually.", "Disco Elysium konnte nicht automatisch gefunden werden. Bitte Ordner manuell auswählen."),
        ["StatusGameFound"] = ("Found Disco Elysium at: {0}", "Disco Elysium gefunden unter: {0}"),

        ["StepCheckMelonLoader"] = ("Checking MelonLoader...", "Prüfe MelonLoader..."),
        ["StepMelonLoaderPresent"] = ("MelonLoader is already installed.", "MelonLoader ist bereits installiert."),
        ["StepMelonLoaderInstalling"] = ("Installing MelonLoader {0} ({1})...", "Installiere MelonLoader {0} ({1})..."),
        ["StepMelonLoaderDone"] = ("MelonLoader installed.", "MelonLoader installiert."),
        ["StepMelonLoaderFallback"] = ("Could not determine the latest MelonLoader version ({0}), falling back to {1}.", "Konnte neueste MelonLoader-Version nicht ermitteln ({0}), nutze stattdessen {1}."),

        ["StepDownloadingRelease"] = ("Downloading mod release {0}...", "Lade Mod-Release {0} herunter..."),
        ["StepExtracting"] = ("Extracting...", "Entpacke..."),
        ["StepCopying"] = ("Copying {0}...", "Kopiere {0}..."),
        ["StepDone"] = ("Installation complete. Launch the game to use the mod.", "Installation abgeschlossen. Starte das Spiel, um den Mod zu nutzen."),
        ["StepError"] = ("Error: {0}", "Fehler: {0}"),

        ["DialogTitle"] = ("Disco Elysium Accessibility Mod", "Disco Elysium Accessibility Mod"),
        ["InstallCompleteDialog"] = ("Installation complete. Launch the game to use the mod.", "Installation abgeschlossen. Starte das Spiel, um den Mod zu nutzen."),
        ["InstallErrorDialog"] = ("Installation failed: {0}", "Installation fehlgeschlagen: {0}"),

        ["GameRunningError"] = ("Disco Elysium is currently running - please close the game first, then install again.", "Disco Elysium läuft gerade - bitte zuerst das Spiel beenden und dann erneut installieren."),

        ["ReinstallPromptTitle"] = ("MelonLoader already installed", "MelonLoader bereits installiert"),
        ["ReinstallPromptText"] = ("MelonLoader is already installed. Reinstall it anyway?", "MelonLoader ist bereits installiert. Trotzdem neu installieren?"),

        ["KeybindEditorNotFound"] = ("Keybind Editor not found next to this installer. Build tools/KeybindEditor first.", "Tastenbelegungs-Editor liegt nicht neben diesem Installer. Bitte zuerst tools/KeybindEditor bauen."),

        ["ShortcutFileName"] = ("Disco Elysium - Accessibility Mod Configurator", "Disco Elysium - Accessibility-Mod-Konfigurator"),
        ["ShortcutDescription"] = ("Configure the Disco Elysium Accessibility Mod (keybinds and settings)", "Disco Elysium Accessibility Mod konfigurieren (Tasten und Einstellungen)"),
        ["StepShortcutCreated"] = ("Start Menu shortcut created: {0}", "Startmenü-Verknüpfung erstellt: {0}"),
        ["StepShortcutFailed"] = ("Could not create Start Menu shortcut: {0}", "Startmenü-Verknüpfung konnte nicht erstellt werden: {0}"),
        ["StepShortcutSkipped"] = ("Keybind Editor not found - skipping Start Menu shortcut.", "Tastenbelegungs-Editor nicht gefunden - Startmenü-Verknüpfung übersprungen."),
    };

    public static string Get(string key) => Current == Language.German ? Table[key].De : Table[key].En;

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
