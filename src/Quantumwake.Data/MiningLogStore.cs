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
    string? Note);

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
            File.WriteAllText(_path, JsonSerializer.Serialize(_runs));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Losing a write costs the newest entry, never the file: the next
            // save rewrites the whole list from memory.
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
    }
}
