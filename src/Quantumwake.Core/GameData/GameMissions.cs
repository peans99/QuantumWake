namespace Quantumwake.Core.GameData;

/// <summary>The ways a contract can end, as the game's <c>EMissionResult</c> lists them.</summary>
/// <remarks>
/// A contract result carries one flag per value, in this order, saying which
/// endings it applies to. Read off the DataCore's own enum on 4.10.2:
/// <c>Completed, InProgress, Failed, Abandoned, Withdrawn</c>.
/// </remarks>
[Flags]
public enum MissionOutcomes
{
    None = 0,
    Completed = 1,
    InProgress = 2,
    Failed = 4,
    Abandoned = 8,
    Withdrawn = 16,
}

/// <summary>One reputation change a contract makes.</summary>
/// <param name="Faction">The organisation's record, <c>FactionReputation_Lawful_RedWindLinehaul</c> - <see cref="GameRepOrganisation.Id"/>.</param>
/// <param name="Scope">The ladder it moves, <c>ReputationScope_Hauling</c> - <see cref="GameRepScope.Id"/>.</param>
/// <param name="Amount">Reputation, negative for a loss.</param>
/// <param name="When">Which endings apply it.</param>
public sealed record GameRepLine(string Faction, string Scope, int Amount, MissionOutcomes When);

/// <summary>What the installed game says one contract pays, besides money.</summary>
/// <param name="Name">The contract's debug name, as the log's <c>contract [...]</c> writes it.</param>
/// <param name="Rep">Every reputation change, in the order the contract lists them.</param>
/// <param name="Blueprint">The chance of a blueprint, 0 to 1, or null when it awards none.</param>
/// <param name="RankScope">The ladder <paramref name="MinRank"/> and <paramref name="MaxRank"/> count on.</param>
/// <param name="MinRank">The lowest rank offered it, as an index into that ladder's ranks; null when open to all.</param>
/// <param name="MaxRank">The highest, likewise.</param>
/// <param name="Reward">
/// A fixed payout in aUEC. Only the older broker missions have one - the Covalex
/// hauls - and 12 of the 14 payouts this install logged equal it to the credit.
/// Generator contracts compute theirs on the server and the files hold no figure.
/// </param>
public sealed record GameMissionPay(
    string Name,
    IReadOnlyList<GameRepLine> Rep,
    double? Blueprint = null,
    string? RankScope = null,
    int? MinRank = null,
    int? MaxRank = null,
    int? Reward = null)
{
    /// <summary>
    /// What finishing it does for the organisation that offers it: the largest
    /// gain applied on completion, or null when it moves no ladder at all.
    /// </summary>
    /// <remarks>
    /// The largest rather than the sum, because a Red Wind haul pays 500 on
    /// Hauling and 50 on Affinity - two bars in the Rep app, and the 500 is the
    /// one a pilot means by "what it pays". The second line is still in
    /// <see cref="Rep"/> for anything that wants it.
    /// </remarks>
    /// <summary>
    /// What finishing it pays on one ladder, or null when it pays nothing there.
    /// </summary>
    /// <remarks>
    /// The Rep app gives some organisations two progressions. Headhunters have
    /// Standing and Ship Combat, and every contract of theirs this install logged
    /// pays +100 on Standing; only the three Hijacked Ship contracts pay Ship
    /// Combat (+500). Reading a Ship Combat screenshot against those +100s is
    /// counting one ladder's rep against another's bar.
    /// </remarks>
    public int? On(string scope) =>
        Rep.Where(r => r.Scope == scope && r.When.HasFlag(MissionOutcomes.Completed) && r.Amount > 0)
            .Select(r => (int?)r.Amount)
            .Max();

    [System.Text.Json.Serialization.JsonIgnore]
    public int? OnCompletion =>
        Rep.Count == 0
            ? null
            : Rep.Where(r => r.When.HasFlag(MissionOutcomes.Completed) && r.Amount > 0)
                .Select(r => (int?)r.Amount)
                .Max() ?? 0;
}

