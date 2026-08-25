using Quantumwake.Core.State;
using System.Text.Json;
using Quantumwake.OrgShared;

namespace Quantumwake.Data;

/// <summary>Why a file was refused, in words the page can show unchanged.</summary>
/// <param name="Status">
/// What the endpoint should answer with. Kept here rather than decided at the
/// route, so the reason and the code cannot drift apart.
/// </param>
public sealed record ImportProblem(string Message, int Status = 400);

/// <summary>How many of each thing came in, were dropped, or were cut short.</summary>
public sealed record ImportCounts(
    int Receipts = 0, int Blueprints = 0, int Jobs = 0, int Checklists = 0, int Trips = 0,
    int RunActions = 0)
{
    /// <summary>Whether anything at all landed in this tally.</summary>
    /// <remarks>
    /// Not serialised: it is a question the C# asks itself, and on the wire it
    /// would read as a sixth count that means something different from the five
    /// beside it.
    /// </remarks>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool Any =>
        Receipts > 0 || Blueprints > 0 || Jobs > 0 || Checklists > 0 || Trips > 0 || RunActions > 0;
}

/// <summary>What a file turned into, before anything is stored.</summary>
public sealed record ImportReading(
    ExportFile Document,
    ImportCounts Counts,
    ImportCounts Rejected,
    ImportCounts Truncated);

/// <summary>
/// Reads a file somebody else wrote.
/// </summary>
/// <remarks>
/// <para>
/// A pure function on purpose: no store, no HTTP, no clock beyond the one passed
/// in. Everything hostile about this feature is decided here, so it is the part
/// worth being able to test exhaustively without arranging a server.
/// </para>
/// <para>
/// The frontend draws with textContent throughout, so script injection is not
/// the worry. What is: sinking the app under a file too big to hold, and
/// poisoning the reader's own view of their own data - a date in 2074 pins
/// itself to the top of a list sorted by date and no amount of scrolling
/// removes it, and one Infinity in a quantity makes a progress bar read NaN%
/// on the Now page and in the overlay.
/// </para>
/// <para>
/// Three tiers of failure, chosen by what the reader can do about it: the file
/// is refused whole, a row is dropped, or a value is cut short. Every drop and
/// every cut is counted, because "why does this say 41 when his file said 43"
/// has to be answerable on screen a month later.
/// </para>
/// </remarks>
public static class ImportReader
{
    /// <summary>
    /// The most a file may weigh.
    /// </summary>
    /// <remarks>
    /// A seven-day window is a few hundred rows; eight mebibytes is roughly
    /// forty thousand rows of everything. Checked before the parse, because a
    /// two-gigabyte paste otherwise becomes a string before anything looks at
    /// the first brace.
    /// </remarks>
    public const int MaxBytes = 8 * 1024 * 1024;

    /// <summary>The deepest legitimate path is envelope, class, array, row, attachments, attachment.</summary>
    private const int MaxDepth = 16;

    public const int MaxReceipts = 20_000;
    public const int MaxBlueprints = 2_000;
    public const int MaxAuthored = 500;
    public const int MaxItems = 200;
    public const int MaxChecklistItems = 500;
    public const int MaxStops = 200;
    public const int MaxRunActions = 200;

    /// <summary>Attachments per line, matching what the authoring path allows.</summary>
    private const int MaxAttachments = 6;

    /// <summary>The largest quantity a job line may ask for.</summary>
    private const double MaxNeeded = 1_000_000;

    /// <summary>A hold is thousands of SCU, never hundreds of thousands.</summary>
    private const int MaxScu = 100_000;

    private const decimal MaxMoney = 1_000_000_000_000m;

    /// <summary>Star Citizen has no logs older than this, and none from the future.</summary>
    private static readonly DateTimeOffset Earliest = new(2012, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static ImportProblem? TooBig(long? bytes) =>
        bytes > MaxBytes
            ? new ImportProblem(
                $"That file is {bytes / (1024 * 1024)} MB. Exports are usually well under "
                + $"{MaxBytes / (1024 * 1024)} MB — this may not be one.", 413)
            : null;

    /// <summary>Reads a document, or says why it will not.</summary>
    public static (ImportReading? Reading, ImportProblem? Problem) Read(string text, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(text))
            return (null, new ImportProblem("That file is empty."));

        // Bytes rather than characters: the cap is about what has to be held.
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxBytes)
            return (null, TooBig(System.Text.Encoding.UTF8.GetByteCount(text)));

        ExportFile? document;

        try
        {
            var options = new JsonSerializerOptions(ExportDocument.Json) { MaxDepth = MaxDepth };
            document = JsonSerializer.Deserialize<ExportFile>(text, options);
        }
        catch (JsonException)
        {
            return (null, new ImportProblem("That file is not readable as JSON."));
        }

