using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>How far along a haul is.</summary>
/// <remarks>
/// Derived from the fields rather than stored. A stage kept beside the dates it
/// summarises is a second thing that can disagree with them, and the one that
/// disagrees is never the one being read carefully.
/// </remarks>
public enum MiningStage
{
    /// <summary>Out of the rock and in a hold. Nothing has been refined yet.</summary>
    Extracted,

    /// <summary>At a refinery, still working as far as the pilot said.</summary>
    Submitted,

    /// <summary>Past the time the pilot expected it to finish.</summary>
    Ready,

    /// <summary>Picked up. What came back may be less than what went in.</summary>
    Collected,

    /// <summary>Sold, and what it made was written down.</summary>
    Sold
}

/// <summary>
/// A refinery job, as the pilot recorded it.
/// </summary>
/// <param name="ExpectedAt">
/// When they expect it done. The game keeps that timer and logs nothing about
/// it, so this is their reading of a screen rather than anything observed - and
/// anything built on it has to be worded as a reminder of what they typed.
/// </param>
/// <param name="Yield">
/// What came back, in SCU. Less than what went in is normal, and the difference
/// is the number worth knowing - so the two are kept apart rather than one
/// overwriting the other.
/// </param>
public sealed record RefineryJob(
    string Place,
    string? Method,
    decimal? Cost,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? ExpectedAt,
    double? Yield = null,
    DateTimeOffset? CollectedAt = null);

/// <summary>One haul, as the pilot recorded it.</summary>
/// <param name="Scu">What came out of the rock, in SCU.</param>
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
    DateTimeOffset? ModifiedAt = null,
    RefineryJob? Refinery = null,
    DateTimeOffset? SoldAt = null) : IStamped<MiningRun>
{
    public string StampId => Id;
    public MiningRun Bare() => this with { ModifiedAt = null };
    public MiningRun Stamped(DateTimeOffset at) => this with { ModifiedAt = at };

    /// <summary>When this last changed - see <see cref="Job.ChangedAt"/>.</summary>
    /// <remarks>At is when the haul happened, which is not when the row was edited.</remarks>
    public DateTimeOffset ChangedAt => ModifiedAt ?? At;

    /// <summary>
    /// How far along this haul is, worked out from what has been filled in.
    /// </summary>
    /// <remarks>
    /// Takes the time because Ready is the one stage that is about now rather
    /// than about the record: a job is ready when the moment the pilot expected
    /// has passed, and nothing writes that down when it happens.
    /// </remarks>
    public MiningStage StageAt(DateTimeOffset now) =>
        SoldAt is not null ? MiningStage.Sold
        : Refinery is not { } job ? MiningStage.Extracted
        : job.CollectedAt is not null ? MiningStage.Collected
        : job.ExpectedAt is { } due && due <= now ? MiningStage.Ready
        : MiningStage.Submitted;

    /// <summary>
    /// SCU that went in and did not come back, when both are known.
    /// </summary>
    /// <remarks>
    /// Null rather than zero before the job is collected: nothing lost and
    /// nothing known yet are different facts, and only one of them is a number.
    /// </remarks>
    public double? Lost => Refinery?.Yield is { } back ? Math.Max(0, Scu - back) : null;
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
            // save rewrites the whole list from memory. That bargain is right
            // for somebody typing a haul in - and wrong for a restore, which
            // would report success over a write that never landed. Put() is
            // the one caller that needs to know, so the failure is re-raised
            // for it and swallowed for everyone else.
            if (_restoring) throw;
        }
    }

    /// <summary>
    /// True while a restore is writing, so a lost write is an error rather than
    /// a shrug. See the catch in <see cref="Save"/>.
    /// </summary>
    private bool _restoring;

    /// <summary>
    /// Sends a haul to a refinery.
    /// </summary>
    /// <remarks>
    /// Only from Extracted. Stages run one way: a haul already collected cannot
    /// be submitted again, and letting it would quietly discard the yield that
    /// came back the first time.
    /// </remarks>
    public bool Submit(string id, string? place, string? method, decimal? cost,
        DateTimeOffset? expected, DateTimeOffset now)
    {
        lock (_gate)
        {
            var index = _runs.FindIndex(r => r.Id == id);

            if (index < 0 || _runs[index].StageAt(now) != MiningStage.Extracted)
                return false;

            _runs[index] = _runs[index] with
            {
                Refinery = new RefineryJob(
                    Sanitise.Clean(place, _runs[index].Place),
                    Sanitise.CleanOptional(method, 40),
                    cost is >= 0 and <= 1_000_000_000m ? cost : null,
                    now,
                    expected),
            };

            Save();
            return true;
        }
    }

    /// <summary>Picks a job up, recording what actually came back.</summary>
    public bool Collect(string id, double? yield, DateTimeOffset now)
    {
        lock (_gate)
        {
            var index = _runs.FindIndex(r => r.Id == id);
            if (index < 0) return false;

            if (_runs[index].StageAt(now) is not (MiningStage.Submitted or MiningStage.Ready))
                return false;

            _runs[index] = _runs[index] with
            {
                Refinery = _runs[index].Refinery! with
                {
                    Yield = yield is >= 0 and <= 100_000 ? yield : null,
                    CollectedAt = now,
                },
            };

            Save();
            return true;
        }
    }

    /// <summary>Records what the refined ore sold for.</summary>
    /// <remarks>
    /// Typed, never inferred. A commodity sale in the ledger might be this ore
    /// or might be anything else, and joining the two would turn a guess into a
    /// figure that looks observed.
    /// </remarks>
    public bool Sell(string id, decimal? revenue, DateTimeOffset now)
    {
        lock (_gate)
        {
            var index = _runs.FindIndex(r => r.Id == id);

            if (index < 0 || _runs[index].StageAt(now) != MiningStage.Collected)
                return false;

            _runs[index] = _runs[index] with
            {
                Revenue = revenue is >= 0 and <= 1_000_000_000m ? revenue : null,
                SoldAt = now,
            };

            Save();
            return true;
        }
    }

    /// <summary>
    /// Hauls waiting on a refinery, soonest first.
    /// </summary>
    /// <remarks>
    /// The feed for "what is owed to me right now", which is the question this
    /// page exists to answer once a haul stops being one row.
    /// </remarks>
    public IReadOnlyList<MiningRun> Pending(DateTimeOffset now)
    {
        lock (_gate)
        {
            return [.. _runs
                .Where(r => r.StageAt(now) is MiningStage.Submitted or MiningStage.Ready)
                .OrderBy(r => r.Refinery!.ExpectedAt ?? DateTimeOffset.MaxValue)];
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

            _restoring = true;

            try
            {
                Save();
            }
            finally
            {
                _restoring = false;
            }
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
