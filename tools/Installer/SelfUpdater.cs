using System.Diagnostics;
using System.Reflection;

namespace Installer;

/// <summary>
/// Mandatory self-update: development is very active, so an outdated installer binary
/// could install wrongly. On startup the embedded BuildId is compared against
/// installer-version.txt in the nightly release; on mismatch the matching setup asset
/// (framework/standalone flavor) is downloaded, the running exe is swapped out
/// (rename-to-.old trick) and restarted. Without a successful, current update check no
/// installation is allowed. Escape hatch for development: --no-selfupdate.
/// </summary>
public static class SelfUpdater
{
    private const string VersionAssetUrl =
        "https://github.com/danijel1124/Disco-A11y/releases/download/nightly/installer-version.txt";

    public enum Result { UpToDate, Restarting, Blocked, UpdatedButRestartFailed, Declined }

    public static string LocalBuildId => GetMetadata("BuildId") ?? "dev";
    private static string Flavor => GetMetadata("Flavor") ?? "framework";

    private static string GetMetadata(string key) =>
        Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => a.Key == key)?.Value;

    /// <param name="confirmUpdate">
    /// Asked before anything is downloaded, with the new version as its argument. Updating
    /// replaces the program the user just started, so it is their decision to make, not
    /// something to do quietly behind their back - even though declining means no install.
    /// </param>
    public static async Task<Result> EnsureLatestAsync(string[] originalArgs, Action<string> log, Func<string, bool> confirmUpdate)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DiscoElysiumInstaller/1.0");
            // Generous, because the same client also fetches the installer binary, and the
            // standalone flavor is ~68 MB: at 20 seconds that download simply dies on a
            // normal home connection, and the user is told the update check failed.
            http.Timeout = TimeSpan.FromMinutes(10);

            var remote = (await http.GetStringAsync(VersionAssetUrl)).Trim();
            if (remote.Length == 0)
            {
                log(Strings.Get("UpdateCheckFailed", "empty version file"));
                return Result.Blocked;
            }

            if (remote == LocalBuildId)
            {
                return Result.UpToDate;
            }

            if (!confirmUpdate(remote))
            {
                log(Strings.Get("UpdateDeclined"));
                return Result.Declined;
            }

            log(Strings.Get("UpdateDownloading", remote));

            var assetName = Flavor == "standalone" ? "DiscoElysiumSetup-standalone.exe" : "DiscoElysiumSetup.exe";
            var assetUrl = $"https://github.com/danijel1124/Disco-A11y/releases/download/nightly/{assetName}";
            var exePath = Environment.ProcessPath!;
            var newPath = exePath + ".new";
            var oldPath = exePath + ".old";

            using (var response = await http.GetAsync(assetUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                await using var content = await response.Content.ReadAsStreamAsync();
                await using var file = File.Create(newPath);
                await content.CopyToAsync(file);
            }

            // A downloaded exe carries a mark-of-the-web stream, which makes ShellExecute
            // refuse to launch it (or throw an empty-message Win32 error) - especially from
            // a removable drive. It is our own release asset; unblock it.
            RemoveMarkOfTheWeb(newPath);

            // A running exe cannot be overwritten but can be renamed away.
            if (File.Exists(oldPath)) File.Delete(oldPath);
            File.Move(exePath, oldPath);
            File.Move(newPath, exePath);

            var args = string.Join(" ", originalArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
            return TryRestart(exePath, args, log) ? Result.Restarting : Result.UpdatedButRestartFailed;
        }
        catch (Exception ex)
        {
            // ex.Message alone can be empty (some Win32/IO failures carry nothing), which
            // produced the useless "update check failed ()" a user actually hit.
            var detail = string.IsNullOrWhiteSpace(ex.Message) ? ex.GetType().Name : $"{ex.GetType().Name}: {ex.Message}";
            log(Strings.Get("UpdateCheckFailed", detail));
            return Result.Blocked;
        }
    }

    private static void RemoveMarkOfTheWeb(string path)
    {
        try
        {
            File.Delete(path + ":Zone.Identifier");
        }
        catch { /* no such stream, or the filesystem has none (FAT32 stick) - both fine */ }
    }

    /// <summary>
    /// Restarts into the freshly installed binary. Tries the shell first (keeps the
    /// window behaving like a user double-click), then a plain process start, which
    /// works in the environments where the shell refuses.
    /// </summary>
    private static bool TryRestart(string exePath, string args, Action<string> log)
    {
        foreach (var useShell in new[] { true, false })
        {
            try
            {
                Process.Start(new ProcessStartInfo(exePath, $"--updated {args}".Trim()) { UseShellExecute = useShell });
                return true;
            }
            catch (Exception ex)
            {
                log(Strings.Get("UpdateRestartFailed", ex.GetType().Name));
            }
        }

        return false;
    }

    /// <summary>Removes the leftover .old binary after a successful swap-restart.</summary>
    public static void CleanupAfterUpdate()
    {
        try
        {
            var oldPath = Environment.ProcessPath + ".old";
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }
        catch { /* still locked - next run gets it */ }
    }
}
