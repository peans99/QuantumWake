namespace Quantumwake.Core.GameData;

/// <summary>One item a loot table can roll, and how often.</summary>
/// <param name="ItemClass">The entity class, as the logs and the catalogue name it.</param>
/// <param name="PerRoll">
/// The chance one roll of the table lands on this item, from 0 to 1. An
/// estimate - <see cref="LootRoll"/> lists what the files leave unsaid.
/// </param>
/// <param name="Entry">The table entry it came through - "Small weapons".</param>
/// <param name="Choice">The archetype entry within that - "Pistols".</param>
public sealed record LootOdds(string ItemClass, double PerRoll, string Entry, string Choice);

/// <summary>One loot table, resolved down to items.</summary>
/// <param name="Name">The record name without its type prefix - <c>V3LootTable_Weapons_Small</c>.</param>
/// <param name="Items">Every item it can roll, likeliest first.</param>
/// <param name="Unfilled">
/// Entries nothing in this install can fill, by name - mission-item slots,
/// and anything a patch broke by renaming a tag. Kept so the second shows up
/// as a gap rather than as quietly different odds.
/// </param>
public sealed record LootTable(string Name, IReadOnlyList<LootOdds> Items, IReadOnlyList<string> Unfilled);

/// <summary>One thing in the game that rolls a loot table.</summary>
/// <param name="Kind"><c>slot</c> for a container a location preset places, <c>body</c> for an NPC or corpse.</param>
/// <param name="Preset">
/// The slot preset record a location names (<c>V3SlotPreset_DistributionCentres_HighSecurtiy</c>),
/// or for a body the NPC's own entity class.
/// </param>
/// <param name="Group">The tagged group within a preset - "Weapons Rack LMG Common" - or empty.</param>
/// <param name="Container">The entity class that holds the loot: the crate, or the body.</param>
/// <param name="SlotChance">
/// How often a slot of this group holds this container when the location
/// spawns: the group's initial-slot probability times this container's share
/// of the relative weights. 1 for a body.
/// </param>
/// <param name="RespawnSeconds">The crate's slot timer with both multipliers applied, or null.</param>
/// <param name="Table">The table's short name, as <see cref="LootTable.Name"/> has it.</param>
/// <param name="PoolFilter">The container's pool-filter record, or null.</param>
/// <param name="SecondaryChoices">The container's secondary-choice record, or null.</param>
/// <param name="ChanceToGenerate">The chance the container generates loot at all.</param>
/// <param name="Themes">
/// The 5.0 loot themes the container rolls under, short record names in the
/// order they apply, joined by <c> + </c>; null on an install without themes.
/// </param>
public sealed record LootSource(
    string Kind, string Preset, string Group, string Container, double SlotChance, int? RespawnSeconds,
    string Table, string? PoolFilter, string? SecondaryChoices, double ChanceToGenerate, string? Themes = null)
{
    /// <summary>The table and constraints, which is what decides the odds.</summary>
    /// <remarks>Themes join the key only when there are any, so a 4.10 install keys exactly as it did.</remarks>
    public string RollKey => Themes is null
        ? $"{Table}|{PoolFilter}|{SecondaryChoices}"
        : $"{Table}|{PoolFilter}|{SecondaryChoices}|{Themes}";
}

/// <summary>
/// The loot tables, read from the install.
/// </summary>
/// <remarks>
/// <para>
/// The chain, as Alpha 4.10's <c>Game2.dcb</c> stores it: a
/// <c>LootTableV3Record</c> is a weighted list of entries, each pointing at an
/// archetype by record (126 on this install) or written inline (71). An
/// archetype is another weighted list whose entries <i>select</i> items - by
/// tag (248 selectors) or by naming classes outright (32). Entities carry
/// their tags as a <c>tags</c> array on <c>EntityClassDefinition</c>, and a tag
/// counts for each of its ancestors in the tag tree: a pistol magazine is
/// tagged beneath <c>Pistol</c>, not with <c>Pistol</c>, and is still pistol
/// ammo. Matching flat, every ammo selector on the install came up empty.
/// </para>
/// <para>
/// The container decides the rest through its <c>LootConstraints</c>: a pool
/// filter that strikes items out (faction gear, event gear) and secondary
/// choices that draw a band first - Common 100, Uncommon 10, Rare 1, Epic 0.1
/// in the common-container layer. Both apply when the caller names them.
/// </para>
/// </remarks>
public sealed class GameLoot
{
    private readonly DataCore _core;
    private readonly Dictionary<Guid, DataRecord> _byHash = [];
    private readonly Dictionary<Guid, string> _classByHash = [];
    private readonly List<LootCandidate> _entities = [];
    private readonly Dictionary<string, LootCandidate> _byClass = new(StringComparer.Ordinal);
    private readonly List<Guid> _themeOrder = [];

