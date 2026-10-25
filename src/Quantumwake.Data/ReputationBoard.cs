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

/// <summary>What one more contract of theirs pays at the rank held, by a title's own tag.</summary>
/// <param name="Source">
/// <c>ladder</c> when the figure is the held rank's on a title's rep ladder
/// (<c>[50/200/250/500/1000/2000/4000 Rep]</c>), which assumes work of the
/// pilot's own rank; <c>tag</c> when a title carried one figure for itself.
/// </param>
/// <param name="Contract">The title it was read from, without its tags, for the page to name.</param>
public sealed record RepPay(int Rep, string Source, string Contract, DateTimeOffset At);

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
/// <param name="Logo">
/// Where the page fetches the organisation's own mark from the game files, or
/// null when the install has none for it - Dead Saints, on 4.10.2.
/// </param>
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
    RepWork? Work,
    string? Logo = null,
    RepPay? Pay = null);

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
            Work(showing.Reading.Organisation!, contracts, DateTimeOffset.MinValue, DateTimeOffset.MaxValue),
            LogoOf(showing.Reading.Organisation!, catalogue),
            Pay(showing.Reading.Organisation!, contracts, described.Place));
    }

    /// <summary>
    /// What the next contract of theirs should pay, from the newest title
    /// that says, or null when none does.
    /// </summary>
    /// <remarks>
    /// A ladder is preferred to a single figure, because it is priced for the
    /// rank the pilot holds now rather than the one they held when the tagged
    /// contract was flown. Both are StarStrings' research, not the game's own
    /// word - see <see cref="ContractTags"/> - and the page lets the pilot
    /// type over either.
    /// </remarks>
    internal static RepPay? Pay(string organisation, IReadOnlyList<ContractRecord> contracts, ReputationPlace? place)
    {
        var theirs = Theirs(organisation, contracts)
            .OrderByDescending(c => c.CompletedAt ?? c.FirstSeen)
            .ToList();

        if (place is not null)
        {
            foreach (var contract in theirs)
            {
                if (ContractTags.LadderFrom(contract.Title) is not { } ladder) continue;

                // Counted from the top: the ladders run seven or eight figures
                // long and both end at Master.
                var at = ladder.Count - (place.RankCount - place.RankIndex);
                if (at < 0 || at >= ladder.Count) continue;

                return new RepPay(ladder[at], "ladder", ContractTags.Clean(contract.Title),
                    contract.CompletedAt ?? contract.FirstSeen);
            }
        }

        return theirs
            .Where(c => ContractTags.RepFrom(c.Title) is > 0)
            .Select(c => new RepPay(ContractTags.RepFrom(c.Title)!.Value, "tag", ContractTags.Clean(c.Title),
                c.CompletedAt ?? c.FirstSeen))
            .FirstOrDefault();
    }

    /// <summary>The contracts whose issuer is the front of the organisation's name.</summary>
    private static List<ContractRecord> Theirs(string organisation, IReadOnlyList<ContractRecord> contracts)
    {
        var org = Letters(organisation);

        return [.. contracts.Where(c => ContractTags.IssuerKey(c.Issuer) is { Length: >= 4 } key
            && org.StartsWith(key, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>
    /// The address of an organisation's logo, by the game's name for it -
    /// INTERSEC DEFENSE SOLUTIONS is the record's "InterSec Defense Solutions"
    /// letter for letter, as every one of the twelve on this install is.
    /// </summary>
    internal static string? LogoOf(string organisation, GameReputationCatalogue catalogue)
    {
        var letters = Letters(organisation);

        return catalogue.Organisations?
            .FirstOrDefault(o => o.Logo is not null && Letters(o.Name) == letters) is { } found
            ? $"/api/reputation/logo/{Uri.EscapeDataString(found.Id)}"
            : null;
    }

    /// <summary>The archive entry behind a logo address <see cref="LogoOf"/> gave out.</summary>
    public static string? LogoEntry(string id, GameReputationCatalogue catalogue) =>
        catalogue.Organisations?.FirstOrDefault(o => string.Equals(o.Id, id, StringComparison.Ordinal))?.Logo;

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
        var theirs = Theirs(organisation, contracts);

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
