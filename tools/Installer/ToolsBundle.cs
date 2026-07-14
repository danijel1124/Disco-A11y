using System.IO.Compression;
using System.Text.Json;

namespace Installer;

/// <summary>
/// Fetches the companion tools (configurator, dev bridge) from the same GitHub release the
/// mod comes from and puts them next to the installer.
///
/// They used to be embedded in the installer binary and dropped to disk on startup. That is
/// exactly what a dropper does, and Defender's ML heuristics said so out loud: a user's
/// machine quarantined the setup as Trojan:Win32/Bearfoos.B!ml. Nothing in the code was
/// wrong - the shape of the delivery was. An installer that unpacks foreign executables out
/// of itself looks like malware, so it stopped doing that: now everything it puts on disk
/// comes from the release, the same way MelonLoader and the mod already did.
///
/// Tolk stays embedded. It is not something the installer installs, it is what the installer
/// SPEAKS with - fetching it first would leave a blind user in front of a silent window until
/// the network answered.
/// </summary>
public static class ToolsBundle
{
    /// <summary>Downloads and unpacks the tools next to the installer. False if unavailable.</summary>
    public static async Task<bool> EnsureAsync(
        string owner,
        string repo,
        bool includePrerelease,
        Action<string>? statusCallback = null)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd("DiscoElysiumInstaller/1.0");
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var url = await FindToolsAssetAsync(http, owner, repo, includePrerelease);
            if (url == null)
            {
                statusCallback?.Invoke(Strings.Get("StepToolsMissing"));
                return false;
            }

            statusCallback?.Invoke(Strings.Get("StepToolsDownloading"));

            var tempZip = Path.Combine(Path.GetTempPath(), $"DiscoA11yTools_{Guid.NewGuid():N}.zip");
            try
            {
                using (var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();
                    await using var content = await response.Content.ReadAsStreamAsync();
                    await using var file = File.Create(tempZip);
                    await content.CopyToAsync(file);
                }

                Directory.CreateDirectory(Program.BundleDir);
                using var zip = ZipFile.OpenRead(tempZip);
                foreach (var entry in zip.Entries)
                {
                    if (entry.Name.Length == 0) continue;   // directory entry

                    // The bundle also carries the installer itself; overwriting the running
                    // exe is neither possible nor wanted (the self-updater does that job).
                    if (entry.Name.Equals("DiscoElysiumInstaller.exe", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        entry.ExtractToFile(Path.Combine(Program.BundleDir, entry.Name), overwrite: true);
                    }
                    catch (IOException)
                    {
                        // In use (the configurator is open right now) - what is there stays.
                    }
                }

                statusCallback?.Invoke(Strings.Get("StepToolsReady"));
                return true;
            }
            finally
            {
                try { if (File.Exists(tempZip)) File.Delete(tempZip); } catch { /* ignore */ }
            }
        }
        catch (Exception ex)
        {
            statusCallback?.Invoke(Strings.Get("StepToolsFailed", ex.Message));
            return false;
        }
    }

    private static async Task<string?> FindToolsAssetAsync(
        HttpClient http, string owner, string repo, bool includePrerelease)
    {
        var releaseUrl = includePrerelease
            ? $"https://api.github.com/repos/{owner}/{repo}/releases?per_page=10"
            : $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

        using var response = await http.GetAsync(releaseUrl);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        var release = includePrerelease ? PickPrerelease(doc.RootElement) : doc.RootElement;
        if (release.ValueKind == JsonValueKind.Undefined) return null;

        foreach (var asset in release.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString();
            if (name != null
                && name.StartsWith("DiscoElysiumTools", StringComparison.OrdinalIgnoreCase)
                && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                return asset.GetProperty("browser_download_url").GetString();
            }
        }
        return null;
    }

    /// <summary>
    /// The nightly tag is updated in place, so its created_at stays old and "newest first"
    /// would wrongly pick a stable release - the same rule the mod download follows.
    /// </summary>
    private static JsonElement PickPrerelease(JsonElement releases)
    {
        JsonElement first = default;
        var haveFirst = false;

        foreach (var candidate in releases.EnumerateArray())
        {
            if (candidate.GetProperty("draft").GetBoolean()) continue;
            if (candidate.GetProperty("tag_name").GetString() == "nightly") return candidate;
            if (!haveFirst)
            {
                first = candidate;
                haveFirst = true;
            }
        }
        return first;
    }
}
