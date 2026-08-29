namespace Quantumwake.Core.GameData;

/// <summary>One crafting recipe, as the game states it.</summary>
/// <param name="OutputClass">The entity class crafted, for joining to prices.</param>
/// <param name="Kind">Creation, dismantle, and so on.</param>
/// <param name="CraftSeconds">How long the first tier takes.</param>
/// <param name="Materials">What it consumes, already worded for display.</param>
/// <param name="RewardPools">The reward pools it can drop from.</param>
public sealed record GameBlueprint(
    string Output,
    string OutputClass,
    string Kind,
    int CraftSeconds,
    IReadOnlyList<string> Materials,
    IReadOnlyList<string> RewardPools);

/// <summary>
/// Crafting recipes read from the install rather than downloaded.
/// </summary>
/// <remarks>
/// <para>
/// A <c>CraftingBlueprintRecord</c> points at a blueprint, which names the
/// entity it makes and carries one tier per quality level. The first tier's
/// recipe holds the craft time and a tree of costs, and that tree is the whole
/// of what the page lists.
/// </para>
/// <para>
/// The tree is worth explaining, because it is not a flat list. Every node is a
/// <c>CraftingCost_Select</c> with a count and some options: when the count
/// equals the number of options everything is required, and when it is smaller
/// the recipe is offering a choice. Flattening without checking would turn "any
/// two of these five" into five mandatory materials.
/// </para>
/// <para>
/// The install also states a minimum quality per material, which the community
/// download never carried.
/// </para>
/// </remarks>
public static class GameBlueprints
{
    /// <summary>Reads every recipe, given an open blob and its English table.</summary>
    /// <param name="facts">
    /// What the items are called. An entity record does not carry its own
    /// display name - that lives on its attachable component - so a recipe
    /// reaching an entity by reference would otherwise list
    /// "Harvestable_Trophy_1H_yormandi_eye" where a player expects a name.
    /// </param>
    public static List<GameBlueprint> Read(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts)
    {
        var found = new List<GameBlueprint>();

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        var pools = Pools(core, byId);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("CraftingBlueprintRecord.", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);
            if (core.PointerAt(at, record.StructIndex, "blueprint") is not { } blueprint) continue;

            var blueprintAt = core.InstanceAt(blueprint);
            var process = core.PointerAt(blueprintAt, blueprint.StructIndex, "processSpecificData");

            var made = Made(core, byId, process);
            if (made is null) continue;

            var tiers = core.PointerArrayAt(blueprintAt, blueprint.StructIndex, "tiers");
            if (tiers.Count == 0) continue;

            // Later tiers are the same recipe at a higher quality, so the first
            // is what the page means by "the recipe".
            if (Costs(core, tiers[0]) is not { } costs) continue;

            var materials = new List<string>();
            Flatten(core, byId, text, facts, costs, materials);

            found.Add(new GameBlueprint(
                Named(core, text, facts, made),
                Bare(made.Name),
                Kind(process is null ? string.Empty : core.StructName(process.Value.StructIndex)),
                Seconds(core, costs),
                materials,
                pools.GetValueOrDefault(record.Hash) ?? []));
        }