        if (document is null)
            return (null, new ImportProblem("That file is empty."));

        if (!string.Equals(document.Format, ExportDocument.Format, StringComparison.Ordinal))
            return (null, new ImportProblem("That is not a Quantum Wake export."));

        // Refused whole rather than read partly: showing somebody half a document
        // and calling it their friend's data is the worse of the two failures.
        if (document.FormatVersion > ExportDocument.FormatVersion)
        {
            return (null, new ImportProblem(
                $"That file was written by a newer Quantum Wake (format {document.FormatVersion}; "
                + $"this build reads {ExportDocument.FormatVersion}). Update, then try again."));
        }

        if (document.FormatVersion < 1)
            return (null, new ImportProblem("That file does not say what format it is in."));

        var counts = new ImportCounts();
        var rejected = new ImportCounts();
        var truncated = new ImportCounts();

        var receipts = ReadReceipts(document, now, ref counts, ref rejected, ref truncated);
        var blueprints = ReadBlueprints(document, now, ref counts, ref rejected, ref truncated);
        var authored = ReadAuthored(document, now, ref counts, ref rejected, ref truncated);

        var classes = (document.Classes ?? [])
            .Where(c => c is ExportDocument.Receipts or ExportDocument.Blueprints or ExportDocument.Authored)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (classes.Count == 0)
            return (null, new ImportProblem("That export does not carry anything this build can read."));

        var clean = document with
        {
            Classes = classes,
            Handle = Sanitise.CleanOptional(document.Handle, Sanitise.Title),
            Note = Sanitise.CleanOptional(document.Note, Sanitise.Title),
            Receipts = receipts,
            Blueprints = blueprints,
            Authored = authored,
        };

