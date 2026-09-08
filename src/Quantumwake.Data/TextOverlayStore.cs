using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>What the text overlay wrote, and what it displaced.</summary>
/// <param name="Layered">
/// True when it was built on top of an installed text mod rather than on the
/// game's own table. Removing then puts that mod's file back, not the game's.
/// </param>
/// <param name="Fingerprint">
/// The size and hash of the table as written. Existence is not enough: another
/// text mod installed afterwards writes the same path, and without this the app
/// would keep reporting marks that had been overwritten.
/// </param>
public sealed record TextOverlayInstall(
    DateTimeOffset InstalledAt,
    string GameRoot,
    int Marked,
    bool Layered,
    IReadOnlyList<InstalledFile> Files,
    string? Fingerprint = null);

/// <summary>Whether the layer this store recorded is still the file on disk.</summary>
public enum OverlayPresence
{
    /// <summary>Nothing recorded, or what was written is no longer there.</summary>
    Gone,

    /// <summary>Our layer, byte for byte.</summary>
    Ours,

    /// <summary>Someone else's file now - a text mod, or a game patch.</summary>
    Replaced,

    /// <summary>Could not be read, so neither of the above is established.</summary>
    Unreadable
}

/// <summary>
/// Remembers a text-overlay install so it can be undone exactly.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately a second store beside <see cref="StarStringsStore"/> rather than
/// a shared one. The two write the same file, and the whole point is that each
/// knows precisely what it displaced: if this one is layered over StarStrings,
/// the file it backed up is StarStrings' - so removing this must restore that,
/// not the game's original. One shared record could not tell those apart.
/// </para>
/// <para>
/// The backup lives in this app's folder, never in the game's. A game patch that
/// overwrites the localisation file simply ends the install, which the page says
/// rather than pretending the overlay is still active.
/// </para>
/// </remarks>
public sealed class TextOverlayStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private TextOverlayInstall? _current;

    public TextOverlayStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "text-overlay.json");
        Load();
    }

    /// <summary>Where displaced files are kept, inside our own folder.</summary>
    public string BackupRoot => Path.Combine(Path.GetDirectoryName(_path)!, "text-overlay-backup");

    public TextOverlayInstall? Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>
    /// True when every file written is still where it was written, and still
    /// says what it said.
    /// </summary>
    /// <remarks>
    /// The content check is the point. Installing StarStrings afterwards writes
    /// the very same path, so a check for existence alone reports the marks as
    /// installed while the file that carries them is gone - and the first anyone
    /// notices is a column that stopped filling in.
    /// </remarks>
    public bool StillPresent() => Presence() == OverlayPresence.Ours;

    /// <summary>
    /// Whether our layer is still the one on disk - and, when that cannot be
    /// established, says so rather than guessing.
    /// </summary>
    /// <remarks>
    /// <see cref="Replaced"/> and <see cref="Unreadable"/> look identical to a
    /// bool and must not be treated alike. Replaced is a fact: somebody else's
    /// file is there, and the record is worthless. Unreadable is the absence of
    /// a fact - the game holding the file open is enough - and throwing the
    /// record away on it destroys the only thing that knows how to undo the
    /// install.
    /// </remarks>
    public OverlayPresence Presence()
    {
        var install = Current;

        if (install is null || install.Files.Count == 0) return OverlayPresence.Gone;
        if (!install.Files.All(f => File.Exists(f.Path))) return OverlayPresence.Gone;

        // An install recorded before fingerprints existed is taken at its word
        // rather than declared missing.
        if (install.Fingerprint is not { Length: > 0 }) return OverlayPresence.Ours;

        var table = install.Files.FirstOrDefault(f =>
            f.Path.EndsWith(".ini", StringComparison.OrdinalIgnoreCase));

        if (table is null) return OverlayPresence.Ours;

        if (!TryFingerprint(table.Path, out var fingerprint))
            return OverlayPresence.Unreadable;

        return fingerprint == install.Fingerprint
            ? OverlayPresence.Ours
            : OverlayPresence.Replaced;
    }

    /// <summary>What a written table looks like, cheaply enough to check often.</summary>
    /// <remarks>
    /// Empty when the file could not be read, which callers deciding whether to
    /// discard a record must not accept - use <see cref="TryFingerprint"/>.
    /// </remarks>
    public static string Fingerprint(string path) =>
        TryFingerprint(path, out var fingerprint) ? fingerprint : string.Empty;

    /// <summary>
    /// The fingerprint, and whether it could be taken at all.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Fingerprint"/> because the difference matters
    /// exactly once and matters a lot: a file that cannot be read is not a file
    /// that changed.
    /// </remarks>
    public static bool TryFingerprint(string path, out string fingerprint)
    {
        try
        {
            using var stream = File.OpenRead(path);
            fingerprint = $"{stream.Length}:{Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))[..16]}";
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            fingerprint = string.Empty;
            return false;
        }
    }

    public void Record(TextOverlayInstall install)
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
                _current = JsonSerializer.Deserialize<TextOverlayInstall>(File.ReadAllText(_path));
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // An unreadable record means we cannot claim anything is installed.
            _current = null;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_current));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing the record would strand the install, so it is written before
            // the files are, and a failure here aborts the install upstream.
            throw;
        }
    }
}
