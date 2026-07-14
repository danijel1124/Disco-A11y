using System.Text;

namespace ModDebugger;

/// <summary>One line the mod spoke, plus whatever the player wants to say about it.</summary>
internal sealed class SpokenLine
{
    public string Time { get; init; } = "";
    public string Text { get; init; } = "";
    public string? Comment { get; set; }

    /// <summary>What the list reads out - one line, because a screen reader reads lines.</summary>
    public override string ToString()
        => string.IsNullOrEmpty(Comment) ? $"{Time}  {Text}" : $"{Time}  {Text}  — Kommentar: {Comment}";
}

/// <summary>
/// The session transcript: what the mod said, in order, with the player's notes attached.
///
/// The point of the notes is that a bug report from a blind player currently has to be
/// written from memory ("somewhere back there it said something odd") - here the remark
/// sticks to the exact line it belongs to, with its timestamp, and the report is a file.
/// </summary>
internal sealed class Transcript
{
    public List<SpokenLine> Lines { get; } = new();

    /// <summary>
    /// Loads the mod's own speech log (UserData/SpeechLog.txt). Its session headers stay
    /// in the list as plain entries - the "=== Session started ..." markers are exactly
    /// what you look for when reconstructing "which run was that?".
    /// </summary>
    public void LoadSpeechLog(string gamePath)
    {
        Lines.Clear();

        var path = Path.Combine(gamePath, "UserData", "SpeechLog.txt");
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var line = raw.TrimEnd();
            if (line.Length == 0) continue;

            // "15:11:52  Egghead: ..." - timestamp, two spaces, text.
            if (line.Length > 10 && line[2] == ':' && line[5] == ':' && line[8] == ' ')
            {
                Lines.Add(new SpokenLine { Time = line[..8], Text = line[10..].TrimStart() });
            }
            else
            {
                Lines.Add(new SpokenLine { Time = "", Text = line });
            }
        }
    }

    public SpokenLine Append(string text)
    {
        var line = new SpokenLine { Time = DateTime.Now.ToString("HH:mm:ss"), Text = text };
        Lines.Add(line);
        return line;
    }

    /// <summary>
    /// Writes the annotated transcript next to the speech log. Deliberately a new file:
    /// the mod owns SpeechLog.txt and appends to it while the game runs - writing into it
    /// from here would race the game and could lose lines.
    /// </summary>
    public string Export(string gamePath)
    {
        var path = Path.Combine(gamePath, "UserData",
            $"ModDebugger-Report-{DateTime.Now:yyyy-MM-dd-HHmm}.txt");

        var sb = new StringBuilder();
        sb.AppendLine($"Disco Elysium accessibility mod - annotated transcript, {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();

        foreach (var line in Lines)
        {
            sb.AppendLine(string.IsNullOrEmpty(line.Time) ? line.Text : $"{line.Time}  {line.Text}");
            if (!string.IsNullOrEmpty(line.Comment))
            {
                sb.AppendLine($"          >> {line.Comment}");
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        return path;
    }
}
