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
    public static GameLootData Read(DataCore core)
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

        return new GameLootData { Items = items, Vias = vias, Rolls = rolls, Sources = sources };
    }
}
