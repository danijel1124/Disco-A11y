using System.Globalization;

namespace ModDebugger;

/// <summary>English/German UI strings, chosen by the OS language (same convention as the
/// installer and the configurator).</summary>
internal static class Strings
{
    public static bool German { get; set; } =
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, (string En, string De)> Table = new()
    {
        ["Title"] = ("Disco Elysium Mod Debugger", "Disco Elysium Mod-Debugger"),
        ["GamePath"] = ("Game folder:", "Spielordner:"),
        ["Browse"] = ("Browse...", "Durchsuchen..."),
        ["Transcript"] = ("What the mod said (Enter or F2 adds a comment)", "Was die Mod gesagt hat (Enter oder F2 kommentiert)"),
        ["Connect"] = ("Connect to the running game", "Mit dem laufenden Spiel verbinden"),
        ["Disconnect"] = ("Disconnect", "Verbindung trennen"),
        ["Reload"] = ("Reload speech log", "Sprechprotokoll neu laden"),
        ["Comment"] = ("Comment on the selected line", "Ausgewählte Zeile kommentieren"),
        ["Export"] = ("Export report", "Bericht exportieren"),
        ["Follow"] = ("Follow live (jump to the newest line)", "Live folgen (zur neuesten Zeile springen)"),
        ["CommandLabel"] = ("Bridge command:", "Bridge-Befehl:"),
        ["Send"] = ("Send", "Senden"),
        ["Response"] = ("Response", "Antwort"),
        ["CommentPrompt"] = ("Your comment on this line:", "Dein Kommentar zu dieser Zeile:"),
        ["CommentTitle"] = ("Add comment", "Kommentar anhängen"),
        ["NoSelection"] = ("No line selected.", "Keine Zeile ausgewählt."),
        ["Exported"] = ("Report written: {0}", "Bericht geschrieben: {0}"),
        ["Loaded"] = ("{0} lines loaded from the speech log.", "{0} Zeilen aus dem Sprechprotokoll geladen."),
        ["NoLog"] = (
            "No speech log found. Switch on 'Speech log' in the mod configurator - without it the mod writes no transcript.",
            "Kein Sprechprotokoll gefunden. Schalte im Konfigurator 'Sprechprotokoll' ein — ohne das schreibt die Mod kein Transkript."),
        ["CommentAdded"] = ("Comment added.", "Kommentar angehängt."),
    };

    public static string Get(string key)
    {
        if (!Table.TryGetValue(key, out var entry)) return key;
        return German ? entry.De : entry.En;
    }

    public static string Get(string key, params object[] args) => string.Format(Get(key), args);
}
