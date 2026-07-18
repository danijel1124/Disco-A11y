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
            ["ContainerSingleTaken"] = (
                "Picked up.",
                "Aufgenommen."),
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
            // Debug mode only: the game's internal screens, said out loud by their raw name.
            ["DebugScreen"] = ("Screen: {0}.", "Bildschirm: {0}."),
            ["DebugModeOff"] = (
                "Debug mode is off. Switch it on in the mod configurator to use this key.",
                "Der Debug-Modus ist aus. Schalte ihn im Konfigurator ein, um diese Taste zu nutzen."),
            ["AreaNoDescription"] = (
                "This area has no description yet.",
                "Für diesen Bereich gibt es noch keine Beschreibung."),
            // Item names in this game are proper nouns from its own world ("Glastara") -
            // meaningless without the description a sighted player reads in the tooltip.
            ["InteractWalkingThere"] = (
                "{0} is out of reach. Walking there, then interacting.",
                "{0} ist außer Reichweite. Ich laufe hin und interagiere dann."),
            ["ItemNoSelection"] = (
                "Nothing selected. Choose an object first.",
                "Nichts ausgewählt. Wähle zuerst ein Objekt."),
            ["ItemNoDescription"] = (
                "{0}. The game gives no description for this.",
                "{0}. Das Spiel gibt dazu keine Beschreibung her."),
            ["DebuggerOpened"] = (
                "Mod debugger opened in its own window.",
                "Mod-Debugger in eigenem Fenster geöffnet."),
            ["DebuggerAlreadyOpen"] = (
                "The mod debugger is already open. Switch to it with Alt plus Tab.",
                "Der Mod-Debugger ist schon offen. Wechsle mit Alt plus Tab dorthin."),
            ["DebuggerNotFound"] = (
                "Mod debugger not found. It ships in the tools bundle - put it in the game folder.",
                "Mod-Debugger nicht gefunden. Er liegt im Tools-Paket — leg ihn in den Spielordner."),
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
            // Inventory tabs (the game's own labels, translated the way the game shows them).
            ["InvTab_TOOLS"] = ("Tools", "Werkzeuge"),
            ["InvTab_CLOTHES"] = ("Clothes", "Kleidung"),
            ["InvTab_PAWNABLES"] = ("Pawnables", "Pfandgut"),
            ["InvTab_READING"] = ("Reading", "Lektüre"),
            ["InvTabWithCount"] = (
                "Tab {0}: {1} items.",
                "Tab {0}: {1} Gegenstände."),
            ["InvTabWithCountOne"] = (
                "Tab {0}: 1 item.",
                "Tab {0}: 1 Gegenstand."),
            // Only reachable with the inventory OPEN but the game's InventoryManager
            // missing - an internal error, not a closed inventory. The old text ("Press I
            // to open it") gave instructions that were wrong in the only situation the
            // line can play (PR review cleanup).
            ["InvTabUnavailable"] = (
                "Tab switching is not available right now.",
                "Tab-Wechsel ist gerade nicht möglich."),
            ["InvTabEmpty"] = (
                "Tab {0}: no items.",
                "Tab {0}: keine Objekte."),
            ["InvNoItems"] = (
                "No items.",
                "Keine Objekte."),
            // Neutral tab announcement when the item COUNT could not be read: claiming
            // "no items" on an interop error would sell a full tab as empty (PR review
            // cleanup). {0} = tab name.
            ["InvTabNoCount"] = (
                "Tab {0}.",
                "Tab {0}."),
            // The current tab itself could not be determined - neutral, no invented state.
            ["InvTabReadError"] = (
                "Inventory tab could not be read.",
                "Inventar-Tab konnte nicht gelesen werden."),
            ["InvSlotEmpty"] = (
                "empty",
                "leer"),
            // The healing plus buttons (mouse-only in the game).
            ["HealedHealth"] = (
                "Health restored. {0} health charges left.",
                "Gesundheit aufgefüllt. {0} Gesundheits-Ladungen übrig."),
            ["HealedMorale"] = (
                "Morale restored. {0} morale charges left.",
                "Moral aufgefüllt. {0} Moral-Ladungen übrig."),
            ["HealNoCharges"] = (
                "No healing charges left for {0}.",
                "Keine Heilladungen übrig für {0}."),
            ["HealNotNeeded"] = (
                "{0} is already full.",
                "{0} ist schon voll."),
            ["HealNoButton"] = (
                "Healing is not available right now.",
                "Heilung ist gerade nicht verfügbar."),
            ["HealWordHealth"] = ("health", "Gesundheit"),
            ["HealWordMorale"] = ("morale", "Moral"),
            // Healed, but the charge count could not be read: say so honestly instead of
            // fabricating a number (PR review cleanup - the old code could announce "0
            // charges left" when the true count was simply unknown). {0} = health/morale.
            ["HealedNoCount"] = (
                "{0} restored.",
                "{0} aufgefüllt."),
            // The thought cabinet splash screen: the research result a sighted player
            // reads off the full-screen panel when a thought finishes cooking.
            // (A "ThoughtCompleted" variant with an effect slot existed here but was
            // never referenced - removed as dead, the effect travels as its own part.)
            ["ThoughtCompletedNoEffect"] = (
                "Thought research completed: {0}.",
                "Gedanke fertig durchdacht: {0}."),
            // The splash screen a finished thought opens is modal and its close button is
            // mouse-only - a keyboard player needs to be TOLD how to get out (bug #57b:
            // "I can walk but not interact" = trapped behind this invisible fullscreen).
            // {0} = the speakable name of the LIVE CloseSplash binding, so the hint stays
            // right after remapping (never hardcode key names in announcements).
            ["SplashCloseHint"] = (
                "Press {0} to close this screen.",
                "{0} schließt diesen Bildschirm."),
            // Name of the splash for the screen announcer ("which screen am I on?").
            ["Screen_THOUGHTSPLASHSCREEN"] = (
                "Research result",
                "Forschungsergebnis"),
            // Appended to the tab announcement: the game auto-selects the first item of a
            // freshly switched tab, and this is how the player learns what it is without
            // the item announcement and the tab announcement interrupting each other.
            ["InvTabFirstItem"] = (
                " Selected: {0}.",
                " Ausgewählt: {0}."),
        };

        /// <summary>Whether we have a name for this key, as opposed to Get's echo of the key itself.</summary>
        public static bool Has(string key) => Table.ContainsKey(key);

        public static string Get(string key) =>
            Table.TryGetValue(key, out var entry) ? (IsGerman ? entry.De : entry.En) : key;

        public static string Get(string key, params object[] args) => string.Format(Get(key), args);
    }
}
