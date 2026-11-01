using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// The Reference pages' detail modal: one item or one ship, everything the
/// app knows about it, with every part and every ship in it a link to its own.
/// </summary>
/// <remarks>
/// <para>
/// The catalogues answered "what is there" and the Loot page answered "where
/// does this come from", but nothing joined them: a part seen on a ship could
/// not be followed to where else it drops, nor a crate find to the ships it
/// ships on. The modal is that join, and it is built from the same answers the
/// other pages give - <see cref="ServerHost.LootWhere"/> for the drops and the
/// entity card for the facts - so the two cannot disagree.
/// </para>
/// </remarks>
public static partial class ServerHost
{
    static void MapReference(WebApplication app)
    {
        // Every hull: the community dataset's, with its spec sheet, and the
        // install's own for any it does not describe - a hull the patch added
        // last week is still a hull, with its stock parts and its hold.
        app.MapGet("/api/reference/ships", (LogLibrary lib, UexData uex, UexFeeds feeds) => ReferenceShips(lib, uex, feeds));

        app.MapGet("/api/reference/item", (string? id, LogLibrary lib, UexData uex, UexFeeds feeds) =>
            string.IsNullOrWhiteSpace(id) ? Results.NotFound() : Results.Ok(ItemDetail(id.Trim(), lib, uex, feeds)));

        app.MapGet("/api/reference/ship", (string? id, LogLibrary lib, UexData uex, UexFeeds feeds) =>
            ShipDetail(id?.Trim(), lib, uex, feeds) is { } ship ? Results.Ok(ship) : Results.NotFound());

        // Ship components by what they do - guns, shields, power, coolers,
        // quantum drives - with the figures each kind is chosen on.
        app.MapGet("/api/reference/components", (LogLibrary lib, UexData uex) => ReferenceComponents(lib, uex));
    }

    /// <summary>The component kinds the page lists, in the order its tabs show them.</summary>
    static readonly string[] ComponentTypes =
    [
        "WeaponGun", "Missile", "MissileLauncher", "Shield", "PowerPlant", "Cooler", "QuantumDrive", "Radar",
        "WeaponMining", "TractorBeam", "SalvageHead", "QuantumInterdictionGenerator", "EMP",
    ];

    /// <summary>
    /// Every ship component a pilot could fit, one row per name and size, with
    /// the community dataset's performance figures where it has them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The figures - a gun's DPS, a shield's HP and regeneration, a plant's
    /// power - are the community dump's reduction of the game's own records,
    /// the same ones the Garage sheet computes with (docs/garage.md). Without
    /// that download the list still comes from the install, by type, size,
    /// grade and maker, and the page says the figures need it.
    /// </para>
    /// <para>
    /// The files carry NPC, Arena Commander and test copies of many parts under
    /// the same display name, so a name and size is one row: the copy that a
    /// shop stocks wins, then the shortest class.
    /// </para>
    /// </remarks>
    static object ReferenceComponents(LogLibrary lib, UexData uex)
    {
        var loot = lib.GameCommodities.Loot;
        var types = ComponentTypes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var community = lib.Community.Parts;

        var rows = new List<(string Class, string Name, string Type, string? SubType, int Size, int Grade, string? Maker, string? Uuid, PartStats? Stats)>();
        if (community.Count > 0)
        {
            foreach (var p in community.Values)
                if (types.Contains(p.Type) && Named(p.Name, p.Class))
                    rows.Add((p.Class, p.Name, p.Type, p.SubType, p.Size, p.Grade, p.Manufacturer, p.Uuid ?? lib.ItemUuid(p.Class), p));
        }
        else
        {
            foreach (var i in lib.Items())
                if (i.Type is { } t && types.Contains(t) && i.Name is { } n && Named(n, i.ClassName))
                    rows.Add((i.ClassName, n, t, i.SubType, i.Size, i.Grade, i.Manufacturer, i.Uuid, null));
        }

