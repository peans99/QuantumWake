using Quantumwake.Core.GameData;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>One photograph of one organisation's ladder.</summary>
/// <param name="Estimate">Null when the bar was not measured or no ladder fits.</param>
public sealed record RepPoint(
    DateTimeOffset At, string Shot, string? Rank, int? RankIndex, double? Progress, long? Estimate);

/// <summary>Contracts for one organisation the logs saw over some stretch.</summary>
/// <param name="Rep">What StarStrings' title tags say those contracts paid; see <see cref="Standing.Rep"/>.</param>
/// <param name="RepFrom">How many of the contracts that figure came from.</param>
public sealed record RepWork(string Issuer, int Completed, int Abandoned, int Failed, int Rep, int RepFrom);

/// <summary>What changed between the two newest measured photographs.</summary>
/// <param name="Ranks">How many ranks up, or down when negative.</param>
/// <param name="Estimate">Reputation gained by the estimates, or null when either has none.</param>
/// <param name="Work">The contracts finished in between, when the logs name this organisation.</param>
public sealed record RepChange(
    DateTimeOffset From, DateTimeOffset To, int Ranks, double? Progress, long? Estimate, RepWork? Work);

/// <summary>One rank on the ladder as the page draws it.</summary>
public sealed record RepLadderRank(string Name, string? Perk, long MinReputation, double? Fill);

/// <summary>Everything the page knows about one organisation.</summary>
/// <param name="Name">As the Rep app prints it - "RED WIND LINEHAUL".</param>
/// <param name="Standing">The word under the name - "NEUTRAL" - which is not the rank.</param>
/// <param name="History">Every reading, oldest first.</param>
/// <param name="Work">All the contracts of theirs the logs hold, when the logs name them.</param>
public sealed record RepOrganisation(
    string Name,
    string? Standing,
    string? Section,
    string? Rank,
    double? Progress,
    ReputationPlace? Place,
    IReadOnlyList<RepLadderRank> Ladder,
    DateTimeOffset LastSeen,
    string LastShot,
    IReadOnlyList<RepPoint> History,
    RepChange? Change,
    RepWork? Work);

/// <param name="Unmeasured">Rep readings with no bars in them yet - taken before 0.18.22 and not yet read again.</param>
public sealed record ReputationBoard(IReadOnlyList<RepOrganisation> Organisations, int Readings, int Unmeasured);

/// <summary>
/// The Reputation page, built from Rep app photographs and the logs.
/// </summary>
/// <remarks>
/// <para>
/// Everything about standing comes from the photographs, because nothing else
/// has it: the logs carry no reputation at all (<c>docs/findings.md</c>). So a
/// trend has as many points as the pilot has taken screenshots of the Rep app,
/// and each organisation says when it was last seen rather than implying the
/// figure is current.
/// </para>
/// <para>
/// What the logs add is the work between two photographs: the contracts the
/// organisation issued that were finished in between. Issuers are matched to
/// the Rep app's names by their letters, the log's name being the front of the
/// app's - "Red Wind" of RED WIND LINEHAUL, "Inter Sec" of INTERSEC DEFENSE
/// SOLUTIONS.
/// </para>
/// </remarks>
public static class ReputationBoards
{
    public static ReputationBoard Build(
        IReadOnlyList<RepSighting> history,
        GameReputationCatalogue catalogue,
        IReadOnlyList<ContractRecord> contracts)
    {
        var believed = history.Where(r => !r.Dismissed && r.Reading.Organisation is { Length: > 0 }).ToList();

        var organisations = believed
            .GroupBy(r => Letters(r.Reading.Organisation!))
            .Select(group => Organisation([.. group.OrderBy(r => r.ShotAt)], catalogue, contracts))
            .OrderByDescending(o => o.LastSeen)
            .ToList();

        var unmeasured = believed.Count(r => r.Reading.Ranks?.Any(c => c.Fill is not null) != true);

        return new ReputationBoard(organisations, believed.Count, unmeasured);
    }

