namespace Quantumwake.Core.GameData;

/// <summary>A rank another ladder must stand at before a contract is offered.</summary>
/// <param name="Faction">The organisation's record, <c>FactionReputation_Lawful_CitizensForProsperity</c>.</param>
/// <param name="Scope">The ladder, <c>FactionReputationScope</c>.</param>
/// <param name="MinRank">The lowest rank, as an index into that ladder; null when open below.</param>
/// <param name="MaxRank">The highest; null when open above.</param>
public sealed record GameMissionGate(string Faction, string Scope, int? MinRank, int? MaxRank);

/// <summary>The four ratings a contract's difficulty profile gives, as the game words them.</summary>
/// <param name="Mechanical">"Normal_PvE_only_action_4"; the trailing number is the rating.</param>
public sealed record GameMissionDifficulty(string? Mechanical, string? MentalLoad, string? RiskOfLoss, string? Knowledge);

/// <summary>
/// What the installed game says about one contract besides what it pays: who
/// offers it, what it is, where and to whom, how long and how often.
/// </summary>
/// <param name="Id">The inline id the log's <c>contractDefinitionId</c> writes; see <see cref="GameMissions"/>.</param>
/// <param name="Name">The debug name the log's <c>contract [...]</c> writes; a sub-contract's is its parent's plus its index.</param>
/// <param name="Generator">The <c>ContractGenerator</c> record it sits in, bare: <c>InterSec_KillShip</c>.</param>
/// <param name="Parent">A sub-contract's parent, by name; null for a contract of its own.</param>
/// <param name="Template">The <c>ContractTemplate</c>, bare: <c>Bounty</c>. Null for a legacy broker mission.</param>
/// <param name="TypeKey">The text key of its mission type, <c>mobiglas_ui_Mercenary</c>.</param>
/// <param name="Faction">The organisation whose ladder it sits on, <c>FactionReputation_Lawful_InterSecDefenseSolutions</c>.</param>
/// <param name="TitleKey">Its title's text key; null when the game picks the title at run time.</param>
/// <param name="DescriptionKey">Its briefing's text key, likewise.</param>
/// <param name="ContractorKey">Who the mobiGlas says offers it, as a text key.</param>
/// <param name="Systems">The <c>MissionLocality</c> records it is offered in, bare: <c>Nyx</c>, <c>Stanton</c>.</param>
/// <param name="Locations">The places it is offered at, by the star map's English name.</param>
/// <param name="MinCrimeStat">The lowest CrimeStat it is offered at; null when the files set none.</param>
/// <param name="MaxCrimeStat">The highest.</param>
/// <param name="Gates">Ranks on other ladders it needs.</param>
/// <param name="RequiresCompleted">How many contracts of a series must be done first; null when none.</param>
/// <param name="Minutes">The game's own estimate of how long it takes, in minutes; null when it gives none.</param>
/// <param name="BuyIn">aUEC paid to take it; null when free.</param>
/// <param name="MaxPlayers">How many can share one instance.</param>
/// <param name="OnceOnly">Offered once per pilot.</param>
/// <param name="RetakeAfterAbandon">Can be taken again after abandoning it.</param>
/// <param name="RetakeAfterFail">Can be taken again after failing it.</param>
/// <param name="Illegal">The template marks it illegal work.</param>
/// <param name="Unreleased">Marked not for release or work in progress, on it, its handler or its template.</param>
public sealed record GameMissionInfo(
    string Id, string Name, string Generator, string? Parent,
    string? Template, string? TypeKey, string? Faction,
    string? TitleKey, string? DescriptionKey, string? ContractorKey,
    IReadOnlyList<string> Systems, IReadOnlyList<string> Locations,
    double? MinCrimeStat, double? MaxCrimeStat,
    IReadOnlyList<GameMissionGate> Gates, int? RequiresCompleted,
    double? Minutes, int? BuyIn, int? MaxPlayers,
    bool OnceOnly, bool? RetakeAfterAbandon, bool? RetakeAfterFail,
    bool Illegal, bool Unreleased,
    GameMissionDifficulty? Difficulty = null);