        return found;
    }

    /// <summary>
    /// Which reward pools can drop each blueprint, by blueprint id.
    /// </summary>
    /// <remarks>
    /// Built the other way round from how it is read: a pool lists the
    /// blueprints it can give, and the page asks a blueprint where it comes
    /// from. The pools carry no display name of their own, so the record's name
    /// is tidied rather than invented - <c>BP_MISSIONREWARD_InterSec_ResourceGathering</c>
    /// becomes <c>InterSec ResourceGathering</c>.
    /// </remarks>
    private static Dictionary<Guid, List<string>> Pools(
        DataCore core, Dictionary<Guid, DataRecord> byId)
    {
        var pools = new Dictionary<Guid, List<string>>();

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("BlueprintPoolRecord.", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);
            var name = Tidied(Bare(record.Name));

            foreach (var reward in core.ClassArrayAt(at, record.StructIndex, "blueprintRewards"))
            {
                var id = core.ReferenceAt(
                    core.InstanceAt(reward), reward.StructIndex, "blueprintRecord");

                if (id is null || !byId.ContainsKey(id.Value)) continue;

                if (!pools.TryGetValue(id.Value, out var named)) pools[id.Value] = named = [];
                if (!named.Contains(name)) named.Add(name);
            }
        }

        return pools;
    }

    /// <summary><c>BP_MISSIONREWARD_InterSec_Gathering</c> reads as words.</summary>
    private static string Tidied(string name)
    {
        foreach (var prefix in new[] { "BP_MISSIONREWARD_", "BP_REWARD_", "BP_POOL_", "BP_" })
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        return name.Replace('_', ' ').Trim();
    }

    /// <summary>The entity a blueprint makes, or null when it makes nothing.</summary>
    private static DataRecord? Made(
        DataCore core, Dictionary<Guid, DataRecord> byId, DataCore.Pointer? process)
    {
        if (process is null) return null;

        var id = core.ReferenceAt(core.InstanceAt(process.Value), process.Value.StructIndex, "entityClass");

        return id is not null && byId.TryGetValue(id.Value, out var record) ? record : null;
    }

    /// <summary>The cost block of one tier.</summary>
    private static DataCore.Pointer? Costs(DataCore core, DataCore.Pointer tier)
    {
        var recipe = core.PointerAt(core.InstanceAt(tier), tier.StructIndex, "recipe");
        if (recipe is null) return null;

        return core.PointerAt(core.InstanceAt(recipe.Value), recipe.Value.StructIndex, "costs");
    }

    /// <summary>
    /// Craft time, which the game stores split into days, hours, minutes and
    /// seconds rather than as one number.
    /// </summary>
    private static int Seconds(DataCore core, DataCore.Pointer costs)
    {
        var time = core.PointerAt(core.InstanceAt(costs), costs.StructIndex, "craftTime");
        if (time is null) return 0;

        var at = core.InstanceAt(time.Value);
        var s = time.Value.StructIndex;

        // Seconds is the one part stored as a float, so reading all four the
        // same way silently dropped it - a recipe of 1m10s came out as 60s.
        return (core.Int32At(at, s, "days") ?? 0) * 86400
             + (core.Int32At(at, s, "hours") ?? 0) * 3600
             + (core.Int32At(at, s, "minutes") ?? 0) * 60
             + (int)Math.Round(core.SingleAt(at, s, "seconds") ?? 0);
    }

    /// <summary>Walks the cost tree into lines a person can read.</summary>
    private static void Flatten(
        DataCore core, Dictionary<Guid, DataRecord> byId, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts, DataCore.Pointer costs, List<string> into)
    {
        var mandatory = core.PointerAt(core.InstanceAt(costs), costs.StructIndex, "mandatoryCost");
        if (mandatory is not null) Walk(core, byId, text, facts, mandatory.Value, into, 0);
    }

    private static void Walk(
        DataCore core, Dictionary<Guid, DataRecord> byId, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts, DataCore.Pointer node, List<string> into, int depth)
    {
        if (depth > 6 || into.Count > 64) return;

        var at = core.InstanceAt(node);
        var s = node.StructIndex;

        switch (core.StructName(s))
        {
            case "CraftingCost_Resource":
            {
                var id = core.ReferenceAt(at, s, "resource");
                var quantity = core.PointerAt(at, s, "quantity");

                var scu = quantity is null
                    ? null
                    : core.SingleAt(
                        core.InstanceAt(quantity.Value), quantity.Value.StructIndex, "standardCargoUnits");

                into.Add($"{Resolved(core, text, byId, facts, id)} {scu ?? 0:0.##} SCU");
                return;
            }

            case "CraftingCost_Item":
            {
                var id = core.ReferenceAt(at, s, "entityClass");
                // The multiplication sign, matching how the download worded the
                // same line, so a page fed by either source reads the same.
                into.Add(
                    $"{Resolved(core, text, byId, facts, id)} {(char)0x00D7}{core.Int32At(at, s, "quantity") ?? 1}");
                return;
            }

            default:
            {
                var options = core.PointerArrayAt(at, s, "options");
                if (options.Count == 0) return;

                var take = core.Int32At(at, s, "count") ?? options.Count;

                // A select that takes fewer than it offers is a choice, and
                // listing its options as separate lines would read as a recipe
                // needing all of them.
                if (take < options.Count)
                {
                    var choices = new List<string>();
                    foreach (var option in options)
                        Walk(core, byId, text, facts, option, choices, depth + 1);

                    if (choices.Count > 0) into.Add($"any {take} of: {string.Join(", ", choices)}");

                    return;
                }

                foreach (var option in options) Walk(core, byId, text, facts, option, into, depth + 1);
                return;
            }
        }
    }

    private static string Resolved(
        DataCore core, IReadOnlyDictionary<string, string> text, Dictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<string, GameItem> facts, Guid? id) =>
        id is not null && byId.TryGetValue(id.Value, out var record)
            ? Named(core, text, facts, record)
            : "something";

    /// <summary>
    /// What a record is called, trying the item catalogue before the record.
    /// </summary>
    /// <remarks>
    /// Resources carry their own <c>displayName</c>; entities do not, and are
    /// named by the attachable component the catalogue already read.
    /// </remarks>
    private static string Named(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts, DataRecord record)
    {
        var bare = Bare(record.Name);

        if (facts.TryGetValue(bare, out var item) && item.Name.Length > 0 && item.Name != bare)
            return item.Name;

        return Name(core, text, record) ?? bare;
    }

    private static string? Name(
        DataCore core, IReadOnlyDictionary<string, string> text, DataRecord record)
    {
        var at = core.InstanceAt(record, record.VariantIndex);

        if (GameItems.Localised(core, text, at, record.StructIndex) is { Length: > 0 } localised)
            return localised;

        if (core.TextProperty(record, "displayName") is { Length: > 0 } key
            && text.TryGetValue(key.TrimStart('@'), out var english) && english.Length > 0)
        {
            return english;
        }

        return null;
    }

    /// <summary><c>CraftingProcess_Creation</c> becomes <c>creation</c>.</summary>
    private static string Kind(string processStruct)
    {
        var underscore = processStruct.LastIndexOf('_');

        return underscore > 0 && underscore < processStruct.Length - 1
            ? processStruct[(underscore + 1)..].ToLowerInvariant()
            : "creation";
    }

    private static string Bare(string recordName) =>
        recordName.Contains('.') ? recordName[(recordName.LastIndexOf('.') + 1)..] : recordName;
}
