using System;
using System.Diagnostics;
using System.IO;
using MelonLoader;
using AccessibilityMod.Settings;

namespace AccessibilityMod.Utils
{
    /// <summary>
    /// Opens the mod debugger (tools/ModDebugger) in its own window, without the player
    /// having to leave the game or find an exe on disk.
    ///
    /// A separate process on purpose: an in-game overlay would have to be navigated with the
    /// game still swallowing keys and the screen reader still reading the game - a second
    /// window is something a screen reader user can simply Alt+Tab to and read at their own
    /// pace while the game keeps running.
    /// </summary>
    public static class ModDebuggerLauncher
    {
        private const string ExeName = "DiscoElysiumModDebugger.exe";

        private static Process running;

        public static void Open()
        {
            try
            {
                // Already open: bring the player back to it rather than piling up windows.
                if (running != null && !running.HasExited)
                {
                    TolkScreenReader.Instance.Speak(Loc.Get("DebuggerAlreadyOpen"), true);
                    return;
                }
            }
            catch
            {
                running = null;
            }

            var exe = FindExe();
            if (exe == null)
            {
                TolkScreenReader.Instance.Speak(Loc.Get("DebuggerNotFound"), true);
                MelonLogger.Warning($"[DEBUGGER] {ExeName} not found next to the game or in Mods/Tools.");
                return;
            }

            try
            {
                // The game folder is the current directory of the running game - hand it over
                // so the tool finds the speech log and the bridge port without asking.
                running = Process.Start(new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"\"{Directory.GetCurrentDirectory()}\"",
                    UseShellExecute = true,
                });
                TolkScreenReader.Instance.Speak(Loc.Get("DebuggerOpened"), true);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[DEBUGGER] Could not start {exe}: {ex}");
                TolkScreenReader.Instance.Speak(Loc.Get("DebuggerNotFound"), true);
            }
        }

        /// <summary>The exe ships in the tools bundle; look where a person would put it.</summary>
        private static string FindExe()
        {
            string[] candidates =
            {
                Path.Combine(Directory.GetCurrentDirectory(), ExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "Mods", ExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "Mods", "Tools", ExeName),
                Path.Combine(Directory.GetCurrentDirectory(), "Tools", ExeName),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
