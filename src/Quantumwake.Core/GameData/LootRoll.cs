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
/// <param name="Category">
/// The category tag of the loot theme the layer came from - Rarity, Faction,
/// Flavour - or null for a layer the container names itself.
/// </param>
/// <param name="Ignores">
/// Theme categories whose later layers stop applying once this band is drawn.
/// 5.0's faction themes draw "Faction 0.25" against "Not specified faction 1",
/// and the faction band switches off the Flavour themes after it: faction gear
/// is not re-sorted by how wealthy the place is.
/// </param>
public sealed record LootBand(string Name, double Weight, LootTerm Term,
    Guid? Category = null, IReadOnlySet<Guid>? Ignores = null);

/// <summary>One step of a pool filter, in order.</summary>
/// <param name="Restore">
/// False strikes out what matches; true puts back what matches and an earlier
/// step struck. 5.0's themes are written as pairs - remove every faction's
/// gear, then add back the one this place belongs to - so a filter is no
/// longer a set of exclusions but a sequence.
/// </param>
public sealed record LootFilterStep(LootTerm Term, bool Restore = false);

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
        IReadOnlyList<IReadOnlyList<LootBand>> layers) =>
        Filtered(entries, [.. excluded.Select(t => new LootFilterStep(t))], layers);

    /// <summary>
    /// Per-roll odds through a pool filter written as ordered steps, which is
    /// how 5.0's loot themes write theirs.
    /// </summary>
    public static (IReadOnlyList<LootOdds> Odds, IReadOnlyList<string> Unfilled) Filtered(
        IReadOnlyList<LootEntry> entries,
        IReadOnlyList<LootFilterStep> steps,
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

                var pool = Filter(choice.Pool, steps);
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

                foreach (var (item, p) in Spread(pool, bands, 0, null))
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

    private static List<LootCandidate> Filter(IReadOnlyList<LootCandidate> pool, IReadOnlyList<LootFilterStep> steps)
    {
        var kept = new bool[pool.Count];
        Array.Fill(kept, true);

        foreach (var step in steps)
            for (var i = 0; i < pool.Count; i++)
                if (kept[i] != step.Restore && step.Term.Matches(pool[i].Tags))
                    kept[i] = step.Restore;

        return [.. pool.Where((_, i) => kept[i])];
    }

    private static IEnumerable<(string Item, double Share)> Spread(
        List<LootCandidate> pool, IReadOnlyList<IReadOnlyList<LootBand>> layers, int depth, IReadOnlySet<Guid>? ignored)
    {
        if (depth == layers.Count)
        {
            // A class can be both named and tagged into one pool; it is one item.
            var distinct = pool.Select(p => p.ItemClass).Distinct(StringComparer.Ordinal).ToList();
            foreach (var item in distinct) yield return (item, 1.0 / distinct.Count);
            yield break;
        }

        var layer = layers[depth];
        var switchedOff = ignored is not null && layer.Count > 0 && layer[0].Category is { } category && ignored.Contains(category);

        var bands = switchedOff ? [] : layer
            .Where(b => b.Weight > 0)
            .Select(b => (Band: b, Members: pool.Where(p => b.Term.Matches(p.Tags)).ToList()))
            .Where(b => b.Members.Count > 0)
            .ToList();

        if (bands.Count == 0)
        {
            foreach (var x in Spread(pool, layers, depth + 1, ignored)) yield return x;
            yield break;
        }

        var total = bands.Sum(b => b.Band.Weight);
        foreach (var (band, members) in bands)
        {
            var next = band.Ignores is { Count: > 0 } more
                ? (IReadOnlySet<Guid>)new HashSet<Guid>([.. ignored ?? new HashSet<Guid>(), .. more])
                : ignored;
            foreach (var (item, share) in Spread(members, layers, depth + 1, next))
                yield return (item, band.Weight / total * share);
        }
    }
}
