namespace Quantumwake.Core.GameData;

/// <summary>One thing that can spawn in one place.</summary>
/// <param name="Deposit">The deposit the ore sits in, when the game names one.</param>
/// <param name="MinPercent">The least of the rock this ore makes up, or null.</param>
/// <param name="MaxPercent">The most of it, or null.</param>
/// <param name="Kind">
/// mineable, salvageable, cave_harvestable - worded exactly as the community
/// download words them, so one filter and one set of labels serve both sources.
/// </param>
/// <param name="Group">The spawn group it belongs to.</param>
/// <param name="GroupChance">
/// The group's share of what spawns at that place, from 0 to 1.
/// </param>
/// <param name="Share">This entry's slice within its group.</param>
public sealed record GameSpawn(
    string Resource,
    string? Deposit,
    double? MinPercent,
    double? MaxPercent,
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
/// The system is read from the designation only when the designation is plainly
/// a body in one: letters then a digit, as in <c>Stanton1a</c> or
/// <c>Pyro5e</c>. Anything else — <c>AaronHalo</c>, <c>Pyro_Cool02</c>,
/// <c>ShipGraveyard_001</c> — is left blank. Splitting those at the first digit
/// produced systems called "Pyro_Cool" and "ShipGraveyard", which is worse than
/// saying nothing. Cave tables name their place outright and get their system
/// from the mission templates instead.
/// </para>
/// <para>
/// This table is not the equal of the community download: 1,321 rows against
/// 2,642, and 50 places against 234. The cave tables are read and land within
/// two of the download's own count of them, so the shortfall is elsewhere —
/// places whose tables are bound to them somewhere this does not reach. It
/// stands as what to show when there is no download, not as a replacement for
/// one.
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
        var caves = new List<DataRecord>();

        // A place's own name does not say which system it is in, but the mission
        // templates pair the two: Planet_Stanton1b_Aberdeen is Aberdeen, and the
        // designation beside it says where Aberdeen is.
        var systemOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            byId.TryAdd(record.Hash, record);

            if (record.Name.StartsWith("StarMapObject.", StringComparison.OrdinalIgnoreCase))
                starMap.TryAdd(Bare(record.Name), record);

            if (record.Name.StartsWith("SubHarvestableConfigRecord.Cave_", StringComparison.OrdinalIgnoreCase))
                caves.Add(record);

            if (record.Name.StartsWith("MissionLocationTemplate.Planet_", StringComparison.OrdinalIgnoreCase))
            {
                var parts = Bare(record.Name).Split('_');
                if (parts.Length >= 3) systemOf.TryAdd(parts[^1], parts[1]);
            }
        }

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("HarvestableProviderPreset.", StringComparison.OrdinalIgnoreCase))
                continue;

            var designation = Designation(Bare(record.Name));
            var location = Place(core, text, starMap, designation) ?? Tidied(designation);
            var system = System(core, text, starMap, designation);

            var at = core.InstanceAt(record, record.VariantIndex);
            var groups = core.ClassArrayAt(at, record.StructIndex, "harvestableGroups");

            // groupProbability is a weight, not a probability, and its scale is
            // the preset's own business: one place's groups sum to 0.196 and
            // another's to 90. Read as a chance they print as 2500%, so they are
            // normalised into each group's share of what spawns at that place.
            var budget = groups
                .Sum(g => core.SingleAt(core.InstanceAt(g), g.StructIndex, "groupProbability") ?? 0);

            foreach (var group in groups)
            {
                var groupAt = core.InstanceAt(group);

                var name = core.StringAt(groupAt, group.StructIndex, "groupName") ?? string.Empty;
                var weight = core.SingleAt(groupAt, group.StructIndex, "groupProbability") ?? 0;
                var chance = budget > 0 ? weight / budget : 0;

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
                            yield.Resource, yield.Deposit, yield.Min, yield.Max, yield.Kind,
                            location, system, Tidied(name), chance, share));
                    }
                }
            }
        }

        Caves(core, text, byId, facts, starMap, systemOf, caves, found);

        return found;
    }

    /// <summary>
    /// The cave tables, which are the same shape one level in.
    /// </summary>
    /// <remarks>
    /// A cave config is named for where it is and how rich it is —
    /// <c>Cave_Aberdeen_Rich</c> — and holds slots rather than groups, each
    /// naming a harvestable and its weight. The place is already a name here
    /// rather than a designation, so the star map is not needed; the system is,
    /// and comes from the mission templates.
    /// </remarks>
    private static void Caves(
        DataCore core, IReadOnlyDictionary<string, string> text, Dictionary<Guid, DataRecord> byId,
        IReadOnlyDictionary<string, GameItem> facts, Dictionary<string, DataRecord> starMap,
        Dictionary<string, string> systemOf, List<DataRecord> caves, List<GameSpawn> found)
    {
        foreach (var record in caves)
        {
            var parts = Bare(record.Name).Split('_');
            if (parts.Length < 2) continue;

            var location = parts[1];
            var richness = parts.Length > 2 ? string.Join(" ", parts[2..]) : "Cave";

            var system = systemOf.TryGetValue(location, out var designation)
                ? System(core, text, starMap, designation)
                : null;

            var (at, field) = core.FieldAt(
                core.InstanceAt(record, record.VariantIndex), record.StructIndex, "subConfig");

            if (at < 0 || field is null) continue;

            var chance = core.SingleAt(at, field.StructIndex, "initialSlotsProbability") ?? 0;
            var slots = core.ClassArrayAt(at, field.StructIndex, "subHarvestables");
            if (slots.Count == 0) continue;

            var weights = slots
                .Select(e => core.SingleAt(core.InstanceAt(e), e.StructIndex, "relativeProbability") ?? 0)
                .ToList();

            var total = weights.Sum();

            for (var i = 0; i < slots.Count; i++)
            {
                var slotAt = core.InstanceAt(slots[i]);
                var preset = core.ReferenceAt(slotAt, slots[i].StructIndex, "harvestable");
                var share = total > 0 ? weights[i] / total : 1.0 / slots.Count;

                foreach (var yield in Yields(core, text, byId, facts, preset, null, "Cave"))
                {
                    found.Add(new GameSpawn(
                        yield.Resource, yield.Deposit, yield.Min, yield.Max, "cave_harvestable",
                        location, system, $"Cave {richness}", chance, share));
                }
            }
        }
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
    private static List<(string Resource, string? Deposit, double? Min, double? Max, string Kind)> Yields(
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
        if (ores.Count > 0)
            return [.. ores.Select(o => (o.Resource, o.Deposit, (double?)o.Min, (double?)o.Max, "mineable"))];

        var bare = Bare(record.Name);

        var named = facts.TryGetValue(bare, out var item) && item.Name.Length > 0 && item.Name != bare
            ? item.Name
            : Tidied(bare);

        return [(named, null, (double?)null, (double?)null, kind)];
    }

    /// <summary>
    /// The ores in a rock, with the deposit they sit in and how much of it they
    /// make up.
    /// </summary>
    /// <remarks>
    /// An ore appears once per richness band - ice is 9.7 to 15.7 per cent of
    /// one kind of rock and 34.3 to 84.3 per cent of another - and the bands are
    /// spanned rather than listed. A row per band would say the same ore is here
    /// twice at different odds, which it is not: it is here once, and how much
    /// of it you get varies.
    /// </remarks>
    private static List<(string Resource, string? Deposit, double Min, double Max)> Ores(
        DataCore core, IReadOnlyDictionary<string, string> text,
        Dictionary<Guid, DataRecord> byId, DataRecord entity)
    {
        var ores = new List<(string Resource, string? Deposit, double Min, double Max)>();

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

                var name = Element(core, text, element);
                var partAt = core.InstanceAt(part);
                var low = core.SingleAt(partAt, part.StructIndex, "minPercentage") ?? 0;
                var high = core.SingleAt(partAt, part.StructIndex, "maxPercentage") ?? 0;

                var seen = ores.FindIndex(o => o.Resource == name);

                if (seen < 0) ores.Add((name, deposit, low, high));
                else
                {
                    ores[seen] = (name, deposit,
                        Math.Min(ores[seen].Min, low), Math.Max(ores[seen].Max, high));
                }
            }

            break;
        }

        return ores;
    }

    /// <summary>
    /// What an element is called: <c>Ice_Raw</c> is Ice, and
    /// <c>MinableElement_FPS_Aphorite</c> is Aphorite.
    /// </summary>
    /// <remarks>
    /// The bookkeeping around the name is the game's, not the player's. Left in
    /// place the cave tables listed "MinableElement FPS Aphorite" where every
    /// other row on the page said Aphorite, and the two would not have looked
    /// like the same ore.
    /// </remarks>
    private static string Element(
        DataCore core, IReadOnlyDictionary<string, string> text, DataRecord element)
    {
        var at = core.InstanceAt(element, element.VariantIndex);

        if (GameItems.Localised(core, text, at, element.StructIndex) is { Length: > 0 } localised)
            return localised;

        var bare = Bare(element.Name);

        foreach (var prefix in new[] { "MinableElement_", "MineableElement_", "FPS_" })
        {
            while (bare.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                bare = bare[prefix.Length..];
        }

        if (bare.EndsWith("_Raw", StringComparison.OrdinalIgnoreCase)) bare = bare[..^4];

        return Tidied(bare);
    }

    /// <summary><c>Mining_AsteroidCommon_Ice</c> is a mineable.</summary>
    private static string Kind(string name) =>
        name.Contains("Mineable", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Mining", StringComparison.OrdinalIgnoreCase) ? "mineable"
        : name.Contains("Salvage", StringComparison.OrdinalIgnoreCase) ? "salvageable"
        : name.Contains("Cave", StringComparison.OrdinalIgnoreCase) ? "cave_harvestable"
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
