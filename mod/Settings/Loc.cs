using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace AccessibilityMod.Settings
{
    /// <summary>
    /// First localization infrastructure for the mod itself (the tools already have
    /// their own). Currently only the tutorial tips use it; migrating the mod's
    /// existing English announcements is a separate, gradual follow-up project.
    /// Language comes from the "Language" preference: auto (OS language), en, de.
    /// </summary>
    public static class Loc
    {
        // Unity's Il2Cpp domain reports an invariant/en CurrentUICulture regardless of
        // the OS language (verified in-game), so "auto" asks Windows directly.
        [DllImport("kernel32.dll")]
        private static extern ushort GetUserDefaultUILanguage();

        public static bool IsGerman
        {
            get
            {
                var setting = AccessibilityPreferences.GetLanguage();
                if (setting == "de") return true;
                if (setting == "en") return false;
                try
                {
                    return (GetUserDefaultUILanguage() & 0x3FF) == 0x07; // LANG_GERMAN
                }
                catch
                {
                    return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "de";
                }
            }
        }

        private static readonly Dictionary<string, (string En, string De)> Table = new()
        {
            ["Tip_Welcome"] = (
                "Welcome to the Disco Elysium Accessibility Mod. Tips like this one play once each at the right moment and can be turned off in the mod configurator. Press {0} to hear the game's control help at any time.",
                "Willkommen beim Disco Elysium Accessibility Mod. Tipps wie dieser kommen je einmal im passenden Moment und lassen sich im Mod-Konfigurator abschalten. Drücke jederzeit {0} für die Spielhilfe."),
            ["Tip_Objects"] = (
                "Tip: there are objects nearby. {0} selects people, {1} locations, {2} loot. {3} and {4} cycle through them, {5} walks to the selected object.",
                "Tipp: In der Nähe gibt es Objekte. {0} wählt Personen, {1} Orte, {2} Beute. {3} und {4} blättern durch, {5} läuft zum ausgewählten Objekt."),
            ["Tip_Conversation"] = (
                "Tip: a conversation has started but dialog reading is off. Press {0} to have dialogue read aloud, and {1} to continue automatically after each line.",
                "Tipp: Ein Gespräch hat begonnen, aber das Vorlesen ist aus. Drücke {0}, um Dialoge vorlesen zu lassen, und {1}, um nach jeder Zeile automatisch weiterzuschalten."),
            ["UpdateAvailable"] = (
                "A mod update is available: {0}. Run the installer to update.",
                "Ein Mod-Update ist verfügbar: {0}. Zum Aktualisieren den Installer ausführen."),
            ["AreaEntered"] = (
                "Entering area: {0}",
                "Neuer Bereich: {0}"),
            ["Tip_Arrival"] = (
                "Tip: you have arrived. Press {0} to interact with the object. {1} makes this happen automatically from now on.",
                "Tipp: Du bist angekommen. Drücke {0}, um mit dem Objekt zu interagieren. {1} erledigt das künftig automatisch."),
            ["DoorLocked"] = (
                "{0} is locked.",
                "{0} ist verschlossen."),
            ["ContainerOpened"] = (
                "Container opened: {0}. Press {1} to take everything, Escape to close.",
                "Container geöffnet: {0}. Drücke {1}, um alles zu nehmen, Escape zum Schließen."),
            ["ContainerEmpty"] = (
                "empty",
                "leer"),
            ["ContainerTakeAll"] = (
                "Taking everything.",
                "Nehme alles."),
            ["ContainerClosed"] = (
                "Container closed.",
                "Container geschlossen."),
        };

        public static string Get(string key) =>
            Table.TryGetValue(key, out var entry) ? (IsGerman ? entry.De : entry.En) : key;

        public static string Get(string key, params object[] args) => string.Format(Get(key), args);
    }
}
