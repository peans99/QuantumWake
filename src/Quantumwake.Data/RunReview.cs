namespace Quantumwake.Data;

/// <summary>One movement of money the run can be shown to have made.</summary>
/// <param name="Confirmed">
/// Whether the game answered, rather than only being asked. Commodity trades
/// carry no server confirmation, so a sale here is what the kiosk was told to
/// do - which is nearly always what happened, and not the same claim.
/// </param>
public sealed record RunClaim(
    DateTimeOffset At,
    string Kind,
    string What,
    string Where,
    decimal Amount,
    bool Confirmed);

/// <summary>What was planned at one stop, and what was recorded there.</summary>
/// <param name="Claimed">
/// Movements this stop can account for. Empty is a real answer and a common
/// one: most stops move no money at all.
/// </param>
public sealed record RunStopReview(
    string StopId,
    string Place,
    string? Note,
    DateTimeOffset? DoneAt,
    IReadOnlyList<RunAction> Planned,
    IReadOnlyList<RunClaim> Claimed);

/// <summary>
/// A figure, with the records behind it and the rule that made it.
/// </summary>
/// <remarks>
/// The first use of the shape "why this number?" needs everywhere. Excluded is
/// the half that is easy to leave out and answers the question people actually
/// ask, which is almost never "where did this come from" and almost always
/// "why is it lower than I expected".
/// </remarks>
public sealed record RunFigure(
    decimal Value,
    string Rule,
    IReadOnlyList<RunClaim> From,
    IReadOnlyList<string> Excluded);

/// <summary>
/// What a run planned against what the logs recorded while it ran.
/// </summary>
/// <param name="Unclaimed">
/// Money that moved during the run which no stop can account for. Shown rather
/// than hidden or forced onto the nearest stop: it is the most useful thing on
/// the page when a total looks wrong, and guessing at it is how a review starts
/// inventing history.
/// </param>
public sealed record RunReview(
    string TripId,
    string Title,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    double? ElapsedSeconds,
    IReadOnlyList<RunStopReview> Stops,
    IReadOnlyList<RunClaim> Unclaimed,
    RunFigure Earned,
    RunFigure Spent);

/// <summary>
/// Works out what a run actually did, from what the logs recorded while it ran.
/// </summary>
/// <remarks>
/// <para>
/// Built on <see cref="LogLibrary.Ledger"/> rather than on the raw stores,
/// because the ledger has already done the hard half: a receipt names a kiosk
/// and never a place, and the ledger back-tracks the position from the last
/// arrival before it. Redoing that here would be a second copy of a rule that
/// is already delicate.
/// </para>
/// <para>
/// That inheritance comes with the caveat attached. A claim's place is inferred,
/// not logged, so this can say a sale happened at a stop and be wrong about
/// which stop - and everything here is written to survive that being true.
/// Nothing is ever moved onto a stop to make a total come out.
/// </para>
/// </remarks>
public static class RunReviewer
{
    /// <summary>Stated on every figure, so the page never has to word it itself.</summary>
    public const string PlaceRule =
        "A movement is matched to a stop by time and place. The place is back-tracked "
        + "from the last arrival before it rather than logged with the receipt.";

    public static RunReview? Build(Trip trip, IReadOnlyList<LedgerEntry> ledger, DateTimeOffset now)
    {
        if (trip.StartedAt is not { } began)
            return null;

        var ended = trip.FinishedAt ?? now;

        // Only what happened while the run was going. A receipt from before the
        // start belongs to whatever came before it.
        var during = ledger
            .Where(entry => entry.At >= began && entry.At <= ended)
            .OrderBy(entry => entry.At)
            .ToList();

        var windows = Windows(trip, began, ended);
        var claims = new Dictionary<string, List<RunClaim>>(StringComparer.Ordinal);
        var unclaimed = new List<RunClaim>();

        foreach (var entry in during)
        {
            // Windows run end to end, so at most one can hold a given moment.
            // That is what lets a station visited twice in one run keep its two
            // visits apart: the time says which one, and without it the app
            // would have to guess between them.
            // Inclusive at the start: a sale can land on the same second as
            // the arrival that made it possible.
            // Place compared the way TripStore.Arrived compares it. A stop is
            // often written from a trade route and carries a UEX terminal name,
            // while a ledger row carries the resolved game place - so an exact
            // match made the reviewer stricter than the arrival that ticked the
            // stop, and a run planned from a route reviewed as nothing earned
            // with every sale unaccounted for.
            var fits = windows.FirstOrDefault(w => entry.At >= w.From && entry.At <= w.To
                && SamePlace(w.Place, entry.Where));

            var claim = new RunClaim(entry.At, entry.Kind, entry.What, entry.Where, entry.Amount, entry.Confirmed);

            if (fits.StopId is null)
            {
                unclaimed.Add(claim);
                continue;
            }

            if (!claims.TryGetValue(fits.StopId, out var list))
                claims[fits.StopId] = list = [];

            list.Add(claim);
        }

        var stops = trip.Stops
            .Select(stop => new RunStopReview(
                stop.Id, stop.Place, stop.Note, stop.DoneAt,
                stop.Actions ?? [],
                claims.GetValueOrDefault(stop.Id, [])))
            .ToList();

        var claimed = stops.SelectMany(s => s.Claimed).ToList();

        return new RunReview(
            trip.Id,
            trip.Title,
            trip.StartedAt,
            trip.FinishedAt,
            trip.Elapsed(now)?.TotalSeconds,
            stops,
            unclaimed,
            Figure(claimed.Where(c => c.Amount > 0).ToList(), unclaimed, earned: true),
            Figure(claimed.Where(c => c.Amount < 0).ToList(), unclaimed, earned: false));
    }

