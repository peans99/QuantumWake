using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>One thing a kit asks for.</summary>
/// <param name="Optional">
/// A kit is usually a core of four things plus preferences. A shopping list
/// that cannot tell those apart is one nobody trusts, because the first time it
/// says "you are missing 3 items" over a spare undersuit it stops being read.
/// </param>
public sealed record KitItem(string Name, int Quantity = 1, bool Optional = false);

/// <summary>A loadout the pilot keeps, so it can be put back together.</summary>
public sealed record Kit(
    string Id,
    string Name,
    IReadOnlyList<KitItem> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ModifiedAt = null) : IStamped<Kit>
{
    public string StampId => Id;
    public Kit Bare() => this with { ModifiedAt = null };
    public Kit Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    public DateTimeOffset ChangedAt => ModifiedAt ?? CreatedAt;
}

/// <summary>
/// The pilot's saved kits.
/// </summary>
/// <remarks>
/// Authored, so its own file beside the jobs and plans, never touched by a
/// rescan, and in the backup from the first commit rather than added to it
/// later - a store that misses that is a backup quietly no longer complete.
///
/// Per install rather than per character, matching every other authored store.
/// The logs could support per-character - session.character fires 227 times
/// across this install - so it is a consistency decision rather than a limit,
/// and if it is ever revisited it should be revisited for all of them at once.
/// </remarks>
public sealed class KitStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Marks what actually changed, so no mutator has to remember to.</summary>
    private readonly ChangeStamp<Kit> _stamp = new(k => JsonSerializer.Serialize(k));

    private List<Kit> _kits = [];

    public KitStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "kits.json");
        Load();
    }

    public IReadOnlyList<Kit> All()
    {
        lock (_gate) return [.. _kits];
    }

    public Kit? Find(string id)
    {
        lock (_gate) return _kits.FirstOrDefault(k => k.Id == id);
    }

    public Kit Add(string? name, IEnumerable<KitItem>? items = null)
    {
        var kit = new Kit(
            Guid.NewGuid().ToString("N")[..8],
            Sanitise.Clean(name, "Kit"),
            [.. Clean(items)],
            DateTimeOffset.UtcNow);

        lock (_gate)
        {
            _kits.Insert(0, kit);
            Save();
            return _kits[0];
        }
    }

    public bool Replace(string id, string? name, IEnumerable<KitItem>? items)
    {
        lock (_gate)
        {
            var index = _kits.FindIndex(k => k.Id == id);
            if (index < 0) return false;

            _kits[index] = _kits[index] with
            {
                Name = Sanitise.Clean(name, _kits[index].Name),
                Items = [.. Clean(items)],
            };

            Save();
            return true;
        }
    }

    public bool Remove(string id)
    {
        lock (_gate)
        {
            if (_kits.RemoveAll(k => k.Id == id) == 0) return false;

            Save();
            return true;
        }
    }

    /// <summary>Puts a record back exactly as given - for restoring a backup.</summary>
    public void Put(Kit kit)
    {
        lock (_gate)
        {
            var index = _kits.FindIndex(k => k.Id == kit.Id);

            if (index >= 0) _kits[index] = kit;
            else _kits.Add(kit);

            // The record keeps the change time it was backed up with.
            _stamp.Adopt(kit);
            Save();
        }
    }

    /// <summary>
    /// Lines a kit may hold, cleaned the way the page would have to clean them.
    /// </summary>
    /// <remarks>
    /// Quantities are clamped rather than rejected: somebody typing 9999 into a
    /// medpen count has made a slip, and refusing the whole kit over it is a
    /// worse answer than saving a sane number.
    /// </remarks>
    private static IEnumerable<KitItem> Clean(IEnumerable<KitItem>? items) =>
        (items ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .Select(item => new KitItem(
                Sanitise.Clean(item.Name, "Item"),
                Math.Clamp(item.Quantity, 1, 999),
                item.Optional))
            .Take(MaxItems);

    /// <summary>A loadout is dozens of things, never hundreds.</summary>
    public const int MaxItems = 100;

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _kits = JsonSerializer.Deserialize<List<Kit>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file must not stop the app; the user starts with none.
            _kits = [];
        }

        _stamp.Loaded(_kits);
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        _stamp.Apply(_kits, DateTimeOffset.UtcNow);
        File.WriteAllText(_path, JsonSerializer.Serialize(_kits));
    }
}