/// <summary>
/// A <c>MissionLocality</c> as a pilot would say it. The record is a list of
/// star-map objects: <c>RegionA</c> is Pyro I, Pyro II and their stations,
/// <c>Stanton1</c> is Hurston with its moons and stations.
/// </summary>
/// <param name="Name">The planets it holds by the star map's names - "Pyro I, Pyro II", "Hurston" - or its systems when it holds no planet: "Stanton".</param>
/// <param name="Systems">"Stanton", "Pyro", "Nyx".</param>
public sealed record GameMissionArea(string Name, IReadOnlyList<string> Systems);

/// <summary>Every contract's details, and the English of every text key they use.</summary>
public sealed class GameMissionCatalogue
{
    public List<GameMissionInfo> Missions { get; init; } = [];

    /// <summary>Text key to English, for the keys <see cref="Missions"/> names and no others.</summary>
    public Dictionary<string, string> Texts { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Each <c>MissionLocality</c> the contracts name, as a pilot would say it.</summary>
    public Dictionary<string, GameMissionArea> Areas { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    public static GameMissionCatalogue Empty { get; } = new();

    /// <summary>A locality's name, or its record name when the catalogue has none.</summary>
    public string Area(string locality) => Areas.TryGetValue(locality, out var area) ? area.Name : locality;

    /// <summary>The English for a key, or null when the table has none or only a placeholder.</summary>
    public string? Text(string? key) =>
        key is { Length: > 0 } && Texts.TryGetValue(key, out var english) ? english : null;
}

/// <summary>
/// Reads each contract's details from its generator, handler, template and
/// prerequisites, the way the game layers them.
/// </summary>
/// <remarks>
/// <para>
/// A contract is assembled from four places, each overriding the one before:
/// its <c>ContractGenerator</c> handler's <c>contractParams</c> and
/// <c>defaultAvailability</c>, its template's display info, its own
/// <c>paramOverrides</c> and <c>additionalPrerequisites</c>, and - for a
/// sub-contract - its parent. Measured on 4.10.2: 2,984 top-level contracts in
/// 282 handlers, 5,174 with their sub-contracts, 367 marked not for release.
/// Titles and briefings are string parameters (2,514 and 2,517 of them); a
/// contractor name is one on 384, and otherwise comes from the handler.
/// </para>
/// <para>
/// The prerequisites a pilot can be told about: a <c>MissionLocality</c> (1,700),
/// a star-map location (373), a CrimeStat range (131), a rank on some ladder
/// (311) and a count of a series' contracts already done (871). A legacy
/// contract names an older <c>MissionBrokerEntry</c>, which carries all of this
/// in fields of its own - and whose title is often <c>~mission(Title)</c>, the
/// game choosing one at run time; such a title is read as none.
/// </para>
/// </remarks>
public static partial class GameMissionDetails
{
    private static readonly string[] ContractArrays = ["contracts", "introContracts", "legacyContracts"];

    /// <summary>What a contract takes from whatever it sits in.</summary>
    private sealed record Layer(
        string? Title, string? Description, string? Contractor, string? Type, string? Faction,
        List<string> Systems, List<string> Locations, double? MinCrime, double? MaxCrime,
        List<GameMissionGate> Gates, int? Requires, int? MaxPlayers, bool OnceOnly,
        bool? RetakeAbandon, bool? RetakeFail, bool Unreleased,
        string? Template = null, bool Illegal = false, double? Minutes = null, int? BuyIn = null,
        GameMissionDifficulty? Difficulty = null);

    public static GameMissionCatalogue Read(
        DataCore core,
        IReadOnlyDictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<Guid, (string Scope, int Index)> standings,
        IReadOnlyDictionary<string, string> text)
    {
        var missions = new List<GameMissionInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var brokerStruct = core.StructIndexOf("MissionBrokerEntry");

        string? Record(Guid? id) => id is { } g && byId.TryGetValue(g, out var r) ? Bare(r.Name) : null;

        // A text key a pilot would read, or null: empty, uninitialised, or a
        // lone run-time placeholder ("~mission(Title)") says nothing yet.
        string? Key(string? value)
        {
            var key = value?.TrimStart('@');
            if (key is not { Length: > 0 } || !text.TryGetValue(key, out var english)) return null;
            english = english.Trim();
            if (english.Length == 0 || english.StartsWith("<=", StringComparison.Ordinal) || english.StartsWith("@", StringComparison.Ordinal)
                || (english.StartsWith("~mission(", StringComparison.Ordinal) && english.EndsWith(')') && english.IndexOf(')') == english.Length - 1))
                return null;
            keys.Add(key);
            return key;
        }

        string? TypeKey(Guid? type) =>
            type is { } g && byId.TryGetValue(g, out var r)
                ? Key(core.StringAt(core.InstanceAt(r, r.VariantIndex), r.StructIndex, "LocalisedTypeName"))
                : null;

        string? PlaceName(Guid? id)
        {
            if (id is not { } g || !byId.TryGetValue(g, out var r)) return null;
            var key = core.StringAt(core.InstanceAt(r, r.VariantIndex), r.StructIndex, "name")?.TrimStart('@');
            return key is { Length: > 0 } && text.TryGetValue(key, out var english) && english.Length > 0 && !english.StartsWith("<=", StringComparison.Ordinal)
                ? english.Trim()
                : Bare(r.Name);
        }

        (string? Scope, int? Index) Rank(Guid? id) =>
            id is { } g && standings.TryGetValue(g, out var place) ? (place.Scope, place.Index) : (null, null);

        // String parameters and the type override, over what is inherited.
        Layer Params(Layer layer, long at, int structIndex, string field)
        {
            var (paramsAt, parameters) = core.FieldAt(at, structIndex, field);
            if (parameters is null) return layer;
            foreach (var param in core.ClassArrayAt(paramsAt, parameters.StructIndex, "stringParamOverrides"))
            {
                var pa = core.InstanceAt(param);
                var value = core.StringAt(pa, param.StructIndex, "value");
                switch (core.EnumAt(pa, param.StructIndex, "param"))
                {
                    case "Title": layer = layer with { Title = Key(value) ?? layer.Title }; break;
                    case "Description": layer = layer with { Description = Key(value) ?? layer.Description }; break;
                    case "Contractor": layer = layer with { Contractor = Key(value) ?? layer.Contractor }; break;
                }
            }
            return TypeKey(core.ReferenceAt(paramsAt, parameters.StructIndex, "missionTypeOverride")) is { } type
                ? layer with { Type = type }
                : layer;
        }

        Layer Prerequisites(Layer layer, IEnumerable<DataCore.Pointer> list)
        {
            foreach (var p in list)
            {
                var pa = core.InstanceAt(p);
                switch (core.StructName(p.StructIndex))
                {
                    case "ContractPrerequisite_Locality":
                        if (Record(core.ReferenceAt(pa, p.StructIndex, "localityAvailable")) is { } locality && !layer.Systems.Contains(locality))
                            layer = layer with { Systems = [.. layer.Systems, locality] };
                        break;
                    case "ContractPrerequisite_Location":
                        if (PlaceName(core.ReferenceAt(pa, p.StructIndex, "locationAvailable")) is { } place && !layer.Locations.Contains(place))
                            layer = layer with { Locations = [.. layer.Locations, place] };
                        break;
                    case "ContractPrerequisite_CrimeStat":
                        layer = layer with
                        {
                            MinCrime = core.SingleAt(pa, p.StructIndex, "minCrimeStat") ?? layer.MinCrime,
                            MaxCrime = core.SingleAt(pa, p.StructIndex, "maxCrimeStat") ?? layer.MaxCrime,
                        };
                        break;
                    case "ContractPrerequisite_Reputation":
                        if (core.BoolAt(pa, p.StructIndex, "exclude") == true) break;
                        var (scope, min) = Rank(core.ReferenceAt(pa, p.StructIndex, "minStanding"));
                        var (maxScope, max) = Rank(core.ReferenceAt(pa, p.StructIndex, "maxStanding"));
                        if ((scope ?? maxScope) is { } s)
                            layer = layer with { Gates = [.. layer.Gates, new GameMissionGate(Record(core.ReferenceAt(pa, p.StructIndex, "factionReputation")) ?? "", s, min, max)] };
                        break;
                    case "ContractPrerequisite_CompletedContractTags":
                        var (tagsAt, tags) = core.FieldAt(pa, p.StructIndex, "requiredCompletedContractTags");
                        if (tags is not null && core.ReferenceArrayAt(tagsAt, tags.StructIndex, "tags").Count > 0
                            && core.Int32At(pa, p.StructIndex, "requiredCountValue") is > 0 and var count)
                            layer = layer with { Requires = Math.Max(layer.Requires ?? 0, count) };
                        break;
                }
            }
            return layer;
        }

        Layer Availability(Layer layer, long at, int structIndex, string field)
        {
            var (availAt, avail) = core.FieldAt(at, structIndex, field);
            if (avail is null) return layer;
            var si = avail.StructIndex;
            layer = layer with
            {
                MaxPlayers = core.Int32At(availAt, si, "maxPlayersPerInstance") ?? layer.MaxPlayers,
                OnceOnly = core.BoolAt(availAt, si, "onceOnly") ?? layer.OnceOnly,
                RetakeAbandon = core.BoolAt(availAt, si, "canReacceptAfterAbandoning") ?? layer.RetakeAbandon,
                RetakeFail = core.BoolAt(availAt, si, "canReacceptAfterFailing") ?? layer.RetakeFail,
            };
            return Prerequisites(layer, core.PointerArrayAt(availAt, si, "prerequisites"));
        }

        bool Unreleased(long at, int structIndex) =>
            core.BoolAt(at, structIndex, "notForRelease") == true || core.BoolAt(at, structIndex, "workInProgress") == true;

        Layer Template(Layer layer, Guid? id)
        {
            if (id is not { } g || !byId.TryGetValue(g, out var record)) return layer;
            var at = core.InstanceAt(record, record.VariantIndex);
            layer = layer with { Template = Bare(record.Name), Unreleased = layer.Unreleased || Unreleased(at, record.StructIndex) };
            if (core.PointerAt(at, record.StructIndex, "contractDisplayInfo") is { StructIndex: >= 0 } display)
            {
                var da = core.InstanceAt(display);
                layer = layer with
                {
                    Type = layer.Type ?? TypeKey(core.ReferenceAt(da, display.StructIndex, "type")),
                    Illegal = core.BoolAt(da, display.StructIndex, "illegal") ?? false,
                };
            }
            return layer;
        }

        Layer Results(Layer layer, long at, int structIndex)
        {
            var (resultsAt, results) = core.FieldAt(at, structIndex, "contractResults");
            if (results is null) return layer;
            var si = results.StructIndex;
            var minutes = core.SingleAt(resultsAt, si, "timeToComplete");
            var buyIn = core.Int32At(resultsAt, si, "contractBuyInAmount");
            GameMissionDifficulty? difficulty = null;
            if (core.PointerAt(resultsAt, si, "difficulty") is { StructIndex: >= 0 } d)
            {
                var dAt = core.InstanceAt(d);
                difficulty = new GameMissionDifficulty(
                    core.EnumAt(dAt, d.StructIndex, "mechanicalSkill"), core.EnumAt(dAt, d.StructIndex, "mentalLoad"),
                    core.EnumAt(dAt, d.StructIndex, "riskOfLoss"), core.EnumAt(dAt, d.StructIndex, "gameKnowledge"));
            }
            return layer with
            {
                Minutes = minutes is > 0 ? Math.Round(minutes.Value, 1) : layer.Minutes,
                BuyIn = buyIn is > 0 ? buyIn : layer.BuyIn,
                Difficulty = difficulty ?? layer.Difficulty,
            };
        }

        // A legacy contract's broker record holds everything as fields of its own.
        Layer Broker(Layer layer, DataRecord broker)
        {
            var at = core.InstanceAt(broker, broker.VariantIndex);
            var si = broker.StructIndex;
            layer = layer with
            {
                Title = Key(core.StringAt(at, si, "title")) ?? layer.Title,
                Description = Key(core.StringAt(at, si, "description")) ?? layer.Description,
                Contractor = Key(core.StringAt(at, si, "missionGiver")) ?? layer.Contractor,
                Type = TypeKey(core.ReferenceAt(at, si, "type")) ?? layer.Type,
                MaxPlayers = core.Int32At(at, si, "maxPlayersPerInstance") ?? layer.MaxPlayers,
                OnceOnly = core.BoolAt(at, si, "onceOnly") ?? layer.OnceOnly,
                RetakeAbandon = core.BoolAt(at, si, "canReacceptAfterAbandoning") ?? layer.RetakeAbandon,
                RetakeFail = core.BoolAt(at, si, "canReacceptAfterFailing") ?? layer.RetakeFail,
                BuyIn = core.Int32At(at, si, "missionBuyInAmount") is > 0 and var b ? b : layer.BuyIn,
                Unreleased = layer.Unreleased || core.BoolAt(at, si, "notForRelease") == true,
            };
            if (Record(core.ReferenceAt(at, si, "localityAvailable")) is { } locality && !layer.Systems.Contains(locality))
                layer = layer with { Systems = [.. layer.Systems, locality] };
            if (PlaceName(core.ReferenceAt(at, si, "locationMissionAvailable")) is { } place && !layer.Locations.Contains(place))
                layer = layer with { Locations = [.. layer.Locations, place] };

            var (repAt, rep) = core.FieldAt(at, si, "reputationPrerequisites");
            if (rep is not null)
            {
                var (rangeAt, range) = core.FieldAt(repAt, rep.StructIndex, "wantedLevel");
                if (range is not null)
                    layer = layer with
                    {
                        MinCrime = core.SingleAt(rangeAt, range.StructIndex, "minValue") ?? layer.MinCrime,
                        MaxCrime = core.SingleAt(rangeAt, range.StructIndex, "maxValue") ?? layer.MaxCrime,
                    };
            }
            return layer;
        }

        void Contract(DataCore.Pointer contract, Layer inherited, string generator, string? parent, string? parentName, int index)
        {
            var at = core.InstanceAt(contract);
            var si = contract.StructIndex;
            var (idAt, _) = core.FieldAt(at, si, "id");
            if (idAt < 0) return;

            var own = (core.StringAt(at, si, "debugName") ?? "").Trim();
            // As GameMissions names them, which is as the log does: a sub-contract
            // with no name of its own is its parent's plus its index.
            var name = own.Length > 0 ? own : parentName is not null ? $"{parentName}_{index}" : "";
            if (name.Length == 0) return;

            var layer = inherited with { Unreleased = inherited.Unreleased || Unreleased(at, si) };
            layer = Template(layer, core.ReferenceAt(at, si, "template"));
            layer = Params(layer, at, si, "paramOverrides");
            layer = Prerequisites(layer, core.PointerArrayAt(at, si, "additionalPrerequisites"));
            layer = Results(layer, at, si);
            if (brokerStruct >= 0 && core.ReferenceAt(at, si, "missionBrokerEntry") is { } b && byId.TryGetValue(b, out var broker)
                && broker.StructIndex == brokerStruct)
                layer = Broker(layer, broker);

            var (scopeMin, min) = Rank(core.ReferenceAt(at, si, "minStanding"));
            var (scopeMax, max) = Rank(core.ReferenceAt(at, si, "maxStanding"));

            var id = GameMissions.ReadId(core, idAt);
            if (seen.Add(id))
            {
                var gates = layer.Gates;
                // The contract's own rank range is a gate on its organisation's ladder.
                if ((scopeMin ?? scopeMax) is { } s && layer.Faction is { } f)
                    gates = [new GameMissionGate(f, s, min, max), .. gates.Where(g => !(g.Faction == f && g.Scope == s))];

                missions.Add(new GameMissionInfo(
                    id, name, generator, parent, layer.Template, layer.Type, layer.Faction,
                    layer.Title, layer.Description, layer.Contractor,
                    layer.Systems, layer.Locations, layer.MinCrime, layer.MaxCrime,
                    gates, layer.Requires, layer.Minutes, layer.BuyIn, layer.MaxPlayers,
                    layer.OnceOnly, layer.RetakeAbandon, layer.RetakeFail, layer.Illegal, layer.Unreleased,
                    layer.Difficulty));
            }

            var subs = core.ClassArrayAt(at, si, "subContracts");
            for (var i = 0; i < subs.Count; i++) Contract(subs[i], layer, generator, name, name, i);
        }

        foreach (var generator in byId.Values.Where(r => r.Name.StartsWith("ContractGenerator.", StringComparison.Ordinal)))
        {
            var generatorAt = core.InstanceAt(generator, generator.VariantIndex);
            var generatorName = Bare(generator.Name);

            foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
            {
                var ha = core.InstanceAt(handler);
                var hs = handler.StructIndex;
                var layer = new Layer(null, null, null, null, Record(core.ReferenceAt(ha, hs, "factionReputation")),
                    [], [], null, null, [], null, null, false, null, null, Unreleased(ha, hs));
                layer = Params(layer, ha, hs, "contractParams");
                layer = Availability(layer, ha, hs, "defaultAvailability");

                foreach (var array in ContractArrays)
                {
                    var list = core.ClassArrayAt(ha, hs, array);
                    if (list.Count == 0) list = core.PointerArrayAt(ha, hs, array);
                    foreach (var contract in list) Contract(contract, layer, generatorName, null, null, 0);
                }
            }
        }

        // Only the localities a contract names; a record name the star map
        // cannot place keeps its own name rather than inventing one.
        var localities = byId.Values
            .Where(r => r.Name.StartsWith("MissionLocality.", StringComparison.Ordinal))
            .GroupBy(r => Bare(r.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var areas = new Dictionary<string, GameMissionArea>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in missions.SelectMany(m => m.Systems).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!localities.TryGetValue(name, out var record)) continue;
            var objects = core.ReferenceArrayAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "availableLocations")
                .Select(id => byId.TryGetValue(id, out var r) ? r : null)
                .OfType<DataRecord>()
                .ToList();
            var systems = objects.Select(o => SystemOf(Bare(o.Name))).OfType<string>().Distinct().ToList();
            // A locality holding a system's star is the whole system: "Pyro", not its six planets.
            var whole = objects.Any(o => SystemWide().IsMatch(Bare(o.Name)));
            var planets = whole ? [] : objects.Where(o => Planet().IsMatch(Bare(o.Name))).Select(o => PlaceName(o.Hash)).OfType<string>().Distinct().ToList();
            areas[name] = new GameMissionArea(
                planets.Count > 0 ? string.Join(", ", planets) : systems.Count > 0 ? string.Join(" and ", systems) : name,
                systems);
        }

        return new GameMissionCatalogue
        {
            Missions = [.. missions.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)],
            Texts = keys.ToDictionary(k => k, k => text[k].Trim(), StringComparer.OrdinalIgnoreCase),
            Areas = areas,
        };
    }

    /// <summary>A planet's star-map id - <c>Pyro1</c>, <c>Stanton4</c> - and not a moon's (<c>Stanton1a</c>) or a station's.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(Stanton|Pyro|Nyx)\d+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex Planet();

    /// <summary>A system's star or the system itself: <c>PyroStar</c>, <c>NyxSolarSystem</c>.</summary>
    [System.Text.RegularExpressions.GeneratedRegex(@"^(Stanton|Pyro|Nyx)(Star|SolarSystem)$", System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex SystemWide();

    /// <summary>The system a star-map id is in: by its prefix, and Pyro's rest stops are <c>RR_P3_L1</c>.</summary>
    private static string? SystemOf(string id) =>
        id.StartsWith("Stanton", StringComparison.OrdinalIgnoreCase) ? "Stanton"
        : id.StartsWith("Pyro", StringComparison.OrdinalIgnoreCase) || id.StartsWith("RR_P", StringComparison.OrdinalIgnoreCase) ? "Pyro"
        : id.StartsWith("Nyx", StringComparison.OrdinalIgnoreCase) ? "Nyx"
        : null;

    private static string Bare(string name) => name.Contains('.') ? name[(name.IndexOf('.') + 1)..] : name;
}
