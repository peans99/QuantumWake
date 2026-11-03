using Quantumwake.Core.GameData;
using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>One photograph of one organisation's ladder.</summary>
/// <param name="Estimate">Null when the bar was not measured or no ladder fits.</param>
public sealed record RepPoint(
    DateTimeOffset At, string Shot, string? Rank, int? RankIndex, double? Progress, long? Estimate);

/// <summary>Contracts for one organisation the logs saw over some stretch.</summary>
/// <param name="Rep">What the finished ones paid, by the installed game's figures; see <see cref="Standing.Rep"/>.</param>
/// <param name="RepFrom">How many of the contracts that figure came from.</param>
/// <param name="RepTagged">How many of those were StarStrings tags standing in for a contract the install did not know.</param>
public sealed record RepWork(string Issuer, int Completed, int Abandoned, int Failed, int Rep, int RepFrom, int RepTagged = 0);

/// <summary>What one more contract of theirs pays at the rank held.</summary>
/// <param name="Source">
/// <c>game</c> when the installed game names it: the most a contract offered at
/// the held rank pays on this ladder. <c>game-open</c> when no contract is
/// offered by rank on it but some still pay onto it, whatever the rank.
/// <c>elsewhere</c>, with a zero figure, when the install knows their contracts
/// and none lands on this ladder - <paramref name="Contract"/> then names the
/// ladder they do move, and no tag is read, since it would be that ladder's
/// figure. <c>ladder</c> when it is the held rank's
/// figure on a StarStrings rep ladder (<c>[50/200/250/500/1000/2000/4000 Rep]</c>),
/// and <c>tag</c> when a title carried one figure for itself - both only when
/// the install has no word.
/// </param>
/// <param name="Contract">
/// For a tag, the title it was read from, without its tags; for the game, the
/// rank the figure is for.
/// </param>
/// <param name="At">When the tagged contract was flown; for the game, null.</param>
/// <param name="Low">For <c>game-open</c>, the least and most those contracts pay, and how many there are.</param>
public sealed record RepPay(
    int Rep, string Source, string Contract, DateTimeOffset? At, int? Low = null, int? High = null, int? Contracts = null);

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

        // One card per organisation per ladder. Headhunters show Standing and
        // Ship Combat in the Rep app; filed under the name alone, a Rank I
        // Ship Combat photograph from October and a Jr. Contractor Standing one
        // from November were one history, and the page called it a rank lost.
        var keyed = believed
            .Select(r => (Sighting: r, Org: Letters(r.Reading.Organisation!), Ladder: LadderKey(r.Reading, catalogue)))
            .ToList();

        // A reading no ladder fits - ranks not read yet, or game files not
        // read yet - stays with the ladder the organisation was last seen on,
        // which is what the page showed it beside before there were two.
        var lastLadder = keyed
            .Where(k => k.Ladder is not null)
            .GroupBy(k => k.Org)
            .ToDictionary(g => g.Key, g => g.MaxBy(k => k.Sighting.ShotAt).Ladder);

        var organisations = keyed
            .GroupBy(k => (k.Org, Ladder: k.Ladder ?? lastLadder.GetValueOrDefault(k.Org)))
            .Select(group => Organisation([.. group.Select(k => k.Sighting).OrderBy(r => r.ShotAt)], catalogue, contracts))
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
                Work(showing.Reading.Organisation!, contracts, from.Point.At, to.Point.At, catalogue, to.Place?.Scope));
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
            scope?.Name ?? KeyHeading(scope?.NameKey)
                ?? (showing.Reading.Section is { } s && !s.StartsWith('@') ? s : null),
            showing.Reading.Rank,
            showing.Reading.Progress,
            described.Place,
            ladder,
            latest.Point.At,
            latest.Point.Shot,
            [.. points.Select(p => p.Point)],
            change,
            Work(showing.Reading.Organisation!, contracts, DateTimeOffset.MinValue, DateTimeOffset.MaxValue, catalogue, described.Place?.Scope),
            LogoOf(showing.Reading.Organisation!, catalogue),
            Pay(showing.Reading.Organisation!, contracts, described.Place, catalogue));
    }

    /// <summary>
    /// What the next contract of theirs should pay at the rank held, or null
    /// when nothing says.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The installed game first. Every Red Wind haul names the lowest and highest
    /// rank it is offered to, and they step up a figure a rank: 50 from Trainee,
    /// 100 and 200 from Rookie, 250, 500, 1000, 2000 and 4000 at Master - which
    /// is StarStrings' ladder figure for figure, read off the contracts rather
    /// than one title. So the figure is the most a contract opening at the
    /// highest rank still at or below the pilot's pays on this ladder.
    /// </para>
    /// <para>
    /// Without the install's word, the newest StarStrings tag: a ladder is
    /// preferred to a single figure, because it is priced for the rank held now
    /// rather than the one held when the tagged contract was flown.
    /// </para>
    /// </remarks>
    internal static RepPay? Pay(
        string organisation, IReadOnlyList<ContractRecord> contracts, ReputationPlace? place,
        GameReputationCatalogue? catalogue = null)
    {
        if (place is not null && catalogue is not null && FromGame(organisation, place, catalogue) is { } game)
            return game;

        // The install knows their contracts and none of them lands on this
        // ladder: a tag would be the other ladder's figure, which is how a
        // Headhunters Ship Combat climb came to be priced at Standing's +100.
        if (place is not null && catalogue is not null && Elsewhere(organisation, place, catalogue) is { } other)
            return other;

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

    /// <summary>What a contract offered at the held rank pays on its ladder, by the installed game.</summary>
    private static RepPay? FromGame(string organisation, ReputationPlace place, GameReputationCatalogue catalogue)
    {
        if (catalogue.Missions is not { Count: > 0 } missions) return null;

        var letters = Letters(organisation);
        if (catalogue.Organisations?.FirstOrDefault(o => Letters(o.Name) == letters) is not { } org) return null;

        // Only contracts that say which ranks they are for, on this ladder: an
        // intro contract is open to everybody once and would price every rank
        // at its own figure.
        var offered = missions.Values
            .Where(m => m.RankScope == place.Scope
                && (m.MinRank ?? 0) <= place.RankIndex
                && (m.MaxRank ?? int.MaxValue) >= place.RankIndex)
            .Select(m => (m.MinRank, Pays: m.Rep
                .Where(r => r.Faction == org.Id && r.Scope == place.Scope
                    && r.When.HasFlag(MissionOutcomes.Completed) && r.Amount > 0)
                .Select(r => (int?)r.Amount)
                .Max()))
            .Where(m => m.Pays is not null)
            .ToList();

        if (offered.Count > 0)
        {
            var top = offered.Max(m => m.MinRank ?? 0);
            var pays = offered.Where(m => (m.MinRank ?? 0) == top).Select(m => m.Pays!.Value).ToList();

            // One figure a rank is Red Wind's way. Headhunters pay by how hard
            // the job is instead: the contracts opening at Jr. Contractor run
            // from 25 to 1,200, and taking the largest priced every one of
            // their +100 patrols as the two Hard outpost raids. A spread is said
            // as what most of them pay, with its ends.
            if (pays.Distinct().Count() == 1)
                return new RepPay(pays[0], ContractPay.Game, place.Rank, null);

            // A tie goes to the larger here: Red Wind's Rookie opens a 100 and a
            // 200 haul, and 200 is the rank's figure on StarStrings' ladder.
            return new RepPay(Common(pays, largerOnTie: true), ContractPay.Game, place.Rank, null,
                pays.Min(), pays.Max(), pays.Count);
        }

        // None offered by rank at this rank, but some still pay onto the ladder
        // whatever the rank - Headhunters' Ship Combat moves only on the three
        // Hijacked Ship contracts, +500 each. Only contracts this ladder does
        // not gate: one gated above the held rank is not on offer, and taking
        // it priced Red Wind's Not Eligible at Master's 4000.
        var open = missions.Values
            .Where(m => m.RankScope != place.Scope)
            .Select(m => m.Rep
                .Where(r => r.Faction == org.Id && r.Scope == place.Scope
                    && r.When.HasFlag(MissionOutcomes.Completed) && r.Amount > 0)
                .Select(r => (int?)r.Amount)
                .Max())
            .Where(p => p is not null)
            .ToList();

        if (open.Count == 0) return null;

        // Open contracts are not one step of a ladder but a spread - Wikelo's
        // run from 10 to 1,000 - and the largest would price every delivery as
        // the rarest one.
        return new RepPay(Common([.. open.Select(p => p!.Value)]), OpenSource, place.Rank, null, open.Min(), open.Max(), open.Count);
    }

    /// <summary>The figure most of them pay, the smaller on a tie unless told otherwise.</summary>
    private static int Common(IReadOnlyList<int> pays, bool largerOnTie = false) =>
        pays.GroupBy(p => p)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => largerOnTie ? -g.Key : g.Key)
            .First().Key;

    /// <summary>Paid by the game's contracts that move this ladder whatever the rank.</summary>
    public const string OpenSource = "game-open";

    /// <summary>The install knows their contracts, and every one pays another ladder.</summary>
    public const string ElsewhereSource = "elsewhere";

    /// <summary>
    /// A zero-figure pay naming the ladder their contracts do move, when the
    /// install knows them and none lands on this one; null when it knows none.
    /// </summary>
    private static RepPay? Elsewhere(string organisation, ReputationPlace place, GameReputationCatalogue catalogue)
    {
        if (catalogue.Missions is not { Count: > 0 } missions) return null;

        var letters = Letters(organisation);
        if (catalogue.Organisations?.FirstOrDefault(o => Letters(o.Name) == letters) is not { } org) return null;

        var theirs = missions.Values
            .SelectMany(m => m.Rep)
            .Where(r => r.Faction == org.Id && r.Amount > 0 && r.When.HasFlag(MissionOutcomes.Completed))
            .ToList();

        // Something of theirs does pay this ladder, just nothing on offer at
        // this rank: that is not "another ladder", and the tag may still say.
        if (theirs.Any(r => r.Scope == place.Scope)) return null;

        var ladder = theirs
            .GroupBy(r => r.Scope)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        if (ladder is null) return null;

        var name = catalogue.Scopes.FirstOrDefault(s => s.Id == ladder)?.Name ?? ladder;
        return new RepPay(0, ElsewhereSource, name, null);
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

    /// <summary>
    /// Which ladder a reading is on, by the rank names of the one it matches;
    /// null when none does.
    /// </summary>
    /// <remarks>
    /// By the names rather than the record, because Hired Muscle and the
    /// Headhunters' Ship Combat share every one and only the heading and perks
    /// tell them apart - and a frame whose heading did not read could be put on
    /// either. One organisation never shows both, so the same names are the
    /// same ladder.
    /// </remarks>
    private static string? LadderKey(ReputationReading reading, GameReputationCatalogue catalogue) =>
        ReputationLadders.Match(reading, catalogue) is { } scope
            ? string.Join('|', scope.Ranks.Select(r => Letters(r.Name)))
            : null;

    /// <summary>
    /// A heading from a ladder's untranslated key, for the ones the game has no
    /// English for and shows the key in place of: "@RepScope_ShipCombat_HeadHunters_Name"
    /// is "Ship Combat". With two Headhunters cards on the page, the one
    /// without a heading would otherwise be told apart from Standing by
    /// nothing at all.
    /// </summary>
    internal static string? KeyHeading(string? key)
    {
        if (key is null || !key.StartsWith("@RepScope_", StringComparison.OrdinalIgnoreCase)) return null;

        var word = key["@RepScope_".Length..].Split('_')[0];
        if (word.Length == 0) return null;

        var spaced = new System.Text.StringBuilder();
        foreach (var c in word)
        {
            if (char.IsUpper(c) && spaced.Length > 0) spaced.Append(' ');
            spaced.Append(c);
        }

        return spaced.ToString();
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
        string organisation, IReadOnlyList<ContractRecord> contracts, DateTimeOffset after, DateTimeOffset until,
        GameReputationCatalogue? catalogue = null, string? ladder = null)
    {
        catalogue ??= GameReputationCatalogue.Empty;
        var theirs = Theirs(organisation, contracts);

        if (theirs.Count == 0) return null;

        var ended = theirs
            .Where(c => (c.CompletedAt ?? c.FirstSeen) is var at && at > after && at <= until)
            .ToList();

        var rep = ended
            .Where(c => c.Outcome == ContractOutcome.Completed)
            .Select(c => (Rep: ContractPay.Rep(c, catalogue, ladder), Source: ContractPay.RepSource(c, catalogue, ladder)))
            .Where(r => r.Rep is not null)
            .ToList();

        return new RepWork(
            theirs.Select(c => c.Issuer).OrderByDescending(n => n.Length).First(),
            ended.Count(c => c.Outcome == ContractOutcome.Completed),
            ended.Count(c => c.Outcome == ContractOutcome.Abandoned),
            ended.Count(c => c.Outcome == ContractOutcome.Failed),
            rep.Sum(r => r.Rep!.Value),
            rep.Count,
            rep.Count(r => r.Source == ContractPay.Tag));
    }

    private static string Letters(string text) =>
        new([.. text.Where(char.IsLetter).Select(char.ToLowerInvariant)]);
}
