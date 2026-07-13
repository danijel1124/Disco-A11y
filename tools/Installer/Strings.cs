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
        ["ModVersionFound"] = ("Found installed mod {0} - overwriting with {1}.", "Installierten Mod {0} gefunden - überschreibe mit {1}."),
        ["ModOverwritePrompt"] = ("Found installed mod {0}. Overwrite with {1}?", "Installierter Mod: {0}. Mit {1} überschreiben?"),
        ["ModOverwriteSkipped"] = ("Mod installation skipped - existing version kept.", "Mod-Installation übersprungen - bestehende Version bleibt."),
        ["StepExtracting"] = ("Extracting...", "Entpacke..."),
        ["StepCopying"] = ("Copying {0}...", "Kopiere {0}..."),
        ["StepDone"] = ("Installation complete. Launch the game to use the mod.", "Installation abgeschlossen. Starte das Spiel, um den Mod zu nutzen."),
        ["StepError"] = ("Error: {0}", "Fehler: {0}"),

        ["DialogTitle"] = ("Disco Elysium Accessibility Mod", "Disco Elysium Accessibility Mod"),
        ["InstallCompleteDialog"] = ("Installation complete. Launch the game to use the mod.", "Installation abgeschlossen. Starte das Spiel, um den Mod zu nutzen."),
        ["InstallErrorDialog"] = ("Installation failed: {0}", "Installation fehlgeschlagen: {0}"),

        ["GameRunningError"] = ("Disco Elysium is currently running - please close the game first, then install again.", "Disco Elysium läuft gerade - bitte zuerst das Spiel beenden und dann erneut installieren."),

        ["UpdateChecking"] = ("Checking for installer updates...", "Prüfe auf Installer-Updates..."),
        ["UpdateDownloading"] = ("A newer installer is available ({0}) - updating and restarting...", "Ein neuerer Installer ist verfügbar ({0}) - aktualisiere und starte neu..."),
        ["UpdateCheckFailed"] = ("Installer update check failed ({0}). Installation is not possible with a possibly outdated installer - please check your internet connection or download the latest setup from the releases page.", "Installer-Update-Prüfung fehlgeschlagen ({0}). Mit einem womöglich veralteten Installer ist keine Installation möglich - bitte Internetverbindung prüfen oder das neueste Setup von der Release-Seite laden."),
        ["UpdateConfirm"] = (
            "A newer version of the installer is available ({0}). It has to be updated before it can install anything, so that an outdated installer does not install the mod wrongly.\n\nUpdate now? The installer will replace itself and restart.",
            "Eine neuere Version des Installers ist verfügbar ({0}). Er muss aktualisiert werden, bevor er etwas installieren kann - sonst könnte ein veralteter Installer den Mod falsch installieren.\n\nJetzt aktualisieren? Der Installer ersetzt sich selbst und startet neu."),
        ["UpdateDeclined"] = (
            "Update declined. Nothing was installed - the installer needs to be up to date before it can install the mod.",
            "Aktualisierung abgelehnt. Es wurde nichts installiert - der Installer muss aktuell sein, bevor er den Mod installieren kann."),
        ["UpdateRestarted"] = ("Installer updated to the latest version.", "Installer wurde auf die neueste Version aktualisiert."),
        ["UpdateRestartFailed"] = (
            "Could not relaunch automatically ({0}) - trying another way...",
            "Automatischer Neustart nicht möglich ({0}) - versuche einen anderen Weg..."),
        ["UpdateDoneRestartYourself"] = (
            "The installer was updated to the latest version but could not relaunch itself. Please start it again - it is ready to use.",
            "Der Installer wurde auf die neueste Version aktualisiert, konnte sich aber nicht selbst neu starten. Bitte starte ihn einfach erneut - er ist einsatzbereit."),

        ["DotNetMissingPrompt"] = ("The mod needs the .NET 6 runtime, which is not installed. Download and install it now (about 30 MB, official Microsoft installer)?", "Der Mod benötigt die .NET-6-Laufzeitumgebung, die nicht installiert ist. Jetzt herunterladen und installieren (ca. 30 MB, offizieller Microsoft-Installer)?"),
        ["DotNetDownloading"] = ("Downloading .NET 6 runtime...", "Lade .NET-6-Laufzeitumgebung herunter..."),
        ["DotNetInstalling"] = ("Installing .NET 6 runtime (a Windows permission prompt may appear)...", "Installiere .NET-6-Laufzeitumgebung (es kann eine Windows-Berechtigungsabfrage erscheinen)..."),
        ["DotNetInstalled"] = (".NET 6 runtime installed.", ".NET-6-Laufzeitumgebung installiert."),
        ["DotNetFailed"] = (".NET 6 runtime still not found - the game may not start modded until it is installed.", ".NET-6-Laufzeitumgebung weiterhin nicht gefunden - das Spiel startet mit Mod womöglich nicht, bis sie installiert ist."),
        ["DotNetSkipped"] = ("Note: .NET 6 runtime is missing - the modded game will not start until it is installed.", "Hinweis: .NET-6-Laufzeitumgebung fehlt - das Spiel startet mit Mod erst, wenn sie installiert ist."),

        ["PresetPromptTitle"] = ("Keyboard layout", "Tastaturlayout"),
        ["PresetPromptText"] = ("First install detected. The mod's original hotkeys only work on US keyboards, so a layout-independent preset will be applied.\n\nDoes this computer's keyboard have a number pad? (Yes = use numpad keys too, No = function keys only)", "Erstinstallation erkannt. Die Original-Tasten des Mods funktionieren nur auf US-Tastaturen, daher wird ein layoutunabhängiges Preset eingerichtet.\n\nHat die Tastatur dieses Computers einen Ziffernblock? (Ja = Ziffernblock mitnutzen, Nein = nur Funktionstasten)"),
        ["PresetApplied"] = ("Layout-independent keybind preset applied ({0}).", "Layoutunabhängiges Tasten-Preset eingerichtet ({0})."),
        ["PresetFailed"] = ("Could not apply the keybind preset - open the Mod Configurator and load a preset manually.", "Tasten-Preset konnte nicht eingerichtet werden - bitte den Mod-Konfigurator öffnen und ein Preset manuell laden."),
        ["PresetToolMissing"] = ("Mod Configurator not found - open it later and load a preset manually.", "Mod-Konfigurator nicht gefunden - bitte später öffnen und ein Preset manuell laden."),

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
