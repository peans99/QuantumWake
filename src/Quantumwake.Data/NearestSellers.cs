namespace Quantumwake.Data;

/// <summary>One counter selling a thing, as the price feed names it, at the price it charges.</summary>
public sealed record SellerCounter(string Terminal, decimal Price);

/// <summary>Something a shopping list can be written with, and every counter known to sell it.</summary>
/// <param name="Kind">"commodity" (priced per SCU) or "item" (priced each).</param>
public sealed record BuyableThing(string Name, string Kind, IReadOnlyList<SellerCounter> Counters);

/// <summary>A counter's place: the map's id, its name, and the body and system it is measured by.</summary>
public sealed record SellerPlace(string Id, string Name, string? Body, string? System);

/// <summary>
/// The counter a picker offers for one thing, chosen by <see cref="SellerOrder"/>.
/// </summary>
/// <param name="Place">The place the counter stands in, or null when the atlas cannot name it.</param>
/// <param name="Here">The counter is in the place the distance is measured from.</param>
/// <param name="Gm">
/// Body centre to body centre from where the pilot is, in Gm; 0 on the same
/// planet or moon; null when either end has no position on file or the two
/// are in different systems, which no straight line describes.
/// </param>
/// <param name="Counters">How many places sell it, so "nearest of 7" can be said.</param>
/// <param name="AlsoSells">How many of the other lines on the list the same place sells (fewest stops only).</param>
public sealed record SellerChoice(
    string Name,
    string Kind,
    string? Terminal,
    string? Place,
    string PlaceId,
    string? Body,
    string? System,
    bool Here,
    double? Gm,
    decimal? Price,
    int Counters,
    int AlsoSells);

/// <summary>What a picker is asked to put first.</summary>
public enum SellerOrder
{
    /// <summary>The counter nearest the pilot, cheapest among equals.</summary>
    Nearest,

    /// <summary>The cheapest counter anywhere, nearest among equals.</summary>
    Cheapest,

    /// <summary>
    /// The place that also sells most of what is already on the list, nearest
    /// among equals: a list is flown as a run, and a second stop costs more
    /// than the difference between two counters' prices usually does.
    /// </summary>
    FewestStops,
}

/// <summary>
/// For each thing that can be bought, the one counter worth naming beside it,
/// and the things in the order a picker should offer them.
/// </summary>
/// <remarks>
/// <para>
/// The list it replaces offered 2,565 names alphabetically on this install
/// (203 commodities, 2,362 priced items), with nothing to say which of them
/// could be had at the station the pilot was standing in.
/// </para>
/// <para>
/// Distance is what <see cref="HaulShare"/> already measures: straight lines
/// between body centres. Two counters on the same moon are both 0, and that
/// is the honest reading of a measure that cannot see a city from its
/// spaceport. A place in another system, or with no position on file, has no
/// distance and comes after every measured one rather than being guessed at.
/// </para>
/// <para>
/// Places are compared, not terminals: an admin desk and a shop in the same
/// station are one stop.
/// </para>
/// </remarks>
public static class NearestSellers
{
    public static IReadOnlyList<SellerChoice> Rank(
        IEnumerable<BuyableThing> things,
        ResolvedPlace? from,
        Func<string, SellerPlace?> resolve,
        Func<StopWhere, StopWhere, double?> gm,
        SellerOrder order = SellerOrder.Nearest,
        IEnumerable<string>? onList = null)
    {
        var all = things.ToList();
        var places = new Dictionary<string, SellerPlace?>(StringComparer.OrdinalIgnoreCase);
        var distances = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);

        SellerPlace? Where(string terminal)
        {
            if (!places.TryGetValue(terminal, out var place))
                places[terminal] = place = resolve(terminal);
            return place;
        }

        // What a stop is called when comparing them: the map's id, or the
        // terminal's own name when the atlas cannot place it, so two unplaced
        // counters are not one stop merely for both being unknown.
        string StopKey(SellerCounter counter) =>
            Where(counter.Terminal) is { Id.Length: > 0 } place ? place.Id : $"terminal:{counter.Terminal}";

