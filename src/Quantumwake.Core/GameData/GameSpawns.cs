namespace Quantumwake.Core.GameData;

/// <summary>One thing that can spawn in one place.</summary>
/// <param name="Deposit">The deposit the ore sits in, when the game names one.</param>
/// <param name="Kind">mineable, salvageable, cave harvestable.</param>
/// <param name="Group">The spawn group it belongs to.</param>
/// <param name="GroupChance">The group's own probability of being used.</param>
/// <param name="Share">This entry's slice within its group.</param>
public sealed record GameSpawn(
    string Resource,
    string? Deposit,
    string Kind,
    string Location,
    string? System,
    string Group,
    double GroupChance,
    double Share);

/// <summary>
/// What spawns where, read from the install rather than downloaded.
/// </summary>
/// <remarks>
/// <para>
/// A <c>HarvestableProviderPreset</c> is one place's deposit table. It holds
/// groups, each with a name and a probability, and each group holds entries
/// with a relative weight. The weight is not a percentage: it is a share of its
/// own group, so it means nothing until divided by the group's total.
/// </para>
/// <para>
/// The place is the preset's own name — <c>HPP_Stanton1a</c>,
/// <c>HPP_ShipGraveyard_001</c> — which is a designation rather than a name
/// anybody uses. The star map's own object list bridges that: a
/// <c>StarMapObject</c> of the same designation carries the localisation key
/// behind the name on the map.
/// </para>
/// <para>
/// The system is read from the designation only when the designation actually
/// says it. <c>Stanton1a</c> gives Stanton because the digit marks where the
/// system name ends; <c>AaronHalo</c> gives nothing, and is left blank rather
/// than guessed at from the places it sits between.
/// </para>
/// </remarks>
public static class GameSpawns
{
    /// <summary>Reads every deposit table, given an open blob and its English table.</summary>
    public static List<GameSpawn> Read(
        DataCore core, IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts)
    {
        var found = new List<GameSpawn>();

        var byId = new Dictionary<Guid, DataRecord>();
        var starMap = new Dictionary<string, DataRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            byId.TryAdd(record.Hash, record);

            if (record.Name.StartsWith("StarMapObject.", StringComparison.OrdinalIgnoreCase))
                starMap.TryAdd(Bare(record.Name), record);
        }

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("HarvestableProviderPreset.", StringComparison.OrdinalIgnoreCase))
                continue;

            var designation = Designation(Bare(record.Name));
            var location = Place(core, text, starMap, designation) ?? Tidied(designation);
            var system = System(core, text, starMap, designation);

            var at = core.InstanceAt(record, record.VariantIndex);

            foreach (var group in core.ClassArrayAt(at, record.StructIndex, "harvestableGroups"))
            {
                var groupAt = core.InstanceAt(group);

                var name = core.StringAt(groupAt, group.StructIndex, "groupName") ?? string.Empty;
                var chance = core.SingleAt(groupAt, group.StructIndex, "groupProbability") ?? 0;

                var entries = core.ClassArrayAt(groupAt, group.StructIndex, "harvestables");
                if (entries.Count == 0) continue;

                var weights = entries
                    .Select(e => core.SingleAt(core.InstanceAt(e), e.StructIndex, "relativeProbability") ?? 0)
                    .ToList();

                // A group whose weights are all zero says nothing about odds, so
                // the share is left even rather than divided by nothing.
                var total = weights.Sum();

                for (var i = 0; i < entries.Count; i++)
                {
                    var entryAt = core.InstanceAt(entries[i]);
                    var s = entries[i].StructIndex;

                    var preset = core.ReferenceAt(entryAt, s, "harvestable");
                    var entity = core.ReferenceAt(entryAt, s, "harvestableEntityClass");
                    var share = total > 0 ? weights[i] / total : 1.0 / entries.Count;

                    foreach (var yield in Yields(core, text, byId, facts, preset, entity, name))
                    {
                        found.Add(new GameSpawn(
                            yield.Resource, yield.Deposit, yield.Kind,
                            location, system, Tidied(name), chance, share));
                    }
                }
            }
        }

        return found;
    }

    /// <summary>
    /// What an entry yields, and what kind of thing each yield is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An entry names either a preset, which points at the entity it places, or
    /// the entity directly. The preset's own name is what says whether this is
    /// mining, salvage or a cave, since the entity is just a rock either way.
    /// </para>
    /// <para>
    /// A rock is not one resource. Its <c>MineableParams</c> point at a
    /// composition holding a deposit name and one element per ore in the mix,
    /// so a single rock is several rows, which is what the page is for. Naming
    /// the rock instead would list "MineableRock AsteroidCommon Ice" where the
    /// answer is Ice, and would undercount the table by more than half.
    /// </para>
    /// </remarks>
    private static List<(string Resource, string? Deposit, string Kind)> Yields(
        DataCore core, IReadOnlyDictionary<string, string> text, Dictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<string, GameItem> facts, Guid? preset, Guid? entity, string groupName)
    {
        // An entry with no preset still sits in a named group, and the group is
        // what says what it is - a derelict listed under Salvage_FreshDerelicts
        // is salvage whether or not a preset spelled that out.
        var kind = Kind(groupName);
        var target = entity;

        if (preset is not null && byId.TryGetValue(preset.Value, out var presetRecord))
        {
            kind = Kind(Bare(presetRecord.Name));

            var at = core.InstanceAt(presetRecord, presetRecord.VariantIndex);
            target ??= core.ReferenceAt(at, presetRecord.StructIndex, "entityClass");
        }

        if (target is null || !byId.TryGetValue(target.Value, out var record)) return [];

        var ores = Ores(core, text, byId, record);
        if (ores.Count > 0) return [.. ores.Select(o => (o.Resource, o.Deposit, "mineable"))];

        var bare = Bare(record.Name);

        var named = facts.TryGetValue(bare, out var item) && item.Name.Length > 0 && item.Name != bare
            ? item.Name
            : Tidied(bare);

        return [(named, null, kind)];
    }

    /// <summary>The ores in a rock, with the deposit they sit in.</summary>
    private static List<(string Resource, string? Deposit)> Ores(
        DataCore core, IReadOnlyDictionary<string, string> text,
        Dictionary<Guid, DataRecord> byId, DataRecord entity)
    {
        var ores = new List<(string Resource, string? Deposit)>();

        foreach (var component in core.PointerArray(entity, "Components"))
        {
            if (core.StructName(component.StructIndex) != "MineableParams") continue;

            var at = core.InstanceAt(component);
            var id = core.ReferenceAt(at, component.StructIndex, "composition");
            if (id is null || !byId.TryGetValue(id.Value, out var composition)) break;

            var compositionAt = core.InstanceAt(composition, composition.VariantIndex);

            var key = core.StringAt(compositionAt, composition.StructIndex, "depositName");
            var deposit = key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var english)
                ? english
                : null;

            foreach (var part in
                core.ClassArrayAt(compositionAt, composition.StructIndex, "compositionArray"))
            {
                var elementId = core.ReferenceAt(
                    core.InstanceAt(part), part.StructIndex, "mineableElement");

                if (elementId is null || !byId.TryGetValue(elementId.Value, out var element)) continue;

                // The same element appears more than once at different yields,
                // which is a richness band rather than a second ore.
                var name = Element(core, text, element);
                if (ores.All(o => o.Resource != name)) ores.Add((name, deposit));
            }

            break;
        }

        return ores;
    }

    /// <summary><c>MineableElement.Ice_Raw</c> is Ice.</summary>
    private static string Element(
        DataCore core, IReadOnlyDictionary<string, string> text, DataRecord element)
    {
        var at = core.InstanceAt(element, element.VariantIndex);

        if (GameItems.Localised(core, text, at, element.StructIndex) is { Length: > 0 } localised)
            return localised;

        var bare = Bare(element.Name);

        return Tidied(bare.EndsWith("_Raw", StringComparison.OrdinalIgnoreCase) ? bare[..^4] : bare);
    }

    /// <summary><c>Mining_AsteroidCommon_Ice</c> is a mineable.</summary>
    private static string Kind(string name) =>
        name.Contains("Mineable", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Mining", StringComparison.OrdinalIgnoreCase) ? "mineable"
        : name.Contains("Salvage", StringComparison.OrdinalIgnoreCase) ? "salvageable"
        : name.Contains("Cave", StringComparison.OrdinalIgnoreCase) ? "cave harvestable"
        : "harvestable";

    /// <summary>The name a player would use for a designation, or null.</summary>
    private static string? Place(
        DataCore core, IReadOnlyDictionary<string, string> text,
        Dictionary<string, DataRecord> starMap, string designation)
    {
        if (!starMap.TryGetValue(designation, out var record)) return null;

        var at = core.InstanceAt(record, record.VariantIndex);
        var key = core.StringAt(at, record.StructIndex, "name");

        return key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var english)
            && english.Length > 0
            ? english
            : null;
    }

    /// <summary>
    /// The system a designation names, when it names one.
    /// </summary>
    /// <remarks>
    /// The digit is what marks where the system name ends, so a designation
    /// with no digit is not a body in a system and gets nothing. Guessing would
    /// put the Aaron Halo in whichever system seemed likely.
    /// </remarks>
    private static string? System(
        DataCore core, IReadOnlyDictionary<string, string> text,
        Dictionary<string, DataRecord> starMap, string designation)
    {
        var digit = designation.IndexOfAny(['0', '1', '2', '3', '4', '5', '6', '7', '8', '9']);
        if (digit <= 0) return null;

        var prefix = designation[..digit];

        // Only a run of plain letters is a system name. Anything carrying an
        // underscore is a named feature rather than a body, and cutting it at
        // the digit invents systems called Pyro_Cool and ShipGraveyard.
        if (!prefix.All(char.IsLetter)) return null;

        // The designation says the system outright, so the star map is only
        // asked to spell it more nicely, not to supply it.
        return Place(core, text, starMap, prefix) ?? prefix;
    }

    /// <summary><c>HPP_Stanton1a</c> is the designation <c>Stanton1a</c>.</summary>
    private static string Designation(string presetName) =>
        presetName.StartsWith("HPP_", StringComparison.OrdinalIgnoreCase)
            ? presetName["HPP_".Length..]
            : presetName;

    private static string Tidied(string name) => name.Replace('_', ' ').Trim();

    private static string Bare(string recordName) =>
        recordName.Contains('.') ? recordName[(recordName.LastIndexOf('.') + 1)..] : recordName;
}
