using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// The Reference Encounters page: every group of NPC ships the install
/// describes, its waves, what each ship's hold is spawned with, and which
/// mission script - if any the client ships - names it.
/// </summary>
/// <remarks>
/// Nothing here is from the logs, which never mention an NPC ship's cargo; it is
/// the game files throughout, priced at UEX where UEX is on. See
/// <see cref="GameEncounters"/> for why most encounters carry only their
/// folder and name rather than a mission.
/// </remarks>
public static partial class ServerHost
{
    static object ReferenceEncounters(LogLibrary lib, UexData uex)
    {
        var data = lib.GameCommodities.Loot.Encounters;

        // Taken once per class: 731 ship entries name about 150 classes.
        var ships = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        object? Ship(string? cls)
        {
            if (cls is not { Length: > 0 }) return null;
            if (ships.TryGetValue(cls, out var known)) return known;

            var (baseHull, scu, from) = NpcHold(lib, cls);
            var view = new
            {
                className = cls,
                name = ShipName(lib, baseHull ?? cls),
                crew = GameShipLoot.IsNpcShip(cls) ? GameShipLoot.Crew(cls) : null,
                hold = scu,
                holdFrom = from,
            };
            ships[cls] = view;
            return view;
        }

        var used = data.Encounters.SelectMany(e => e.Waves).SelectMany(w => w.Ships).SelectMany(s => s.Cargo)
            .ToHashSet(StringComparer.Ordinal);

        return new
        {
            ready = data.Encounters.Count > 0,
            scriptsRead = data.ScriptsRead,
            uex = uex.IsEnabled,
            encounters = data.Encounters.Select(e => new
            {
                e.Name,
                e.Folder,
                e.Scripts,
                arena = e.ArenaCommander,
                waves = e.Waves.Select(w => new
                {
                    w.Id,
                    ships = w.Ships.Select(s => new
                    {
                        ship = Ship(s.Ship),
                        s.Flown,
                        s.Amount,
                        s.MinAmount,
                        s.MaxAmount,
                        s.Skill,
                        s.Cargo,
                    }),
                }),
            }),
            manifests = data.Manifests.Values.Where(m => used.Contains(m.Name)).ToDictionary(m => m.Name, m => ManifestView(m, uex)),
            unused = data.Unused().Select(m => ManifestView(m, uex)),
        };
    }

    /// <summary>
    /// The encounters one NPC ship turns up in and what its hold is spawned
    /// with in them, for its Reference card. Null for a hull nobody spawns.
    /// </summary>
    static object? EncountersOf(LogLibrary lib, UexData uex, string cls)
    {
        var data = lib.GameCommodities.Loot.Encounters;
        bool Same(GameEncounterShip s) => string.Equals(s.Ship, cls, StringComparison.OrdinalIgnoreCase);

        var mine = data.Encounters.Where(e => e.Waves.Any(w => w.Ships.Any(Same))).ToList();
        if (mine.Count == 0) return null;

        var (_, scu, from) = NpcHold(lib, cls);
        var cargo = mine.SelectMany(e => e.Waves).SelectMany(w => w.Ships).Where(Same).SelectMany(s => s.Cargo)
            .Distinct(StringComparer.Ordinal)
            .Select(n => data.Manifests.GetValueOrDefault(n))
            .OfType<GameCargoManifest>()
            .Select(m => ManifestView(m, uex))
            .ToList();

        return new
        {
            names = mine.Select(e => new { e.Name, arena = e.ArenaCommander, linked = e.Scripts.Count > 0 }),
            hold = scu,
            holdFrom = from,
            uex = uex.IsEnabled,
            cargo,
        };
    }

    /// <summary>
    /// The hull an NPC class is built on and the hold it is spawned with: the
    /// install's grids first, on the class or that hull; the community
    /// dataset's capacity only when the install has no grid for either, so a
    /// hold the install measures is never replaced.
    /// </summary>
    static (string? BaseHull, double? Scu, string? From) NpcHold(LogLibrary lib, string cls)
    {
        var baseHull = lib.GameCommodities.Loot.Stock.Keys
            .Where(h => cls.StartsWith(h + "_", StringComparison.OrdinalIgnoreCase))
            .MaxBy(h => h.Length);

        if (GameEncounters.Hold(cls, lib.GameCommodities.CargoHulls) is { } own) return (baseHull, own.Scu, "install");
        if (lib.Community.GarageShip(baseHull ?? cls) is { CargoScu: > 0 } community) return (baseHull, community.CargoScu, "community");
        return (baseHull, null, null);
    }

    static object ManifestView(GameCargoManifest m, UexData uex) => new
    {
        m.Name,
        m.Mode,
        m.MinFill,
        m.MaxFill,
        goods = m.Goods.Select(g => new
        {
            g.Commodity,
            g.Probability,
            // What a seller is paid: a hold taken off an NPC is sold, not bought.
            price = uex.IsEnabled ? uex.Best(g.Commodity)?.BestSell : null,
        }),
    };
}
