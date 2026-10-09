using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>One step on a progress track: the points it takes and what it hands over.</summary>
/// <param name="MinPoints">Points at which the tier is reached, as the file states them.</param>
/// <param name="Badge">The game's id for the reward - <c>R_PU_IASI_TRANSPORT_1</c>, <c>ORS_Heavy_Armor</c>.</param>
/// <param name="Reward">
/// What the game's text says the tier awards, or the badge id made readable
/// when the text names nothing - ORS's tiers have no text, and "ORS Heavy
/// Armor" is still the game's own word for it.
/// </param>
/// <param name="Items">What the reward line names, matched to the catalogue - see <see cref="RewardItems"/>.</param>
public sealed record GameScenarioTier(int MinPoints, string Badge, string Reward, IReadOnlyList<GameRewardItem>? Items = null);

/// <summary>One bar in an event's journal.</summary>
/// <param name="Id">The track's text key - <c>iasi_Journal_Transport</c> - which is stable across reads.</param>
/// <param name="Name">The journal's label for it, without the trailing colon some events write.</param>
/// <param name="Overall">
/// True for the bar every point counts towards - the file calls it global
/// progression and the journal labels it "Your total". The others count only
/// contracts that award their completion tag.
/// </param>
/// <param name="Tag">The completion tag a contract must award to count here; null on the overall bar.</param>
/// <param name="Color">The journal's colour for the bar, when it sets one.</param>
public sealed record GameScenarioTrack(
    string Id,
    string Name,
    bool Overall,
    string? Tag,
    string? Color,
    IReadOnlyList<GameScenarioTier> Tiers);

/// <summary>
/// A progress campaign - an event's journal, or a standing one like Orison
/// Relief - as the install states it.
/// </summary>
/// <param name="Id">The record's own name - <c>Iasi_ScenarioProgress</c>.</param>
/// <param name="Title">The journal's title, or the record's name made readable when no journal names it.</param>
/// <param name="Description">The journal's blurb, when it has one.</param>
/// <param name="Percent">
/// True when the journal shows each bar as a percentage of its tiers rather
/// than as points - the file's own flag, and the reason the page is worth
/// having: the game never shows the number.
/// </param>
public sealed record GameScenario(
    string Id,
    string Title,
    string Description,
    bool Percent,
    IReadOnlyList<GameScenarioTrack> Tracks);

/// <summary>One contract that pays into a campaign.</summary>
/// <param name="Id">
/// The contract's debug name - <c>Iasi_Patrol_Hard</c> - which is also what
/// <c>Game.log</c>'s objective marker writes as <c>contract [...]</c>: 14 of 14
/// sampled markers named a generator contract exactly.
/// </param>
/// <param name="Scenario">The <see cref="GameScenario.Id"/> it pays into.</param>
/// <param name="Title">The contract's title, with the game's <c>~mission(...)</c> placeholders read as words.</param>
/// <param name="Issuer">Who offers it, from the generator's contractor line.</param>
/// <param name="Points">Points on completion.</param>
/// <param name="Tags">The completion tags it awards, which decide the bars it counts on besides the overall one.</param>
public sealed record GameScenarioContract(
    string Id,
    string Scenario,
    string Title,
    string Issuer,
    int Points,
    IReadOnlyList<string> Tags);

/// <summary>Every campaign the install describes, and every contract that pays into one.</summary>
public sealed record GameScenarioCatalogue(
    IReadOnlyList<GameScenario> Scenarios,
    IReadOnlyList<GameScenarioContract> Contracts)
{
    public static GameScenarioCatalogue Empty { get; } = new([], []);
}

/// <summary>
/// Reads event progress out of the DataCore: the tiers, and what each contract pays.
/// </summary>
/// <remarks>
/// <para>
/// An event's journal is a <c>ScenarioProgress</c> record. Its
/// <c>tierProgressions</c> are the bars: each has a label, a colour, a list of
/// <c>minPoints</c> tiers with the badge each one awards, and a completion type
/// - global, so every point counts, or a completion tag, so only contracts
/// awarding that tag do. RSI Discovery Month (4.10.2) has four bars: the
/// overall one at 4,500 / 9,900 / 24,000 / 30,000 and Transport, Collection
/// and Defense at 2,000 / 4,000 / 6,500 / 10,000.
/// </para>
/// <para>
/// The other half is in the contract generators: a contract that counts
/// carries an <c>SContractPlugin_SScenarioProgress</c> naming the record, a
/// <c>ContractResult_ScenarioProgress</c> with its <c>PointsToAward</c>, and a
/// <c>ContractResult_CompletionTags</c> naming the tag that files it under a bar.
/// The tag bars count points, not tags: a tag is awarded once per contract,
/// and no bar reaching 2,000 by ones could be what Discovery Month means by a
/// month-long event.
/// </para>
/// <para>
/// Read generically, not for one event: seven campaigns in 4.10.2 share the
/// shape, and Orison Relief is one this install has history against.
/// </para>
/// </remarks>
public static partial class GameScenarios
{
    private const string ProgressPrefix = "ScenarioProgress.";

