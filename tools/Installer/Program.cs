namespace Installer;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length >= 1 && args[0] == "--cli")
        {
            // The game path can be any non-flag argument after --cli, not just args[1] -
            // otherwise a flag placed before the path (e.g. "--cli --force <path>") would
            // silently discard the path in favor of auto-detection.
            var gamePathArg = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
            var force = args.Contains("--force");
            var prerelease = args.Contains("--prerelease");
            // tri-state: --devbridge installs it, --no-devbridge removes it, neither leaves it alone
            bool? devBridge = args.Contains("--devbridge") ? true : args.Contains("--no-devbridge") ? false : null;
            RunCli(gamePathArg, force, prerelease, devBridge).GetAwaiter().GetResult();
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    /// <summary>
    /// Non-interactive install: DiscoElysiumInstaller.exe --cli [gamePath] [--force] [--prerelease] [--devbridge|--no-devbridge]
    /// Installs/updates MelonLoader (skipped if already present, unless --force) and the
    /// mod itself, printing progress to the console. Auto-detects the game folder via
    /// Steam if gamePath is omitted. --prerelease installs the newest release including
    /// prereleases (the nightly channel) instead of the latest stable. --devbridge
    /// additionally installs the AI dev bridge companion mod, --no-devbridge removes it.
    /// </summary>
    private static async Task RunCli(string? gamePathOverride, bool force, bool includePrerelease, bool? devBridge)
    {
        void Log(string s) => Console.WriteLine(s);

        var gamePath = gamePathOverride ?? GamePathFinder.FindGamePath();
        Log($"Game path: {gamePath ?? "(not found)"}");
        if (gamePath == null || !GamePathFinder.IsValid(gamePath))
        {
            Log("FAILED: game path invalid or not found. Pass it explicitly: --cli \"<path>\"");
            Environment.ExitCode = 1;
            return;
        }

        try
        {
            if (MelonLoaderInstaller.IsInstalled(gamePath) && !force)
            {
                Log("MelonLoader is already installed (use --force to reinstall).");
            }
            else
            {
                var exePath = Path.Combine(gamePath, "disco.exe");
                await MelonLoaderInstaller.InstallAsync(gamePath, exePath, Log);
                Log("MelonLoader installed.");
            }

            var tag = await ModInstaller.InstallLatestAsync(gamePath, Log, includePrerelease);
            Log($"Mod installed (release {tag}).");

            if (devBridge.HasValue)
            {
                var bridgeResult = ModInstaller.SetDevBridgeEnabled(gamePath, devBridge.Value);
                Log(bridgeResult switch
                {
                    ModInstaller.DevBridgeResult.Installed => "AI dev bridge installed (Mods/DevBridge.dll). Command channel: UserData/DevBridge/.",
                    ModInstaller.DevBridgeResult.Removed => "AI dev bridge removed.",
                    ModInstaller.DevBridgeResult.SourceMissing => "DevBridge.dll not found next to this installer - AI dev bridge skipped.",
                    _ => "AI dev bridge not present.",
                });
            }

            var exe = KeybindEditorLocator.Find();
            if (exe != null)
            {
                Log(StartMenuShortcut.TryCreate(gamePath, exe, out var result)
                    ? $"Start Menu shortcut created: {result}"
                    : $"Could not create Start Menu shortcut: {result}");
            }
            else
            {
                Log("Keybind Editor not found next to this installer - skipping Start Menu shortcut.");
            }

            Log("Done. Launch the game to use the mod.");
        }
        catch (Exception ex)
        {
            Log($"FAILED: {ex.Message}");
            Environment.ExitCode = 1;
        }
    }
}
