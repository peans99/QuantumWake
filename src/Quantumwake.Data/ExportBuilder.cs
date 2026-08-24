using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>What the user asked to share, and how far back.</summary>
/// <param name="Days">
/// The receipts window. Seven by default: a trading week is what somebody means
/// by "recent prices", and a price older than that is a rumour rather than a
/// lead.
/// </param>
/// <param name="Handle">
/// Whether to put the sender's name on it. The only field in the document that
/// is a person rather than a fact, so it is its own decision.
/// </param>
public sealed record ExportChoice(
    bool Receipts = false,
    bool Blueprints = false,
    bool Authored = false,
    int Days = ExportBuilder.DefaultDays,
    bool Handle = true,
    string? Note = null)
{
    public bool AskedForNothing => !Receipts && !Blueprints && !Authored;
}

/// <summary>How many of each thing a document holds, for a preview or a receipt.</summary>
public sealed record ExportCounts(int Receipts, int Blueprints, int Jobs, int Checklists, int Trips);

/// <summary>
/// Assembles a shareable document out of the stores.
/// </summary>
/// <remarks>
/// Nothing here reaches for an API projection. The endpoints add live joins —
/// <c>/api/commodities</c> attaches the current UEX best sell, <c>/api/jobs</c>
/// attaches what is in your stash and what you are wearing — and every one of
/// those is either somebody else's data or a fact about the sender's lockers
/// that they did not offer to share. Reading the stores keeps that impossible
/// rather than merely avoided.
/// </remarks>
public sealed class ExportBuilder(
    LogLibrary library,
    JobStore jobs,
    ChecklistStore checklists,
    TripStore trips,
    WipeStore wipe)
{
    /// <summary>A trading week.</summary>
    public const int DefaultDays = 7;

    /// <summary>The widest window anybody can ask for; 0 would mean everything.</summary>
    public const int MaxDays = 3650;

    public ExportFile Build(ExportChoice choice, ExportProducer producer, DateTimeOffset now)
    {
        var classes = new List<string>();

        var receipts = choice.Receipts ? BuildReceipts(choice.Days) : null;
        if (receipts is not null) classes.Add(ExportDocument.Receipts);

        var blueprints = choice.Blueprints ? BuildBlueprints() : null;
        if (blueprints is not null) classes.Add(ExportDocument.Blueprints);

        var authored = choice.Authored ? BuildAuthored() : null;
        if (authored is not null) classes.Add(ExportDocument.Authored);

        return new ExportFile(
            ExportDocument.Format,
            ExportDocument.FormatVersion,
            ExportDocument.ContentVersion,
            now,
            producer,
            classes,
            choice.Handle ? library.Handle() : null,
            Trim(choice.Note, 240),
            receipts,
            blueprints,
            authored);
    }

    /// <summary>Counts only — what a preview shows before anything leaves.</summary>
    public ExportCounts Preview(ExportChoice choice) =>
        new(choice.Receipts ? library.TradesWithin(Window(choice.Days)).Count : 0,
            choice.Blueprints ? library.Blueprints().Count : 0,
            choice.Authored ? jobs.All().Count : 0,
            choice.Authored ? checklists.All().Count : 0,
            choice.Authored ? trips.All().Count : 0);

    private ExportReceipts BuildReceipts(int days)
    {
        var window = Window(days);

        var rows = library.TradesWithin(window)
            .Select(t => new ExportReceiptRow(
                t.At, t.IsSell, t.Place, Blank(t.PlaceId), t.Scu, t.Amount, t.UnitPrice,
                t.Mode, t.Commodity, t.ResourceId))
            .OrderBy(r => r.At)
            .ToList();

        var caveats = new List<string>
        {
            ExportCaveats.PlaceInferred,
            ExportCaveats.RequestedNotConfirmed,
        };

        // Trades() already counts from the wipe line, so say so rather than let
        // a short list read as a quiet fortnight. Flags, so ask whether Money is
        // set rather than compare: Everything has it, and so does a wipe that
        // only reset the wallet.
        var counted = wipe.Current.Scope.HasFlag(WipeScope.Money);
        var wipedAt = counted ? wipe.Current.At : (DateTimeOffset?)null;
        if (counted) caveats.Add(ExportCaveats.AfterWipeLine);

        return new ExportReceipts(
            window,
            rows.Count > 0 ? rows[0].At : null,
            rows.Count > 0 ? rows[^1].At : null,
            caveats,
            rows,
            wipedAt);
    }

    private ExportBlueprints BuildBlueprints()
    {
        var rows = library.Blueprints()
            .Select(b => new ExportBlueprintRow(b.At, b.Name))
            .OrderBy(r => r.At)
            .ToList();

        return new ExportBlueprints(
            rows.Count > 0 ? rows[0].At : null,
            rows.Count > 0 ? rows[^1].At : null,
            [ExportCaveats.EarliestSighting],
            rows);
    }

    private ExportAuthored BuildAuthored()
    {
        // Pinned and Tracked are this machine's, so they are dropped on the way
        // out rather than argued about on the way in.
        var jobList = jobs.All().Select(j => j with { Pinned = false }).ToList();
        var listList = checklists.All().Select(c => c with { Pinned = false }).ToList();
        var tripList = trips.All().Select(t => t with { Tracked = false }).ToList();

        var stamps = jobList.Select(j => j.CreatedAt)
            .Concat(listList.Select(c => c.CreatedAt))
            .Concat(tripList.Select(t => t.CreatedAt))
            .ToList();

        return new ExportAuthored(
            stamps.Count > 0 ? stamps.Min() : null,
            stamps.Count > 0 ? stamps.Max() : null,
            jobList,
            listList,
            tripList);
    }

    /// <summary>
    /// A window in days, clamped. Zero means everything, which is a thing to
    /// choose deliberately rather than to arrive at by sending a negative number.
    /// </summary>
    private static int Window(int days) => days <= 0 ? 0 : Math.Min(days, MaxDays);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : Sanitise.Clip(value.Trim(), max);
}
