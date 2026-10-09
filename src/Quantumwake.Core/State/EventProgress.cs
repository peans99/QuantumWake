using Quantumwake.Core.GameData;

namespace Quantumwake.Core.State;

/// <summary>One tier on a bar, and whether the logs say it has been reached.</summary>
public sealed record EventTierStatus(int MinPoints, string Badge, string Reward, bool Reached);

/// <summary>A contract worth doing next to reach a bar's next tier.</summary>
/// <param name="Needed">How many completions of it close the gap on their own.</param>
/// <param name="Done">How many this install has finished already, so a familiar one can be preferred.</param>
public sealed record EventSuggestion(string Contract, string Title, string Issuer, int Points, int Needed, int Done);

/// <summary>One bar of an event's journal, as the logs fill it.</summary>
/// <param name="Points">Points from completed contracts this install saw - a floor, see <see cref="EventProgress"/>.</param>
/// <param name="NextTier">Points at which the next tier is reached; null once every tier is.</param>
/// <param name="ToNext">Points still wanted for it.</param>
/// <param name="Fastest">The contracts that close that gap in the fewest completions.</param>
public sealed record EventTrackStatus(
    string Id,
    string Name,
    bool Overall,
    string? Color,
    int Points,
    IReadOnlyList<EventTierStatus> Tiers,
    int? NextTier,
    int? ToNext,
    IReadOnlyList<EventSuggestion> Fastest);

/// <summary>What one paying contract has earned on this install.</summary>
/// <param name="Tracks">The bars it counts on, by name, overall first.</param>
/// <param name="Open">
/// Still running when its log was last read. A contract whose log simply
/// stopped is not counted: it may have been finished in a session the backups
/// no longer hold, and calling it open would put it in front of the pilot for ever.
/// </param>
public sealed record EventContractStatus(
    string Id,
    string Title,
    string Issuer,
    int Points,
    IReadOnlyList<string> Tracks,
    int Completed,
    int Open,
    DateTimeOffset? LastCompleted);

/// <summary>One campaign as this install has played it.</summary>
/// <param name="FirstSeen">The first contract of it the logs hold; null when none.</param>
/// <param name="Completed">Its contracts this install finished.</param>
/// <param name="Unrecognised">
/// Contracts the logs show that look like this campaign's - same debug-name
/// prefix - but that the installed game does not list. A patch that renames
/// them leaves the page short without this, and says nothing.
/// </param>
public sealed record EventStatus(
    string Id,
    string Title,
    string Description,
    bool Percent,
    DateTimeOffset? FirstSeen,
    DateTimeOffset? LastSeen,
    int Completed,
    int Unrecognised,
    IReadOnlyList<EventTrackStatus> Tracks,
    IReadOnlyList<EventContractStatus> Contracts);

/// <summary>
/// Event progress from the logs: the game's points table against the contracts
/// this install finished.
/// </summary>
/// <remarks>
/// <para>
/// Every number here is a <b>floor</b>. A contract counts when <c>Game.log</c>
/// raised an objective marker for it - that is what names the contract - and
/// later ended it as complete. A contract that never puts a marker up, one
/// finished in a session whose log has rolled out of the backups, or points the
/// server granted some other way, are all invisible. The journal in game is the
/// truth; this is the part of it the logs can prove, with the number the journal
/// will not show.
/// </para>
/// <para>
/// Points do not split across a party - the file's own <c>splitPointsForParty</c>
/// is false on every Discovery Month contract - so a party member's completion
/// is theirs alone, and the logs only ever hold this pilot's.
/// </para>
/// </remarks>
public static class EventProgress
{
    private const int SuggestionCount = 3;

