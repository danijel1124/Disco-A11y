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

    private static string NotesPath(string gamePath) =>
        Path.Combine(gamePath, "UserData", "ModDebugger-Notes.txt");

    /// <summary>
    /// Saves the comments the moment one is written, so closing the window never destroys
    /// them. They used to live only in memory: a tester could annotate an hour of play, close
    /// the window without exporting, and lose everything - and the whole point of this tool is
    /// that a report is not written from memory.
    ///
    /// The line's timestamp and text are the key, so the notes survive a restart, a reload of
    /// the speech log and any number of new lines arriving in between.
    /// </summary>
    public void SaveNotes(string gamePath)
    {
        var sb = new StringBuilder();
        foreach (var line in Lines)
        {
            if (string.IsNullOrEmpty(line.Comment)) continue;
            // One record per line, tab-separated; comments cannot contain newlines (the
            // dialog is single-line), so this stays readable and parseable.
            sb.AppendLine($"{line.Time}\t{line.Text}\t{line.Comment}");
        }

        var path = NotesPath(gamePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    /// <summary>Re-attaches saved comments to the lines they belong to.</summary>
    public void LoadNotes(string gamePath)
    {
        var path = NotesPath(gamePath);
        if (!File.Exists(path)) return;

        foreach (var raw in File.ReadAllLines(path, Encoding.UTF8))
        {
            var parts = raw.Split('\t');
            if (parts.Length < 3) continue;

            var line = Lines.FirstOrDefault(l => l.Time == parts[0] && l.Text == parts[1]);
            if (line != null) line.Comment = parts[2];
        }
    }

    /// <summary>
    /// Writes the annotated transcript. Deliberately a separate file from SpeechLog.txt: the
    /// mod owns that one and appends to it while the game runs - writing into it from here
    /// would race the game and could lose lines.
    /// </summary>
    public string Export(string gamePath, string? targetPath = null)
    {
        var path = targetPath ?? Path.Combine(gamePath, "UserData",
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
