using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>Where a line on an entity card came from. Shown, never inferred.</summary>
/// <remarks>
/// The app's whole claim is that it reads this install rather than a website, so
/// a card that mixes four sources without saying which is which quietly gives
/// that up. "You have been here 41 times" and "UEX last saw it at 2,340 aUEC"
/// deserve very different amounts of trust, and only the label can say so.
/// </remarks>
public static class EntitySource
{
    public const string Logs = "your logs";
    public const string Install = "the game files";
    public const string Uex = "UEX";
    public const string Community = "the community dataset";
}

/// <summary>What the app can be asked to do with an entity.</summary>
public static class EntityAction
{
    public const string Map = "map";
    public const string Stop = "stop";
    public const string Shopping = "shopping";
    public const string Overlay = "overlay";
    public const string Details = "details";
}

/// <summary>One stated fact, with the source that is answerable for it.</summary>
public sealed record EntityFact(string Label, string Value, string Source);

/// <summary>Whether the pilot has this already, which changes every decision about it.</summary>
public sealed record EntityHolding(string Status, string? Detail);

/// <param name="AsOf">
/// When the price was collected, not when it was true. A stale price presented
/// without its age is the one number on this card that can lose real money.
/// </param>
public sealed record EntityPrice(
    decimal? Amount, string? Unit, string? Where, DateTimeOffset? AsOf, string Source);

/// <summary>A place on the map this entity gives the pilot a reason to visit.</summary>
public sealed record EntityWhere(string? PlaceId, string Name, string? Note);

/// <summary>One entity, as every surface in the app should describe it.</summary>
public sealed record EntityCard(
    string Kind,
    string Id,
    string Name,
    string? Subtitle,
    IReadOnlyList<EntityFact> Facts,
    EntityHolding? Holding,
    EntityPrice? Price,
    IReadOnlyList<EntityWhere> Places,
    IReadOnlyList<string> Actions,
    string? Blurb = null,
    IReadOnlyList<string>? Tags = null);

/// <summary>
/// One description of a thing, wherever the pilot clicked on it.
/// </summary>
/// <remarks>
/// <para>
/// The app had grown a detail panel per view - the map's place card, the parts
/// table's expanding row, the cargo panel's three modes - each showing a
/// different subset of what is known and offering a different set of actions.
/// Clicking Hurston on the map and Hurston in a trade list told you different
/// things about Hurston, and only one of them let you add it to a plan.
/// </para>
/// <para>
/// The shape is deliberately the same for every kind, because the questions are:
/// what is known and who says so, do I already have it, what does it cost and
/// how old is that, where would I go for it, and what can I do about it now.
/// A kind with no answer for a section omits it rather than filling it in.
/// </para>
/// </remarks>
public static class EntityCards
{
    public static EntityCard? Build(
        string? kind, string? id, LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return kind?.Trim().ToLowerInvariant() switch
        {
            "place" => Place(id, lib, uex, feeds),
            "commodity" => Commodity(id, lib, uex),
            "ship" => Ship(id, lib, uex),
            "part" => Part(id, lib, uex),
            _ => null
        };
    }

