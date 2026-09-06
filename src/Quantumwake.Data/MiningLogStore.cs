using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>One haul, as the pilot recorded it.</summary>
/// <param name="Scu">What came out, in SCU.</param>
/// <param name="Quality">The quality it came out at, when they noted one.</param>
/// <param name="Revenue">What it sold for, when they know yet.</param>
public sealed record MiningRun(
    string Id,
    DateTimeOffset At,
    string Place,
    string Resource,
    double Scu,
    int? Quality,
    decimal? Revenue,
    string? Note,
    DateTimeOffset? ModifiedAt = null) : IStamped<MiningRun>
{
    public string StampId => Id;
    public MiningRun Bare() => this with { ModifiedAt = null };
    public MiningRun Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    /// <remarks>At is when the haul happened, which is not when the row was edited.</remarks>
    public DateTimeOffset ChangedAt => ModifiedAt ?? At;
}

/// <summary>
/// A mining record the pilot keeps, because the game keeps none.
/// </summary>
/// <remarks>
/// <para>
/// This is the one page in the app whose numbers are typed rather than read.
/// That is not a shortcut: <c>Game.log</c> records no extraction, no rock
/// scanned and no refinery job. The only trace mining leaves is ore turning up
/// in a sale that was never a purchase, which the page already shows and which
/// cannot say where it came from or what it assayed at.
/// </para>
/// <para>
/// So everything here is authored evidence and has to be presented as such,
/// beside figures that were observed. The two must never be added together into
/// one total, because one of them is checkable and the other is a memory.
/// </para>
/// </remarks>
public sealed class MiningLogStore
{
    private readonly string _path;
    private readonly Lock _gate = new();

    /// <summary>Marks what actually changed, so no mutator has to remember to.</summary>
    private readonly ChangeStamp<MiningRun> _stamp = new(r => JsonSerializer.Serialize(r));
    private List<MiningRun> _runs = [];

    public MiningLogStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "mining-log.json");
        Load();
    }

    /// <summary>Newest first, which is the order anybody reads a log in.</summary>
    public IReadOnlyList<MiningRun> All()
    {
        lock (_gate) return [.. _runs.OrderByDescending(r => r.At)];
    }

    /// <summary>Records a haul, or nothing when there is no haul to record.</summary>
    public MiningRun? Add(
        string? place, string? resource, double scu, int? quality, decimal? revenue, string? note)
    {
        if (string.IsNullOrWhiteSpace(resource) || scu <= 0) return null;

        var run = new MiningRun(
            Guid.NewGuid().ToString("N")[..12],
            DateTimeOffset.UtcNow,
            place?.Trim() is { Length: > 0 } where ? where : "somewhere",
            resource.Trim(),
            scu,
            // The game's own scale is 1 to 1000. A number outside it is a typo
            // rather than a reading, and storing it would put a quality on the
            // page that no rock could have had.
            quality is >= 1 and <= 1000 ? quality : null,
            revenue is > 0 ? revenue : null,
            note?.Trim() is { Length: > 0 } text ? text : null);

        lock (_gate)
        {
            _runs.Add(run);
            Save();
        }

        return run;
    }

    public bool Remove(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        lock (_gate)
        {
            var removed = _runs.RemoveAll(r => r.Id == id) > 0;
            if (removed) Save();

            return removed;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            _stamp.Apply(_runs, DateTimeOffset.UtcNow);
            File.WriteAllText(_path, JsonSerializer.Serialize(_runs));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a write costs the newest entry, never the file: the next
            // save rewrites the whole list from memory.
        }
    }

    /// <summary>
    /// Puts a record back exactly as given, replacing any with the same id.
    /// </summary>
    /// <remarks>
    /// For restoring a backup, and nothing else. Every other way in makes its
    /// own record so the store owns the id and the dates; this one deliberately
    /// does not, because a restore has to reproduce what was backed up rather
    /// than author something new that resembles it.
    /// </remarks>
    public void Put(MiningRun run)
    {
        lock (_gate)
        {
            var index = _runs.FindIndex(x => x.Id == run.Id);

            if (index >= 0) _runs[index] = run;
            else _runs.Add(run);

            // The record keeps the change time it was backed up with.
            _stamp.Adopt(run);
            Save();
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_path)) return;

            _runs = JsonSerializer.Deserialize<List<MiningRun>>(File.ReadAllText(_path)) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _runs = [];
        }

        _stamp.Loaded(_runs);
    }
}