    public GameLoot(DataCore core)
    {
        _core = core;
        var parents = new Dictionary<Guid, Guid>();

        foreach (var record in core.Records())
        {
            _byHash.TryAdd(record.Hash, record);

            // The order themes apply in, by category: HCDSafetyNet, Event,
            // Rarity, Faction, Flavour, ContentType on 5.0. Absent before it.
            if (core.StructName(record.StructIndex) == "LootGenerationGlobalParams")
            {
                var (order, field) = core.FieldAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "themeCategoryOrdering");
                if (order >= 0 && field is not null) _themeOrder.AddRange(core.ReferenceArrayAt(order, field.StructIndex, "tags"));
            }

            if (record.Name.StartsWith("Tag.", StringComparison.Ordinal))
            {
                foreach (var child in core.ReferenceArrayAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "children"))
                    parents.TryAdd(child, record.Hash);
            }
        }

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;

            var name = record.Name["EntityClassDefinition.".Length..];
            _classByHash.TryAdd(record.Hash, name);

            var tags = core.ReferenceArrayAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "tags");
            var candidate = new LootCandidate(name, WithAncestors(tags, parents));
            _byClass.TryAdd(name, candidate);
            if (tags.Count > 0) _entities.Add(candidate);
        }
    }

    private static HashSet<Guid> WithAncestors(IEnumerable<Guid> tags, Dictionary<Guid, Guid> parents)
    {
        var all = new HashSet<Guid>();

        foreach (var tag in tags)
        {
            // all.Add fails on a tag already walked, which also ends a cycle.
            for (var t = tag; all.Add(t) && parents.TryGetValue(t, out var up);)
                t = up;
        }

        return all;
    }

    /// <summary>
    /// Every table in the install, V3 and the older kind alike: 767 of the
    /// 2,088 loot slots on this install still point at an older table.
    /// </summary>
    public IEnumerable<DataRecord> Tables() =>
        _core.Records().Where(r => _core.StructName(r.StructIndex) is "LootTableV3Record" or "LootTable");

    private Dictionary<string, DataRecord>? _byName;
    private Dictionary<string, DataRecord>? _tables;

    /// <summary>The record a reference names, or null.</summary>
    public DataRecord? Record(Guid id) => _byHash.GetValueOrDefault(id);

    /// <summary>A source's table resolved through its own constraints, or null when the table is gone.</summary>
    public LootTable? Resolve(LootSource source)
    {
        _tables ??= Tables().GroupBy(t => Short(t.Name), StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        if (!_tables.TryGetValue(source.Table, out var table)) return null;

        return Resolve(table, Named(source.PoolFilter), Named(source.SecondaryChoices), ThemeIds(source));
    }

    private Guid? Named(string? name)
    {
        _byName ??= _byHash.Values.GroupBy(r => r.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        return name is not null && _byName.TryGetValue(name, out var r) ? r.Hash : null;
    }

    private List<Guid> ThemeIds(LootSource source) => source.Themes is null ? []
        : [.. source.Themes.Split(" + ").Select(n => Named("LootThemeRecord." + n)).OfType<Guid>()];

    /// <summary>
    /// A preset's sources as they roll in one faction's land: each keeps its
    /// own themes and takes the land's, under <paramref name="key"/> as its
    /// preset so the places that land holds are its own.
    /// </summary>
    /// <param name="land">The land's preset record, short - <c>Faction_HCD_Shubin</c>.</param>
    /// <remarks>
    /// A land is a <c>HierarchicalContextualDataPresetRecord</c>, and the part
    /// loot reads is its <c>LocationLootGenerationContextData</c>: a faction
    /// theme and three flavours (wealth, tech, lawfulness). Its
    /// <c>parentThemesToIgnore</c> names the Faction and Flavour categories,
    /// which reads as the lands above it - <see cref="GameLootPlaces.LandOf"/>
    /// takes the nearest. Whether it also strikes a crate's own faction or
    /// flavour is untested: on 5.0 PTU the one preset placed in a land,
    /// <c>V3SlotPreset_Loot_Generic_HCD</c>, carries neither, only the safety
    /// net and a rarity.
    /// </remarks>
    public IEnumerable<LootSource> OnLand(IEnumerable<LootSource> sources, string land, string key)
    {
        if (Named("HierarchicalContextualDataPresetRecord." + land) is not { } id || Record(id) is not { } record) return [];

        var themes = new List<Guid>();
        foreach (var hcd in _core.PointerArrayAt(_core.InstanceAt(record, record.VariantIndex), record.StructIndex, "HCDs"))
            if (_core.StructName(hcd.StructIndex) == "LocationLootGenerationContextData")
                themes.AddRange(_core.ReferenceArrayAt(_core.InstanceAt(hcd), hcd.StructIndex, "themes"));

        if (themes.Count == 0) return [];
        return [.. sources.Select(s => s with { Preset = key, Themes = ThemeNames([.. ThemeIds(s), .. themes]) })];
    }

    /// <summary>
    /// Everything that rolls a table: every container a slot preset can put
    /// in a slot, and every NPC or corpse that carries a loot component.
    /// </summary>
    /// <remarks>
    /// A slot's container is a <c>HarvestablePreset</c> - the crate's entity
    /// class and its <c>respawnInSlotTime</c> (1,200 s on the outpost crates) -
    /// and the slot adds its own <c>lootConfig</c>, so the same crate rolls
    /// food in one preset and rifles in another. Slots without a table are
    /// mineables and plants, which are the item themselves, and are left out.
    /// </remarks>
    public IEnumerable<LootSource> Sources()
    {
        foreach (var record in _byHash.Values)
        {
            var at = _core.InstanceAt(record, record.VariantIndex);
            var structName = _core.StructName(record.StructIndex);

            if (structName == "SubHarvestableConfigRecord")
            {
                var (config, field) = _core.FieldAt(at, record.StructIndex, "subConfig");
                if (config >= 0 && field is not null)
                    foreach (var s in SlotSources(Short(record.Name), "", config, field.StructIndex, []))
                        yield return s;
            }
            else if (structName == "SubHarvestableMultiConfigRecord")
            {
                var (multi, field) = _core.FieldAt(at, record.StructIndex, "multiConfig");
                if (multi < 0 || field is null) continue;

                var presetThemes = _core.ReferenceArrayAt(multi, field.StructIndex, "lootThemes");

                foreach (var tagged in _core.ClassArrayAt(multi, field.StructIndex, "taggedConfigs"))
                {
                    var t = _core.InstanceAt(tagged);
                    var group = _core.StringAt(t, tagged.StructIndex, "name") ?? "";
                    if (_core.PointerAt(t, tagged.StructIndex, "subConfig") is not { } sub) continue;

                    var s = _core.InstanceAt(sub);
                    long config = -1;
                    int configStruct = -1;

                    if (_core.StructName(sub.StructIndex) == "SubHarvestableConfigSingleManual")
                    {
                        var (c, f) = _core.FieldAt(s, sub.StructIndex, "subConfigManual");
                        if (f is not null) (config, configStruct) = (c, f.StructIndex);
                    }
                    else if (_core.ReferenceAt(s, sub.StructIndex, "subConfigRef") is { } id && Record(id) is { } single)
                    {
                        var (c, f) = _core.FieldAt(_core.InstanceAt(single, single.VariantIndex), single.StructIndex, "subConfig");
                        if (f is not null) (config, configStruct) = (c, f.StructIndex);
                    }

                    if (config >= 0)
                        foreach (var source in SlotSources(Short(record.Name), group, config, configStruct, presetThemes))
                            yield return source;
                }
            }
            else if (structName == "EntityClassDefinition")
            {
                foreach (var component in _core.PointerArrayAt(at, record.StructIndex, "Components"))
                {
                    if (_core.StructName(component.StructIndex) != "LootGenerationComponentParams") continue;

                    var (config, field) = _core.FieldAt(_core.InstanceAt(component), component.StructIndex, "lootConfig");
                    if (config < 0 || field is null) continue;

                    var name = Short(record.Name);
                    if (Roll(config, field.StructIndex) is { } roll)
                        yield return new LootSource("body", name, "", name, 1, null,
                            roll.Table, roll.Filter, roll.Secondary, roll.Chance, ThemeNames(roll.Themes));
                }
            }
        }
    }

    private IEnumerable<LootSource> SlotSources(string preset, string group, long config, int configStruct, IReadOnlyList<Guid> outer)
    {
        // A preset's themes, then the tagged config's, then the slot's own:
        // V3SlotPreset_Loot_Generic_HCD carries the safety net, a tagged config
        // a rarity, a slot its constraints' rarity.
        IReadOnlyList<Guid> themes = [.. outer, .. _core.ReferenceArrayAt(config, configStruct, "lootThemes")];
        var initial = _core.SingleAt(config, configStruct, "initialSlotsProbability") ?? 1;
        var configRespawn = _core.SingleAt(config, configStruct, "configRespawnTimeMultiplier") ?? 1;
        var slots = _core.ClassArrayAt(config, configStruct, "subHarvestables");
        var total = slots.Sum(s => _core.SingleAt(_core.InstanceAt(s), s.StructIndex, "relativeProbability") ?? 0);

        foreach (var slot in slots)
        {
            var at = _core.InstanceAt(slot);
            if (_core.PointerAt(at, slot.StructIndex, "lootConfig") is not { } loot) continue;
            if (Roll(_core.InstanceAt(loot), loot.StructIndex) is not { } roll) continue;

            var relative = _core.SingleAt(at, slot.StructIndex, "relativeProbability") ?? 0;
            var slotRespawn = _core.SingleAt(at, slot.StructIndex, "harvestableRespawnTimeMultiplier") ?? 1;

            string? container = null;
            int? respawn = null;

            if (_core.ReferenceAt(at, slot.StructIndex, "harvestable") is { } presetId && Record(presetId) is { } p)
            {
                var pa = _core.InstanceAt(p, p.VariantIndex);
                container = _core.ReferenceAt(pa, p.StructIndex, "entityClass") is { } e ? _classByHash.GetValueOrDefault(e) : null;
                container ??= Short(p.Name);

                if (_core.SingleAt(pa, p.StructIndex, "respawnInSlotTime") is { } seconds and > 0)
                    respawn = (int)Math.Round(seconds * configRespawn * slotRespawn);
            }
            else if (_core.ReferenceAt(at, slot.StructIndex, "harvestableEntityClass") is { } entity)
            {
                container = _classByHash.GetValueOrDefault(entity);

                // The V3 boxes name a HarvestableSetup instead of a preset. On
                // this install every one reads respawnInSlotTime 0 - the "30M"
                // in V3HarvestableSetup_Lootbox_30M is the despawn after a box
                // is touched, not a refill - so null is the honest answer.
                if (_core.ReferenceAt(at, slot.StructIndex, "harvestableSetup") is { } setupId && Record(setupId) is { } setup
                    && _core.SingleAt(_core.InstanceAt(setup, setup.VariantIndex), setup.StructIndex, "respawnInSlotTime") is { } seconds and > 0)
                    respawn = (int)Math.Round(seconds * configRespawn * slotRespawn);
            }

            yield return new LootSource(
                "slot", preset, group, container ?? "?",
                total > 0 ? initial * relative / total : 0,
                respawn, roll.Table, roll.Filter, roll.Secondary, roll.Chance, ThemeNames([.. themes, .. roll.Themes]));
        }
    }

    /// <summary>
    /// What a <c>LootConfig</c> rolls: the V3 table when it names one, else the
    /// older one, and the constraints that go with it.
    /// </summary>
    private (string Table, string? Filter, string? Secondary, double Chance, IReadOnlyList<Guid> Themes)? Roll(long config, int configStruct)
    {
        var table = _core.ReferenceAt(config, configStruct, "lootTableV3") is { } v3 && Record(v3) is { } r3 ? r3
            : _core.ReferenceAt(config, configStruct, "lootTable") is { } old && Record(old) is { } r ? r
            : null;
        if (table is null) return null;

        string? filter = null, secondary = null;
        var chance = 1.0;
        IReadOnlyList<Guid> themes = [];

        var (constraints, field) = _core.FieldAt(config, configStruct, "lootConstraints");
        if (constraints >= 0 && field is not null)
        {
            var cs = field.StructIndex;
            chance = _core.SingleAt(constraints, cs, "chanceToGenerate") ?? 1;
            themes = _core.ReferenceArrayAt(constraints, cs, "themes");

            if (_core.ReferenceAt(constraints, cs, "poolFilter") is { } f && Record(f) is { } fr)
                filter = fr.Name;

            if (_core.PointerAt(constraints, cs, "secondaryChoices") is { } sc)
            {
                var sa = _core.InstanceAt(sc);
                var id = _core.ReferenceAt(sa, sc.StructIndex, "multiLayerRecord")
                    ?? _core.ReferenceAt(sa, sc.StructIndex, "singleLayerRecord");
                if (id is { } g && Record(g) is { } sr) secondary = sr.Name;
            }
        }

        return (Short(table.Name), filter, secondary, chance, themes);
    }

    /// <summary>
    /// Themes from every level, each once, in the order the game applies them:
    /// by category as <c>themeCategoryOrdering</c> lists them, outer levels
    /// first within a category. Null when there are none, which is every
    /// container before 5.0.
    /// </summary>
    private string? ThemeNames(IReadOnlyList<Guid> themes)
    {
        if (themes.Count == 0) return null;

        var named = themes.Distinct()
            .Select((id, i) => (Record: Record(id), At: i))
            .Where(t => t.Record is not null)
            .OrderBy(t => CategoryRank(t.Record!))
            .ThenBy(t => t.At)
            .Select(t => Short(t.Record!.Name))
            .ToList();

        return named.Count == 0 ? null : string.Join(" + ", named);
    }

    private int CategoryRank(DataRecord theme)
    {
        var rank = Category(theme) is { } c ? _themeOrder.IndexOf(c) : -1;
        return rank < 0 ? int.MaxValue : rank;
    }

    /// <summary>A theme's category: the first tag on its <c>LootTheme_Manual</c>.</summary>
    private Guid? Category(DataRecord theme)
    {
        if (ThemeBody(theme) is not { } body) return null;
        var (list, field) = _core.FieldAt(_core.InstanceAt(body), body.StructIndex, "tags");
        return list >= 0 && field is not null && _core.ReferenceArrayAt(list, field.StructIndex, "tags") is { Count: > 0 } tags
            ? tags[0] : null;
    }

    /// <summary>
    /// The written-out theme. Every one of the 51 on 5.0 PTU is a
    /// <c>LootTheme_Manual</c>; <c>LootTheme_RecordRef</c> has no instance on
    /// this build and is left unread rather than guessed at.
    /// </summary>
    private DataCore.Pointer? ThemeBody(DataRecord theme) =>
        _core.PointerAt(_core.InstanceAt(theme, theme.VariantIndex), theme.StructIndex, "theme") is { } body
        && _core.StructName(body.StructIndex) == "LootTheme_Manual" ? body : null;

    /// <summary>A theme's filter steps and choice layers, its layers stamped with its category.</summary>
    private (List<LootFilterStep> Steps, List<IReadOnlyList<LootBand>> Layers) Theme(Guid id)
    {
        if (Record(id) is not { } record || ThemeBody(record) is not { } body) return ([], []);
        var at = _core.InstanceAt(body);
        var category = Category(record);

        var (filters, filtersField) = _core.FieldAt(at, body.StructIndex, "poolFilters");
        var steps = filters >= 0 && filtersField is not null ? SequenceSteps(filters, filtersField.StructIndex, 0) : [];

        var layers = new List<IReadOnlyList<LootBand>>();
        foreach (var layer in _core.PointerArrayAt(at, body.StructIndex, "secondaryChoices"))
        {
            var l = _core.InstanceAt(layer);
            switch (_core.StructName(layer.StructIndex))
            {
                case "LootV3SecondaryChoicesLayer":
                    layers.Add(Bands(l, layer.StructIndex, category));
                    break;
                case "LootV3SecondaryChoiceLayer_RecordRef"
                    when _core.ReferenceAt(l, layer.StructIndex, "singleLayerRecord") is { } single && Record(single) is { } sr:
                    layers.Add(Bands(_core.InstanceAt(sr, sr.VariantIndex), sr.StructIndex, category));
                    break;
            }
        }

        return (steps, layers);
    }

    /// <summary>A table resolved to items, through a container's constraints when given.</summary>
    public LootTable Resolve(DataRecord table, Guid? poolFilter = null, Guid? secondaryChoices = null, IReadOnlyList<Guid>? themes = null)
    {
        var steps = poolFilter is { } f ? FilterTerms(f, 0) : [];
        var layers = secondaryChoices is { } s ? Layers(s, 0) : [];

        // The container's own constraints first, then each theme in order.
        // No 5.0 container names both, so which goes first is not tested by
        // the files: 2,071 of 2,071 PTU constraints leave secondaryChoices null.
        foreach (var theme in themes ?? [])
        {
            var (themeSteps, themeLayers) = Theme(theme);
            steps.AddRange(themeSteps);
            layers.AddRange(themeLayers);
        }

        var (odds, unfilled) = LootRoll.Filtered(Entries(table), steps, layers);

        return new LootTable(Short(table.Name), odds, unfilled);
    }

    private static string Short(string recordName) => recordName[(recordName.IndexOf('.') + 1)..];

    private List<LootEntry> Entries(DataRecord table)
    {
        if (_core.StructName(table.StructIndex) == "LootTable") return LegacyEntries(table);

        var body = _core.PointerAt(_core.InstanceAt(table, table.VariantIndex), table.StructIndex, "lootTable");
        if (body is not { } b) return [];

        var entries = new List<LootEntry>();
        foreach (var (name, weight, at, structIndex) in Weighted(b, "lootArchetypes"))
        {
            var archetype = Archetype(at, structIndex);
            var choices = archetype is { } a
                ? Weighted(a, "entries").Select(c => new LootChoice(c.Name, c.Weight, Pool(c.At, c.Struct))).ToList()
                : [];

            entries.Add(new LootEntry(name, weight, choices));
        }

        return entries;
    }

    /// <summary>
    /// An older table. Same idea in different fields: weighted references to
    /// <c>LootArchetype</c> records, whose primary entries each select by one
    /// tag plus a DNF term, whose <c>excludedTags</c> strike items out for that
    /// archetype alone, and whose secondary groups ("Rarity") are bands the
    /// archetype draws itself.
    /// </summary>
    private List<LootEntry> LegacyEntries(DataRecord table)
    {
        var entries = new List<LootEntry>();
        var at = _core.InstanceAt(table, table.VariantIndex);

        foreach (var weighted in _core.ClassArrayAt(at, table.StructIndex, "lootArchetypes"))
        {
            var w = _core.InstanceAt(weighted);
            var weight = _core.SingleAt(w, weighted.StructIndex, "weight") ?? 0;

            if (_core.ReferenceAt(w, weighted.StructIndex, "archetype") is not { } id || Record(id) is not { } record)
            {
                entries.Add(new LootEntry("(missing archetype)", weight, []));
                continue;
            }

            var a = _core.InstanceAt(record, record.VariantIndex);
            var s = record.StructIndex;

            var (excludedAt, excludedField) = _core.FieldAt(a, s, "excludedTags");
            var excluded = excludedAt >= 0 && excludedField is not null
                ? _core.ReferenceArrayAt(excludedAt, excludedField.StructIndex, "tags").ToHashSet()
                : [];

            var choices = new List<LootChoice>();
            var (primaryAt, primaryField) = _core.FieldAt(a, s, "primaryOrGroup");
            if (primaryAt >= 0 && primaryField is not null)
            {
                foreach (var entry in _core.ClassArrayAt(primaryAt, primaryField.StructIndex, "entries"))
                {
                    var e = _core.InstanceAt(entry);
                    var extra = TermAt(e, entry.StructIndex, "additionalTags");
                    var positive = extra.Positive.ToHashSet();
                    if (_core.ReferenceAt(e, entry.StructIndex, "tag") is { } tag) positive.Add(tag);

                    var term = new LootTerm(positive, extra.Negative);
                    choices.Add(new LootChoice(
                        _core.StringAt(e, entry.StructIndex, "name") ?? "",
                        _core.SingleAt(e, entry.StructIndex, "weight") ?? 0,
                        positive.Count == 0
                            ? []
                            : [.. _entities.Where(c => term.Matches(c.Tags) && !c.Tags.Overlaps(excluded))]));
                }
            }

            var layers = new List<IReadOnlyList<LootBand>>();
            foreach (var group in _core.ClassArrayAt(a, s, "secondaryOrGroups"))
            {
                var g = _core.InstanceAt(group);
                var groupName = _core.StringAt(g, group.StructIndex, "groupName") ?? "";

                layers.Add([.. _core.ClassArrayAt(g, group.StructIndex, "entries").Select(band =>
                {
                    var b = _core.InstanceAt(band);
                    var tag = _core.ReferenceAt(b, band.StructIndex, "tag");
                    return new LootBand(
                        groupName,
                        _core.SingleAt(b, band.StructIndex, "weight") ?? 0,
                        new LootTerm(tag is { } t ? new HashSet<Guid> { t } : new HashSet<Guid>(), new HashSet<Guid>()));
                })]);
            }

            entries.Add(new LootEntry(LootLabels.Preset(Short(record.Name)), weight, choices, layers));
        }

        return entries;
    }

    /// <summary>A weighted list: a table's entries or an archetype's.</summary>
    private IEnumerable<(string Name, double Weight, long At, int Struct)> Weighted(DataCore.Pointer owner, string field)
    {
        foreach (var item in _core.ClassArrayAt(_core.InstanceAt(owner), owner.StructIndex, field))
        {
            var at = _core.InstanceAt(item);
            yield return (
                _core.StringAt(at, item.StructIndex, "name") ?? "",
                _core.SingleAt(at, item.StructIndex, "weight") ?? 0,
                at,
                item.StructIndex);
        }
    }

    private DataCore.Pointer? Archetype(long entry, int entryStruct)
    {
        if (_core.PointerAt(entry, entryStruct, "archetype") is not { } p) return null;
        if (_core.StructName(p.StructIndex) != "LootArchetypeV3_RecordRef") return p;

        return _core.ReferenceAt(_core.InstanceAt(p), p.StructIndex, "lootArchetypeRecord") is { } id
            && Record(id) is { } record
            ? _core.PointerAt(_core.InstanceAt(record, record.VariantIndex), record.StructIndex, "lootArchetype")
            : null;
    }

    private LootTerm TermAt(long instance, int structIndex, string field)
    {
        var (at, prop) = _core.FieldAt(instance, structIndex, field);
        if (at < 0 || prop is null) return new LootTerm(new HashSet<Guid>(), new HashSet<Guid>());

        return new LootTerm(
            _core.ReferenceArrayAt(at, prop.StructIndex, "positiveTags").ToHashSet(),
            _core.ReferenceArrayAt(at, prop.StructIndex, "negativeTags").ToHashSet());
    }

    private List<LootCandidate> Pool(long choice, int choiceStruct)
    {
        if (_core.PointerAt(choice, choiceStruct, "selector") is not { } s) return [];
        var at = _core.InstanceAt(s);

        switch (_core.StructName(s.StructIndex))
        {
            case "LootArchetypeV3Selector_Tags":
            {
                var term = TermAt(at, s.StructIndex, "tags");

                // A selector with only negative tags would take the whole game;
                // none exists on this install, and none is honoured.
                return term.Positive.Count == 0 ? [] : [.. _entities.Where(e => term.Matches(e.Tags))];
            }

            case "LootArchetypeV3Selector_EntityClasses":
                return _core.PointerAt(at, s.StructIndex, "entityClasses") is { } list
                    ? [.. _core.ReferenceArrayAt(_core.InstanceAt(list), list.StructIndex, "entityClasses")
                        .Select(g => _classByHash.GetValueOrDefault(g))
                        .OfType<string>()
                        .Select(c => _byClass[c])]
                    : [];

            default:
                return [];
        }
    }

    /// <summary>
    /// A pool filter as ordered steps. A Cumulative filter's negative tags
    /// strike what carries them; a positive one keeps only what carries it, so
    /// it strikes what lacks it. Every sequence on 4.10 is Cumulative, which
    /// is the union of its parts. 5.0 adds Additive, which puts back what
    /// matches its term - written only on <c>PoolFilter_Tags</c>, 34 times
    /// across the themes, so that is the only form read.
    /// </summary>
    private List<LootFilterStep> FilterTerms(Guid recordId, int depth) =>
        depth <= 8 && Record(recordId) is { } record
        && _core.PointerAt(_core.InstanceAt(record, record.VariantIndex), record.StructIndex, "filter") is { } f
            ? FilterTerms(f, depth)
            : [];

    private List<LootFilterStep> FilterTerms(DataCore.Pointer filter, int depth)
    {
        var at = _core.InstanceAt(filter);
        var none = new HashSet<Guid>();

        return _core.StructName(filter.StructIndex) switch
        {
            "PoolFilter_Tags" => TermAt(at, filter.StructIndex, "tagFilter") is var term
                ? [.. term.Negative.Select(t => new LootFilterStep(new LootTerm(new HashSet<Guid> { t }, none))),
                   .. term.Positive.Select(t => new LootFilterStep(new LootTerm(none, new HashSet<Guid> { t })))]
                : [],

            "PoolFilter_RecordRef" => _core.ReferenceAt(at, filter.StructIndex, "filterRecord") is { } id
                ? FilterTerms(id, depth + 1)
                : [],

            "PoolFilter_Sequence" => SequenceSteps(at, filter.StructIndex, depth),

            _ => [],
        };
    }

    private List<LootFilterStep> SequenceSteps(long at, int structIndex, int depth)
    {
        var steps = new List<LootFilterStep>();
        if (depth >= 8) return steps;

        foreach (var instance in _core.ClassArrayAt(at, structIndex, "filters"))
        {
            var i = _core.InstanceAt(instance);
            if (_core.PointerAt(i, instance.StructIndex, "filter") is not { } inner) continue;

            if (_core.EnumAt(i, instance.StructIndex, "mode") == "Additive")
            {
                if (_core.StructName(inner.StructIndex) == "PoolFilter_Tags"
                    && TermAt(_core.InstanceAt(inner), inner.StructIndex, "tagFilter") is { IsEmpty: false } back)
                    steps.Add(new LootFilterStep(back, Restore: true));
            }
            else
            {
                steps.AddRange(FilterTerms(inner, depth + 1));
            }
        }

        return steps;
    }

    private List<IReadOnlyList<LootBand>> Layers(Guid recordId, int depth)
    {
        if (depth > 8 || Record(recordId) is not { } record) return [];
        var at = _core.InstanceAt(record, record.VariantIndex);

        return _core.StructName(record.StructIndex) switch
        {
            "LootV3SecondaryChoicesMultiLayerRecord" =>
                [.. _core.ReferenceArrayAt(at, record.StructIndex, "layers").SelectMany(l => Layers(l, depth + 1))],
            "LootV3SecondaryChoicesSingleLayerRecord" => [Bands(at, record.StructIndex, null)],
            _ => [],
        };
    }

    private List<LootBand> Bands(long at, int structIndex, Guid? category) =>
        [.. _core.ClassArrayAt(at, structIndex, "choices").Select(choice =>
        {
            var c = _core.InstanceAt(choice);
            var selector = _core.PointerAt(c, choice.StructIndex, "selector");
            return new LootBand(
                _core.StringAt(c, choice.StructIndex, "name") ?? "",
                _core.SingleAt(c, choice.StructIndex, "weight") ?? 0,
                selector is { } s
                    ? TermAt(_core.InstanceAt(s), s.StructIndex, "tags")
                    : new LootTerm(new HashSet<Guid>(), new HashSet<Guid>()),
                category,
                Ignores(c, choice.StructIndex));
        })];

    /// <summary>
    /// The theme categories a band switches off: <c>LootSubThemeModifier_Ignore</c>,
    /// whose selector is a tag DNF over theme categories - #Flavour on all 14 on 5.0 PTU.
    /// </summary>
    private HashSet<Guid>? Ignores(long choice, int choiceStruct)
    {
        if (_core.PointerAt(choice, choiceStruct, "subThemeModifier") is not { } m
            || _core.StructName(m.StructIndex) != "LootSubThemeModifier_Ignore"
            || _core.PointerAt(_core.InstanceAt(m), m.StructIndex, "themesToIgnore") is not { } selector)
            return null;

        var (dnf, field) = _core.FieldAt(_core.InstanceAt(selector), selector.StructIndex, "tags");
        if (dnf < 0 || field is null) return null;

        var tags = _core.ClassArrayAt(dnf, field.StructIndex, "terms")
            .SelectMany(t => _core.ReferenceArrayAt(_core.InstanceAt(t), t.StructIndex, "positiveTags"))
            .ToHashSet();
        return tags.Count > 0 ? tags : null;
    }
}