    // The arrays a generator handler keeps its contracts in, by handler kind:
    // a list or series, the career ladders' intro contracts, and the legacy
    // handlers. Service beacons and PvP bounties pay into nothing.
    private static readonly string[] ContractArrays = ["contracts", "introContracts", "legacyContracts"];

    public static GameScenarioCatalogue Read(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem>? facts = null, IReadOnlyList<GamePaint>? paints = null)
    {
        var byName = Catalogue(facts, paints);

        var progressRecords = new List<DataRecord>();
        var journals = new List<DataRecord>();
        var generators = new List<DataRecord>();

        foreach (var record in core.Records())
        {
            if (record.Name.StartsWith(ProgressPrefix, StringComparison.Ordinal)) progressRecords.Add(record);
            else if (record.Name.StartsWith("JournalEntry.", StringComparison.Ordinal)) journals.Add(record);
            else if (record.Name.StartsWith("ContractGenerator.", StringComparison.Ordinal)) generators.Add(record);
        }

        if (progressRecords.Count == 0) return GameScenarioCatalogue.Empty;

        var byHash = progressRecords.ToDictionary(r => r.Hash);
        var headings = Headings(core, text, journals, byHash);

        var scenarios = progressRecords
            .Select(r => Scenario(core, text, byName, r, headings.GetValueOrDefault(r.Hash)))
            .Where(s => s.Tracks.Count > 0)
            .ToList();

        var contracts = new List<GameScenarioContract>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var generator in generators)
        {
            var generatorAt = core.InstanceAt(generator, generator.VariantIndex);

            foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
            {
                var handlerAt = core.InstanceAt(handler);
                var issuer = Contractor(core, text, handlerAt, handler.StructIndex);

                foreach (var array in ContractArrays)
                {
                    foreach (var contract in ContractsIn(core, handlerAt, handler.StructIndex, array))
                    {
                        if (Contract(core, text, byHash, contract, issuer) is { } read && seen.Add(read.Id))
                            contracts.Add(read);
                    }
                }
            }
        }

        return new GameScenarioCatalogue(
            scenarios.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase).ToList(),
            contracts.OrderBy(c => c.Scenario).ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <summary>
    /// The journal's title and blurb for each progress record, found through the
    /// leaderboard journal entry that points at it.
    /// </summary>
    private static Dictionary<Guid, (string Title, string Description)> Headings(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IEnumerable<DataRecord> journals, IReadOnlyDictionary<Guid, DataRecord> progress)
    {
        var headings = new Dictionary<Guid, (string, string)>();

        foreach (var journal in journals)
        {
            var at = core.InstanceAt(journal, journal.VariantIndex);
            if (core.PointerAt(at, journal.StructIndex, "type") is not { StructIndex: >= 0 } type) continue;

            var typeAt = core.InstanceAt(type);
            if (core.ReferenceAt(typeAt, type.StructIndex, "scenarioProgressRecord") is not { } id
                || !progress.ContainsKey(id))
            {
                continue;
            }

            var title = Localised(text, core.StringAt(at, journal.StructIndex, "Title")) ?? "";
            var description = "";

            foreach (var faction in core.ClassArrayAt(typeAt, type.StructIndex, "factions"))
            {
                description = Localised(text, core.StringAt(core.InstanceAt(faction), faction.StructIndex, "factionDescription")) ?? "";
                if (description.Length > 0) break;
            }

            headings.TryAdd(id, (title, description));
        }

        return headings;
    }

