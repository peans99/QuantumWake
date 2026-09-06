using Quantumwake.Core;
using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>A record the pilot deleted, and when.</summary>
/// <param name="Store">
/// Which store it was deleted from - "jobs", "trips", "checklists", "mining",
/// "notes". Ids are only unique within a store, and a restore has to ask the
/// question per store rather than across all of them.
/// </param>
public sealed record Tombstone(string Store, string Id, DateTimeOffset At);

/// <summary>
/// What the pilot has thrown away, kept so a restore can tell the difference
/// between something they never had and something they got rid of.
/// </summary>
/// <remarks>
/// <para>
/// Without this a restore is guessing. A backup taken last month holds a trip
/// deleted last week; on the way back in, "not on this machine" and "removed on
/// purpose" look identical, and the quiet answer - put it back - is the wrong
/// one. This is the only thing that lets the preview offer the choice.
/// </para>
/// <para>
/// One store rather than a list inside each of the five, because a tombstone is
/// a fact about the pilot's intent rather than about the record: the record is
/// gone. Recorded where that intent is expressed - the delete endpoint - which
/// also keeps five existing file formats from having to change shape.
/// </para>
/// <para>
/// Deliberately tiny and never read by anything but export and restore. It is
/// not an undo history and must not grow into one: no titles, no contents, and
/// nothing here can rebuild what was deleted.
/// </para>
/// </remarks>
public sealed class TombstoneStore
{
    /// <summary>Store names, spelled once so a typo cannot split a store in two.</summary>
    public static class Kinds
    {
        public const string Jobs = "jobs";
        public const string Checklists = "checklists";
        public const string Trips = "trips";
        public const string Mining = "mining";
        public const string Notes = "notes";
    }

    private readonly string _path;
    private readonly Lock _gate = new();
    private List<Tombstone> _stones = [];

    public TombstoneStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "deleted.json");
        Load();
    }

    public IReadOnlyList<Tombstone> All()
    {
        lock (_gate) return [.. _stones];
    }

    /// <summary>Notes that something was deleted. Repeats keep the first time.</summary>
    /// <remarks>
    /// The first deletion is the true one: deleting, restoring and deleting
    /// again should not make the record look newer than the backup that still
    /// carries it.
    /// </remarks>
    public void Record(string store, string? id)
    {
        if (string.IsNullOrWhiteSpace(store) || string.IsNullOrWhiteSpace(id))
            return;

        lock (_gate)
        {
            if (_stones.Any(s => s.Store == store && s.Id == id))
                return;

            _stones.Add(new Tombstone(store, id, DateTimeOffset.UtcNow));
            Save();
        }
    }

    /// <summary>
    /// Forgets a deletion, because the pilot chose to take the record back.
    /// </summary>
    /// <remarks>
    /// Restoring something over its own tombstone has to clear it, or the very
    /// next restore offers to remove it again - and a preview that keeps asking
    /// the same question is one people stop reading.
    /// </remarks>
    public void Forget(string store, string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return;

        lock (_gate)
        {
            if (_stones.RemoveAll(s => s.Store == store && s.Id == id) > 0)
                Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _stones = JsonSerializer.Deserialize<List<Tombstone>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file that cannot be read means the app forgets what was deleted,
            // which costs one over-eager question in a restore preview. Refusing
            // to start over it would cost the whole app.
            _stones = [];
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_stones));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Same bargain as loading: a lost tombstone degrades one preview.
        }
    }
}
