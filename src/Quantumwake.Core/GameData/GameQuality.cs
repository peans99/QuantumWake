namespace Quantumwake.Core.GameData;

/// <summary>The quality a resource comes out at.</summary>
/// <param name="Min">The floor: nothing of this class is ever worse.</param>
/// <param name="Max">The ceiling, which is 1000 for everything so far.</param>
/// <param name="Mean">The middle of the distribution.</param>
/// <param name="Spread">Its standard deviation - how often the extremes turn up.</param>
/// <param name="Local">
/// True when this place overrides the class default, which is the interesting
/// case: it means the answer here is not the answer everywhere.
/// </param>
public sealed record QualityBand(int Min, int Max, double Mean, double Spread, bool Local = false);

/// <summary>
/// What quality a resource comes out at, by what it is and where it is.
/// </summary>
/// <remarks>
/// <para>
/// The game keeps a normal distribution per class of resource, and lets a place
/// override it. The classes are the mining method and, for ship mining, the
/// rarity tier: <c>CommonShipMineable</c> through <c>LegendaryShipMineable</c>,
/// plus <c>FPSMineable</c>, <c>GroundMineable</c> and <c>Gatherable</c>.
/// </para>
/// <para>
/// The floors differ by method and are the part worth knowing: ship mining never
/// yields below 501, hand mining never below 201, and ground mining or gathering
/// can yield anything from 1. The overrides then move it by place. Pyro widens
/// every ship-mining spread - common ore from 143 to 149, legendary from 203 to
/// 225 - which is more chance of the very good and the very poor. The Nyx
/// rockcrackers raise the floor outright, from 501 to 651.
/// </para>
/// <para>
/// This is the half that makes a recipe's quality requirement actionable. A
/// recipe asking for 900 is asking where you mined, not just what.
/// </para>
/// </remarks>
public static class GameQuality
{
    /// <summary>
    /// Every distribution, keyed by class and by class at a place.
    /// </summary>
    /// <remarks>
    /// A place key is <c>Class@Place</c>, using the name the star map shows, so
    /// a caller that knows only where a rock is can ask without resolving
    /// anything itself.
    /// </remarks>
    public static Dictionary<string, QualityBand> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var bands = new Dictionary<string, QualityBand>(StringComparer.OrdinalIgnoreCase);

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        foreach (var record in core.Records())
        {
            var bare = Bare(record.Name);
            if (bare.Contains("TEMPLATE", StringComparison.OrdinalIgnoreCase)) continue;

            var at = core.InstanceAt(record, record.VariantIndex);

            if (record.Name.StartsWith("CraftingQualityDistributionRecord.", StringComparison.OrdinalIgnoreCase))
            {
                // Only the defaults are keyed bare. The others are per-ore and
                // there is no way from a spawn row to know which applies.
                if (!bare.EndsWith("_Default", StringComparison.OrdinalIgnoreCase)) continue;

                if (Band(core, core.PointerAt(at, record.StructIndex, "qualityDistribution")) is { } band)
                    bands.TryAdd(Class(bare), band);

                continue;
            }

            if (!record.Name.StartsWith("CraftingQualityLocationOverrideRecord.", StringComparison.OrdinalIgnoreCase))
                continue;

            if (core.PointerAt(at, record.StructIndex, "locationOverride") is not { } list) continue;

            var listAt = core.InstanceAt(list);

            foreach (var entry in core.PointerArrayAt(listAt, list.StructIndex, "locationOverrideList"))
            {
                var entryAt = core.InstanceAt(entry);

                var where = Place(core, text, byId,
                    core.ReferenceAt(entryAt, entry.StructIndex, "location"));

                if (where is null) continue;

                if (Band(core, core.PointerAt(entryAt, entry.StructIndex, "qualityDistribution")) is not { } band)
                    continue;

                bands.TryAdd($"{Class(bare)}@{where}", band);

                // A system is mapped as "Pyro System" here and named "Pyro" by
                // the designation a spawn row carries, so it is indexed both
                // ways rather than the caller being asked to guess which.
                if (where.EndsWith(" System", StringComparison.OrdinalIgnoreCase))
                    bands.TryAdd($"{Class(bare)}@{where[..^" System".Length]}", band);
            }
        }

