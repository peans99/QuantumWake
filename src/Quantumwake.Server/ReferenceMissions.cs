using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// The Reference Missions page and the mission window: every contract the
/// installed game defines, what it is and what it pays, joined to every time
/// the logs show you taking it.
/// </summary>
/// <remarks>
/// The join is the log's <c>contractDefinitionId</c>, which finds all 751
/// contracts this install has logged; a contract that came without one is
/// matched by its debug name. Money is the one thing the files cannot answer:
/// a generator contract's payout is computed by the server, so the window says
/// so, and shows the fixed sum only for the older broker missions that have one
/// and the awards the logs printed.
/// </remarks>
public static partial class ServerHost
{
    static void MapMissions(WebApplication app)
    {
        app.MapGet("/api/reference/missions", (LogLibrary lib) => Results.Ok(ReferenceMissions(lib)));
        app.MapGet("/api/reference/mission", (string? id, LogLibrary lib) =>
            MissionDetail(id?.Trim(), lib) is { } mission ? Results.Ok(mission) : Results.NotFound());
    }

    /// <summary>Everything a page needs to name a contract's organisation, ladder and ranks.</summary>
    sealed record MissionNames(
        IReadOnlyDictionary<string, string> Organisations,
        IReadOnlyDictionary<string, GameRepScope> Ladders)
    {
        public string Organisation(string? id) =>
            id is { Length: > 0 } && Organisations.TryGetValue(id, out var name) ? name : Readable(id);

        public string Ladder(string scope) =>
            Ladders.TryGetValue(scope, out var s) && s.Name is { Length: > 0 } n ? n : Readable(scope);

        public string? Rank(string scope, int? index) =>
            index is { } i && Ladders.TryGetValue(scope, out var s) && i >= 0 && i < s.Ranks.Count ? s.Ranks[i].Name : null;

        static string Readable(string? id) =>
            (id ?? "").Replace("FactionReputation_", "").Replace("ReputationScope_", "").Replace("Lawful_", "").Replace("Unlawful_", "").Replace('_', ' ');
    }