        bool IsHere(SellerPlace? place) =>
            from is not null && place is not null &&
            (from.Id.Length > 0 && place.Id.Length > 0
                ? string.Equals(from.Id, place.Id, StringComparison.OrdinalIgnoreCase)
                : string.Equals(from.Name, place.Name, StringComparison.OrdinalIgnoreCase));

        double? Distance(SellerPlace? place)
        {
            if (from is null || place is null) return null;
            if (IsHere(place)) return 0;
            var key = $"{place.Id}|{place.Name}";
            if (!distances.TryGetValue(key, out var d))
                distances[key] = d = gm(new StopWhere(from.Body, from.System), new StopWhere(place.Body, place.System));
            return d;
        }

        // Fewest stops: every place selling something already on the list,
        // with how many of those lines it sells.
        var listed = new HashSet<string>(onList ?? [], StringComparer.OrdinalIgnoreCase);
        var stopCover = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        if (order == SellerOrder.FewestStops && listed.Count > 0)
            foreach (var thing in all.Where(t => listed.Contains(t.Name)))
                foreach (var counter in thing.Counters)
                {
                    var key = StopKey(counter);
                    if (!stopCover.TryGetValue(key, out var names))
                        stopCover[key] = names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    names.Add(thing.Name);
                }

        int Also(string name, SellerCounter counter) =>
            stopCover.TryGetValue(StopKey(counter), out var names) ? names.Count(n => !string.Equals(n, name, StringComparison.OrdinalIgnoreCase)) : 0;

        var chosen = new List<SellerChoice>(all.Count);

        foreach (var group in all.GroupBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var thing = group.First();
            var counters = group.SelectMany(t => t.Counters).Where(c => c.Price > 0).ToList();

            if (counters.Count == 0)
            {
                chosen.Add(new SellerChoice(thing.Name, thing.Kind, null, null, "", null, null, false, null, null, 0, 0));
                continue;
            }

            var best = counters
                .Select(c => (Counter: c, Place: Where(c.Terminal), Gm: Distance(Where(c.Terminal)), Also: Also(thing.Name, c)))
                .Select(c => (c.Counter, c.Place, c.Gm, c.Also, Key: Key(order, IsHere(c.Place), c.Gm, c.Counter.Price, c.Also)))
                .MinBy(c => c.Key);

            chosen.Add(new SellerChoice(
                    thing.Name,
                    thing.Kind,
                    best.Counter.Terminal,
                    best.Place?.Name,
                    best.Place?.Id ?? "",
                    best.Place?.Body,
                    best.Place?.System,
                    IsHere(best.Place),
                    best.Gm,
                    best.Counter.Price,
                    counters.Select(StopKey).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    best.Also));
        }

        // Cheapest compares prices across things, which means nothing - a
        // missile is not "cheaper first" than a hat - so the list keeps the
        // nearness order between things and the price decides only which
        // counter each one names. Fewest stops puts the things sold where the
        // list already goes first.
        return [.. chosen
            .OrderBy(c => order == SellerOrder.FewestStops ? -c.AlsoSells : 0)
            .ThenBy(c => c.Counters == 0 ? 1 : 0)
            .ThenBy(c => c.Here ? 0 : 1)
            .ThenBy(c => c.Gm ?? double.MaxValue)
            .ThenBy(c => c.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>Smaller is better: which counter of one thing is named.</summary>
    private static (double, double, double) Key(SellerOrder order, bool here, double? gm, decimal price, int also)
    {
        var near = here ? -1 : gm ?? double.MaxValue;
        return order switch
        {
            SellerOrder.Cheapest => ((double)price, near, 0),
            SellerOrder.FewestStops => (-also, near, (double)price),
            _ => (near, (double)price, 0),
        };
    }
}
