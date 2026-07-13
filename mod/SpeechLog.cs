using System;
using System.IO;
using System.Text;
using MelonLoader;
using AccessibilityMod.Settings;

namespace AccessibilityMod
{
    /// <summary>
    /// A transcript of everything the mod said, with timestamps, in
    /// UserData/SpeechLog.txt.
    ///
    /// Speech is gone the moment it is spoken. Without a transcript there is no way to
    /// answer "what did the game actually tell me before that happened?" - and for a
    /// blind player that question cannot be answered by looking at the screen either.
    /// The log makes a session reconstructable after the fact.
    ///
    /// Off by default: it is a diagnostic aid, not something to write to disk on every
    /// line of every playthrough behind the player's back.
    /// </summary>
    public static class SpeechLog
    {
        private static readonly object writeLock = new object();
        private static bool sessionHeaderWritten;

        public static string FilePath => Path.Combine("UserData", "SpeechLog.txt");

        public static void Write(string text)
        {
            if (!AccessibilityPreferences.GetSpeechLog() || string.IsNullOrWhiteSpace(text)) return;

            try
            {
                lock (writeLock)
                {
                    var sb = new StringBuilder();

                    if (!sessionHeaderWritten)
                    {
                        sessionHeaderWritten = true;
                        sb.AppendLine();
                        sb.AppendLine($"=== Session started {DateTime.Now:yyyy-MM-dd HH:mm:ss} (mod {AccessibilityMod.ModVersion}) ===");
                    }

                    // One line per utterance: a transcript is read top to bottom, and a
                    // line break inside an entry would make the timestamps stop lining up.
                    sb.AppendLine($"{DateTime.Now:HH:mm:ss}  {text.Replace("\r", " ").Replace("\n", " ")}");

                    // With a BOM, so editors read the umlauts as UTF-8 instead of guessing
                    // the system codepage and turning "Nähe" into "NÃ¤he".
                    File.AppendAllText(FilePath, sb.ToString(), new UTF8Encoding(true));
                }
            }
            catch (Exception ex)
            {
                // A failing log must never take the speech down with it.
                MelonLogger.Warning($"[SPEECHLOG] Could not write: {ex.Message}");
            }
        }
    }
}