    static MissionNames Names(GameReputationCatalogue rep) => new(
        (rep.Organisations ?? []).GroupBy(o => o.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Name, StringComparer.OrdinalIgnoreCase),
        rep.Scopes.GroupBy(s => s.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Every logged contract with the session it was in, by the definition it
    /// joins to: the id when the marker gave one, else the debug name.
    /// </summary>
    static ILookup<string, (ContractRecord Contract, SessionSummary Session)> Flown(LogLibrary lib, GameMissionCatalogue details)
    {
        var byName = details.Missions.GroupBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.OrdinalIgnoreCase);
        return lib.Sessions()
            .SelectMany(s => s.Contracts.Select(c => (Contract: c, Session: s)))
            .Select(x => (Key: x.Contract.DefinitionId ?? (byName.TryGetValue(x.Contract.Raw, out var id) ? id : null), x))
            .Where(x => x.Key is not null)
            .ToLookup(x => x.Key!, x => x.x, StringComparer.OrdinalIgnoreCase);
    }

    static object ReferenceMissions(LogLibrary lib)
    {
        var rep = lib.GameCommodities.Reputation;
        var details = rep.Details ?? GameMissionCatalogue.Empty;
        var names = Names(rep);
        var flown = Flown(lib, details);

        // Released contracts, and an unreleased one only when the logs show it
        // flown: a contract the game never offers is not a reference entry.
        var rows = details.Missions
            .Where(m => !m.Unreleased || flown.Contains(m.Id))
            .Select(m =>
            {
                var pay = rep.Mission(m.Id);
                var runs = flown[m.Id].ToList();
                var gate = m.Gates.FirstOrDefault(g => g.Faction == m.Faction) ?? m.Gates.FirstOrDefault();
                return new
                {
                    m.Id,
                    m.Name,
                    title = details.Text(m.TitleKey),
                    loggedTitle = LoggedTitle(runs),
                    contractor = details.Text(m.ContractorKey) ?? names.Organisation(m.Faction),
                    type = details.Text(m.TypeKey),
                    areas = m.Systems.Select(details.Area).Distinct().ToList(),
                    systems = m.Systems.SelectMany(s => details.Areas.TryGetValue(s, out var a) ? a.Systems : []).Distinct().ToList(),
                    rep = pay?.OnCompletion,
                    blueprint = pay?.Blueprint,
                    reward = pay?.Reward,
                    rank = gate is null ? null : new
                    {
                        ladder = names.Ladder(gate.Scope),
                        min = names.Rank(gate.Scope, gate.MinRank),
                        max = names.Rank(gate.Scope, gate.MaxRank),
                    },
                    m.Illegal,
                    m.Unreleased,
                    taken = runs.Count,
                    completed = runs.Count(r => r.Contract.Outcome == ContractOutcome.Completed),
                };
            })
            .ToList();

        return new { ready = details.Missions.Count > 0, count = rows.Count, missions = rows };
    }

    /// <summary>The newest title the logs show for a contract whose files name none, cleaned of mod tags.</summary>
    static string? LoggedTitle(IEnumerable<(ContractRecord Contract, SessionSummary Session)> runs) =>
        runs.Where(r => !string.IsNullOrWhiteSpace(r.Contract.Title))
            .OrderByDescending(r => r.Contract.FirstSeen)
            .Select(r => ContractTags.Clean(r.Contract.Title))
            .FirstOrDefault();

    static object? MissionDetail(string? id, LogLibrary lib)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        var rep = lib.GameCommodities.Reputation;
        var details = rep.Details ?? GameMissionCatalogue.Empty;

        // An id from the log's marker, else the debug name a page knows it by.
        var m = details.Missions.FirstOrDefault(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? details.Missions.FirstOrDefault(x => string.Equals(x.Name, id, StringComparison.OrdinalIgnoreCase));
        if (m is null) return null;

        var names = Names(rep);
        var pay = rep.Mission(m.Id);
        var runs = Flown(lib, details)[m.Id].OrderByDescending(r => r.Contract.FirstSeen).ToList();

        // A payout belongs to a run when the award toast came within seconds of
        // that run's completion in the same session, under the title it showed.
        // Every one of the 14 awards here lands within 0.4 s of its completion.
        decimal? Paid(ContractRecord c, SessionSummary s) =>
            c.CompletedAt is { } done
                ? s.Payouts.Where(p => Math.Abs((p.At - done).TotalSeconds) <= 5
                        && (c.Title is null || p.Contract is null
                            || string.Equals(ContractTags.Clean(p.Contract), ContractTags.Clean(c.Title), StringComparison.OrdinalIgnoreCase)))
                    .Select(p => (decimal?)p.Amount).FirstOrDefault()
                : null;

        var runViews = runs.Select(r => new
        {
            at = r.Contract.FirstSeen,
            title = ContractTags.Clean(r.Contract.Name),
            outcome = r.Contract.Outcome.ToString(),
            minutes = r.Contract.TimeToComplete?.TotalMinutes,
            session = r.Session.Id,
            paid = Paid(r.Contract, r.Session),
        }).ToList();

        var done = runViews.Where(r => r.outcome == nameof(ContractOutcome.Completed) && r.minutes is > 0).Select(r => r.minutes!.Value).Order().ToList();

        // The blueprints its reward pools can give, by the pools that name it as a source.
        var pools = lib.GameCommodities.Scenarios.BlueprintSources
            .Where(p => p.Value.Any(s => s.Kind == "contract" && string.Equals(s.Id, m.Name, StringComparison.OrdinalIgnoreCase)))
            .Select(p => p.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var blueprints = pools.Count == 0 ? [] : lib.GameCommodities.Blueprints
            .Where(b => b.Kind == "creation" && b.RewardPools.Any(pools.Contains))
            .GroupBy(b => b.OutputClass, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { className = g.Key, name = g.First().Output })
            .OrderBy(b => b.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // The same handler's other contracts - the difficulty steps and system
        // copies of one job - so a window can walk from Easy to Hard.
        var related = details.Missions
            .Where(x => x.Id != m.Id && x.Generator == m.Generator && x.Template == m.Template && !x.Unreleased)
            .Take(24)
            .Select(x => new { x.Id, x.Name, title = details.Text(x.TitleKey), areas = x.Systems.Select(details.Area).Distinct().ToList() })
            .ToList();

        return new
        {
            m.Id,
            m.Name,
            m.Generator,
            m.Parent,
            m.Template,
            title = details.Text(m.TitleKey),
            loggedTitle = LoggedTitle(runs),
            description = details.Text(m.DescriptionKey),
            contractor = details.Text(m.ContractorKey) ?? names.Organisation(m.Faction),
            organisation = m.Faction is null ? null : names.Organisation(m.Faction),
            type = details.Text(m.TypeKey),
            areas = m.Systems.Select(details.Area).Distinct().ToList(),
            m.Locations,
            crime = m.MinCrimeStat is null && m.MaxCrimeStat is null ? null : new { min = m.MinCrimeStat, max = m.MaxCrimeStat },
            gates = m.Gates.Select(g => new
            {
                organisation = names.Organisation(g.Faction),
                ladder = names.Ladder(g.Scope),
                min = names.Rank(g.Scope, g.MinRank),
                max = names.Rank(g.Scope, g.MaxRank),
            }),
            m.RequiresCompleted,
            m.Minutes,
            m.BuyIn,
            m.MaxPlayers,
            m.OnceOnly,
            m.RetakeAfterAbandon,
            m.RetakeAfterFail,
            m.Illegal,
            m.Unreleased,
            difficulty = m.Difficulty,
            pay = new
            {
                known = pay is not null,
                rep = (pay?.Rep ?? []).Select(r => new
                {
                    organisation = names.Organisation(r.Faction),
                    ladder = names.Ladder(r.Scope),
                    r.Amount,
                    when = Enum.GetValues<MissionOutcomes>().Where(o => o != MissionOutcomes.None && r.When.HasFlag(o)).Select(o => o.ToString()),
                }),
                blueprint = pay?.Blueprint,
                reward = pay?.Reward,
            },
            blueprints,
            runs = runViews,
            summary = new
            {
                taken = runViews.Count,
                completed = runViews.Count(r => r.outcome == nameof(ContractOutcome.Completed)),
                failed = runViews.Count(r => r.outcome == nameof(ContractOutcome.Failed)),
                abandoned = runViews.Count(r => r.outcome == nameof(ContractOutcome.Abandoned)),
                medianMinutes = done.Count == 0 ? (double?)null : done[done.Count / 2],
                paid = runViews.Sum(r => r.paid ?? 0),
                payouts = runViews.Count(r => r.paid is not null),
            },
            related,
        };
    }
}
