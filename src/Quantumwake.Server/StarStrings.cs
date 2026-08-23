using System.IO.Compression;
using System.Text.Json;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>What the newest StarStrings release is, when the question can be answered.</summary>
public sealed record StarStringsRelease(string Name, DateTimeOffset? PublishedAt, string Url, string DownloadUrl);

/// <summary>
/// Installs MrKraken's StarStrings into the player's game folder, on request.
/// </summary>
/// <remarks>
/// <para>
/// StarStrings is a text mod: it replaces the game's English localisation file
/// so contracts, item names and the mining guide read more usefully. It touches
/// no binary and no memory - it is two files copied into the LIVE folder - but
/// it is still the only thing this app writes outside its own data folder, and
/// the only thing it puts into somebody else's install. So it is opt-in, it is
/// explicit about what it writes, and everything it writes is recorded so it
/// can be taken back out.
/// </para>
/// <para>
/// The archive is never trusted to be what it claims. Only two paths are
/// accepted - the localisation file and the config that switches the language
/// on - and anything else in the zip aborts the install without a byte being
/// written. That is not a theoretical worry: an archive that unpacks where it
/// likes, into a folder chosen by its own contents, is the oldest trick there
/// is, and this one is unpacked into a game install.
/// </para>
/// </remarks>
public sealed class StarStrings(IHttpClientFactory factory, StarStringsStore store, ILogger<StarStrings> log)
{
    public const string Repository = "https://github.com/MrKraken/StarStrings";
    public const string LatestUrl = "https://api.github.com/repos/MrKraken/StarStrings/releases/latest";

    /// <summary>The newest release, or null when GitHub cannot be reached.</summary>
    public async Task<StarStringsRelease?> LatestAsync(CancellationToken token = default)
    {
        try
        {
            var http = factory.CreateClient("community");
            using var response = await http.GetAsync(LatestUrl, token);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            var root = doc.RootElement;

            var asset = root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array
                ? assets.EnumerateArray().FirstOrDefault(a =>
                    a.TryGetProperty("name", out var n)
                    && n.GetString()?.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) == true)
                : default;

            if (asset.ValueKind != JsonValueKind.Object)
                return null;

            return new StarStringsRelease(
                root.TryGetProperty("name", out var name) ? name.GetString() ?? "latest" : "latest",
                root.TryGetProperty("published_at", out var at) && at.TryGetDateTimeOffset(out var when)
                    ? when
                    : null,
                root.TryGetProperty("html_url", out var page) ? page.GetString() ?? Repository : Repository,
                asset.GetProperty("browser_download_url").GetString() ?? "");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            // Same rule as the app's own update check: no news is not an error
            // the player has to read about.
            log.LogInformation(e, "StarStrings release check failed");
            return null;
        }
    }

    /// <summary>
    /// True when the newest release is newer than what is installed.
    /// </summary>
    /// <remarks>
    /// By publish date, because the tag never changes: the project publishes
    /// every build to a tag literally called "latest", so a version string
    /// would compare equal forever.
    /// </remarks>
    public static bool IsNewer(StarStringsInstall? installed, StarStringsRelease? latest)
    {
        if (latest is null)
            return false;

        if (installed is null)
            return false;

        if (installed.PublishedAt is { } had && latest.PublishedAt is { } now)
            return now > had;

        return !string.Equals(installed.Release, latest.Name, StringComparison.Ordinal);
    }

    /// <summary>Downloads the release and writes it into the game folder.</summary>
    /// <returns>What was installed, or a sentence saying why nothing was.</returns>
    public async Task<(StarStringsInstall? Install, string? Problem)> InstallAsync(
        GameInstall game, CancellationToken token = default)
    {
        var latest = await LatestAsync(token);

        if (latest is null || string.IsNullOrWhiteSpace(latest.DownloadUrl))
            return (null, "GitHub did not answer, so there is nothing to install yet. Try again in a moment.");

        var root = game.RootPath;

        if (!Directory.Exists(root))
            return (null, $"The game folder is not where this app expects it: {root}");

        byte[] archive;

        try
        {
            var http = factory.CreateClient("community");
            archive = await http.GetByteArrayAsync(latest.DownloadUrl, token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(e, "StarStrings download failed");
            return (null, "The download did not finish. Nothing was changed.");
        }

        using var zip = new ZipArchive(new MemoryStream(archive), ZipArchiveMode.Read);

        // Everything is checked before anything is written: a half-installed
        // localisation file is a game that starts with missing text.
        var planned = new List<(ZipArchiveEntry Entry, string Target)>();

        foreach (var entry in zip.Entries)
        {
            if (entry.Length == 0 && entry.FullName.EndsWith('/'))
                continue;

            if (StarStringsArchive.TargetFor(entry.FullName, root) is not { } target)
                return (null, $"The archive holds something this install does not expect ({entry.FullName}), so nothing was written.");

            planned.Add((entry, target));
        }

        if (planned.Count == 0)
            return (null, "The release archive was empty, so nothing was written.");

        // Take the previous install out first, so its backups are restored and
        // this one's are of the game's own files rather than of the last mod.
        Remove();

        var written = new List<InstalledFile>();
        var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");

        try
        {
            foreach (var (entry, target) in planned)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);

                string? backup = null;

                if (File.Exists(target))
                {
                    backup = Path.Combine(store.BackupRoot, stamp, Path.GetFileName(target));
                    Directory.CreateDirectory(Path.GetDirectoryName(backup)!);
                    File.Copy(target, backup, overwrite: true);
                }

                entry.ExtractToFile(target, overwrite: true);
                written.Add(new InstalledFile(target, backup));
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            log.LogWarning(e, "StarStrings install failed partway");

            // Put back whatever we had already displaced rather than leaving
            // the game half-modded.
            Restore(written);

            return (null, e is UnauthorizedAccessException
                ? "Windows refused the write. If the game is installed under Program Files, run the app as administrator once, or close the game and try again."
                : "A file could not be written, so what had been changed was put back.");
        }

        var install = new StarStringsInstall(
            latest.Name, latest.PublishedAt, DateTimeOffset.UtcNow, root, written);

        store.Record(install);
        return (install, null);
    }

    /// <summary>Takes it back out, restoring anything it displaced.</summary>
    public bool Remove()
    {
        var install = store.Current;

        if (install is null)
            return false;

        Restore(install.Files);
        store.Forget();
        return true;
    }

    private void Restore(IEnumerable<InstalledFile> files)
    {
        foreach (var file in files)
        {
            try
            {
                if (file.Backup is { } backup && File.Exists(backup))
                {
                    File.Copy(backup, file.Path, overwrite: true);
                    File.Delete(backup);
                }
                else if (File.Exists(file.Path))
                {
                    File.Delete(file.Path);
                }
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // One file refusing to move is not a reason to leave the rest
                // in place; the page reports what is still there.
                log.LogWarning(e, "Could not restore {Path}", file.Path);
            }
        }
    }
}
