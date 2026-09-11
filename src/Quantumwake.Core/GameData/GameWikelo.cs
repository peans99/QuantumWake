using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>One thing a Wikelo trade asks for or hands over.</summary>
/// <param name="Class">The entity class or resource type, for joining to the catalogue.</param>
/// <param name="Name">What the game calls it, for the page.</param>
/// <param name="Count">Pieces, or SCU when <paramref name="Unit"/> says so.</param>
public sealed record GameWikeloItem(string Class, string Name, double Count, string Unit = "");

/// <summary>A rank at Wikelo's, and the reputation it takes.</summary>
public sealed record GameWikeloStanding(string Id, string Name, long MinReputation);

/// <summary>
/// One of Wikelo's collection contracts, as the install states it.
/// </summary>
/// <param name="Id">The game's debug name - <c>TheCollector_Vehicle_Small_Fortune</c> - which is also what <c>Game.log</c> writes.</param>
/// <param name="Group">Which of the emporium's three lists it sits in: vehicles, favours, small items.</param>
/// <param name="Requirements">What to bring, in the order the file lists them.</param>
/// <param name="Rewards">What comes back; empty for the trades whose reward is a blueprint pool.</param>
/// <param name="Reputation">The rep gained on completion, or 0 when the file names none.</param>
/// <param name="MinStanding">The rank that unlocks it, or null when any customer can take it.</param>
/// <param name="Retired">
/// True when the debug name carries <c>DO_NOT_USE</c>: still in the file,
/// no longer on offer. Listed so nothing is silently missing, hidden by default.
/// </param>
public sealed record GameWikeloTrade(
    string Id,
    string Title,
    string Description,
    string Group,
    IReadOnlyList<GameWikeloItem> Requirements,
    IReadOnlyList<GameWikeloItem> Rewards,
    int Reputation,
    string? MinStanding,
    bool Retired);

/// <summary>Everything the install says about the emporium.</summary>
public sealed record GameWikeloCatalogue(
    IReadOnlyList<GameWikeloTrade> Trades,
    IReadOnlyList<GameWikeloStanding> Standings)
{
    public static GameWikeloCatalogue Empty { get; } = new([], []);
}

/// <summary>
/// Wikelo's emporium, read from the install rather than from a guide.
/// </summary>
/// <remarks>
/// <para>
/// The whole emporium is one <c>ContractGenerator.TheCollector</c>: three
/// handlers, each a list of contracts. A contract carries its title key, its
/// reward (<c>contractResults</c>), its reputation reward and any rank gate; its
/// requirements are on the <c>ContractTemplate</c> it points at - or, for the
/// contracts that share a template, in the contract's own
/// <c>HaulingOverride</c> property. Both shapes are the same list of hauling
/// orders: an entity class with a count, or a resource type with SCU.
/// </para>
/// <para>
/// Three guides were read before this was written and disagreed with each other
/// on what the Polaris costs. The file says 50 Favors. See docs/wikelo.md for
/// the survey and the record paths this walks.
/// </para>
/// </remarks>
public static partial class GameWikelo
{
    private const string Generator = "ContractGenerator.TheCollector";
    private const string StandingPrefix = "SReputationStandingParams.ReputationStanding_Wikelo_";

