using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>How the app handles runs left going.</summary>
/// <param name="ArchiveAfterDays">
/// How long a started run may sit untouched before it is filed on its own.
/// Zero turns the sweep off entirely, which is a thing somebody may reasonably
/// want: a pilot who flies one long run a month is not abandoning it.
/// </param>
public sealed record RunSettings(int ArchiveAfterDays = RunSettings.DefaultDays)
{
    /// <summary>
    /// Ten days, which is a guess and is admitted as one on the page.
    /// </summary>
    /// <remarks>
    /// Long enough that somebody who flies most weeks never meets it, short
    /// enough that the working list does not fill with runs nobody will finish.
    /// The logs could answer this better - the gap between sessions in an
    /// install is measurable - and until that is done this is a default rather
    /// than a finding.
    /// </remarks>
    public const int DefaultDays = 10;

    /// <summary>A month is the widest that still means "went quiet".</summary>
    public const int MaxDays = 30;

    public TimeSpan Idle => TimeSpan.FromDays(ArchiveAfterDays);

    /// <summary>The setting as it will actually be used, out of anything sent.</summary>
    public static RunSettings Clean(int? days) =>
        new(days is null ? DefaultDays : Math.Clamp(days.Value, 0, MaxDays));
}

/// <summary>
/// Remembers how patient the app is with a run that has gone quiet.
/// </summary>
/// <remarks>
/// Its own small file beside the goal, for the same reason that has one: it is
/// a single authored value, and folding it into the trips file would change a
/// format that already has people's plans in it.
/// </remarks>
public sealed class RunSettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private RunSettings _current = new();

    public RunSettingsStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "run-settings.json");
        Load();
    }

    public RunSettings Current
    {
        get { lock (_gate) return _current; }
    }

    public RunSettings Save(int? days)
    {
        var settled = RunSettings.Clean(days);

        lock (_gate)
        {
            _current = settled;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(settled));
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

            _current = RunSettings.Clean(
                JsonSerializer.Deserialize<RunSettings>(File.ReadAllText(_path))?.ArchiveAfterDays);
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = new RunSettings();
        }
    }
}