        var parts = rows
            .Select(r =>
            {
                var stock = uex.ItemMarket(r.Uuid);
                var cheapest = stock.Count > 0 ? stock.MinBy(s => s.Buy) : null;
                var rolls = loot.For(r.Class);
                return new
                {
                    className = r.Class,
                    name = r.Name,
                    type = r.Type,
                    subType = r.SubType,
                    size = r.Size,
                    grade = r.Grade,
                    maker = r.Maker,
                    price = cheapest?.Buy,
                    cheapestAt = cheapest?.Terminal,
                    stockedAt = stock.Count,
                    bestRoll = rolls.Count > 0 ? rolls.Max(h => h.PerRoll) : (double?)null,
                    npcShips = loot.ShipsFor(r.Class).Count,
                    // The player hulls that come with it, one per name.
                    fittedTo = loot.StockOn(r.Class).Where(s => IsShipForSale(s.Ship))
                        .Select(s => ShipName(lib, s.Ship)).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                    stats = r.Stats is { } s ? new
                    {
                        s.Health, s.PowerGen, s.CoolantGen, s.Em, s.Ir, s.PowerUseMax,
                        weapon = s.Weapon is { } w ? new { w.Dps, w.SustainedDps, w.Alpha, w.RateOfFire, w.Range, w.AmmoSpeed } : null,
                        shield = s.Shield is { } sh ? new { sh.Hp, sh.Regen, sh.DownedDelay, sh.DamagedDelay } : null,
                        quantum = s.Quantum is { } q ? new { q.Speed, q.SpoolTime, q.Cooldown, q.FuelRate } : null,
                        missile = s.Missile is { } m ? new { m.Damage, m.Speed, m.LockTime, m.Range, m.TrackingSignal } : null,
                    } : null,
                };
            })
            .GroupBy(p => (p.name.ToLowerInvariant(), p.size, p.type))
            .Select(g => g.OrderByDescending(p => p.stockedAt > 0).ThenByDescending(p => p.fittedTo)
                .ThenBy(p => p.className.Length).First())
            .OrderBy(p => p.name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new { source = community.Count > 0 ? "community" : "install", uex = uex.IsEnabled, parts };

        // A part the text table never named is a test or a template.
        static bool Named(string name, string cls) =>
            name.Length > 0 && !name.StartsWith('@') && !string.Equals(name, cls, StringComparison.OrdinalIgnoreCase)
            && !name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
    }

