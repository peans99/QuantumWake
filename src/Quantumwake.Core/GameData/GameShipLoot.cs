using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>One part an NPC ship carries as standard.</summary>
/// <param name="Ship">The NPC ship's entity class - <c>AEGS_Gladius_PU_AI_CRIM_IK</c>.</param>
/// <param name="Count">How many it carries: two coolers, four of a missile.</param>
public sealed record ShipFit(string Ship, int Count);

/// <summary>One part a hull carries as standard, from the hull's side.</summary>
/// <param name="Part">The part's entity class.</param>
/// <param name="Count">How many of it the hull fits.</param>
public sealed record PartFit(string Part, int Count);

/// <summary>
/// The parts NPC ships are built with, from their default loadouts.
/// </summary>
/// <remarks>
/// <para>
/// Ships the game flies as NPCs are their own entity classes, named for who
/// flies them: <c>AEGS_Gladius_PU_AI_CRIM_IK</c>, <c>MISC_Starfarer_PU_AI_CIV</c>.
/// 642 on this install, and 637 of them carry their loadout in the DataCore
/// as port-by-port entries - power plant, coolers, shields, quantum drive,
/// guns on their mounts, missiles in their racks - which is what a disabled
/// one has aboard to take.
/// </para>
/// <para>
/// What NPCs on foot wear is a different matter: 1,864 of 2,312 actor classes
/// name their outfits only through loadout XML under <c>Data\Scripts\Loadouts</c>,
/// which the archive stores encrypted; <see cref="GameNpcOutfits"/> reads them.
/// </para>
/// </remarks>
public static partial class GameShipLoot
{
    /// <summary>
    /// The kinds of item worth listing: what a pilot would take off a ship.
    /// Seats, screens, thrusters and controllers are part of the hull.
    /// </summary>
    public static readonly IReadOnlySet<string> Kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "WeaponGun", "Shield", "Cooler", "PowerPlant", "QuantumDrive", "Radar",
        "Missile", "MissileLauncher", "WeaponMining", "TractorBeam", "SalvageHead",
        "QuantumInterdictionGenerator", "EMP",
    };

    /// <summary>Whether an entity class is a ship or vehicle the game flies as an NPC in the universe.</summary>
    /// <remarks>The <c>_EA_</c> variants are Arena Commander's, which nothing can be taken from.</remarks>
    public static bool IsNpcShip(string entityClass) =>
        NpcShip().IsMatch(entityClass) && !entityClass.Contains("_EA_", StringComparison.Ordinal);

    [GeneratedRegex(@"_AI(_|$)|_Pirate(_|$)|_PU_Criminal", RegexOptions.CultureInvariant)]
    private static partial Regex NpcShip();

    /// <summary>
    /// Every kept part on every NPC ship, by the part's class.
    /// </summary>
    /// <param name="typeOf">The item type for a class - <see cref="GameItem.Type"/> - or null when unknown.</param>
    public static Dictionary<string, List<ShipFit>> Read(DataCore core, Func<string, string?> typeOf) =>
        ReadAll(core, typeOf).Npc;

    /// <summary>
    /// The NPC ships by part, as <see cref="Read"/>, and every other hull's own
    /// stock parts by hull - what one comes out of the shop with.
    /// </summary>
    /// <remarks>
    /// The same loadout walk serves both: a player hull's default loadout is
    /// the fit it is sold with, and the Reference page's ship card lists it so
    /// a part can be followed from the ship it ships on to where else it drops.
    /// </remarks>
    public static (Dictionary<string, List<ShipFit>> Npc, Dictionary<string, List<PartFit>> Stock) ReadAll(
        DataCore core, Func<string, string?> typeOf)
    {
        var stock = new Dictionary<string, List<PartFit>>(StringComparer.OrdinalIgnoreCase);
        var byHash = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byHash.TryAdd(record.Hash, record);

        var loadoutComponent = core.StructIndexOf("SEntityComponentDefaultLoadoutParams");
        var found = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;
            if (!record.FileName.Contains("/entities/spaceships/", StringComparison.OrdinalIgnoreCase)
                && !record.FileName.Contains("/entities/groundvehicles/", StringComparison.OrdinalIgnoreCase)) continue;

            var ship = record.Name["EntityClassDefinition.".Length..];
            if (ship.EndsWith("_Template", StringComparison.OrdinalIgnoreCase)) continue;
            var npc = IsNpcShip(ship);

            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex != loadoutComponent) continue;
                if (core.PointerAt(core.InstanceAt(component), component.StructIndex, "loadout") is { } loadout)
                    Walk(core, byHash, loadout, counts, 0);
            }

            if (!npc)
            {
                var parts = counts
                    .Where(c => typeOf(c.Key) is { } t && Kept.Contains(t))
                    .Select(c => new PartFit(c.Key, c.Value))
                    .OrderBy(p => p.Part, StringComparer.Ordinal)
                    .ToList();
                if (parts.Count > 0) stock[ship] = parts;
                continue;
            }

            foreach (var (item, count) in counts)
            {
                if (typeOf(item) is not { } type || !Kept.Contains(type)) continue;
                if (!found.TryGetValue(item, out var ships)) found[item] = ships = new(StringComparer.Ordinal);
                ships[ship] = count;
            }
        }

        return (found.ToDictionary(
            f => f.Key,
            f => f.Value.Select(s => new ShipFit(s.Key, s.Value)).OrderBy(s => s.Ship, StringComparer.Ordinal).ToList(),
            StringComparer.OrdinalIgnoreCase), stock);
    }

    /// <summary>Counts every class a manual loadout fits, through nested ports - a gun on its mount.</summary>
    private static void Walk(
        DataCore core, Dictionary<Guid, DataRecord> byHash, DataCore.Pointer loadout,
        Dictionary<string, int> counts, int depth)
    {
        // Turret, mount, gun, attachment is as deep as a ship goes; the bound
        // is against a loadout that names itself.
        if (depth > 8) return;

        var at = core.InstanceAt(loadout);
        switch (core.StructName(loadout.StructIndex))
        {
            case "SItemPortLoadoutManualParams":
                foreach (var entry in core.ClassArrayAt(at, loadout.StructIndex, "entries"))
                {
                    var e = core.InstanceAt(entry);
                    var item = core.StringAt(e, entry.StructIndex, "entityClassName");
                    if (item is not { Length: > 0 }
                        && core.ReferenceAt(e, entry.StructIndex, "entityClassReference") is { } id
                        && byHash.TryGetValue(id, out var referenced))
                        item = referenced.Name[(referenced.Name.IndexOf('.') + 1)..];

                    if (item is { Length: > 0 }) counts[item] = counts.GetValueOrDefault(item) + 1;

                    if (core.PointerAt(e, entry.StructIndex, "loadout") is { } nested)
                        Walk(core, byHash, nested, counts, depth + 1);
                }
                break;

            case "SItemPortLoadoutRecordParams":
                if (core.ReferenceAt(at, loadout.StructIndex, "loadoutRecord") is { } recordId
                    && byHash.TryGetValue(recordId, out var record)
                    && core.PointerAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "loadout") is { } inner)
                    Walk(core, byHash, inner, counts, depth + 1);
                break;
        }
    }

    /// <summary>
    /// Who flies a ship, from its class name: <c>_AI_CRIM_IK</c> is a
    /// criminal's, <c>_AI_UEE</c> the Navy's, <c>_AI_Super_CIV</c> an elite
    /// civilian's. Only the faction is kept; the rest of a name - QIG,
    /// FullFuel, NoCargoGridMarkup - is how the ship is set up, not who flies
    /// it, and listed beside the faction it buried it.
    /// </summary>
    public static string Crew(string entityClass)
    {
        var at = entityClass.IndexOf("_AI", StringComparison.Ordinal);
        var tail = at >= 0 ? entityClass[(at + 3)..]
            : entityClass.Contains("_Pirate", StringComparison.Ordinal) ? "PIR"
            : "CRIM";

        var tokens = tail.Split('_', StringSplitOptions.RemoveEmptyEntries);
        var faction = tokens.Select(t => Factions.GetValueOrDefault(t)).FirstOrDefault(f => f is not null) ?? "NPC";
        var elite = tokens.Any(t => t.Equals("Super", StringComparison.OrdinalIgnoreCase) || t.Equals("Elite", StringComparison.OrdinalIgnoreCase));

        return elite ? $"{faction} · Elite" : faction;
    }

    private static readonly Dictionary<string, string> Factions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CRIM"] = "Criminal",
        ["CIV"] = "Civilian",
        ["UEE"] = "UEE Navy",
        ["NT"] = "Nine Tails",
        ["NineTails"] = "Nine Tails",
        ["PIR"] = "Pirate",
        ["SEC"] = "Security",
        ["VAN"] = "Vanduul",
        ["BH"] = "Bounty hunter",
        ["HH"] = "Headhunters",
        ["HeadHunters"] = "Headhunters",
        ["CFP"] = "Citizens for Prosperity",
        ["FF"] = "Frontier Fighters",
        ["Xenothreat"] = "Xenothreat",
        ["Advocacy"] = "Advocacy",
    };
}