    private static RepOrganisation Organisation(
        List<RepSighting> readings, GameReputationCatalogue catalogue, IReadOnlyList<ContractRecord> contracts)
    {
        var points = readings.Select(r =>
        {
            var place = ReputationLadders.Place(r.Reading, catalogue);
            return (Sighting: r, Place: place, Point: new RepPoint(
                r.ShotAt, r.Shot, r.Reading.Rank,
                place?.RankIndex ?? IndexOf(r.Reading), r.Reading.Progress, place?.Estimate));
        }).ToList();

        var latest = points[^1];

        // The newest reading with a rank, which is the one worth describing;
        // the newest of all may be one the bars did not measure.
        var described = points.LastOrDefault(p => p.Point.Rank is not null);
        var showing = described.Sighting ?? latest.Sighting;

        var measured = points.Where(p => p.Point.Rank is not null).ToList();
        RepChange? change = null;

        if (measured.Count >= 2)
        {
            var (from, to) = (measured[^2], measured[^1]);

            change = new RepChange(
                from.Point.At,
                to.Point.At,
                (to.Point.RankIndex ?? 0) - (from.Point.RankIndex ?? 0),
                from.Point.RankIndex == to.Point.RankIndex ? to.Point.Progress - from.Point.Progress : null,
                from.Place is { } a && to.Place is { } b && a.Scope == b.Scope ? b.Estimate - a.Estimate : null,
                Work(showing.Reading.Organisation!, contracts, from.Point.At, to.Point.At));
        }

        var scope = ReputationLadders.Match(showing.Reading, catalogue);
        var cards = showing.Reading.Ranks ?? [];

        // The ladder from the game where one fits, with the bars laid on it;
        // otherwise the cards as read, with no thresholds to give.
        IReadOnlyList<RepLadderRank> ladder = scope is not null
            ? [.. scope.Ranks.Select((rank, i) => new RepLadderRank(
                rank.Name, rank.Perk, rank.MinReputation, i < cards.Count ? cards[i].Fill : null))]
            : [.. cards.Select(c => new RepLadderRank(c.Name, c.Perk, 0, c.Fill))];

        return new RepOrganisation(
            showing.Reading.Organisation!,
            showing.Reading.Standing,
            scope?.Name ?? (showing.Reading.Section is { } s && !s.StartsWith('@') ? s : null),
            showing.Reading.Rank,
            showing.Reading.Progress,
            described.Place,
            ladder,
            latest.Point.At,
            latest.Point.Shot,
            [.. points.Select(p => p.Point)],
            change,
            Work(showing.Reading.Organisation!, contracts, DateTimeOffset.MinValue, DateTimeOffset.MaxValue));
    }

    private static int? IndexOf(ReputationReading reading) =>
        reading.Rank is { } rank && reading.Ranks is { } cards
            ? cards.ToList().FindIndex(c => c.Name == rank) is >= 0 and var i ? i : null
            : null;

    /// <summary>
    /// The organisation's contracts that ended between two moments, or null
    /// when the logs name nobody that could be them.
    /// </summary>
    /// <remarks>
    /// Ended, not taken: reputation moves when a contract is handed in. A
    /// contract with no recorded end is dated by when it was first seen,
    /// which is the best the log offers.
    /// </remarks>
    internal static RepWork? Work(
        string organisation, IReadOnlyList<ContractRecord> contracts, DateTimeOffset after, DateTimeOffset until)
    {
        var org = Letters(organisation);

        var theirs = contracts
            .Where(c => ContractTags.IssuerKey(c.Issuer) is { Length: >= 4 } key
                && org.StartsWith(key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (theirs.Count == 0) return null;

        var ended = theirs
            .Where(c => (c.CompletedAt ?? c.FirstSeen) is var at && at > after && at <= until)
            .ToList();

        var rep = ended
            .Where(c => c.Outcome == ContractOutcome.Completed)
            .Select(c => ContractTags.RepFrom(c.Title ?? c.DisplayName))
            .Where(r => r is not null)
            .Select(r => r!.Value)
            .ToList();

        return new RepWork(
            theirs.Select(c => c.Issuer).OrderByDescending(n => n.Length).First(),
            ended.Count(c => c.Outcome == ContractOutcome.Completed),
            ended.Count(c => c.Outcome == ContractOutcome.Abandoned),
            ended.Count(c => c.Outcome == ContractOutcome.Failed),
            rep.Sum(),
            rep.Count);
    }

    private static string Letters(string text) =>
        new([.. text.Where(char.IsLetter).Select(char.ToLowerInvariant)]);
}