    static List<object> ReferenceShips(LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        var loot = lib.GameCommodities.Loot;

        // One row per name: the community keys variants by class and names
        // some the same, and the shortest class is the base hull.
        var community = lib.Community.Ships
            .GroupBy(s => s.Value.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(s => s.Key.Length).First())
            .ToList();

        var named = community.Select(c => c.Value.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var classes = community.Select(c => c.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var install = lib.GameCommodities.Vehicles.Values
            .Where(v => v.Name is { Length: > 0 } && !v.Name.StartsWith('@')
                && IsShipForSale(v.Class)
                && !classes.Contains(v.Class) && !named.Contains(v.Name))
            .GroupBy(v => v.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(v => v.Class.Length).First());

        var rows = new List<(string Name, object Row)>();

        foreach (var (cls, s) in community)
        {
            var hull = CargoHull(lib, cls);
            rows.Add((s.Name, new
            {
                className = cls,
                source = "community",
                rental = feeds.CheapestRental(s.Name),
                shops = uex.VehicleShops(s.Name),
                rentals = feeds.RentalDesks(s.Name),
                s.Name,
                s.Career,
                s.Role,
                s.Crew,
                isSpaceship = (bool?)s.IsSpaceship,
                s.ExpeditedCost,
                s.StandardClaimTime,
                // The install's hold where it places one, as Cargo fit does.
                cargoScu = hull is { Source: "install" } ? hull.CargoScu : s.CargoScu,
                s.ScmSpeed,
                s.MaxSpeed,
                s.ShieldHp,
                s.Health,
                parts = loot.PartsOf(cls).Sum(p => p.Count),
                price = uex.VehiclePrice(s.Name)
            }));
        }

        foreach (var v in install)
        {
            var hull = CargoHull(lib, v.Class);
            rows.Add((v.Name, new
            {
                className = v.Class,
                source = "install",
                rental = feeds.CheapestRental(v.Name),
                shops = uex.VehicleShops(v.Name),
                rentals = feeds.RentalDesks(v.Name),
                name = v.Name,
                career = (string?)null,
                role = (string?)null,
                crew = 0,
                // Only a hull with grids says which folder it came from.
                isSpaceship = lib.GameCommodities.CargoHulls.TryGetValue(v.Class, out var h) ? (bool?)h.Spaceship : null,
                expeditedCost = (decimal?)null,
                standardClaimTime = (double?)null,
                cargoScu = hull?.CargoScu ?? 0,
                scmSpeed = 0d,
                maxSpeed = 0d,
                shieldHp = 0d,
                health = 0d,
                parts = loot.PartsOf(v.Class).Sum(p => p.Count),
                price = uex.VehiclePrice(v.Name)
            }));
        }

        return [.. rows.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).Select(r => r.Row)];
    }

    // Vehicles that are set dressing or mission props rather than ships: the
    // probes, sentries and Arena Commander objectives, the salvage wrecks, and
    // the hijacked hulks a mission puts in the way. 10 of the 21 hulls the
    // community does not describe; the rest are real - the Mk IV Constellations,
    // the Skylark, the Best in Show editions.
    [System.Text.RegularExpressions.GeneratedRegex(@"^EAObjective|^probe_|^Orbital_Sentry|^SalvageableDebris|_Hijacked|_Derelict", System.Text.RegularExpressions.RegexOptions.IgnoreCase)]
    private static partial System.Text.RegularExpressions.Regex NotForSale();

    /// <summary>A hull a pilot can own: not an NPC's, not a copy, not a prop.</summary>
    static bool IsShipForSale(string cls) =>
        !GameShipLoot.IsNpcShip(cls) && !NotAHull().IsMatch(cls) && !NotForSale().IsMatch(cls);

    /// <summary>A hull's display name: the install's text, then the community's, then the class.</summary>
    static string ShipName(LogLibrary lib, string cls) =>
        lib.GameCommodities.Vehicles.TryGetValue(cls, out var v) && v.Name is { Length: > 0 } n && !n.StartsWith('@') ? n
        : lib.Community.Ships.TryGetValue(cls, out var s) ? s.Name
        : lib.ItemName(cls);

    static object ItemDetail(string id, LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        var card = EntityCards.Build("part", id, lib, uex, feeds);
        var cls = card?.Id ?? id;
        var loot = lib.GameCommodities.Loot;

        var uuid = lib.ItemUuid(cls);
        var shops = uuid is null ? [] : uex.ItemMarket(uuid)
            .Where(r => r.Buy > 0)
            .OrderBy(r => r.Buy)
            .Select(r => new { place = r.Terminal, price = r.Buy })
            .ToList();

        var recipe = lib.GameCommodities.Blueprints
            .FirstOrDefault(b => b.Kind == "creation" && string.Equals(b.OutputClass, cls, StringComparison.OrdinalIgnoreCase));

        return new
        {
            className = cls,
            card,
            uex = uex.IsEnabled,
            shops,
            craft = recipe is null ? null : new
            {
                recipe.Output,
                recipe.CraftSeconds,
                recipe.Materials,
                // No reward pool is a recipe every crafter starts with.
                byDefault = recipe.RewardPools.Count == 0
            },
            // The player hulls that come out of the shop with it fitted.
            // Arena Commander copies, Teach and collector editions are the same
            // hull under another class, so one row per name.
            stockOn = loot.StockOn(cls)
                .Where(s => IsShipForSale(s.Ship))
                .Select(s => new { className = s.Ship, name = ShipName(lib, s.Ship), s.Count })
                .GroupBy(s => s.name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(s => s.className.Length).First())
                .OrderBy(s => s.name, StringComparer.OrdinalIgnoreCase),
            where = LootWhere(lib, uex, cls)
        };
    }

    static object? ShipDetail(string? id, LogLibrary lib, UexData uex, UexFeeds feeds)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var vehicles = lib.GameCommodities.Vehicles;
        var loot = lib.GameCommodities.Loot;

        // By class, then by the name a table row or a log carries.
        var cls = vehicles.ContainsKey(id) || lib.Community.Ships.ContainsKey(id) || loot.PartsOf(id).Count > 0 ? id
            : lib.Community.Ships.Where(s => string.Equals(s.Value.Name, id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(s => s.Key.Length).Select(s => s.Key).FirstOrDefault()
            ?? vehicles.Values.Where(v => string.Equals(v.Name, id, StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.Class.Length).Select(v => v.Class).FirstOrDefault();

        if (cls is null) return null;

        var community = lib.Community.Ships.TryGetValue(cls, out var c) ? c : null;
        vehicles.TryGetValue(cls, out var vehicle);
        var name = ShipName(lib, cls);
        var npc = GameShipLoot.IsNpcShip(cls);

        var items = new Dictionary<string, ItemReference>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in lib.Items()) items.TryAdd(item.ClassName, item);

        object Part(PartFit p) => items.TryGetValue(p.Part, out var i)
            ? new { className = p.Part, name = i.Name is { Length: > 0 } n ? n : lib.ItemName(p.Part), i.Type, i.Size, i.Grade, p.Count }
            : new { className = p.Part, name = lib.ItemName(p.Part), Type = (string?)null, Size = 0, Grade = 0, p.Count };

        var hull = CargoHull(lib, cls);
        var owned = lib.Stats().Ships.FirstOrDefault(s =>
            string.Equals(s.ClassName, cls, StringComparison.OrdinalIgnoreCase)
            || string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));

        // An NPC ship's base is the hull it is built on, so its card can link
        // back to the one a pilot can buy.
        var baseHull = npc
            ? loot.Stock.Keys.Where(h => cls.StartsWith(h + "_", StringComparison.OrdinalIgnoreCase)).MaxBy(h => h.Length)
            : null;

        return new
        {
            className = cls,
            name,
            npc,
            crew = npc ? GameShipLoot.Crew(cls) : null,
            baseHull = baseHull is null ? null : new { className = baseHull, name = ShipName(lib, baseHull) },
            community = community is null ? null : new
            {
                community.Career,
                community.Role,
                community.Crew,
                community.IsSpaceship,
                community.ExpeditedCost,
                community.StandardClaimTime,
                community.ScmSpeed,
                community.MaxSpeed,
                community.ShieldHp,
                community.Health
            },
            size = vehicle is null || vehicle.Length <= 0 ? null : new { vehicle.Beam, vehicle.Length, vehicle.Height },
            cargo = hull is null || hull.CargoScu <= 0 ? null : new { scu = hull.CargoScu, from = hull.Source, grids = hull.Grids.Count },
            flown = owned is null ? null : new { owned.Sorties, owned.Sessions, owned.LastFlown },
            uex = uex.IsEnabled,
            shops = npc ? [] : uex.VehicleShops(name),
            rentals = npc ? [] : feeds.RentalDesks(name),
            parts = loot.PartsOf(cls).Select(Part),
            // Who flies one, and what theirs carries: the parts a disabled one has aboard.
            npcVersions = npc ? [] : loot.NpcVersionsOf(cls).Select(v => new
            {
                className = v,
                crew = GameShipLoot.Crew(v),
                parts = loot.PartsOf(v).Select(Part)
            })
        };
    }
}
