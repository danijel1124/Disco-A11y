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
            ["SkillPointOne"] = (
                "{0} skill point to spend.",
                "{0} Fähigkeitspunkt zu vergeben."),
            ["SkillPointsMany"] = (
                "{0} skill points to spend.",
                "{0} Fähigkeitspunkte zu vergeben."),
            ["ExperienceAndLevel"] = (
                "Experience {0}, level {1}.",
                "Erfahrung {0}, Level {1}."),
            ["Screen_INVENTORY"] = ("Inventory", "Inventar"),
            ["Screen_CHARACTERSHEET"] = ("Character sheet", "Charakterbogen"),
            ["Screen_THOUGHTCABINET"] = ("Thought cabinet", "Gedankenkabinett"),
            ["Screen_JOURNAL"] = ("Journal", "Journal"),
            ["Screen_OPTIONS"] = ("Options", "Optionen"),
            ["Screen_MAINMENU"] = ("Main menu", "Hauptmenü"),
            ["Screen_SAVE"] = ("Save game", "Spiel speichern"),
            ["Screen_LOAD"] = ("Load game", "Spiel laden"),
            ["Screen_HELPOVERLAY"] = ("Help", "Hilfe"),
            ["ItemCountOne"] = (
                "{0} item.",
                "{0} Gegenstand."),
            ["ItemCountMany"] = (
                "{0} items.",
                "{0} Gegenstände."),
            ["ResponseOne"] = (
                "One option:",
                "Eine Option:"),
            ["ResponseMany"] = (
                "{0} options:",
                "{0} Optionen:"),
            // The game's own dialogue shortcut keys (1-9) exist, but proved unreliable in
            // testing; arrow keys plus Enter always work, so that is what we tell people.
            // Diagnostics: every name source for the selected object, each one labelled.
            // The dialogue source is reported precisely so it can be heard to be wrong -
            // it names the conversation's actor, not the object.
            ["NameSourcesHeader"] = ("Name sources:", "Namensquellen:"),
            ["NameSourcesNoSelection"] = (
                "Nothing selected. Choose an object first.",
                "Nichts ausgewählt. Wähle zuerst ein Objekt."),
            ["NameSourceSpoken"] = ("spoken: {0}.", "gesprochen: {0}."),
            ["NameSourceUnity"] = ("internal: {0}.", "intern: {0}."),
            ["NameSourceEntity"] = ("entity: {0}.", "Entität: {0}."),
            ["NameSourceItem"] = ("item database: {0}.", "Item-Datenbank: {0}."),
            ["NameSourceDialogue"] = ("dialogue database: {0}.", "Dialog-Datenbank: {0}."),
            ["NameSourceNone"] = ("nothing", "nichts"),
            ["AreaNoDescription"] = (
                "This area has no description yet.",
                "Für diesen Bereich gibt es noch keine Beschreibung."),
            // The journal's map tab: keyboard access to the travel destinations.
            ["MapOpened"] = (
                "Map. {0} travel destinations. {1} to cycle, {2} to travel.",
                "Karte. {0} Reiseziele. {1} zum Blättern, {2} zum Reisen."),
            ["MapNoDestinations"] = (
                "No travel destinations on the map yet.",
                "Noch keine Reiseziele auf der Karte."),
            ["MapNothingSelected"] = (
                "No destination selected. Cycle to one first.",
                "Kein Reiseziel ausgewählt. Blättere zuerst zu einem."),
            ["MapAlreadyHere"] = (
                "You are already there.",
                "Dort bist du bereits."),
            ["MapTravelling"] = (
                "Travelling to {0}.",
                "Reise nach {0}."),
            ["DialogEnded"] = (
                "Conversation ended.",
                "Dialog beendet."),
            ["ResponseHowTo"] = (
                "Use the arrow keys to choose, Enter to confirm.",
                "Mit den Pfeiltasten wählen, Enter bestätigen."),
            // Buttons whose label is a picture, not text (I2LocalizeButton). All the game
            // leaves behind is the sprite's name, so these are our own translations of it.
            // See TextExtractor.NameFromSpriteTerm for how the key is derived.
            ["UITerm_begin"] = ("Begin", "Beginnen"),
            ["UITerm_continue"] = ("Continue", "Fortsetzen"),
            ["UITerm_back"] = ("Back", "Zurück"),
            ["UITerm_cancel"] = ("Cancel", "Abbrechen"),
            ["UITerm_confirm"] = ("Confirm", "Bestätigen"),
            ["UITerm_accept"] = ("Accept", "Annehmen"),
            ["UITerm_skip"] = ("Skip", "Überspringen"),
        };

        /// <summary>Whether we have a name for this key, as opposed to Get's echo of the key itself.</summary>
        public static bool Has(string key) => Table.ContainsKey(key);

        public static string Get(string key) =>
            Table.TryGetValue(key, out var entry) ? (IsGerman ? entry.De : entry.En) : key;

        public static string Get(string key, params object[] args) => string.Format(Get(key), args);
    }
}