    public static GameWikeloCatalogue Read(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts)
    {
        var byId = new Dictionary<Guid, DataRecord>();
        DataRecord? generator = null;
        var standings = new List<GameWikeloStanding>();

        foreach (var record in core.Records())
        {
            byId.TryAdd(record.Hash, record);
            if (record.Name == Generator) generator = record;

            if (record.Name.StartsWith(StandingPrefix, StringComparison.Ordinal))
            {
                var at = core.InstanceAt(record, record.VariantIndex);
                standings.Add(new GameWikeloStanding(
                    record.Name[StandingPrefix.Length..],
                    Localised(text, core.StringAt(at, record.StructIndex, "displayName"))
                        ?? core.StringAt(at, record.StructIndex, "name") ?? record.Name,
                    core.Int64At(at, record.StructIndex, "minReputation") ?? 0));
            }
        }

        if (generator is null) return GameWikeloCatalogue.Empty;

        var trades = new List<GameWikeloTrade>();
        var generatorAt = core.InstanceAt(generator, generator.VariantIndex);

        foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
        {
            var handlerAt = core.InstanceAt(handler);
            var group = GroupName(core.StringAt(handlerAt, handler.StructIndex, "debugName"));

            foreach (var contract in core.ClassArrayAt(handlerAt, handler.StructIndex, "contracts"))
            {
                var at = core.InstanceAt(contract);
                var id = (core.StringAt(at, contract.StructIndex, "debugName") ?? "").Trim();
                if (id.Length == 0) continue;

                var (overridesAt, overrides) = core.FieldAt(at, contract.StructIndex, "paramOverrides");
                if (overrides is null) continue;

                string title = "", description = "";
                foreach (var param in core.ClassArrayAt(overridesAt, overrides.StructIndex, "stringParamOverrides"))
                {
                    var paramAt = core.InstanceAt(param);
                    var which = core.EnumAt(paramAt, param.StructIndex, "param");
                    var value = Localised(text, core.StringAt(paramAt, param.StructIndex, "value")) ?? "";
                    if (which == "Title") title = value;
                    if (which == "Description") description = value;
                }

                // Requirements: the contract's own HaulingOverride when it has one -
                // it overrides, so the template's orders are not read as well, or
                // the Polaris Bit asks for its 24 SCU twice - and otherwise the
                // template's.
                var requirements = new List<GameWikeloItem>();

                foreach (var property in core.ClassArrayAt(overridesAt, overrides.StructIndex, "propertyOverrides"))
                {
                    var propertyAt = core.InstanceAt(property);
                    if (core.StringAt(propertyAt, property.StructIndex, "missionVariableName") != "HaulingOverride") continue;
                    if (core.PointerAt(propertyAt, property.StructIndex, "value") is not { StructIndex: >= 0 } value) continue;

                    Orders(core, byId, text, facts,
                        core.PointerArrayAt(core.InstanceAt(value), value.StructIndex, "haulingOrderContent"),
                        requirements);
                }

                if (requirements.Count == 0
                    && core.ReferenceAt(at, contract.StructIndex, "template") is { } templateId
                    && byId.TryGetValue(templateId, out var template))
                {
                    var templateAt = core.InstanceAt(template, template.VariantIndex);

                    foreach (var token in core.ClassArrayAt(templateAt, template.StructIndex, "objectiveTokens"))
                    {
                        var tokenAt = core.InstanceAt(token);
                        if (core.PointerAt(tokenAt, token.StructIndex, "objectiveHandler") is not { StructIndex: >= 0 } handlerPointer)
                            continue;

                        Orders(core, byId, text, facts,
                            core.PointerArrayAt(core.InstanceAt(handlerPointer), handlerPointer.StructIndex, "haulingOrders"),
                            requirements);
                    }
                }

                // Rewards and the reputation they carry.
                var rewards = new List<GameWikeloItem>();
                var reputation = 0;
                var (resultsAt, results) = core.FieldAt(at, contract.StructIndex, "contractResults");

                if (results is not null)
                {
                    foreach (var result in core.PointerArrayAt(resultsAt, results.StructIndex, "contractResults"))
                    {
                        var resultAt = core.InstanceAt(result);

                        if (core.ReferenceAt(resultAt, result.StructIndex, "entityClass") is { } rewardId
                            && rewardId != Guid.Empty && byId.TryGetValue(rewardId, out var reward))
                        {
                            rewards.Add(new GameWikeloItem(
                                Bare(reward.Name), ItemName(text, facts, reward, core),
                                core.Int32At(resultAt, result.StructIndex, "amount") ?? 1));
                        }

                        var (repAt, rep) = core.FieldAt(resultAt, result.StructIndex, "contractResultReputationAmounts");
                        if (rep is not null
                            && core.ReferenceAt(repAt, rep.StructIndex, "reward") is { } repId
                            && byId.TryGetValue(repId, out var repRecord))
                        {
                            // SReputationRewardAmount.Wikelo_30: the amount is the suffix.
                            var suffix = repRecord.Name[(repRecord.Name.LastIndexOf('_') + 1)..];
                            if (int.TryParse(suffix, out var amount)) reputation = amount;
                        }
                    }
                }

                string? minStanding = null;
                foreach (var prerequisite in core.PointerArrayAt(at, contract.StructIndex, "additionalPrerequisites"))
                {
                    var prerequisiteAt = core.InstanceAt(prerequisite);
                    if (core.ReferenceAt(prerequisiteAt, prerequisite.StructIndex, "minStanding") is { } standingId
                        && standingId != Guid.Empty && byId.TryGetValue(standingId, out var standing)
                        && standing.Name.StartsWith(StandingPrefix, StringComparison.Ordinal))
                    {
                        minStanding = standing.Name[StandingPrefix.Length..];
                    }
                }

                // Three ATLS paints have a title key global.ini does not fill in;
                // the reward is the game's own word for what the trade is.
                if (title.Length == 0 || title.StartsWith('@'))
                    title = rewards.FirstOrDefault()?.Name ?? WordBoundary().Replace(Bare(id).Replace('_', ' '), " ");

                trades.Add(new GameWikeloTrade(
                    id,
                    title,
                    description,
                    group,
                    requirements,
                    rewards,
                    reputation,
                    minStanding,
                    id.Contains("DO_NOT_USE", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return new GameWikeloCatalogue(trades, standings.OrderBy(s => s.Id).ToList());
    }

    /// <summary>One list of hauling orders, entity or resource, appended as items.</summary>
    private static void Orders(
        DataCore core, Dictionary<Guid, DataRecord> byId, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts, IReadOnlyList<DataCore.Pointer> orders,
        List<GameWikeloItem> into)
    {
        foreach (var order in orders)
        {
            var at = core.InstanceAt(order);

            if (core.ReferenceAt(at, order.StructIndex, "entityClass") is { } entityId
                && entityId != Guid.Empty && byId.TryGetValue(entityId, out var entity))
            {
                var count = core.Int32At(at, order.StructIndex, "minAmount") ?? 0;
                if (count > 0)
                    into.Add(new GameWikeloItem(Bare(entity.Name), ItemName(text, facts, entity, core), count));
                continue;
            }

            if (core.ReferenceAt(at, order.StructIndex, "resource") is { } resourceId
                && resourceId != Guid.Empty && byId.TryGetValue(resourceId, out var resource))
            {
                // SCU is a float in the file, though every value seen is whole.
                var scu = core.SingleAt(at, order.StructIndex, "minSCU") ?? 0;
                if (scu > 0)
                    into.Add(new GameWikeloItem(Bare(resource.Name), ItemName(text, facts, resource, core), scu, "SCU"));
            }
        }
    }

    /// <summary>
    /// The English for a thing, from the catalogue when it is an item and
    /// from the record's own display name otherwise; the class name, spaced,
    /// when neither has one - the game's own word, never invented.
    /// </summary>
    private static string ItemName(
        IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, GameItem> facts,
        DataRecord record, DataCore core)
    {
        var bare = Bare(record.Name);

        // The catalogue names a vehicle by its class when the item table has
        // no better word, so a name that is only the class is not an answer.
        if (facts.TryGetValue(bare, out var item) && item.Name is { Length: > 0 }
            && !string.Equals(item.Name, bare, StringComparison.OrdinalIgnoreCase))
            return item.Name;

        if (Localised(text, core.TextProperty(record, "displayName")) is { Length: > 0 } named)
            return named;

        // A ship is not an item: its name lives under vehicle_Name<class> in
        // the localisation table, which is how the fleet page names one too.
        if (Localised(text, "vehicle_Name" + bare) is { Length: > 0 } vehicle)
            return vehicle;

        return WordBoundary().Replace(bare.Replace('_', ' '), " ").Trim();
    }

    /// <summary>The English for a key, trying the ",P" grammatical variant the table sometimes uses instead.</summary>
    private static string? Localised(IReadOnlyDictionary<string, string> text, string? key)
    {
        if (key is not { Length: > 0 }) return null;
        var bare = key.TrimStart('@');

        if (text.TryGetValue(bare, out var english) && english.Length > 0) return english;
        if (text.TryGetValue(bare + ",P", out var variant) && variant.Length > 0) return variant;
        return null;
    }

    private static string Bare(string recordName) =>
        recordName.Contains('.') ? recordName[(recordName.LastIndexOf('.') + 1)..] : recordName;

    private static string GroupName(string? debugName) => debugName switch
    {
        "TheCollector_Vehicles" => "vehicles",
        "TheCollector_Standard" => "favours",
        "TheCollector_Small_Items" => "items",
        _ => (debugName ?? "other").ToLowerInvariant(),
    };

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])")]
    private static partial Regex WordBoundary();
}
