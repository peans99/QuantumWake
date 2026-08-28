using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>What the text overlay wrote, and what it displaced.</summary>
/// <param name="Layered">
/// True when it was built on top of an installed text mod rather than on the
/// game's own table. Removing then puts that mod's file back, not the game's.
/// </param>
public sealed record TextOverlayInstall(
    DateTimeOffset InstalledAt,
    string GameRoot,
    int Marked,
    bool Layered,
    IReadOnlyList<InstalledFile> Files);

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

    /// <summary>True when every file written is still where it was written.</summary>
    public bool StillPresent()
    {
        var install = Current;

        return install is not null && install.Files.Count > 0
            && install.Files.All(f => File.Exists(f.Path));
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
