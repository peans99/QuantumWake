namespace Quantumwake.Core.GameData;

/// <summary>One way a blueprint reward pool pays out.</summary>
/// <param name="Kind"><c>contract</c>, or <c>event</c> for a tier of an event's progress bar.</param>
/// <param name="Issuer">Who offers the contract, or the event's name.</param>
/// <param name="Title">The contract's title, or the bar the tier sits on.</param>
/// <param name="Id">The contract's debug name or the event's record, for telling variants apart.</param>
/// <param name="Chance">The chance the contract pays from the pool at all; 1 for an event tier.</param>
/// <param name="Points">For an event tier, the points it is reached at.</param>
public sealed record GameBlueprintSource(
    string Kind, string Issuer, string Title, string Id, double Chance, int Points = 0);

public static partial class GameScenarios
{
    /// <summary>
    /// What pays out each blueprint pool, keyed by the pool's name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pool says which blueprints it can give; nothing in it says who gives
    /// it. That is the other way round: a contract lists a <c>BlueprintRewards</c>
    /// among its <c>contractResults</c> - the pool, and the chance it pays at
    /// all - and an event's progress bar lists pools on its tiers. On 4.10.2
    /// the contracts reach 1,037 times into 111 of the 160 pools, and the
    /// Xenothreat event's tiers into 25 more.
    /// </para>
    /// <para>
    /// The pools no contract or tier names are written down by their absence:
    /// the page says nothing on this build was found to pay them, rather than
    /// guessing at a mission from the pool's name.
    /// </para>
    /// </remarks>
    private static Dictionary<string, List<GameBlueprintSource>> BlueprintSources(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyList<DataRecord> generators, IReadOnlyList<DataRecord> progressRecords, IReadOnlyList<DataRecord> journals)
    {
        var found = new Dictionary<string, List<GameBlueprintSource>>(StringComparer.OrdinalIgnoreCase);
        var pools = new Dictionary<Guid, string>();
        foreach (var record in core.Records())
            if (record.Name.StartsWith("BlueprintPoolRecord.", StringComparison.OrdinalIgnoreCase))
                pools.TryAdd(record.Hash, GameBlueprints.PoolName(record.Name));
        if (pools.Count == 0) return found;

        void Add(Guid? pool, GameBlueprintSource source)
        {
            if (pool is not { } id || !pools.TryGetValue(id, out var name)) return;
            if (!found.TryGetValue(name, out var list)) found[name] = list = [];
            if (!list.Any(s => s.Id == source.Id && s.Kind == source.Kind && s.Title == source.Title)) list.Add(source);
        }

        foreach (var generator in generators)
        {
            var generatorAt = core.InstanceAt(generator, generator.VariantIndex);
            var fallbackIssuer = Readable(generator.Name["ContractGenerator.".Length..]);

            foreach (var handler in core.PointerArrayAt(generatorAt, generator.StructIndex, "generators"))
            {
                var handlerAt = core.InstanceAt(handler);
                var issuer = Contractor(core, text, handlerAt, handler.StructIndex) is { Length: > 0 } named ? named : fallbackIssuer;

                // A handler's own title stands for contracts that do not set one.
                var (paramsAt, parameters) = core.FieldAt(handlerAt, handler.StructIndex, "contractParams");
                var handlerTitle = parameters is null ? null : StringParam(core, text, paramsAt, parameters.StructIndex, "Title");

                foreach (var array in ContractArrays)
                    foreach (var contract in ContractsIn(core, handlerAt, handler.StructIndex, array))
                        Walk(contract, issuer, handlerTitle, 0);
            }
        }

        // Sub-contracts carry results of their own, so the walk goes down.
        void Walk(DataCore.Pointer contract, string issuer, string? inherited, int depth)
        {
            if (depth > 4) return;
            var at = core.InstanceAt(contract);
            var id = (core.StringAt(at, contract.StructIndex, "debugName") ?? "").Trim();

            var (overridesAt, overrides) = core.FieldAt(at, contract.StructIndex, "paramOverrides");
            var title = overrides is null ? null : StringParam(core, text, overridesAt, overrides.StructIndex, "Title");
            title = title is { Length: > 0 } ? Placeholders(title) : inherited is { Length: > 0 } ? Placeholders(inherited) : Readable(id);

            var (resultsAt, results) = core.FieldAt(at, contract.StructIndex, "contractResults");
            if (results is not null)
            {
                foreach (var result in core.PointerArrayAt(resultsAt, results.StructIndex, "contractResults"))
                {
                    if (core.StructName(result.StructIndex) != "BlueprintRewards") continue;
                    var resultAt = core.InstanceAt(result);
                    var chance = core.SingleAt(resultAt, result.StructIndex, "chance") ?? 1;
                    Add(core.ReferenceAt(resultAt, result.StructIndex, "blueprintPool"),
                        new GameBlueprintSource("contract", issuer, title, id, chance > 0 ? chance : 1));
                }
            }

            foreach (var sub in ContractsIn(core, at, contract.StructIndex, "subContracts"))
                Walk(sub, issuer, title, depth + 1);
        }

        // An event pays pools on the tiers of its bars.
        var byHash = progressRecords.ToDictionary(r => r.Hash);
        var headings = Headings(core, text, journals, byHash);
        foreach (var record in progressRecords)
        {
            var eventId = record.Name[ProgressPrefix.Length..];
            var eventTitle = headings.GetValueOrDefault(record.Hash).Title is { Length: > 0 } t
                ? t
                : Readable(eventId.Replace("_ScenarioProgress", "", StringComparison.OrdinalIgnoreCase));
            var at = core.InstanceAt(record, record.VariantIndex);

            foreach (var tiers in core.ClassArrayAt(at, record.StructIndex, "factionRewardTiers"))
                foreach (var progression in core.ClassArrayAt(core.InstanceAt(tiers), tiers.StructIndex, "tierProgressions"))
                {
                    var progressionAt = core.InstanceAt(progression);
                    var key = core.StringAt(progressionAt, progression.StructIndex, "progressionText") ?? "";
                    var bar = (Localised(text, key) ?? Readable(key.TrimStart('@'))).Trim().TrimEnd(':').Trim();

                    foreach (var reward in core.ClassArrayAt(progressionAt, progression.StructIndex, "tierRewards"))
                    {
                        var rewardAt = core.InstanceAt(reward);
                        var points = core.Int32At(rewardAt, reward.StructIndex, "minPoints") ?? 0;
                        foreach (var pool in core.ReferenceArrayAt(rewardAt, reward.StructIndex, "blueprintPool"))
                            Add(pool, new GameBlueprintSource("event", eventTitle, bar, eventId, 1, points));
                    }
                }
        }

        return found;
    }
}