/// <summary>
/// Reads what every contract pays in reputation, keyed by the id the log writes.
/// </summary>
/// <remarks>
/// <para>
/// <c>Game.log</c>'s objective marker writes a <c>contractDefinitionId</c>. It
/// is not a record: it is the inline <c>id</c> of one contract inside a
/// <c>ContractGenerator</c> record - the generator's handlers hold
/// <c>contracts</c>, <c>introContracts</c> and <c>legacyContracts</c>, and a
/// contract can hold <c>subContracts</c> with ids of their own. Measured on
/// 4.10.2 against this install's logs: 5,174 contracts and sub-contracts, and
/// all 734 logged missions found by id, the debug name agreeing every time.
/// </para>
/// <para>
/// Reputation is a <c>ContractResult_LegacyReputation</c> among the contract's
/// results: an organisation, a ladder and an <c>SReputationRewardAmount</c>
/// record. The amount is read off the record, never its name - one named
/// <c>_1600</c> holds 1500. Against the StarStrings tags in this install's
/// titles, which the files never saw, 46 single-figure tags out of 46 agreed.
/// </para>
/// <para>
/// A legacy contract has no results of its own: it names an older
/// <c>MissionBrokerEntry</c> record, and that record's
/// <c>missionResultReputationRewards</c> - one list per ending - and fixed
/// <c>missionReward</c> are what it pays.
/// </para>
/// </remarks>
public static class GameMissions
{
    private static readonly string[] ContractArrays = ["contracts", "introContracts", "legacyContracts"];

    /// <param name="byId">Every record, by its id.</param>
    /// <param name="standings">Where each rank record sits: its ladder and its index on it.</param>
    public static Dictionary<string, GameMissionPay> Read(
        DataCore core,
        IReadOnlyDictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<Guid, (string Scope, int Index)> standings)
    {
        var read = new Dictionary<string, GameMissionPay>(StringComparer.OrdinalIgnoreCase);
        var brokerStruct = core.StructIndexOf("MissionBrokerEntry");
        var brokers = new Dictionary<string, DataRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in byId.Values)
        {
            if (record.StructIndex == brokerStruct) brokers.TryAdd(Bare(record.Name), record);
        }

        foreach (var generator in byId.Values.Where(r => r.Name.StartsWith("ContractGenerator.", StringComparison.Ordinal)))
        {
            var generatorAt = core.InstanceAt(generator, generator.VariantIndex);

            foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
            {
                var handlerAt = core.InstanceAt(handler);

                foreach (var array in ContractArrays)
                {
                    foreach (var contract in ContractsIn(core, handlerAt, handler.StructIndex, array))
                        Contract(core, byId, standings, brokers, contract, null, read);
                }
            }
        }

