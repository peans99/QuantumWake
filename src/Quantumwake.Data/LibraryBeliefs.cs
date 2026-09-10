using Quantumwake.Core.Locations;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>
/// What the logs had led the app to believe at a moment, read out of the library.
/// </summary>
/// <remarks>
/// Every answer is back-tracked to the screenshot's own timestamp rather than
/// taken from the live state, because a screenshot read a week late still
/// describes the week before. The session covering the moment is found by
/// its span; a moment no session covers gets null, and the check says so.
/// </remarks>
public sealed class LibraryBeliefs(LogLibrary library) : IScreenBeliefs
{
    public (string Id, string Name, string? System)? WhereAt(DateTimeOffset at)
    {
        if (Session(at) is not { } session) return null;

        DateTimeOffset? bestAt = null;
        (string Id, string Name, string? System)? best = null;

        for (var i = session.Locations.Count - 1; i >= 0; i--)
        {
            var visit = session.Locations[i];
            if (visit.At > at) continue;

            bestAt = visit.At;
            best = (visit.RawId, visit.DisplayName, visit.System);
            break;
        }

        // A jump after the last arrival is the more recent word on where the
        // pilot is, the same way the ledger places a sale.
        for (var i = session.Jumps.Count - 1; i >= 0; i--)
        {
            var jump = session.Jumps[i];
            if (jump.At > at) continue;

            if (bestAt is null || jump.At > bestAt)
                best = (jump.ToId, jump.ToName, LocationResolver.Resolve(jump.ToId).System);

            break;
        }

        return best;
    }

    public (string Id, string Name, string? System)? PlaceNamed(string read) =>
        library.Terminals.Resolve(read) is { } place
            ? (place.RawId, place.Name, place.System)
            : null;

    public IReadOnlyList<string>? OpenContractsAt(DateTimeOffset at)
    {
        if (Session(at) is not { } session) return null;

        // Not ContractRecord.Accepted, which nothing sets: this returned an
        // empty list for every screenshot ever checked, so the Contracts app
        // read "differs - the tab says 5, the logs say 0" against a log
        // carrying every one of those five. A contract is here because an
        // objective marker fired for it, and the game raises those for missions
        // in the journal - which is what having taken one means.
        return [.. session.Contracts
            .Where(c => c.FirstSeen <= at)
            .Where(c => c.CompletedAt is null || c.CompletedAt > at)
            .Where(c => c.Outcome is ContractOutcome.Unknown or ContractOutcome.InProgress
                || (c.CompletedAt is not null && c.CompletedAt > at))
            .Select(c => ContractTags.Clean(c.DisplayName))];
    }

    public decimal? LedgerRunningAt(DateTimeOffset at)
    {
        var ledger = library.Ledger();
        if (ledger.Count == 0) return null;

        // Newest first, so the first entry at or before the moment carries the
        // running total up to it. Nothing before it means nothing had moved.
        return ledger.FirstOrDefault(e => e.At <= at)?.Running ?? 0m;
    }

    /// <summary>
    /// The factory parts, by the ship's display name.
    /// </summary>
    /// <remarks>
    /// The slot digest is keyed by class - <c>DRAK_Corsair</c> - and the screen
    /// prints the name. The dataset's ship table joins the two, and where a
    /// name has several classes (the base ship and its editions) the shortest
    /// class is the base ship, which is the one whose factory fit is meant.
    /// </remarks>
    public IReadOnlyList<string> StockParts(string ship)
    {
        var classes = library.Community.Ships
            .Where(pair => string.Equals(pair.Value.Name, ship, StringComparison.OrdinalIgnoreCase))
            .Select(pair => pair.Key)
            .OrderBy(key => key.Length)
            .ToList();

        // The name itself last, for a digest keyed the other way.
        classes.Add(ship);

        foreach (var key in classes)
        {
            var parts = library.Community.Slots(key)
                .Select(slot => slot.Fitted)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => name!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (parts.Count > 0) return parts;
        }

        return [];
    }

    public IReadOnlyList<string> FlownShips() =>
        [.. library.Stats().Ships
            .Select(ship => ship.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private SessionSummary? Session(DateTimeOffset at) =>
        library.Sessions().FirstOrDefault(s => s.StartedAt <= at && at <= s.EndedAt.AddMinutes(5));
}
