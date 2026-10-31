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

    public GameLoot(DataCore core)
    {
        _core = core;
        var parents = new Dictionary<Guid, Guid>();

        foreach (var record in core.Records())
        {
            _byHash.TryAdd(record.Hash, record);

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

    /// <summary>Every V3 table in the install.</summary>
    public IEnumerable<DataRecord> Tables() =>
        _core.Records().Where(r => _core.StructName(r.StructIndex) == "LootTableV3Record");

    /// <summary>The record a reference names, or null.</summary>
    public DataRecord? Record(Guid id) => _byHash.GetValueOrDefault(id);

    /// <summary>A table resolved to items, through a container's constraints when given.</summary>
    public LootTable Resolve(DataRecord table, Guid? poolFilter = null, Guid? secondaryChoices = null)
    {
        var (odds, unfilled) = LootRoll.Resolve(
            Entries(table),
            poolFilter is { } f ? FilterTerms(f, 0) : [],
            secondaryChoices is { } s ? Layers(s, 0) : []);

        return new LootTable(Short(table.Name), odds, unfilled);
    }

    private static string Short(string recordName) => recordName[(recordName.IndexOf('.') + 1)..];

    private List<LootEntry> Entries(DataRecord table)
    {
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
    /// What a pool filter strikes out, as terms that match the struck item. A
    /// filter's negative tags remove what carries them; a positive one keeps
    /// only what carries it, so it removes what lacks it. Every sequence on
    /// this install is Cumulative, which is the union of its parts.
    /// </summary>
    private List<LootTerm> FilterTerms(Guid recordId, int depth) =>
        depth <= 8 && Record(recordId) is { } record
        && _core.PointerAt(_core.InstanceAt(record, record.VariantIndex), record.StructIndex, "filter") is { } f
            ? FilterTerms(f, depth)
            : [];

    private List<LootTerm> FilterTerms(DataCore.Pointer filter, int depth)
    {
        var at = _core.InstanceAt(filter);
        var none = new HashSet<Guid>();

        return _core.StructName(filter.StructIndex) switch
        {
            "PoolFilter_Tags" => TermAt(at, filter.StructIndex, "tagFilter") is var term
                ? [.. term.Negative.Select(t => new LootTerm(new HashSet<Guid> { t }, none)),
                   .. term.Positive.Select(t => new LootTerm(none, new HashSet<Guid> { t }))]
                : [],

            "PoolFilter_RecordRef" => _core.ReferenceAt(at, filter.StructIndex, "filterRecord") is { } id
                ? FilterTerms(id, depth + 1)
                : [],

            "PoolFilter_Sequence" => [.. _core.ClassArrayAt(at, filter.StructIndex, "filters")
                .SelectMany(i => _core.PointerAt(_core.InstanceAt(i), i.StructIndex, "filter") is { } inner && depth < 8
                    ? FilterTerms(inner, depth + 1)
                    : [])],

            _ => [],
        };
    }

    private List<IReadOnlyList<LootBand>> Layers(Guid recordId, int depth)
    {
        if (depth > 8 || Record(recordId) is not { } record) return [];
        var at = _core.InstanceAt(record, record.VariantIndex);

        return _core.StructName(record.StructIndex) switch
        {
            "LootV3SecondaryChoicesMultiLayerRecord" =>
                [.. _core.ReferenceArrayAt(at, record.StructIndex, "layers").SelectMany(l => Layers(l, depth + 1))],
            "LootV3SecondaryChoicesSingleLayerRecord" =>
                [[.. _core.ClassArrayAt(at, record.StructIndex, "choices").Select(choice =>
                {
                    var c = _core.InstanceAt(choice);
                    var selector = _core.PointerAt(c, choice.StructIndex, "selector");
                    return new LootBand(
                        _core.StringAt(c, choice.StructIndex, "name") ?? "",
                        _core.SingleAt(c, choice.StructIndex, "weight") ?? 0,
                        selector is { } s
                            ? TermAt(_core.InstanceAt(s), s.StructIndex, "tags")
                            : new LootTerm(new HashSet<Guid>(), new HashSet<Guid>()));
                })]],
            _ => [],
        };
    }
}