    private static EntityCard? Place(string id, LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        var stats = lib.Stats();

        // Somewhere visited is described from the logs; somewhere merely known
        // is described from the atlas. Both are real places and both get a card,
        // because the pilot has just clicked one of them.
        var visited = stats.Locations.FirstOrDefault(p => p.RawId == id)
            ?? stats.Destinations.FirstOrDefault(p => p.RawId == id);

        var resolved = visited is null ? Quantumwake.Core.Locations.LocationResolver.Resolve(id) : null;

        var name = visited?.Name ?? resolved?.DisplayName;
        if (string.IsNullOrWhiteSpace(name))
            return null;

        var facts = new List<EntityFact>();

        var kindWord = (visited?.Kind ?? resolved?.Kind.ToString() ?? "").Trim();
        if (kindWord.Length > 0)
            facts.Add(new EntityFact("Kind", Spaced(kindWord), EntitySource.Install));

        var where = string.Join(" · ",
            new[] { visited?.Body ?? resolved?.Body, visited?.System ?? resolved?.System }
                .Where(x => !string.IsNullOrWhiteSpace(x)));

        if (where.Length > 0)
            facts.Add(new EntityFact("Where", where, EntitySource.Install));

        // A floor, and said as one: a visit is only recorded where the game
        // wrote an inventory request, so somewhere flown past leaves no trace.
        facts.Add(visited is { Visits: > 0 }
            ? new EntityFact("Visits",
                $"{visited.Visits} recorded"
                + (visited.LastVisit is { } last ? $", last {last:d MMM yyyy}" : ""),
                EntitySource.Logs)
            : new EntityFact("Visits", "none recorded", EntitySource.Logs));

        if (lib.GameCommodities.Places.TryGetValue(name, out var place) && place.Amenities.Count > 0)
            facts.Add(new EntityFact("Amenities",
                string.Join(", ", place.Amenities.Take(6)), EntitySource.Install));

        if (uex.IsEnabled)
            facts.Add(new EntityFact("Trade counter",
                uex.TerminalFor(name) is not null ? "listed" : "not listed", EntitySource.Uex));

        if (feeds.IsEnabled(UexFeeds.Places))
            facts.Add(new EntityFact("Clinic", feeds.HasClinic(name) switch
            {
                true => "listed",
                false => "not listed",
                _ => "not reported"
            }, EntitySource.Uex));

        var stash = stats.Stash.FirstOrDefault(s => s.LocationId == id
            || string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        var holding = stash is null
            ? null
            : new EntityHolding($"{stash.ItemCount} of your things are here",
                string.Join(", ", stash.Groups.SelectMany(g => g.Items).Take(4).Select(i => i.Name)));

        var leads = uex.Opportunities(name, limit: 3);

        return new EntityCard(
            "place", id, name,
            where.Length > 0 ? where : null,
            facts,
            holding,
            null,
            [
                new EntityWhere(id, name, "here"),
                .. leads.Select(o => new EntityWhere(null, o.SellTerminal, $"sell {o.Commodity} here"))
            ],
            [EntityAction.Map, EntityAction.Stop, EntityAction.Overlay]);
    }

    private static EntityCard? Commodity(string id, LogLibrary lib, UexData uex)
    {
        var best = uex.Best(id);
        var mine = lib.Market(uex).FirstOrDefault(e =>
            string.Equals(e.Name, id, StringComparison.OrdinalIgnoreCase));

        if (best is null && mine is null)
            return null;

        var name = mine?.Name ?? id;
        var facts = new List<EntityFact>();

        // The pilot's own trading first: it is the only part of this card that
        // is an observation rather than somebody else's report.
        if (mine is { MyTrades: > 0 })
            facts.Add(new EntityFact("You have traded",
                $"{mine.MyScuSold:N0} SCU sold over {mine.MyTrades} run{(mine.MyTrades == 1 ? "" : "s")}"
                + (mine.MyRevenue > 0 ? $", {mine.MyRevenue:N0} aUEC" : ""),
                EntitySource.Logs));
        else
            facts.Add(new EntityFact("You have traded", "never", EntitySource.Logs));

        var (sells, buys) = uex.TradeLocations(name);

        if (uex.IsEnabled)
            facts.Add(new EntityFact("Counters",
                $"{sells.Count} buy it from you, {buys.Count} sell it", EntitySource.Uex));

        return new EntityCard(
            "commodity", name, name,
            best?.BestSellTerminal is { Length: > 0 } t ? $"best price at {t}" : null,
            facts,
            null,
            best is null ? null : new EntityPrice(
                best.BestSell > 0 ? best.BestSell : null, "aUEC/SCU",
                best.BestSellTerminal, uex.FetchedAt, EntitySource.Uex),
            [.. sells.Take(4).Select(s => new EntityWhere(null, s, "buys it from you"))],
            [EntityAction.Map, EntityAction.Shopping, EntityAction.Details]);
    }

    private static EntityCard? Ship(string id, LogLibrary lib, UexData uex)
    {
        var reference = lib.Community.Ship(id);
        var owned = lib.Stats().Ships.FirstOrDefault(s =>
            string.Equals(s.ClassName, id, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Name, id, StringComparison.OrdinalIgnoreCase));

        if (reference is null && owned is null)
            return null;

        var name = reference?.Name ?? owned!.Name;
        var facts = new List<EntityFact>();

        if (reference is not null)
        {
            facts.Add(new EntityFact("Role",
                string.Join(" · ", new[] { reference.Career, reference.Role }
                    .Where(x => !string.IsNullOrWhiteSpace(x))),
                EntitySource.Community));

            if (reference.Crew > 0)
                facts.Add(new EntityFact("Crew", $"{reference.Crew}", EntitySource.Community));

            if (reference.CargoScu > 0)
                facts.Add(new EntityFact("Cargo", $"{reference.CargoScu:N0} SCU", EntitySource.Community));

            if (reference.ExpeditedCost is { } fee)
                facts.Add(new EntityFact("Claim",
                    $"{fee:N0} aUEC to expedite"
                    + (reference.StandardClaimTime is { } wait ? $", ~{wait:N0}m standard" : ""),
                    EntitySource.Community));
        }

        // Sorties are the reliable metric and lead wherever this ship is
        // described; time aboard is inferred and never stands alone.
        var holding = owned is null
            ? new EntityHolding("not flown here", "no sortie in these logs")
            : new EntityHolding($"flown {owned.Sorties} time{(owned.Sorties == 1 ? "" : "s")}",
                $"across {owned.Sessions} session{(owned.Sessions == 1 ? "" : "s")}, "
                + $"last {owned.LastFlown:d MMM yyyy}");

        var price = uex.VehiclePrice(reference?.Name ?? name);

        return new EntityCard(
            "ship", owned?.ClassName is { Length: > 0 } c ? c : id, name,
            reference?.Role,
            facts,
            holding,
            price is null ? null : new EntityPrice(
                price.Price > 0 ? price.Price : null, "aUEC",
                price.Terminal, uex.FetchedAt, EntitySource.Uex),
            [],
            [EntityAction.Details, EntityAction.Overlay]);
    }

    private static EntityCard? Part(string id, LogLibrary lib, UexData uex)
    {
        var item = lib.Items().FirstOrDefault(i =>
            string.Equals(i.ClassName, id, StringComparison.OrdinalIgnoreCase));

        if (item is null)
            return null;

        var facts = new List<EntityFact>();

        var what = string.Join(" · ", new[] { item.Type, item.SubType }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        if (what.Length > 0)
            facts.Add(new EntityFact("Type", what, EntitySource.Install));

        if (item.Size > 0 || item.Grade > 0)
            facts.Add(new EntityFact("Size and grade",
                string.Join(" · ", new[]
                {
                    item.Size > 0 ? $"S{item.Size}" : null,
                    item.Grade > 0 ? $"grade {item.Grade}" : null
                }.Where(x => x is not null)),
                EntitySource.Install));

        if (!string.IsNullOrWhiteSpace(item.Manufacturer))
            facts.Add(new EntityFact("Made by", item.Manufacturer, EntitySource.Install));

        if (item.MicroScu > 0)
            facts.Add(new EntityFact("Takes up", Volume(item.MicroScu), EntitySource.Install));

        var stats = lib.Stats();

        // The loadout is recorded by display name rather than class - the port
        // lines never carry one - so this is the one holding question that has
        // to be asked in names. Exact match only: "P4-AR" and "P4-AR Ballistic"
        // are different rifles and a contains would claim you own both.
        var equipped = stats.Loadout
            .SelectMany(slot => slot.Items.Select(i => (Slot: slot, Item: i)))
            .FirstOrDefault(x => string.Equals(x.Item.Name, item.Name, StringComparison.OrdinalIgnoreCase));

        var stashedAt = stats.Stash
            .Where(s => s.Groups.SelectMany(g => g.Items)
                .Any(i => string.Equals(i.ItemClass, id, StringComparison.OrdinalIgnoreCase)))
            .Select(s => s.Name)
            .ToList();

        var holding = equipped.Item is not null
            ? new EntityHolding("you are wearing it", equipped.Slot.Label)
            : stashedAt.Count > 0
                ? new EntityHolding("in your stash", string.Join(", ", stashedAt.Take(3)))
                : new EntityHolding("not yours", "never seen in your kit or a stash here");

        var stock = uex.ItemMarket(item.Uuid);
        var cheapest = stock.Count > 0 ? stock.MinBy(r => r.Buy) : null;

        return new EntityCard(
            "part", item.ClassName, item.Name, what.Length > 0 ? what : null,
            facts,
            holding,
            uex.ItemPrice(item.Uuid) is { } price
                ? new EntityPrice(price, "aUEC", cheapest?.Terminal, uex.FetchedAt, EntitySource.Uex)
                : null,
            [.. stock.OrderBy(r => r.Buy).Take(4)
                .Select(r => new EntityWhere(null, r.Terminal, $"{r.Buy:N0} aUEC"))],
            [EntityAction.Shopping, EntityAction.Map, EntityAction.Details],
            item.Description,
            [.. (item.Tags ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries
                | StringSplitOptions.TrimEntries)]);
    }

    /// <summary>"DistributionCentre" -> "Distribution Centre".</summary>
    private static string Spaced(string value) =>
        System.Text.RegularExpressions.Regex.Replace(value, "([a-z])([A-Z])", "$1 $2");

    /// <summary>Matches the Parts table's own wording, so one item reads the same in both.</summary>
    private static string Volume(long microScu) => microScu switch
    {
        >= 1_000_000 => $"{microScu / 1_000_000d:0.##} SCU",
        >= 10_000 => $"{microScu / 10_000d:0.#} centiSCU",
        _ => $"{microScu:N0} µSCU"
    };
}