    public static IReadOnlyList<EventStatus> Build(GameScenarioCatalogue catalogue, IEnumerable<ContractRecord> contracts)
    {
        if (catalogue.Scenarios.Count == 0) return [];

        // The same mission can reach here twice - the stored copy of a log and
        // the live session both hold it - and the later word on it wins: a
        // contract seen open in the store and finished live is finished.
        var missions = contracts
            .GroupBy(c => c.MissionId ?? $"{c.Raw}@{c.FirstSeen:O}")
            .Select(g => g.OrderByDescending(Rank).First())
            .ToList();

        var byId = catalogue.Contracts.ToDictionary(c => c.Id, StringComparer.OrdinalIgnoreCase);

        return catalogue.Scenarios
            .Select(s => Status(s, catalogue.Contracts.Where(c => c.Scenario == s.Id).ToList(), missions, byId))
            .OrderByDescending(s => s.LastSeen ?? DateTimeOffset.MinValue)
            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Completed outranks ended badly outranks still open, then the latest word.</summary>
    private static (int, DateTimeOffset) Rank(ContractRecord c) => (c.Outcome switch
    {
        ContractOutcome.Completed => 3,
        ContractOutcome.Abandoned or ContractOutcome.Failed => 2,
        ContractOutcome.InProgress => 1,
        _ => 0,
    }, c.CompletedAt ?? c.FirstSeen);

    private static EventStatus Status(
        GameScenario scenario, IReadOnlyList<GameScenarioContract> paying,
        IReadOnlyList<ContractRecord> missions, IReadOnlyDictionary<string, GameScenarioContract> catalogue)
    {
        var mine = paying.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = missions.Where(m => mine.Contains(m.Raw)).ToList();

        // Discovery Month's are all Iasi_*, Orison Relief's ORS_*: a prefix
        // every paying contract shares is the campaign's mark on the ones a
        // patch may have renamed out from under the table.
        var prefix = SharedPrefix(paying.Select(p => p.Id));
        var unrecognised = prefix is null
            ? 0
            : missions.Count(m => m.Raw.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                                  && !catalogue.ContainsKey(m.Raw));

        var done = seen.Where(m => m.Outcome == ContractOutcome.Completed).ToList();
        var doneCount = done.GroupBy(m => m.Raw, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        var tracks = scenario.Tracks.Select(track =>
        {
            bool Counts(GameScenarioContract c) => track.Overall || (track.Tag is { } tag && c.Tags.Contains(tag));

            var points = done.Sum(m => catalogue.TryGetValue(m.Raw, out var c) && Counts(c) ? c.Points : 0);
            var next = track.Tiers.FirstOrDefault(t => t.MinPoints > points);
            var toNext = next is null ? (int?)null : next.MinPoints - points;

            var fastest = toNext is not { } gap
                ? []
                : paying.Where(Counts)
                    .Select(c => new EventSuggestion(
                        c.Id, c.Title, c.Issuer, c.Points,
                        (gap + c.Points - 1) / c.Points,
                        doneCount.GetValueOrDefault(c.Id)))
                    .OrderBy(s => s.Needed)
                    .ThenByDescending(s => s.Done)
                    .ThenByDescending(s => s.Points)
                    .Take(SuggestionCount)
                    .ToList();

            return new EventTrackStatus(
                track.Id, track.Name, track.Overall, track.Color, points,
                track.Tiers.Select(t => new EventTierStatus(t.MinPoints, t.Badge, t.Reward, points >= t.MinPoints)).ToList(),
                next?.MinPoints, toNext, fastest);
        }).ToList();

        var contracts = paying.Select(c =>
        {
            var runs = seen.Where(m => string.Equals(m.Raw, c.Id, StringComparison.OrdinalIgnoreCase)).ToList();
            var finished = runs.Where(m => m.Outcome == ContractOutcome.Completed).ToList();

            return new EventContractStatus(
                c.Id, c.Title, c.Issuer, c.Points,
                scenario.Tracks
                    .Where(t => t.Overall || (t.Tag is { } tag && c.Tags.Contains(tag)))
                    .Select(t => t.Name)
                    .ToList(),
                finished.Count,
                runs.Count(m => m.Outcome == ContractOutcome.InProgress),
                finished.Count > 0 ? finished.Max(m => m.CompletedAt ?? m.FirstSeen) : null);
        })
        .OrderByDescending(c => c.Points)
        .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
        .ToList();

        return new EventStatus(
            scenario.Id, scenario.Title, scenario.Description, scenario.Percent,
            seen.Count > 0 ? seen.Min(m => m.FirstSeen) : null,
            seen.Count > 0 ? seen.Max(m => m.CompletedAt ?? m.FirstSeen) : null,
            done.Count, unrecognised, tracks, contracts);
    }

    /// <summary>The <c>Name_</c> every id starts with, or null when they share none.</summary>
    private static string? SharedPrefix(IEnumerable<string> ids)
    {
        var heads = ids.Select(id => id.IndexOf('_') is var cut and > 0 ? id[..(cut + 1)] : null).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return heads.Count == 1 ? heads[0] : null;
    }
}

/// <summary>What one contract pays into an event journal, for a contract row.</summary>
public sealed record EventPayment(int Points, string Event, IReadOnlyList<string> Tracks);

/// <summary>
/// Contract id to what it pays, built once per request from the catalogue so a
/// logbook of hundreds of rows is a dictionary lookup each.
/// </summary>
public sealed class EventPay
{
    private readonly Dictionary<string, EventPayment> _byId;

    private EventPay(Dictionary<string, EventPayment> byId) => _byId = byId;

    public static EventPay For(GameScenarioCatalogue catalogue)
    {
        var scenarios = catalogue.Scenarios.ToDictionary(s => s.Id);
        var byId = new Dictionary<string, EventPayment>(StringComparer.OrdinalIgnoreCase);

        foreach (var contract in catalogue.Contracts)
        {
            if (!scenarios.TryGetValue(contract.Scenario, out var scenario)) continue;

            byId.TryAdd(contract.Id, new EventPayment(
                contract.Points,
                scenario.Title,
                scenario.Tracks
                    .Where(t => t.Overall || (t.Tag is { } tag && contract.Tags.Contains(tag)))
                    .Select(t => t.Name)
                    .ToList()));
        }

        return new EventPay(byId);
    }

    public EventPayment? Pay(string? contractId) =>
        contractId is { Length: > 0 } && _byId.TryGetValue(contractId, out var pay) ? pay : null;
}
