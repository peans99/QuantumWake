using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// What a StarStrings archive is allowed to put into a game folder.
/// </summary>
/// <remarks>
/// The download is never trusted to be what it claims. Two paths are accepted -
/// the English localisation file and the config line that switches it on - and
/// anything else refuses the whole install rather than being skipped, because a
/// release carrying a file we did not expect is a release we do not understand.
/// An archive that unpacks where it likes, into a folder named by its own
/// contents, is the oldest trick there is, and this one unpacks into somebody's
/// game install.
/// </remarks>
public static class StarStringsArchive
{
    /// <summary>
    /// The absolute path an entry may be written to, or null when it may not be
    /// written at all.
    /// </summary>
    public static string? TargetFor(string entryName, string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(entryName) || string.IsNullOrWhiteSpace(gameRoot))
            return null;

        var written = entryName.Replace('\\', '/').TrimStart('/');

        // A rooted or drive-qualified entry is not relative to anything, so it
        // is refused before Path.Combine can quietly honour it.
        if (Path.IsPathRooted(written) || written.Contains(':'))
            return null;

        var root = Path.GetFullPath(gameRoot);
        var fence = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(root, written.Replace('/', Path.DirectorySeparatorChar)));

        if (!target.StartsWith(fence, StringComparison.OrdinalIgnoreCase))
            return null;

        // Judge the path it RESOLVES to, never the path it claims. Landing
        // inside the game folder is not enough on its own:
        // "Data/Localization/../../Bin64/StarCitizen.exe" starts with an allowed
        // prefix and lands on the executable.
        var resolved = target[fence.Length..].Replace(Path.DirectorySeparatorChar, '/');

        var allowed = string.Equals(resolved, "user.cfg", StringComparison.OrdinalIgnoreCase)
            || resolved.StartsWith("data/localization/", StringComparison.OrdinalIgnoreCase);

        return allowed ? target : null;
    }
}

/// <summary>One file this app put into the game folder, and what was there before.</summary>
/// <param name="Path">Absolute path written.</param>
/// <param name="Backup">
/// Where the file that used to be there was moved to, or null when there was
/// nothing to move. Removing puts it back.
/// </param>
public sealed record InstalledFile(string Path, string? Backup);

/// <summary>
/// What is installed, from which release, and what it displaced.
/// </summary>
/// <param name="Release">The release's own name - "SC LIVE Build (release-2026-07-22-...)".</param>
/// <param name="PublishedAt">When that release was published, which is how "newer" is decided.</param>
public sealed record StarStringsInstall(
    string Release,
    DateTimeOffset? PublishedAt,
    DateTimeOffset InstalledAt,
    string GameRoot,
    IReadOnlyList<InstalledFile> Files);

/// <summary>
/// Remembers a StarStrings install so it can be undone exactly.
/// </summary>
/// <remarks>
/// <para>
/// This is the only part of Quantumwake that writes outside its own data
/// folder, and it writes into the player's game install. That asymmetry is the
/// whole reason this store exists: an install must be a list of specific files
/// with specific origins, not a folder we later delete on a guess. Removing
/// restores what was displaced, and deletes only what we put there.
/// </para>
/// <para>
/// The record survives the game being patched over the top. If the files are
/// gone the install is simply no longer there, which the page says rather than
/// pretending the mod is active.
/// </para>
/// </remarks>
public sealed class StarStringsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private StarStringsInstall? _current;

    public StarStringsStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "starstrings.json");
        Load();
    }

    /// <summary>Where displaced files are kept, inside our own folder rather than the game's.</summary>
    public string BackupRoot => Path.Combine(Path.GetDirectoryName(_path)!, "starstrings-backup");

    /// <summary>What we believe is installed, or null.</summary>
    public StarStringsInstall? Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>True when every file we wrote is still where we wrote it.</summary>
    public bool StillPresent()
    {
        var install = Current;

        return install is not null && install.Files.Count > 0
            && install.Files.All(f => File.Exists(f.Path));
    }

    public void Record(StarStringsInstall install)
    {
        lock (_gate)
        {
            _current = install;
            Save();
        }
    }

    public void Forget()
    {
        lock (_gate)
        {
            _current = null;

            if (File.Exists(_path))
                File.Delete(_path);
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _current = JsonSerializer.Deserialize<StarStringsInstall>(File.ReadAllText(_path));
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A record we cannot read is treated as no record: the worst case is
            // offering an install that is already there, which overwrites the
            // same files it would have overwritten anyway.
            _current = null;
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_current));
    }
}
