namespace Quantumwake.Core.GameData;

/// <summary>
/// One table-and-constraints roll, resolved: what it can produce and how often.
/// </summary>
/// <remarks>
/// Three parallel arrays rather than a list of objects, because there are
/// 179,229 item-and-roll pairs on this install and the cache is read at every
/// start. <see cref="Via"/> indexes <see cref="GameLootData.Vias"/>; when an
/// item comes through more than one choice of the same roll the odds are
/// summed and the largest contributor names the route.
/// </remarks>
public sealed record GameLootRoll(
    string Table, string? PoolFilter, string? SecondaryChoices, IReadOnlyList<string> Unfilled,
    int[] Item, float[] PerRoll, int[] Via)
{
    public string Key => $"{Table}|{PoolFilter}|{SecondaryChoices}";
}

/// <summary>Where one item can come from, for the page.</summary>
/// <param name="PerRoll">The chance one roll of this container lands on the item.</param>
/// <param name="Via">The table entry and choice it comes through - "Small weapons / Pistols".</param>
public sealed record GameLootHit(LootSource Source, double PerRoll, string Via);

/// <summary>One named place, or one body's unmarked spots, where an item can be rolled.</summary>
/// <param name="Best">The best hit there: the likeliest container and roll among the presets set down in it.</param>
/// <param name="Spots">Crate sets the likeliest preset has there - a floor, see <see cref="LootPlace.Spots"/>.</param>
/// <param name="Presets">How many of the item's presets are set down there.</param>
public sealed record GameLootPlaceHit(string? Name, string? Body, GameLootHit Best, int Spots, int Presets, string? Id);

/// <summary>
/// The loot model as the install has it, cut down to what the pages ask:
/// for one item, every container and body that can roll it.
/// </summary>
public sealed class GameLootData
{
    public List<string> Items { get; init; } = [];
    public List<string> Vias { get; init; } = [];
    public List<GameLootRoll> Rolls { get; init; } = [];
    public List<LootSource> Sources { get; init; } = [];

    /// <summary>
    /// Where each slot preset is placed, keyed by <see cref="LootSource.Preset"/>.
    /// Empty when the map files were not read; a preset missing from a full
    /// read is one no map file on this build sets down.
    /// </summary>
    public Dictionary<string, List<LootPlace>> Places { get; init; } = [];

    /// <summary>Whether the map files were read at all, so an empty answer can say which kind it is.</summary>
    public bool PlacesRead { get; init; }