        return bands;
    }

    /// <summary>
    /// The band for a resource class at a place, falling back to the class's own.
    /// </summary>
    public static QualityBand? For(
        IReadOnlyDictionary<string, QualityBand> bands, string? resourceClass, params string?[] places)
    {
        if (resourceClass is not { Length: > 0 }) return null;

        foreach (var place in places)
        {
            if (place is { Length: > 0 } && bands.TryGetValue($"{resourceClass}@{place}", out var here))
                return here with { Local = true };
        }

        return bands.GetValueOrDefault(resourceClass);
    }

    /// <summary>
    /// Which distribution a spawn falls under, from how it is mined and how rare
    /// the cluster it sits in is.
    /// </summary>
    /// <remarks>
    /// The preset says the method - <c>FPSMining_Hadanite</c> is hand mining -
    /// and for ship mining the cluster says the tier, since
    /// <c>CommonShipMineable_Cluster</c> names it outright. Salvage has no
    /// quality of this kind and gets nothing rather than a guess.
    /// </remarks>
    public static string? ClassOf(string? preset, string? cluster)
    {
        if (preset is not { Length: > 0 }) return null;

        if (preset.StartsWith("FPSMining", StringComparison.OrdinalIgnoreCase)) return "FPSMineable";
        if (preset.StartsWith("GroundVehicleMining", StringComparison.OrdinalIgnoreCase)) return "GroundMineable";
        if (preset.StartsWith("Plant", StringComparison.OrdinalIgnoreCase)) return "Gatherable";
        if (!preset.StartsWith("Mining", StringComparison.OrdinalIgnoreCase)) return null;

        foreach (var tier in new[] { "Legendary", "Epic", "Rare", "Uncommon", "Common" })
        {
            if (cluster is { Length: > 0 } && cluster.StartsWith(tier, StringComparison.OrdinalIgnoreCase))
                return $"{tier}ShipMineable";
        }

        return "CommonShipMineable";
    }

    private static QualityBand? Band(DataCore core, DataCore.Pointer? distribution)
    {
        if (distribution is null) return null;

        var at = core.InstanceAt(distribution.Value);
        var s = distribution.Value.StructIndex;

        var min = core.Int32At(at, s, "min");
        var max = core.Int32At(at, s, "max");
        if (min is null || max is null) return null;

        return new QualityBand(
            min.Value, max.Value,
            core.SingleAt(at, s, "mean") ?? 0,
            core.SingleAt(at, s, "stddev") ?? 0);
    }

    /// <summary>The name the star map shows for an overridden location.</summary>
    private static string? Place(
        DataCore core, IReadOnlyDictionary<string, string> text,
        Dictionary<Guid, DataRecord> byId, Guid? id)
    {
        if (id is null || !byId.TryGetValue(id.Value, out var record)) return null;

        var at = core.InstanceAt(record, record.VariantIndex);
        var key = core.StringAt(at, record.StructIndex, "name");

        return key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var name)
            && name.Length > 0 && !name.Contains("UNINITIALIZED", StringComparison.OrdinalIgnoreCase)
            ? name
            : null;
    }

    /// <summary><c>CommonShipMineable_QualityOverride_Pyro</c> is CommonShipMineable.</summary>
    private static string Class(string recordName)
    {
        var cut = recordName.IndexOf("_Quality", StringComparison.OrdinalIgnoreCase);
        return cut > 0 ? recordName[..cut] : recordName;
    }

    private static string Bare(string recordName) =>
        recordName.Contains('.') ? recordName[(recordName.LastIndexOf('.') + 1)..] : recordName;
}