        return read;
    }

    private static void Contract(
        DataCore core,
        IReadOnlyDictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<Guid, (string Scope, int Index)> standings,
        IReadOnlyDictionary<string, DataRecord> brokers,
        DataCore.Pointer contract,
        (string Name, GameMissionPay Pay)? parent,
        Dictionary<string, GameMissionPay> read)
    {
        var at = core.InstanceAt(contract);
        var (idAt, _) = core.FieldAt(at, contract.StructIndex, "id");
        if (idAt < 0) return;

        var name = (core.StringAt(at, contract.StructIndex, "debugName") ?? "").Trim();
        if (name.Length == 0 && parent is null) return;

        var rep = new List<GameRepLine>();
        double? blueprint = null;
        var hasResults = false;
        var (resultsAt, results) = core.FieldAt(at, contract.StructIndex, "contractResults");

        if (results is not null)
        {
            foreach (var result in core.PointerArrayAt(resultsAt, results.StructIndex, "contractResults"))
            {
                hasResults = true;
                var resultAt = core.InstanceAt(result);
                var kind = core.StructName(result.StructIndex);

                if (kind == "ContractResult_LegacyReputation")
                {
                    var (amountsAt, amounts) = core.FieldAt(resultAt, result.StructIndex, "contractResultReputationAmounts");
                    if (amounts is null) continue;

                    if (Line(core, byId, amountsAt, amounts.StructIndex,
                            When(core.BoolArrayAt(resultAt, result.StructIndex, "missionResults"))) is { } line)
                        rep.Add(line);
                }
                else if (kind == "BlueprintRewards")
                {
                    var chance = core.SingleAt(resultAt, result.StructIndex, "chance") ?? 1;
                    // Two pools on one contract are two chances, not a certainty.
                    blueprint = blueprint is { } before ? 1 - (1 - before) * (1 - chance) : chance;
                }
            }
        }

        int? reward = null;

        // A sub-contract is its parent with some parameters overridden; when it
        // lists no results it pays what the parent pays.
        if (!hasResults && parent is { } p)
        {
            rep = [.. p.Pay.Rep];
            blueprint = p.Pay.Blueprint;
            reward = p.Pay.Reward;
        }
        else if (!hasResults && brokers.TryGetValue(name, out var broker))
        {
            (rep, reward) = Broker(core, byId, broker);
        }

        var (scope, min) = Standing(core, standings, at, contract.StructIndex, "minStanding");
        var (maxScope, max) = Standing(core, standings, at, contract.StructIndex, "maxStanding");
        scope ??= maxScope;

        if (scope is null && parent is { } inherited)
        {
            scope = inherited.Pay.RankScope;
            min = inherited.Pay.MinRank;
            max = inherited.Pay.MaxRank;
        }

        var ownName = parent is { } of && name.Length == 0 ? of.Name : name;
        var pay = new GameMissionPay(ownName, rep, blueprint, scope, min, max, reward);
        read.TryAdd(ReadId(core, idAt), pay);

        // The log names a sub-contract after its parent and its index:
        // the third of Iasi_DefendShip_Easy's is Iasi_DefendShip_Easy_2.
        var subs = core.ClassArrayAt(at, contract.StructIndex, "subContracts");
        for (var i = 0; i < subs.Count; i++)
            Contract(core, byId, standings, brokers, subs[i], ($"{ownName}_{i}", pay), read);
    }

    private static (List<GameRepLine> Rep, int? Reward) Broker(
        DataCore core, IReadOnlyDictionary<Guid, DataRecord> byId, DataRecord broker)
    {
        var at = core.InstanceAt(broker, broker.VariantIndex);
        var (rewardAt, rewardField) = core.FieldAt(at, broker.StructIndex, "missionReward");
        var reward = rewardField is null ? null : core.Int32At(rewardAt, rewardField.StructIndex, "reward");

        var rep = new List<GameRepLine>();
        var lists = core.ClassArrayAt(at, broker.StructIndex, "missionResultReputationRewards");

        // One list per ending, in EMissionResult order.
        for (var i = 0; i < lists.Count && i < 5; i++)
        {
            var listAt = core.InstanceAt(lists[i]);
            foreach (var amount in core.ClassArrayAt(listAt, lists[i].StructIndex, "reputationAmounts"))
            {
                if (Line(core, byId, core.InstanceAt(amount), amount.StructIndex, (MissionOutcomes)(1 << i)) is { } line)
                    rep.Add(line);
            }
        }

        return (rep, reward is > 0 ? reward : null);
    }

    private static GameRepLine? Line(
        DataCore core, IReadOnlyDictionary<Guid, DataRecord> byId, long at, int structIndex, MissionOutcomes when)
    {
        if (core.ReferenceAt(at, structIndex, "reward") is not { } amountId
            || !byId.TryGetValue(amountId, out var amountRecord))
            return null;

        var amount = core.Int32At(core.InstanceAt(amountRecord, amountRecord.VariantIndex), amountRecord.StructIndex, "reputationAmount");
        if (amount is null or 0) return null;

        var faction = core.ReferenceAt(at, structIndex, "factionReputation") is { } f && byId.TryGetValue(f, out var fr) ? Bare(fr.Name) : "";
        var scope = core.ReferenceAt(at, structIndex, "reputationScope") is { } s && byId.TryGetValue(s, out var sr) ? Bare(sr.Name) : "";

        return new GameRepLine(faction, scope, amount.Value, when);
    }

    private static MissionOutcomes When(IReadOnlyList<bool> flags)
    {
        var when = MissionOutcomes.None;
        for (var i = 0; i < flags.Count && i < 5; i++)
            if (flags[i]) when |= (MissionOutcomes)(1 << i);
        return when;
    }

    private static (string? Scope, int? Index) Standing(
        DataCore core, IReadOnlyDictionary<Guid, (string Scope, int Index)> standings, long at, int structIndex, string field) =>
        core.ReferenceAt(at, structIndex, field) is { } id && standings.TryGetValue(id, out var place)
            ? (place.Scope, place.Index)
            : (null, null);

    private static IReadOnlyList<DataCore.Pointer> ContractsIn(DataCore core, long at, int structIndex, string field)
    {
        var inline = core.ClassArrayAt(at, structIndex, field);
        return inline.Count > 0 ? inline : core.PointerArrayAt(at, structIndex, field);
    }

    /// <summary>
    /// The inline guid, in the form the log prints it.
    /// </summary>
    /// <remarks>
    /// Stored the way a record's own id is - two little-endian halves - so it
    /// takes the same unscrambling as <see cref="DataRecord.Hash"/>; read
    /// straight it matched none of the 734 logged ids, read this way all of them.
    /// </remarks>
    private static string ReadId(DataCore core, long at) => core.GuidAt(at).ToString();

    private static string Bare(string name) => name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
}
