namespace Quantumwake.Core.GameData;

/// <summary>An item a selector can draw, with its tags (ancestors included).</summary>
public sealed record LootCandidate(string ItemClass, IReadOnlySet<Guid> Tags);

/// <summary>
/// A tag test: every positive tag present, no negative one. The game calls
/// it a DNF term; every one on this install is a single conjunction.
/// </summary>
public sealed record LootTerm(IReadOnlySet<Guid> Positive, IReadOnlySet<Guid> Negative)
{
    public bool IsEmpty => Positive.Count == 0 && Negative.Count == 0;

    public bool Matches(IReadOnlySet<Guid> tags) =>
        !IsEmpty && Positive.All(tags.Contains) && !Negative.Any(tags.Contains);
}

/// <summary>An archetype entry: a weight and the items its selector finds.</summary>
public sealed record LootChoice(string Name, double Weight, IReadOnlyList<LootCandidate> Pool);

/// <summary>A table entry: a weight and its archetype's choices.</summary>
/// <param name="Layers">
/// Bands the archetype draws itself, before any the container adds. Only the
/// older tables have these - a "Rarity" group on the archetype - where V3
/// moved the bands onto the container.
/// </param>
public sealed record LootEntry(
    string Name, double Weight, IReadOnlyList<LootChoice> Choices,
    IReadOnlyList<IReadOnlyList<LootBand>>? Layers = null);

/// <summary>One band in a secondary-choice layer - "Rare", weight 1.</summary>
public sealed record LootBand(string Name, double Weight, LootTerm Term);

/// <summary>
/// The arithmetic of one roll, apart from reading the files so it can be
/// tested without a 316 MB DataCore.
/// </summary>
/// <remarks>
/// <para>
/// A roll picks a table entry by weight, then one of its archetype's choices
/// by weight, then - when the container names secondary choices - a band from
/// each layer by weight, and last an item from whatever is left. The files
/// give the weights and not the draw, so three things here are assumptions,
/// each the reading under which every table on this install sums to one:
/// </para>
/// <list type="bullet">
/// <item>Anything with nothing to draw from is skipped and its weight goes to
/// the rest - an entry whose pool is empty, a choice likewise, a rarity band
/// with no member in the pool. The armour tables give 26 of their 32 parts of
/// weight to mission-item slots no item in the install fills; counting those
/// would have a small armour crate empty four rolls in five.</item>
/// <item>A layer none of whose bands matches anything in the pool does not
/// apply to it, rather than emptying it.</item>
/// <item>The last pick is uniform. Nothing in the files weights one pistol
/// over another once the bands have been drawn.</item>
/// </list>
/// </remarks>
public static class LootRoll
{
    /// <summary>
    /// Per-roll odds for every item, and the entries nothing could fill.
    /// </summary>
    /// <param name="entries">The table, its pools already read.</param>
    /// <param name="excluded">Pool-filter terms: an item matching any is struck out.</param>
    /// <param name="layers">Secondary-choice layers, applied in order.</param>
    public static (IReadOnlyList<LootOdds> Odds, IReadOnlyList<string> Unfilled) Resolve(
        IReadOnlyList<LootEntry> entries,
        IReadOnlyList<LootTerm> excluded,
        IReadOnlyList<IReadOnlyList<LootBand>> layers)
    {
        var unfilled = new List<string>();
        var live = new List<(LootEntry Entry, List<(LootChoice Choice, List<LootCandidate> Pool)> Choices)>();

        foreach (var entry in entries)
        {
            if (entry.Weight <= 0) continue;

            var choices = new List<(LootChoice, List<LootCandidate>)>();
            foreach (var choice in entry.Choices)
            {
                if (choice.Weight <= 0) continue;

                var pool = choice.Pool.Where(c => !excluded.Any(t => t.Matches(c.Tags))).ToList();
                if (pool.Count > 0)
                    choices.Add((choice, pool));
                else
                    unfilled.Add(choice.Name == entry.Name ? entry.Name : $"{entry.Name} / {choice.Name}");
            }

            if (choices.Count > 0)
                live.Add((entry, choices));
            else if (entry.Choices.Count == 0)
                unfilled.Add(entry.Name);
        }

        var odds = new Dictionary<(string Item, string Entry, string Choice), double>();
        var total = live.Sum(l => l.Entry.Weight);

        foreach (var (entry, choices) in live)
        {
            var choiceTotal = choices.Sum(c => c.Choice.Weight);
            IReadOnlyList<IReadOnlyList<LootBand>> bands = entry.Layers is { Count: > 0 } own ? [.. own, .. layers] : layers;

            foreach (var (choice, pool) in choices)
            {
                var share = entry.Weight / total * (choice.Weight / choiceTotal);

                foreach (var (item, p) in Spread(pool, bands, 0))
                {
                    var key = (item, entry.Name, choice.Name);
                    odds[key] = odds.GetValueOrDefault(key) + share * p;
                }
            }
        }

        return (
            [.. odds.Select(o => new LootOdds(o.Key.Item, o.Value, o.Key.Entry, o.Key.Choice))
                .OrderByDescending(o => o.PerRoll)
                .ThenBy(o => o.ItemClass, StringComparer.Ordinal)],
            unfilled);
    }

    private static IEnumerable<(string Item, double Share)> Spread(
        List<LootCandidate> pool, IReadOnlyList<IReadOnlyList<LootBand>> layers, int depth)
    {
        if (depth == layers.Count)
        {
            // A class can be both named and tagged into one pool; it is one item.
            var distinct = pool.Select(p => p.ItemClass).Distinct(StringComparer.Ordinal).ToList();
            foreach (var item in distinct) yield return (item, 1.0 / distinct.Count);
            yield break;
        }

        var bands = layers[depth]
            .Where(b => b.Weight > 0)
            .Select(b => (b.Weight, Members: pool.Where(p => b.Term.Matches(p.Tags)).ToList()))
            .Where(b => b.Members.Count > 0)
            .ToList();

        if (bands.Count == 0)
        {
            foreach (var x in Spread(pool, layers, depth + 1)) yield return x;
            yield break;
        }

        var total = bands.Sum(b => b.Weight);
        foreach (var (weight, members) in bands)
            foreach (var (item, share) in Spread(members, layers, depth + 1))
                yield return (item, weight / total * share);
    }
}
