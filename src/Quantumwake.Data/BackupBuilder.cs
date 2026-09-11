namespace Quantumwake.Data;

/// <summary>
/// Assembles the whole of the pilot's authored work into one file.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not a wider <see cref="ExportBuilder"/>. That one answers "what
/// am I willing to send a stranger", and every question it asks - which window,
/// which classes, drop the sender's pins - is the wrong question here. A backup
/// is not a selection: leaving something out of it is how somebody loses work.
/// </para>
/// <para>
/// What it still leaves out is the same short list, for the same reason. The
/// singular-by-design fields (<c>Pinned</c>, <c>Tracked</c>) are this machine's
/// view state, and a file carrying one would either fight the reader's own pin
/// or be discarded on arrival. Everything else that was typed is here.
/// </para>
/// <para>
/// Nothing observed is here at all. Sessions, trades, payouts and contracts are
/// rebuilt by reading the logs again, and putting a copy in this file would
/// create a second version of them that can disagree with the game. The page
/// has to say so, because the first person to lose a drive will otherwise
/// believe this covers everything.
/// </para>
/// </remarks>
public sealed class BackupBuilder(
    JobStore jobs,
    ChecklistStore checklists,
    TripStore trips,
    MiningLogStore mining,
    MapNoteStore notes,
    GoalStore goals,
    WipeStore wipe,
    ItemLabelStore labels,
    TombstoneStore deleted,
    KitStore kits,
    ScreenReadingStore readings)
{
    /// <summary>The format this build writes and can read back.</summary>
    public const int Version = 1;

    public ExportFile Build(ExportProducer producer, DateTimeOffset now, string? handle = null) =>
        new(ExportDocument.Format,
            ExportDocument.FormatVersion,
            Version,
            now,
            producer,
            [ExportDocument.Backup],
            handle,
            null,
            null,
            null,
            null,
            Contents());

    /// <summary>What a backup would contain, without building one.</summary>
    /// <remarks>
    /// For telling somebody what they are about to download. Counts only - the
    /// contents are the thing being protected, and a preview that had to
    /// assemble them would read every store to say "8 trips".
    /// </remarks>
    public BackupCounts Preview() => new(
        jobs.All().Count,
        checklists.All().Count,
        trips.All().Count,
        mining.All().Count,
        notes.All().Count,
        deleted.All().Count,
        kits.All().Count,
        goals.Current is not null,
        wipe.Current is not null,
        readings.Pinned().Count);

    private ExportBackup Contents() => new(
        // Pinned and Tracked belong to the machine, not to the work.
        [.. jobs.All().Select(j => j with { Pinned = false })],
        [.. checklists.All().Select(c => c with { Pinned = false })],
        [.. trips.All().Select(t => t with { Tracked = false })],
        [.. mining.All()],
        [.. notes.All()],
        [.. deleted.All()],
        [.. kits.All()],
        goals.Current,
        wipe.Current,
        labels.Current,
        // The points, but not the readings they came from: a point is named
        // and annotated by the pilot, a reading is observed and comes back
        // from the screenshot folder.
        [.. readings.Pinned()]);
}

/// <summary>How much a backup would carry, for saying so before it is taken.</summary>
public sealed record BackupCounts(
    int Jobs,
    int Checklists,
    int Trips,
    int MiningRuns,
    int Notes,
    int Deleted,
    int Kits,
    bool Goal,
    bool Wipe,
    int Pins = 0);
