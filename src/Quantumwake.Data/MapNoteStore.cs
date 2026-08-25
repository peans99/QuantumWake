using Quantumwake.Core;
using System.Text.Json;
using Quantumwake.OrgShared;

namespace Quantumwake.Data;

/// <summary>A personal point of interest pinned to a place the atlas can draw.</summary>
/// <remarks>
/// A note is authored evidence, never a claim that the game reported a service,
/// route, or live stock there. Keeping the place id beside the readable name
/// lets a renamed label still land on the right map node.
/// </remarks>
public sealed record MapNote(
    string Id,
    string PlaceId,
    string Place,
    string Title,
    string? Note,
    IReadOnlyList<string> Tags,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>The pilot's reusable map notes, separate from log-derived places.</summary>
public sealed class MapNoteStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private List<MapNote> _notes = [];

    public MapNoteStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "map-notes.json");
        Load();
    }

    public IReadOnlyList<MapNote> All()
    {
        lock (_gate) return [.. _notes];
    }

    public MapNote? Add(string? placeId, string? place, string? title, string? note,
        IReadOnlyList<string>? tags)
    {
        if (string.IsNullOrWhiteSpace(placeId) || string.IsNullOrWhiteSpace(place))
            return null;

        var now = DateTimeOffset.UtcNow;
        var item = new MapNote(
            NewId(),
            Sanitise.Clean(placeId, string.Empty, 80),
            Sanitise.Clean(place, "Somewhere"),
            Sanitise.Clean(title, "Map note"),
            Sanitise.CleanOptional(note),
            CleanTags(tags),
            now,
            now);

        lock (_gate)
        {
            _notes.Insert(0, item);
            Save();
            return item;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (_notes.RemoveAll(note => note.Id == id) == 0) return false;
            Save();
            return true;
        }
    }

    private static IReadOnlyList<string> CleanTags(IReadOnlyList<string>? tags) =>
        [.. (tags ?? [])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => Sanitise.Clean(tag, string.Empty, 32))
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)];

    private static string NewId() => Guid.NewGuid().ToString("N")[..8];

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _notes = JsonSerializer.Deserialize<List<MapNote>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // Notes are conveniences, not the map itself. A damaged file must
            // leave the atlas usable instead of preventing the dashboard loading.
            _notes = [];
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_notes));
    }
}