    private static GameScenario Scenario(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, (string Class, string Kind)> byName,
        DataRecord record, (string Title, string Description) heading)
    {
        var id = record.Name[ProgressPrefix.Length..];
        var at = core.InstanceAt(record, record.VariantIndex);
        var tracks = new List<GameScenarioTrack>();
        var percent = false;

        foreach (var tiers in core.ClassArrayAt(at, record.StructIndex, "factionRewardTiers"))
        {
            var tiersAt = core.InstanceAt(tiers);
            percent |= core.BoolAt(tiersAt, tiers.StructIndex, "convertTiersPointsToPercent") == true;

            foreach (var progression in core.ClassArrayAt(tiersAt, tiers.StructIndex, "tierProgressions"))
            {
                var progressionAt = core.InstanceAt(progression);
                var key = core.StringAt(progressionAt, progression.StructIndex, "progressionText") ?? "";

                // A tag bar names its tag; the overall bar has a completion type
                // with nothing in it. Anything else is a kind this reader has not
                // met, and counting it as overall would credit it every point.
                string? tag = null;
                var overall = false;

                if (core.PointerAt(progressionAt, progression.StructIndex, "completionType") is { StructIndex: >= 0 } completion)
                {
                    var kind = core.StructName(completion.StructIndex);
                    if (kind == "CompletionType_GlobalProgression") overall = true;
                    else if (core.ReferenceAt(core.InstanceAt(completion), completion.StructIndex, "completionTags") is { } tagId
                             && tagId != Guid.Empty)
                        tag = tagId.ToString();
                }

                if (!overall && tag is null) continue;

                var badgePrefix = id.Split('_')[0];
                var steps = new List<GameScenarioTier>();

                foreach (var reward in core.ClassArrayAt(progressionAt, progression.StructIndex, "tierRewards"))
                {
                    var rewardAt = core.InstanceAt(reward);
                    var points = core.Int32At(rewardAt, reward.StructIndex, "minPoints") ?? 0;
                    var badge = core.StringAt(rewardAt, reward.StructIndex, "badgeToAward")
                        ?? core.EnumAt(rewardAt, reward.StructIndex, "badgeToAward") ?? "";

                    // Items only from the game's own words: a line made up from the
                    // badge id - "CA OP 1" - names nothing to look for.
                    var (line, written) = RewardText(text, badgePrefix, badge);
                    steps.Add(new GameScenarioTier(points, badge, line, written ? RewardItems.Match(line, byName) : []));
                }

                if (steps.Count == 0) continue;

                var name = (Localised(text, key) ?? Readable(key.TrimStart('@'))).Trim().TrimEnd(':').Trim();
                var color = core.StringAt(progressionAt, progression.StructIndex, "progressionColor");

                tracks.Add(new GameScenarioTrack(
                    key.TrimStart('@') is { Length: > 0 } k ? k : $"track{tracks.Count}",
                    name,
                    overall,
                    tag,
                    string.IsNullOrWhiteSpace(color) ? null : color.Trim(),
                    steps.OrderBy(s => s.MinPoints).ToList()));
            }
        }

        var title = heading.Title is { Length: > 0 } t
            ? t
            : Readable(id.Replace("_ScenarioProgress", "", StringComparison.OrdinalIgnoreCase));

        // The overall bar first, as the journal draws it.
        return new GameScenario(
            id, title, heading.Description, percent,
            tracks.OrderByDescending(x => x.Overall).ToList());
    }

    private static GameScenarioContract? Contract(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<Guid, DataRecord> progress, DataCore.Pointer contract, string issuer)
    {
        var at = core.InstanceAt(contract);
        var id = (core.StringAt(at, contract.StructIndex, "debugName") ?? "").Trim();
        if (id.Length == 0) return null;

        var (resultsAt, results) = core.FieldAt(at, contract.StructIndex, "contractResults");
        if (results is null) return null;

        var points = 0;
        string? scenario = null;
        var tags = new List<string>();

        foreach (var result in core.PointerArrayAt(resultsAt, results.StructIndex, "contractResults"))
        {
            var resultAt = core.InstanceAt(result);
            var kind = core.StructName(result.StructIndex);

            if (kind == "ContractResult_ScenarioProgress")
            {
                points += core.Int32At(resultAt, result.StructIndex, "PointsToAward") ?? 0;

                var (pluginAt, plugin) = core.FieldAt(resultAt, result.StructIndex, "scenarioProgressPlugin");
                if (plugin is not null
                    && core.ReferenceAt(pluginAt, plugin.StructIndex, "scenarioProgressRecord") is { } record
                    && progress.TryGetValue(record, out var found))
                {
                    scenario ??= found.Name[ProgressPrefix.Length..];
                }
            }
            else if (kind == "ContractResult_CompletionTags")
            {
                foreach (var entry in core.ClassArrayAt(resultAt, result.StructIndex, "completionTags"))
                {
                    if (core.ReferenceAt(core.InstanceAt(entry), entry.StructIndex, "tag") is { } tag && tag != Guid.Empty)
                        tags.Add(tag.ToString());
                }
            }
        }

        if (scenario is null || points <= 0) return null;

        var title = "";
        var (overridesAt, overrides) = core.FieldAt(at, contract.StructIndex, "paramOverrides");
        if (overrides is not null)
            title = StringParam(core, text, overridesAt, overrides.StructIndex, "Title") ?? "";

        return new GameScenarioContract(
            id, scenario,
            title.Length > 0 ? Placeholders(title) : Readable(id),
            issuer, points, tags.Distinct().ToList());
    }

