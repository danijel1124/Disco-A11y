using System.Text;
using System.Text.RegularExpressions;

namespace KeybindEditor;

/// <summary>
/// Reads/writes UserData/AccessibilityMod.cfg - the same file the mod's MelonPreferences
/// categories ("KeyBindings" and "AccessibilityMod") persist to. Regenerates the whole
/// file from known fields rather than doing a partial patch, since these two categories
/// are the entire contents of this file.
/// </summary>
public sealed class ModConfig
{
    public Dictionary<string, string> KeyBindings { get; } = new();

    public int DialogReadingMode { get; set; } // 0 = Disabled, 1 = Full, 2 = SpeakerOnly
    public bool OrbAnnouncements { get; set; } = true;
    public bool SpeechInterrupt { get; set; } = false;
    public bool SpeakAudioCaptions { get; set; } = true;
    public bool DialogAutoAdvance { get; set; } = false;
    public bool AutoInteract { get; set; } = false;

    /// <summary>
    /// Actions that were not present in the loaded file and therefore fell back to
    /// their default binding - i.e. actions added by a mod update since the config
    /// was written. Empty when the file didn't exist at all (fresh config).
    /// </summary>
    public List<string> AddedActions { get; } = new();

    private readonly HashSet<string> actionsSeenInFile = new();

    public static ModConfig LoadOrDefault(string path)
    {
        var config = new ModConfig();
        foreach (var action in GameKeyCatalog.Actions)
        {
            config.KeyBindings[action.Name] = action.DefaultBinding;
        }

        if (!File.Exists(path))
        {
            return config;
        }

        string? currentSection = null;
        foreach (var rawLine in File.ReadAllLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var sectionMatch = Regex.Match(line, @"^\[(.+)\]$");
            if (sectionMatch.Success)
            {
                currentSection = sectionMatch.Groups[1].Value;
                continue;
            }

            // MelonPreferences sometimes writes a whole category as a TOML inline table
            // on a single line ("AccessibilityMod = { DialogReadingMode = 0, ... }")
            // instead of a [Section] block - both forms occur in real files. Splitting
            // on commas is safe for our values (ints, bools, and binding strings, none
            // of which can contain a comma).
            var inlineMatch = Regex.Match(line, @"^([A-Za-z0-9_]+)\s*=\s*\{(.*)\}\s*$");
            if (inlineMatch.Success)
            {
                var inlineSection = inlineMatch.Groups[1].Value;
                foreach (var pair in inlineMatch.Groups[2].Value.Split(','))
                {
                    var pairMatch = Regex.Match(pair.Trim(), @"^([A-Za-z0-9_]+)\s*=\s*(.+)$");
                    if (pairMatch.Success)
                    {
                        ApplyValue(config, inlineSection, pairMatch.Groups[1].Value, pairMatch.Groups[2].Value.Trim());
                    }
                }
                continue;
            }

            var kvMatch = Regex.Match(line, @"^([A-Za-z0-9_]+)\s*=\s*(.+)$");
            if (!kvMatch.Success || currentSection == null)
            {
                continue;
            }

            ApplyValue(config, currentSection, kvMatch.Groups[1].Value, kvMatch.Groups[2].Value.Trim());
        }

        foreach (var action in GameKeyCatalog.Actions)
        {
            if (!config.actionsSeenInFile.Contains(action.Name))
            {
                config.AddedActions.Add(action.Name);
            }
        }

        return config;
    }

    private static void ApplyValue(ModConfig config, string section, string key, string value)
    {
        if (section == "KeyBindings" && config.KeyBindings.ContainsKey(key))
        {
            config.KeyBindings[key] = Unquote(value);
            config.actionsSeenInFile.Add(key);
        }
        else if (section == "AccessibilityMod")
        {
            switch (key)
            {
                case "DialogReadingMode":
                    if (int.TryParse(value, out var mode)) config.DialogReadingMode = mode;
                    break;
                case "OrbAnnouncements":
                    config.OrbAnnouncements = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "SpeechInterrupt":
                    config.SpeechInterrupt = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "SpeakAudioCaptions":
                    config.SpeakAudioCaptions = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "DialogAutoAdvance":
                    config.DialogAutoAdvance = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "AutoInteract":
                    config.AutoInteract = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
            }
        }
    }

    public void Save(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[KeyBindings]");
        foreach (var action in GameKeyCatalog.Actions)
        {
            sb.AppendLine($"{action.Name} = \"{KeyBindings[action.Name]}\"");
        }

        sb.AppendLine();
        sb.AppendLine("[AccessibilityMod]");
        sb.AppendLine($"DialogReadingMode = {DialogReadingMode}");
        sb.AppendLine($"OrbAnnouncements = {(OrbAnnouncements ? "true" : "false")}");
        sb.AppendLine($"SpeechInterrupt = {(SpeechInterrupt ? "true" : "false")}");
        sb.AppendLine($"SpeakAudioCaptions = {(SpeakAudioCaptions ? "true" : "false")}");
        sb.AppendLine($"DialogAutoAdvance = {(DialogAutoAdvance ? "true" : "false")}");
        sb.AppendLine($"AutoInteract = {(AutoInteract ? "true" : "false")}");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, sb.ToString());
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1];
        }
        return value;
    }
}
