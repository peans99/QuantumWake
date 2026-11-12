using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>A wingmate the pilot hauls with.</summary>
/// <param name="Hull">The game's class id for the ship they usually bring, so its hold can be looked up; null when not said.</param>
public sealed record CrewMate(string Id, string Name, string? Hull);

/// <summary>
/// The wingmates a pilot shares hauling runs with (1.2.32).
/// </summary>
/// <remarks>
/// <para>
/// Kept because a group is mostly the same few people in the same few ships,
/// and typing them in each run is the kind of chore that stops a feature
/// being used. Where each one is changes every run, so that is asked on the
/// page and never stored here.
/// </para>
/// <para>
/// Names only, typed by the pilot - nothing here is read from the game or
/// sent anywhere. Its own file, like the other small settings, so a damaged
/// list costs the list and nothing else.
/// </para>
/// </remarks>
public sealed class CrewStore
{
    /// <summary>A group, not a fleet: the share-out is tried for every ship, so more than this would be slow and unreadable.</summary>
    public const int Max = 12;

    private readonly string _path;
    private readonly Lock _gate = new();
    private List<CrewMate> _crew = [];

    public CrewStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "crew.json");
        Load();
    }

    public IReadOnlyList<CrewMate> All
    {
        get { lock (_gate) return [.. _crew]; }
    }

    /// <summary>Adds a wingmate, or changes one when <paramref name="id"/> names an existing one. Null when the name is empty or the list is full.</summary>
    public CrewMate? Save(string? id, string? name, string? hull)
    {
        var clean = (name ?? "").Trim();
        if (clean.Length == 0) return null;
        if (clean.Length > 40) clean = clean[..40];
        var ship = string.IsNullOrWhiteSpace(hull) ? null : hull.Trim();

        lock (_gate)
        {
            var at = id is null ? -1 : _crew.FindIndex(c => c.Id == id);
            CrewMate mate;
            if (at >= 0)
            {
                mate = _crew[at] with { Name = clean, Hull = ship };
                _crew[at] = mate;
            }
            else
            {
                if (_crew.Count >= Max) return null;
                mate = new CrewMate(Guid.NewGuid().ToString("N")[..10], clean, ship);
                _crew.Add(mate);
            }
            Write();
            return mate;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (_crew.RemoveAll(c => c.Id == id) == 0) return false;
            Write();
            return true;
        }
    }

    private void Write()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_crew));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // The list holds for this run of the app and is asked again next
            // time, which beats refusing the change.
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;
            _crew = (JsonSerializer.Deserialize<List<CrewMate>>(File.ReadAllText(_path)) ?? [])
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Name))
                .Take(Max)
                .ToList();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _crew = [];
        }
    }
}
