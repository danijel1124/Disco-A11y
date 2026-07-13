namespace Installer;

internal static class Program
{
    /// <summary>Folder the embedded companion files get extracted into (subfolder next to the exe).</summary>
    public static string BundleDir => Path.Combine(AppContext.BaseDirectory, "SetupFiles");

    // Makes P/Invoke find Tolk.dll inside the extraction subfolder.
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SetDllDirectory(string path);

    // A WinExe has no console of its own - attach to the caller's so --cli output shows up.
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    private static extern bool AttachConsole(int processId);
    private const int ATTACH_PARENT_PROCESS = -1;

    /// <summary>
    /// Self-extracts the embedded companion files (configurator, dev bridge, Tolk
    /// natives) into a SetupFiles subfolder next to the exe, so a single
    /// downloaded/copied installer file is a complete setup. Locked or read-only
    /// destinations are skipped silently - an already-present file simply stays, and on
    /// a read-only medium the installer still works, just without spoken status.
    /// </summary>
    private static void ExtractBundledFiles()
    {
        var assembly = typeof(Program).Assembly;
        try
        {
            Directory.CreateDirectory(BundleDir);
        }
        catch
        {
            return; // read-only medium - nothing to extract to
        }

        foreach (var resource in assembly.GetManifestResourceNames())
        {
            if (!resource.StartsWith("bundle:")) continue;
            var dest = Path.Combine(BundleDir, resource.Substring("bundle:".Length));
            try
            {
                using var source = assembly.GetManifestResourceStream(resource)!;
                using var file = File.Create(dest);
                source.CopyTo(file);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        SetDllDirectory(BundleDir);
    }

    /// <summary>Set when the mandatory startup update check failed - installing stays blocked.</summary>
    public static string UpdateBlockReason { get; private set; }

    [STAThread]
    private static void Main(string[] args)
    {
        var wasUpdated = args.Contains("--updated");
        // Unconditional: when a relaunch fails, the .old binary is left behind and the
        // next run is a plain start without --updated, which used to leave it lying there
        // next to the installer forever.
        SelfUpdater.CleanupAfterUpdate();
        args = args.Where(a => a != "--updated").ToArray();

        ExtractBundledFiles();

        var isCli = args.Length >= 1 && args[0] == "--cli";
        if (isCli) AttachConsole(ATTACH_PARENT_PROCESS);
        void Log(string s) { if (isCli) Console.WriteLine(s); }

        if (!isCli) ApplicationConfiguration.Initialize();

        // Mandatory self-update: with very active development an outdated installer
        // binary could install wrongly, so no update = no installation. Mandatory, but
        // never silent - the GUI asks first, because replacing the program someone just
        // started is not something to do behind their back. In --cli every step is
        // printed to the console the caller is watching, so it proceeds on its own.
        if (!args.Contains("--no-selfupdate") && SelfUpdater.LocalBuildId != "dev")
        {
            bool Confirm(string newVersion) => isCli || MessageBox.Show(
                Strings.Get("UpdateConfirm", newVersion),
                Strings.Get("WindowTitle"),
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Information) == DialogResult.OK;

            var result = SelfUpdater.EnsureLatestAsync(args, Log, Confirm).GetAwaiter().GetResult();
            if (result == SelfUpdater.Result.Restarting) return;

            if (result == SelfUpdater.Result.Declined)
            {
                var declined = Strings.Get("UpdateDeclined");
                if (isCli) { Log(declined); Environment.ExitCode = 1; return; }
                MessageBox.Show(declined, Strings.Get("WindowTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // The update itself worked - only the relaunch did not. Saying "update check
            // failed, no installation possible" here would be a lie that leaves the user
            // stuck with a working, current installer they think is broken.
            if (result == SelfUpdater.Result.UpdatedButRestartFailed)
            {
                var message = Strings.Get("UpdateDoneRestartYourself");
                if (isCli) { Log(message); return; }
                MessageBox.Show(message, Strings.Get("WindowTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (result == SelfUpdater.Result.Blocked)
            {
                var reason = Strings.Get("UpdateCheckFailed", "see above");
                if (isCli) { Environment.ExitCode = 1; return; }
                UpdateBlockReason = reason;
            }
        }

        if (isCli)
        {
            if (wasUpdated) Log(Strings.Get("UpdateRestarted"));
            // The game path can be any non-flag argument after --cli, not just args[1] -
            // otherwise a flag placed before the path (e.g. "--cli --force <path>") would
            // silently discard the path in favor of auto-detection.
            var gamePathArg = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
            var force = args.Contains("--force");
            var prerelease = args.Contains("--prerelease");
            // tri-state: --devbridge installs it, --no-devbridge removes it, neither leaves it alone
            bool? devBridge = args.Contains("--devbridge") ? true : args.Contains("--no-devbridge") ? false : null;
            var preset = "numpad";
            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--preset" && i + 1 < args.Length) preset = args[i + 1];
            }
            RunCli(gamePathArg, force, prerelease, devBridge, preset).GetAwaiter().GetResult();
            return;
        }

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
    private static async Task RunCli(string? gamePathOverride, bool force, bool includePrerelease, bool? devBridge, string preset)
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
            if (!DotNetRuntime.IsModRuntimePresent())
            {
                await DotNetRuntime.InstallAsync(Log);
            }

            var freshConfig = ModInstaller.IsFreshConfig(gamePath);

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

            if (freshConfig)
            {
                await ModInstaller.ApplyPresetAsync(gamePath, preset, Log);
            }

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