    /// <summary>
    /// The NPC ships that carry each part as standard, keyed by the part's
    /// class. Empty when item types were not known at read time.
    /// </summary>
    public Dictionary<string, List<ShipFit>> Ships { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The NPC ships carrying a part, or none.</summary>
    public IReadOnlyList<ShipFit> ShipsFor(string? itemClass)
    {
        if (itemClass is not { Length: > 0 }) return [];

        // The cache hands the dictionary back with an ordinal comparer, and
        // logs and tables do not always agree on case.
        _ships ??= new Dictionary<string, List<ShipFit>>(Ships, StringComparer.OrdinalIgnoreCase);
        return _ships.TryGetValue(itemClass, out var ships) ? ships : [];
    }

    private Dictionary<string, List<ShipFit>>? _ships;

    /// <summary>
    /// The kinds of NPC on foot that wear or carry each item, keyed by the
    /// item's class. Empty when the archive or item types were not available.
    /// </summary>
    public Dictionary<string, List<NpcWear>> Npcs { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The NPCs that wear or carry an item, or none.</summary>
    public IReadOnlyList<NpcWear> NpcsFor(string? itemClass)
    {
        if (itemClass is not { Length: > 0 }) return [];
        _npcs ??= new Dictionary<string, List<NpcWear>>(Npcs, StringComparer.OrdinalIgnoreCase);
        return _npcs.TryGetValue(itemClass, out var npcs) ? npcs : [];
    }

    private Dictionary<string, List<NpcWear>>? _npcs;

    /// <summary>The places one preset is set down, or none.</summary>
    public IReadOnlyList<LootPlace> PlacesOf(string preset) =>
        Places.TryGetValue(preset, out var places) ? places : [];

    public static GameLootData Empty { get; } = new();

    private Dictionary<string, List<(int Roll, float PerRoll, int Via)>>? _byItem;
    private Dictionary<string, List<LootSource>>? _byRoll;

    /// <summary>Every container and body that can roll an item, likeliest per roll first.</summary>
    public IReadOnlyList<GameLootHit> For(string? itemClass)
    {
        if (itemClass is not { Length: > 0 }) return [];
        Index();

        if (!_byItem!.TryGetValue(itemClass, out var rolls)) return [];

        return [.. rolls
            .SelectMany(r => _byRoll!.GetValueOrDefault(Rolls[r.Roll].Key) ?? [],
                (r, s) => new GameLootHit(s, r.PerRoll, Vias[r.Via]))
            .OrderByDescending(h => h.PerRoll * h.Source.SlotChance)
            .ThenBy(h => h.Source.Preset, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every place the item can be rolled, likeliest first: each place once,
    /// carrying its best container.
    /// </summary>
    /// <remarks>
    /// Ranked by the chance a slot there holds a container that then rolls
    /// the item, which is the per-roll odds times the slot chance - the same
    /// order as <see cref="For"/>. A place with more crate sets of the same
    /// odds comes first, since there is more to open.
    /// </remarks>
    public IReadOnlyList<GameLootPlaceHit> PlacesFor(string? itemClass)
    {
        var found = new Dictionary<(string?, string?), (GameLootHit Best, int Spots, HashSet<string> Presets, string? Id)>();

        foreach (var hit in For(itemClass))
        {
            if (hit.Source.Kind != "slot") continue;

            foreach (var place in PlacesOf(hit.Source.Preset))
            {
                var key = (place.Name, place.Body);
                if (!found.TryGetValue(key, out var at))
                {
                    found[key] = (hit, place.Spots, [hit.Source.Preset], place.Id);
                    continue;
                }

                at.Presets.Add(hit.Source.Preset);
                if (Chance(hit) > Chance(at.Best)) found[key] = (hit, place.Spots, at.Presets, at.Id);
            }
        }

        return [.. found
            .Select(f => new GameLootPlaceHit(f.Key.Item1, f.Key.Item2, f.Value.Best, f.Value.Spots, f.Value.Presets.Count, f.Value.Id))
            .OrderByDescending(p => Chance(p.Best))
            .ThenByDescending(p => p.Spots)
            .ThenBy(p => p.Name is null)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Body, StringComparer.OrdinalIgnoreCase)];
    }

    private static double Chance(GameLootHit hit) => hit.PerRoll * hit.Source.SlotChance;

    /// <summary>Whether any container or body can roll the item.</summary>
    public bool CanRoll(string? itemClass)
    {
        if (itemClass is not { Length: > 0 }) return false;
        Index();
        return _byItem!.TryGetValue(itemClass, out var rolls)
            && rolls.Any(r => _byRoll!.ContainsKey(Rolls[r.Roll].Key));
    }

    private void Index()
    {
        if (_byItem is not null) return;

        var byItem = new Dictionary<string, List<(int, float, int)>>(StringComparer.OrdinalIgnoreCase);
        for (var r = 0; r < Rolls.Count; r++)
        {
            var roll = Rolls[r];
            for (var i = 0; i < roll.Item.Length; i++)
            {
                var name = Items[roll.Item[i]];
                if (!byItem.TryGetValue(name, out var list)) byItem[name] = list = [];
                list.Add((r, roll.PerRoll[i], roll.Via[i]));
            }
        }

        _byRoll = Sources.GroupBy(s => s.RollKey).ToDictionary(g => g.Key, g => g.ToList());
        _byItem = byItem;
    }

    /// <summary>Reads and resolves every roll the install's sources make.</summary>
    /// <param name="p4k">The archive, to read where presets are placed; null leaves places empty.</param>
    /// <param name="text">The English table, for the star map's names.</param>
    /// <param name="typeOf">An item class's type, to keep the parts worth taking off an NPC ship; null skips ships.</param>
    public static GameLootData Read(
        DataCore core, P4kArchive? p4k = null, IReadOnlyDictionary<string, string>? text = null,
        Func<string, string?>? typeOf = null)
    {
        var loot = new GameLoot(core);
        var sources = loot.Sources().ToList();

        var items = new List<string>();
        var itemIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var vias = new List<string>();
        var viaIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        var rolls = new List<GameLootRoll>();

        static int Intern(List<string> list, Dictionary<string, int> index, string value)
        {
            if (!index.TryGetValue(value, out var i)) { i = list.Count; list.Add(value); index[value] = i; }
            return i;
        }

        foreach (var group in sources.GroupBy(s => s.RollKey))
        {
            var first = group.First();
            if (loot.Resolve(first) is not { } table) continue;

            var perItem = table.Items
                .GroupBy(o => o.ItemClass, StringComparer.Ordinal)
                .Select(g => (Item: g.Key, PerRoll: g.Sum(o => o.PerRoll), Top: g.MaxBy(o => o.PerRoll)!))
                .ToList();

            rolls.Add(new GameLootRoll(
                first.Table, first.PoolFilter, first.SecondaryChoices, table.Unfilled,
                [.. perItem.Select(p => Intern(items, itemIndex, p.Item))],
                [.. perItem.Select(p => (float)p.PerRoll)],
                [.. perItem.Select(p => Intern(vias, viaIndex,
                    p.Top.Entry == p.Top.Choice ? p.Top.Entry : $"{p.Top.Entry} / {p.Top.Choice}"))]));
        }

        // Only presets the page can show: the map files also place plants,
        // minerals and rock-cracker nodes, which roll no loot table.
        var places = new Dictionary<string, List<LootPlace>>(StringComparer.Ordinal);
        if (p4k is not null)
        {
            var shown = sources.Where(s => s.Kind == "slot").Select(s => s.Preset).ToHashSet(StringComparer.Ordinal);
            foreach (var (preset, list) in GameLootPlaces.Read(p4k, core, text ?? new Dictionary<string, string>()))
                if (shown.Contains(preset)) places[preset] = list;
        }

        return new GameLootData
        {
            Items = items, Vias = vias, Rolls = rolls, Sources = sources,
            Places = places, PlacesRead = p4k is not null,
            Ships = typeOf is null ? new(StringComparer.OrdinalIgnoreCase) : GameShipLoot.Read(core, typeOf),
            Npcs = typeOf is null || p4k is null ? new(StringComparer.OrdinalIgnoreCase) : GameNpcOutfits.Read(p4k, core, typeOf),
        };
    }
}
