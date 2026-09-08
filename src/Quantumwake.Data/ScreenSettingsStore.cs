using System.Text.Json;
using System.Text.Json.Serialization;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>What the pilot has allowed the screen panel to do.</summary>
/// <param name="Watch">
/// Whether the clipboard is checked on its own rather than on a button. Only
/// meaningful when the mode is not <see cref="ScreenMode.Off"/>.
/// </param>
public sealed record ScreenSettings(
    ScreenMode Mode = ScreenMode.Off,
    bool Watch = false)
{
    /// <summary>The setting as it will actually be used, out of anything sent.</summary>
    /// <remarks>
    /// Watching with nothing switched on is not a state worth storing, and
    /// storing it would let the panel come back saying it is watching when it
    /// is doing nothing at all.
    /// </remarks>
    public static ScreenSettings Clean(ScreenMode? mode, bool? watch)
    {
        var settled = mode ?? ScreenMode.Off;

        return new ScreenSettings(settled, settled != ScreenMode.Off && watch == true);
    }
}

/// <summary>
/// Remembers what the screen panel has been allowed to do.
/// </summary>
/// <remarks>
/// Its own file, beside the other authored settings, and it starts at
/// <see cref="ScreenMode.Off"/>. Reading a pilot's screenshots is not something
/// to arrive switched on because a default said so - see
/// <c>docs/screen-insight.md</c>.
/// </remarks>
public sealed class ScreenSettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private ScreenSettings _current = new();

    public ScreenSettingsStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "screen-settings.json");
        Load();
    }

    public ScreenSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public ScreenSettings Save(ScreenMode? mode, bool? watch)
    {
        var settled = ScreenSettings.Clean(mode, watch);

        lock (_gate)
        {
            _current = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(settled, Json));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // A setting that fails to save reverts on the next start, which
                // beats refusing to change it.
            }

            return settled;
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            var read = JsonSerializer.Deserialize<ScreenSettings>(File.ReadAllText(_path), Json);
            _current = ScreenSettings.Clean(read?.Mode, read?.Watch);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = new ScreenSettings();
        }
    }

    /// <summary>
    /// The mode is written by name, so the file says what it means and a value
    /// added later does not shift the meaning of one already on disk.
    /// </summary>
    private static readonly JsonSerializerOptions Json = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
