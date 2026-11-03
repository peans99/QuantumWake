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
public static partial class GameMissions
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

    /// <summary>A title names both ends: <c>A &gt; B</c>.</summary>
    public const string BothEnds = "AToB";

    /// <summary>A title names where it is delivered: <c>to X</c>.</summary>
    public const string DeliveryEnd = "to";

    /// <summary>A title names where it is picked up: <c>from X</c>.</summary>
    public const string PickupEnd = "from";

    /// <summary>
    /// Every hauling contract title key, and which ends a title written under
    /// it can honestly name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The places are not in the files: one Red Wind definition was logged as
    /// "to Checkmate", "to Gaslight" and "to Ruin Station" - the server picks
    /// them per run. The game fills <c>~mission(Location|name)</c> and
    /// <c>~mission(Destination|name)</c> into a title at run time, so the text
    /// table can ask for them; that is how StarStrings got places into the log.
    /// </para>
    /// <para>
    /// A key whose name states a shape - <c>Covalex_HaulCargo_SingleToMulti_title</c> -
    /// is read by its name. That is the evidence that holds: the Covalex hauls
    /// flown here are older broker missions that choose their title key at run
    /// time, so no contract record points at it, and all 54 of them logged under
    /// it read "from Port Tressler", as the name says. The generator contracts
    /// that do name such a key mostly agree; three keys carry one contract of
    /// another shape each (Ling's AToB title has a multi-pickup haul, Dead
    /// Saints' MultiToSingle title a single-pickup one), and the name is kept
    /// over those, as StarStrings keeps it.
    /// </para>
    /// <para>
    /// A key whose name says nothing - an intro haul's, the Iasi event's - is
    /// read off the contracts that use it, and names an end only when every one
    /// of them has exactly one of it: a direct haul has one of each, one with
    /// many drops has one pickup, one with many pickups has one drop.
    /// </para>
    /// </remarks>
    /// <param name="textKeys">The game's text table keys, where the keys that name their shape are found.</param>
    public static Dictionary<string, string> HaulTitles(
        DataCore core, IReadOnlyDictionary<Guid, DataRecord> byId, IEnumerable<string>? textKeys = null)
    {
        var shapes = new Dictionary<string, HashSet<State.HaulShape>>(StringComparer.OrdinalIgnoreCase);

        void Visit(DataCore.Pointer contract, string? inherited)
        {
            var at = core.InstanceAt(contract);
            var name = (core.StringAt(at, contract.StructIndex, "debugName") ?? "").Trim();
            var (overridesAt, overrides) = core.FieldAt(at, contract.StructIndex, "paramOverrides");
            var key = overrides is null ? inherited : TitleKey(core, overridesAt, overrides.StructIndex) ?? inherited;

            if (key is { Length: > 0 } && name.Length > 0
                && State.HaulingContract.FromArchetype(name)?.Shape is { } shape and not State.HaulShape.Unknown)
            {
                (shapes.TryGetValue(key, out var seen) ? seen : shapes[key] = []).Add(shape);
            }

            foreach (var sub in core.ClassArrayAt(at, contract.StructIndex, "subContracts")) Visit(sub, key);
        }

        foreach (var generator in byId.Values.Where(r => r.Name.StartsWith("ContractGenerator.", StringComparison.Ordinal)))
        {
            var generatorAt = core.InstanceAt(generator, generator.VariantIndex);

            foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
            {
                var handlerAt = core.InstanceAt(handler);
                var (paramsAt, parameters) = core.FieldAt(handlerAt, handler.StructIndex, "contractParams");
                var handlerKey = parameters is null ? null : TitleKey(core, paramsAt, parameters.StructIndex);

                foreach (var array in ContractArrays)
                    foreach (var contract in ContractsIn(core, handlerAt, handler.StructIndex, array))
                        Visit(contract, handlerKey);
            }
        }

        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in (textKeys ?? []).Concat(shapes.Keys))
        {
            var bare = key.EndsWith(",P", StringComparison.OrdinalIgnoreCase) ? key[..^2] : key;
            if (!titles.ContainsKey(bare) && EndsByName(bare) is { } ends) titles[bare] = ends;
        }

        foreach (var (key, seen) in shapes)
        {
            if (titles.ContainsKey(key)) continue;

            var onePickup = seen.All(s => s is State.HaulShape.Direct or State.HaulShape.SingleToMulti);
            var oneDrop = seen.All(s => s is State.HaulShape.Direct or State.HaulShape.MultiToSingle);

            if (onePickup && oneDrop) titles[key] = BothEnds;
            else if (oneDrop) titles[key] = DeliveryEnd;
            else if (onePickup) titles[key] = PickupEnd;
        }

        return titles;
    }

    /// <summary>
    /// The ends a hauling title key's own name promises, or null when it is not
    /// a title or names no shape: <c>Covalex_HaulCargo_SingleToMulti_title</c>
    /// is <see cref="PickupEnd"/>, a description key is nothing.
    /// </summary>
    public static string? EndsByName(string key)
    {
        if (!key.Contains("title", StringComparison.OrdinalIgnoreCase)) return null;

        var named = NamedShape().Match(key);
        if (!named.Success) return null;

        return named.Groups["shape"].Value.ToLowerInvariant() switch
        {
            "atob" => BothEnds,
            "singletomulti" => PickupEnd,
            _ => DeliveryEnd,
        };
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"HaulCargo_(?<shape>AToB|SingleToMulti|MultiToSingle)", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex NamedShape();

    private static string? TitleKey(DataCore core, long overridesAt, int structIndex)
    {
        foreach (var param in core.ClassArrayAt(overridesAt, structIndex, "stringParamOverrides"))
        {
            var paramAt = core.InstanceAt(param);
            if (core.EnumAt(paramAt, param.StructIndex, "param") == "Title")
                return core.StringAt(paramAt, param.StructIndex, "value")?.TrimStart('@');
        }

        return null;
    }

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
    internal static string ReadId(DataCore core, long at) => core.GuidAt(at).ToString();

    private static string Bare(string name) => name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
}
