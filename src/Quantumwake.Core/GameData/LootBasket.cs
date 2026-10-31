namespace Quantumwake.Core.GameData;

/// <summary>One item of a list as a place can yield it.</summary>
/// <param name="Chance">The best per-slot chance the place gives it - see <see cref="GameLootData.PlacesFor"/>.</param>
public sealed record BasketItem(string Item, double Chance);

/// <summary>A place ranked for a whole list.</summary>
/// <param name="Items">The list's items the place can yield, likeliest first.</param>
public sealed record BasketPlace(string? Name, string? Body, string? Id, IReadOnlyList<BasketItem> Items, int Spots)
{
    /// <summary>The chance summed over the items: two at 10% beats one at 15%.</summary>
    public double Score => Items.Sum(i => i.Chance);
}

/// <summary>
/// Where to go for a list of things rather than one: the places that can
/// yield the most of it.
/// </summary>
/// <remarks>
/// loot-verse calls this the basket. Ranked by how many of the list's items a
/// place can yield first, then by their per-slot chances added up - which is
/// the expected number of list items per crate slot opened there, if each slot
/// is one draw, and the same estimate as every number on the Loot page.
/// </remarks>
public static class LootBasket
{
    /// <param name="wanted">Each item on the list with the places that can yield it.</param>
    /// <param name="take">How many places to return.</param>
    public static IReadOnlyList<BasketPlace> Rank(
        IReadOnlyList<(string Item, IReadOnlyList<GameLootPlaceHit> Places)> wanted, int take = 10)
    {
        var byPlace = new Dictionary<(string?, string?), (string? Id, Dictionary<string, double> Items, int Spots)>();

        foreach (var (item, places) in wanted)
        {
            foreach (var place in places)
            {
                var key = (place.Name, place.Body);
                if (!byPlace.TryGetValue(key, out var at))
                    byPlace[key] = at = (place.Id, new(StringComparer.Ordinal), 0);

                var chance = place.Best.PerRoll * place.Best.Source.SlotChance;
                at.Items[item] = Math.Max(at.Items.GetValueOrDefault(item), chance);
                byPlace[key] = (at.Id, at.Items, Math.Max(at.Spots, place.Spots));
            }
        }

        var ranked = byPlace
            .Select(p => new BasketPlace(
                p.Key.Item1, p.Key.Item2, p.Value.Id,
                [.. p.Value.Items.Select(i => new BasketItem(i.Key, i.Value)).OrderByDescending(i => i.Chance)],
                p.Value.Spots))
            .ToList();

        // The same military crate set sits unmarked on a dozen moons, and a
        // dozen identical rows would push every named place off the list:
        // spots that yield the same items at the same odds are one row, with
        // the bodies named together.
        var named = ranked.Where(p => p.Name is not null);
        var unmarked = ranked
            .Where(p => p.Name is null)
            .GroupBy(p => string.Join("|", p.Items.Select(i => $"{i.Item}:{i.Chance:R}")))
            .Select(g => g.Count() == 1 ? g.First() : g.First() with
            {
                Body = string.Join(", ", g.Select(p => p.Body).OfType<string>().Order(StringComparer.OrdinalIgnoreCase)),
                Id = null,
                Spots = g.Max(p => p.Spots),
            });

        return [.. named.Concat(unmarked)
            .OrderByDescending(p => p.Items.Count)
            .ThenByDescending(p => p.Score)
            .ThenByDescending(p => p.Spots)
            // A named place before an unmarked spot of the same worth: it can be found.
            .ThenBy(p => p.Name is null)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Take(take)];
    }
}