    /// <summary>
    /// The stretch of the run each ticked stop can account for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A stop's window runs from arriving at it to arriving at the next one.
    /// Landing is what ticks a stop, so DoneAt is when the player got there -
    /// and everything they did there happened after it. Running the window up
    /// to DoneAt instead put every sale in the leg of the journey before the
    /// stop that earned it, and left them all unclaimed.
    /// </para>
    /// <para>
    /// A stop that was never ticked has no window and claims nothing. That is
    /// not a gap to paper over: without a time the app has no idea when the
    /// player was there, and a stop that claims by place alone would take a
    /// sale from a station visited twice in one run.
    /// </para>
    /// </remarks>
    /// <summary>
    /// Whether two spellings of a place are the same place.
    /// </summary>
    /// <remarks>
    /// The same problem TripStore.Arrived solves, and solved the same way on
    /// purpose: one of them ticks the stop and the other decides what the stop
    /// earned, and a review that disagreed with the arrival is a review of a
    /// run that never happened.
    /// </remarks>
    private static bool SamePlace(string? a, string? b)
    {
        if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase)) return true;

        var left = Compact(a);
        var right = Compact(b);

        return left.Length > 0 && right.Length > 0
            && (left.Contains(right, StringComparison.Ordinal)
                || right.Contains(left, StringComparison.Ordinal));
    }

    /// <summary>Letters and digits only, lowered - punctuation and spacing differ everywhere.</summary>
    private static string Compact(string value) =>
        new([.. value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant)]);

    private static List<(string StopId, string Place, DateTimeOffset From, DateTimeOffset To)> Windows(
        Trip trip, DateTimeOffset began, DateTimeOffset ended)
    {
        var windows = new List<(string, string, DateTimeOffset, DateTimeOffset)>();

        var reached = trip.Stops
            .Where(stop => stop.DoneAt is not null)
            .OrderBy(stop => stop.DoneAt)
            .ToList();

        for (var i = 0; i < reached.Count; i++)
        {
            var from = reached[i].DoneAt!.Value;
            var to = i + 1 < reached.Count ? reached[i + 1].DoneAt!.Value : ended;

            if (from >= ended) continue;

            windows.Add((reached[i].Id, reached[i].Place, from, to > ended ? ended : to));
        }

        return windows;
    }

    private static RunFigure Figure(
        IReadOnlyList<RunClaim> from, IReadOnlyList<RunClaim> unclaimed, bool earned)
    {
        var excluded = new List<string>();

        var loose = unclaimed.Count(c => earned ? c.Amount > 0 : c.Amount < 0);

        if (loose > 0)
        {
            excluded.Add($"{loose} movement{(loose == 1 ? "" : "s")} during this run that no stop can "
                + "account for - listed below rather than added in.");
        }

        var unconfirmed = from.Count(c => !c.Confirmed);

        if (unconfirmed > 0)
        {
            excluded.Add($"{unconfirmed} of these are what a terminal was asked for rather than what it "
                + "confirmed - the game logs the request, never the answer.");
        }

        return new RunFigure(
            from.Sum(c => Math.Abs(c.Amount)),
            earned
                ? $"Money in, from movements this run's stops can account for. {PlaceRule}"
                : $"Money out, from movements this run's stops can account for. {PlaceRule}",
            from,
            excluded);
    }
}