        return (new ImportReading(clean, counts, rejected, truncated), null);
    }

    private static ExportReceipts? ReadReceipts(
        ExportFile document, DateTimeOffset now,
        ref ImportCounts counts, ref ImportCounts rejected, ref ImportCounts truncated)
    {
        if (document.Receipts is not { } block)
            return null;

        var kept = new List<ExportReceiptRow>();
        var dropped = 0;
        var seen = 0;

        foreach (var row in block.Rows ?? [])
        {
            seen++;

            if (kept.Count >= MaxReceipts)
                continue;

            if (row is null || !Dated(row.At, now)
                || row.Scu < 0 || row.Scu > MaxScu
                || !Money(row.Amount) || !Money(row.UnitPrice))
            {
                dropped++;
                continue;
            }

            kept.Add(new ExportReceiptRow(
                row.At.ToUniversalTime(),
                row.IsSell,
                Sanitise.Clean(Printable(row.Place), "Somewhere"),
                Identifier(row.PlaceId),
                row.Scu,
                row.Amount,
                row.UnitPrice,
                Sanitise.CleanOptional(Printable(row.Mode), Sanitise.Title),
                Sanitise.CleanOptional(Printable(row.Commodity), Sanitise.Title),
                Identifier(row.ResourceId, 64)));
        }

        counts = counts with { Receipts = kept.Count };
        rejected = rejected with { Receipts = dropped };
        if (seen > MaxReceipts) truncated = truncated with { Receipts = seen - kept.Count - dropped };

        var order = kept.OrderBy(r => r.At).ToList();

        return new ExportReceipts(
            Math.Clamp(block.WindowDays, 0, ExportBuilder.MaxDays),
            order.Count > 0 ? order[0].At : null,
            order.Count > 0 ? order[^1].At : null,
            Caveats(block.Caveats),
            order,
            Dated(block.WipeAt, now) ? block.WipeAt!.Value.ToUniversalTime() : null);
    }

    private static ExportBlueprints? ReadBlueprints(
        ExportFile document, DateTimeOffset now,
        ref ImportCounts counts, ref ImportCounts rejected, ref ImportCounts truncated)
    {
        if (document.Blueprints is not { } block)
            return null;

        var kept = new List<ExportBlueprintRow>();
        var dropped = 0;
        var seen = 0;

        foreach (var row in block.Rows ?? [])
        {
            seen++;
            if (kept.Count >= MaxBlueprints) continue;

            if (row is null || !Dated(row.At, now) || string.IsNullOrWhiteSpace(row.Name))
            {
                dropped++;
                continue;
            }

            kept.Add(new ExportBlueprintRow(
                row.At.ToUniversalTime(),
                Sanitise.Clean(Printable(row.Name), "A blueprint")));
        }

        counts = counts with { Blueprints = kept.Count };
        rejected = rejected with { Blueprints = dropped };
        if (seen > MaxBlueprints) truncated = truncated with { Blueprints = seen - kept.Count - dropped };

        var order = kept.OrderBy(b => b.At).ToList();

        return new ExportBlueprints(
            order.Count > 0 ? order[0].At : null,
            order.Count > 0 ? order[^1].At : null,
            Caveats(block.Caveats),
            order);
    }

    private static ExportAuthored? ReadAuthored(
        ExportFile document, DateTimeOffset now,
        ref ImportCounts counts, ref ImportCounts rejected, ref ImportCounts truncated)
    {
        if (document.Authored is not { } block)
            return null;

        var jobsDropped = 0;
        var listsDropped = 0;
        var tripsDropped = 0;

        var jobs = new List<Job>();
        foreach (var job in (block.Jobs ?? []).Take(MaxAuthored))
        {
            if (job is null || string.IsNullOrWhiteSpace(job.Title) || !Dated(job.CreatedAt, now))
            {
                jobsDropped++;
                continue;
            }

            jobs.Add(new Job(
                Identifier(job.Id) ?? string.Empty,
                Sanitise.Clean(Printable(job.Title), "A job"),
                job.Kind == "list" ? "list" : "craft",
                Sanitise.CleanOptional(Printable(job.Source), Sanitise.Title),
                job.CreatedAt.ToUniversalTime(),
                job.Done,
                [.. (job.Items ?? []).Take(MaxItems).Where(Quantified).Select(CleanItem)],
                // This machine decides what is pinned, whatever a file says.
                Pinned: false,
                Sanitise.CleanOptional(Printable(job.Destination), Sanitise.Title),
                Identifier(job.DestinationId)));
        }

        var lists = new List<Checklist>();
        foreach (var list in (block.Checklists ?? []).Take(MaxAuthored))
        {
            if (list is null || string.IsNullOrWhiteSpace(list.Title) || !Dated(list.CreatedAt, now))
            {
                listsDropped++;
                continue;
            }

            lists.Add(new Checklist(
                Identifier(list.Id) ?? string.Empty,
                Sanitise.Clean(Printable(list.Title), "A checklist"),
                list.CreatedAt.ToUniversalTime(),
                [.. (list.Items ?? []).Take(MaxChecklistItems)
                    .Where(i => i is not null && !string.IsNullOrWhiteSpace(i.Text))
                    .Select(i => CleanChecklistItem(i, now))],
                Pinned: false));
        }

        var trips = new List<Trip>();
        foreach (var trip in (block.Trips ?? []).Take(MaxAuthored))
        {
            if (trip is null || string.IsNullOrWhiteSpace(trip.Title) || !Dated(trip.CreatedAt, now))
            {
                tripsDropped++;
                continue;
            }

            trips.Add(new Trip(
                Identifier(trip.Id) ?? string.Empty,
                Sanitise.Clean(Printable(trip.Title), "A flight plan"),
                trip.CreatedAt.ToUniversalTime(),
                [.. (trip.Stops ?? []).Take(MaxStops)
                    .Where(s => s is not null)
                    .Select(s => CleanStop(s, now))],
                Tracked: false));
        }

        counts = counts with
        {
            Jobs = jobs.Count,
            Checklists = lists.Count,
            Trips = trips.Count,
            RunActions = trips.Sum(t => t.Stops.Sum(s => (s.Actions ?? []).Count)),
        };
        rejected = rejected with { Jobs = jobsDropped, Checklists = listsDropped, Trips = tripsDropped };
        truncated = truncated with
        {
            Jobs = Over(block.Jobs?.Count, MaxAuthored),
            Checklists = Over(block.Checklists?.Count, MaxAuthored),
            Trips = Over(block.Trips?.Count, MaxAuthored),

            // A run sheet trimmed in silence breaks the rule the rest of this
            // file keeps: every drop is counted, so "why does his stop show
            // twelve when he wrote fourteen" stays answerable a month later.
            RunActions = (block.Trips ?? []).Take(MaxAuthored)
                .Sum(t => (t?.Stops ?? []).Take(MaxStops)
                    .Sum(s => Over(s?.Actions?.Count, MaxRunActions))),
        };

        var stamps = jobs.Select(j => j.CreatedAt)
            .Concat(lists.Select(c => c.CreatedAt))
            .Concat(trips.Select(t => t.CreatedAt))
            .ToList();

        return new ExportAuthored(
            stamps.Count > 0 ? stamps.Min() : null,
            stamps.Count > 0 ? stamps.Max() : null,
            jobs, lists, trips);
    }

    /// <summary>
    /// A quantity that arithmetic can survive.
    /// </summary>
    /// <remarks>
    /// Needed is a double, and 1e400 in a file parses to Infinity rather than
    /// failing: it would reach a progress bar as NaN% on the Jobs page, on Now,
    /// and in the overlay. This is the one number in the format that poisons
    /// something a reader looks at rather than merely being wrong.
    /// </remarks>
    private static bool Quantified(JobItem? item) =>
        item is not null
        && !string.IsNullOrWhiteSpace(item.Name)
        && double.IsFinite(item.Needed)
        && item.Needed >= 0
        && item.Needed <= MaxNeeded;

    private static JobItem CleanItem(JobItem item) =>
        new(Sanitise.Clean(Printable(item.Name), "Something"),
            item.Needed,
            Sanitise.Clean(Printable(item.Unit), string.Empty, 16));

    private static ChecklistItem CleanChecklistItem(ChecklistItem item, DateTimeOffset now) =>
        new(Identifier(item.Id) ?? string.Empty,
            Sanitise.Clean(Printable(item.Text), "A task"),
            Dated(item.DueAt, now) ? item.DueAt!.Value.ToUniversalTime() : null,
            Sanitise.CleanOptional(Printable(item.Note, breaks: true)),
            [.. (item.Attachments ?? [])
                .Where(a => a is not null && !string.IsNullOrWhiteSpace(a.Label))
                .Where(Linkable)
                .Take(MaxAttachments)
                .Select(a => new ChecklistAttachment(
                    Sanitise.Clean(Printable(a.Kind), "note", 24),
                    Sanitise.Clean(Printable(a.Label), "Attachment"),
                    Sanitise.CleanOptional(Printable(a.Target)),
                    Identifier(a.PlaceId)))],
            item.Done,
            Dated(item.DoneAt, now) ? item.DoneAt!.Value.ToUniversalTime() : null);

    /// <summary>
    /// An attachment whose target is somewhere it is safe to point at.
    /// </summary>
    /// <remarks>
    /// The render guard already refuses anything that is not http(s), but the
    /// boundary is the place to decide it: a scheme that never enters the store
    /// cannot be reached by a later view that forgets to ask.
    /// </remarks>
    private static bool Linkable(ChecklistAttachment attachment) =>
        attachment.Kind != "url"
        || (attachment.Target is { } target
            && (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || target.StartsWith("https://", StringComparison.OrdinalIgnoreCase)));

    private static TripStop CleanStop(TripStop stop, DateTimeOffset now) =>
        new(Identifier(stop.Id) ?? string.Empty,
            Identifier(stop.PlaceId) ?? string.Empty,
            Sanitise.Clean(Printable(stop.Place), "Somewhere"),
            Sanitise.CleanOptional(Printable(stop.Note, breaks: true)),
            stop.Done,
            Dated(stop.DoneAt, now) ? stop.DoneAt!.Value.ToUniversalTime() : null,
            [.. (stop.Actions ?? []).Take(MaxRunActions)
                .Where(action => action is not null && !string.IsNullOrWhiteSpace(action.Text))
                .Select(action => CleanRunAction(action, now))]);

    // The same three rules the authoring path uses, so a kind added there
    // cannot silently arrive as "do" from somebody's file.
    private static RunAction CleanRunAction(RunAction action, DateTimeOffset now) =>
        new(Identifier(action.Id) ?? string.Empty,
            RunAction.CleanKind(action.Kind),
            Sanitise.Clean(Printable(action.Text), "Action"),
            RunAction.CleanQuantity(action.Quantity),
            RunAction.CleanUnit(action.Unit),
            action.Done,
            Dated(action.DoneAt, now) ? action.DoneAt!.Value.ToUniversalTime() : null);

    /// <summary>
    /// A date the reader's own lists can be sorted by without being hijacked.
    /// </summary>
    /// <remarks>
    /// Every list this data joins is sorted by date, so a single row stamped
    /// 2074 sits at the top of the Logbook for ever and nothing the reader does
    /// scrolls past it. That is the check that poisons somebody's view of their
    /// own history rather than merely looking wrong.
    /// </remarks>
    private static bool Dated(DateTimeOffset? at, DateTimeOffset now) =>
        at is { } value && value >= Earliest && value <= now.AddHours(24);

    private static bool Money(decimal value) => value >= 0 && value <= MaxMoney;

    /// <summary>
    /// An id kept only if it looks like one this app would have minted.
    /// </summary>
    /// <remarks>
    /// The incoming id carries no authority - imported rows are re-addressed
    /// before they reach a page - so an id holding four kilobytes of text or a
    /// path fragment is a hazard for nothing gained. Dropped rather than
    /// refused: the row is still the sender's data.
    /// </remarks>
    private static string? Identifier(string? value, int max = 40)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > max)
            return null;

        return value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? value : null;
    }

    private static string? Printable(string? value, bool breaks = false) =>
        value is null ? null : Sanitise.Printable(value, breaks);

    private static IReadOnlyList<string> Caveats(IReadOnlyList<string>? caveats) =>
        [.. (caveats ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => Sanitise.Clean(c, "note", 48))
            .Distinct(StringComparer.Ordinal)
            .Take(12)];

    private static int Over(int? count, int cap) => count > cap ? count.Value - cap : 0;
}