    /// <summary>
    /// Every name a reward line could mean: the paints first, since a livery's
    /// item and its paint share a name and only the paint carries the game's
    /// picture, then the item catalogue. A name several classes share keeps the
    /// shortest class - the plain item rather than a display or a variant.
    /// </summary>
    private static Dictionary<string, (string Class, string Kind)> Catalogue(
        IReadOnlyDictionary<string, GameItem>? facts, IReadOnlyList<GamePaint>? paints)
    {
        var byName = new Dictionary<string, (string Class, string Kind)>(StringComparer.OrdinalIgnoreCase);

        foreach (var paint in paints ?? [])
            if (!paint.Stock && paint.Name.Length > 0) byName.TryAdd(paint.Name, (paint.Item, "Paint"));

        foreach (var (cls, item) in (facts ?? new Dictionary<string, GameItem>()).OrderBy(f => f.Key.Length).ThenBy(f => f.Key, StringComparer.Ordinal))
        {
            // Placeholder names are the game admitting it has no words yet.
            if (item.Name.Length == 0 || item.Name.StartsWith("PH - ", StringComparison.Ordinal) || item.Name.StartsWith("<=", StringComparison.Ordinal))
                continue;
            byName.TryAdd(item.Name, (cls, item.Type));
        }

        return byName;
    }

    /// <summary>A handler's contracts, whether the array holds them inline or by pointer.</summary>
    private static IReadOnlyList<DataCore.Pointer> ContractsIn(DataCore core, long at, int structIndex, string field)
    {
        var inline = core.ClassArrayAt(at, structIndex, field);
        return inline.Count > 0 ? inline : core.PointerArrayAt(at, structIndex, field);
    }

    private static string Contractor(DataCore core, IReadOnlyDictionary<string, string> text, long handlerAt, int handlerStruct)
    {
        var (paramsAt, parameters) = core.FieldAt(handlerAt, handlerStruct, "contractParams");
        return parameters is null ? "" : StringParam(core, text, paramsAt, parameters.StructIndex, "Contractor") ?? "";
    }

    private static string? StringParam(
        DataCore core, IReadOnlyDictionary<string, string> text, long at, int structIndex, string which)
    {
        foreach (var param in core.ClassArrayAt(at, structIndex, "stringParamOverrides"))
        {
            var paramAt = core.InstanceAt(param);
            if (core.EnumAt(paramAt, param.StructIndex, "param") == which)
                return Localised(text, core.StringAt(paramAt, param.StructIndex, "value"));
        }

        return null;
    }

    /// <summary>
    /// The tier's reward as the game words it. Only Discovery Month has the
    /// text, keyed by track and tier rather than by badge -
    /// <c>R_PU_IASI_TRANSPORT_1</c> is <c>IASI_Badge_Reward_Transport_T1_Desc</c>,
    /// and the overall bar's <c>OP</c> badges are its "Personal" tiers.
    /// </summary>
    private static (string Line, bool Written) RewardText(IReadOnlyDictionary<string, string> text, string prefix, string badge)
    {
        // Return of XenoThreat awards "None" at every tier: its prizes are
        // granted some other way, and the file says nothing about them.
        if (badge.Length == 0 || badge.Equals("None", StringComparison.OrdinalIgnoreCase)) return ("", false);

        var match = BadgeShape().Match(badge);

        if (match.Success)
        {
            var track = match.Groups["track"].Value;
            if (track.Equals("OP", StringComparison.OrdinalIgnoreCase)) track = "Personal";

            if (text.TryGetValue($"{prefix}_Badge_Reward_{track}_T{match.Groups["tier"].Value}_Desc", out var line))
                return (AwardedPrefix().Replace(line, "").Trim(), true);
        }

        return (Readable(badge.StartsWith("R_PU_", StringComparison.OrdinalIgnoreCase) ? badge[5..] : badge), false);
    }

    private static string? Localised(IReadOnlyDictionary<string, string> text, string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (!key.StartsWith('@')) return key;

        // An unfilled key reads "<= PLACEHOLDER =>" in the table, which is
        // the game admitting it has no words yet - not a title.
        var bare = key[1..];
        return (text.TryGetValue(bare, out var english) || text.TryGetValue(bare + ",P", out english))
               && !english.StartsWith("<=", StringComparison.Ordinal)
            ? english
            : null;
    }

    /// <summary>The game's <c>~mission(Ship)</c> becomes "Ship"; the title is still the game's.</summary>
    private static string Placeholders(string title) => MissionToken().Replace(title, "$1");

    private static string Readable(string id) =>
        WordBoundary().Replace(id.Replace('_', ' '), " ").Trim();

    [GeneratedRegex(@"_(?<track>[A-Za-z]+)_(?<tier>\d+)$")]
    private static partial Regex BadgeShape();

    [GeneratedRegex(@"^You've been awarded:\s*", RegexOptions.IgnoreCase)]
    private static partial Regex AwardedPrefix();

    [GeneratedRegex(@"~mission\(([^|)]+)(?:\|[^)]*)?\)")]
    private static partial Regex MissionToken();

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
