using System.Text.Json;
using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>
/// The Garage's two digests: every ship's base figures and loadout tree, and
/// every part's stat block. Both come out of files the dataset already
/// downloads; the earlier digests kept a ship's name and a part's size from
/// them and dropped the rest.
/// </summary>
public sealed partial class CommunityData
{
    private Dictionary<string, PartStats> _parts = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, ShipBase> _shipBases = new(StringComparer.OrdinalIgnoreCase);

    private string PartStatsDigestPath => Path.Combine(_directory, "digest-part-stats.json");
    private string ShipStatsDigestPath => Path.Combine(_directory, "digest-ship-stats.json");

    /// <summary>
    /// Whether the cached dataset carries the garage digests. An install that
    /// downloaded the dataset before 0.13 has every other file and not these,
    /// which the page says as "refresh the reference data" rather than as a
    /// ship with no numbers.
    /// </summary>
    public bool HasGarage => _shipBases.Count > 0 && _parts.Count > 0;

    /// <summary>Older digests discarded industrial port compatibility and need a refresh.</summary>
    public bool HasIndustrialPorts { get; private set; }

    /// <summary>
    /// Whether the ship digest carries cargo grids. A digest written before
    /// 0.14.9 has the ships and not their grids - null, not empty - and the
    /// cargo panel says "refresh the reference data" rather than showing a
    /// hull with no hold.
    /// </summary>
    public bool HasCargoGrids => _shipBases.Values.Any(s => s.CargoGrids is not null);

    /// <summary>Every part with figures, by class name.</summary>
    public IReadOnlyDictionary<string, PartStats> Parts => _parts;

    /// <summary>
    /// A ship's base figures and stock loadout, by class name, or null.
    /// </summary>
    /// <remarks>
    /// The same matching as <see cref="Ship"/>: exact, else the shortest class
    /// that extends the name with an underscore - the logs say MISC_Starlancer
    /// and the dump says MISC_Starlancer_Max.
    /// </remarks>
    public ShipBase? GarageShip(string? className)
    {
        if (string.IsNullOrWhiteSpace(className) || _shipBases.Count == 0)
            return null;

        var key = className.Trim().Replace(' ', '_');

        if (_shipBases.TryGetValue(key, out var exact))
            return exact;

        return _shipBases
            .Where(p => p.Key.StartsWith(key + "_", StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Key.Length)
            .Select(p => p.Value)
            .FirstOrDefault();
    }

    /// <summary>Every ship the garage can draw, by class name.</summary>
    public IReadOnlyDictionary<string, ShipBase> GarageShips => _shipBases;

    private void LoadGarage()
    {
        if (File.Exists(PartStatsDigestPath))
            _parts = JsonSerializer.Deserialize<Dictionary<string, PartStats>>(File.ReadAllText(PartStatsDigestPath))
                is { } p ? new Dictionary<string, PartStats>(p, StringComparer.OrdinalIgnoreCase)
                         : new Dictionary<string, PartStats>(StringComparer.OrdinalIgnoreCase);

        if (File.Exists(ShipStatsDigestPath))
            _shipBases = JsonSerializer.Deserialize<Dictionary<string, ShipBase>>(File.ReadAllText(ShipStatsDigestPath))
                is { } s ? new Dictionary<string, ShipBase>(s, StringComparer.OrdinalIgnoreCase)
                         : new Dictionary<string, ShipBase>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Part class name → stat block, from <c>ship-items.json</c>.
    /// </summary>
    /// <remarks>
    /// Paints and flair are left out - a thousand entries with nothing the
    /// sheet reads - and so is anything without a <c>stdItem</c>, which is the
    /// dump's own signal that it could not describe the thing.
    /// </remarks>
    public static Dictionary<string, PartStats> DigestPartStats(string shipItemsJson)
    {
        var result = new Dictionary<string, PartStats>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(shipItemsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in doc.RootElement.EnumerateArray())
        {
            var className = Str(entry, "className");
            var type = Str(entry, "type");

            if (className is null || type is null || type.StartsWith("Flair", StringComparison.Ordinal) || type == "Paints")
                continue;

            if (!entry.TryGetProperty("stdItem", out var std) || std.ValueKind != JsonValueKind.Object)
                continue;

            var name = Str(std, "Name") ?? Str(entry, "name") ?? className;
            if (name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
                name = className;

            var networked = std.TryGetProperty("ResourceNetwork", out var rn) && rn.ValueKind == JsonValueKind.Object;

            result[className] = new PartStats(
                className,
                type,
                (int)(Num(entry, "size") ?? Num(std, "Size") ?? 0),
                (int)(Num(entry, "grade") ?? Num(std, "Grade") ?? 0),
                name,
                At(std, "Manufacturer", "Name") is { ValueKind: JsonValueKind.String } m ? m.GetString() : null,
                Str(std, "UUID"),
                Num(std, "Mass") ?? 0,
                Number(std, "Emission", "Em", "Maximum"),
                Number(std, "Emission", "Ir") is var ir && ir > 0 ? ir : Number(std, "Emission", "IR"),
                Number(std, "Durability", "Health"),
                Number(std, "ResourceNetwork", "Generation", "Power"),
                Number(std, "ResourceNetwork", "Usage", "Power", "Minimum"),
                Number(std, "ResourceNetwork", "Usage", "Power", "Maximum"),
                Number(std, "ResourceNetwork", "Generation", "Coolant"),
                Number(std, "ResourceNetwork", "Usage", "Coolant", "Minimum"),
                Number(std, "ResourceNetwork", "Usage", "Coolant", "Maximum"),
                Weapon(std),
                Shield(std),
                Quantum(std),
                Missile(std),
                Armor(std),
                Str(entry, "subType"),
                networked,
                At(std, "Manufacturer", "Code") is { ValueKind: JsonValueKind.String } mc && mc.GetString() is { Length: > 0 } code && code != "UNKN" ? code : null);
        }

        return result;

        static WeaponStats? Weapon(JsonElement std)
        {
            if (At(std, "Weapon", "Damage") is not { ValueKind: JsonValueKind.Object } damage)
                return null;

            var byType = new Dictionary<string, double>(StringComparer.Ordinal);
            if (damage.TryGetProperty("Dps", out var dps) && dps.ValueKind == JsonValueKind.Object)
                foreach (var kind in dps.EnumerateObject())
                    if (kind.Value.ValueKind == JsonValueKind.Number && kind.Value.GetDouble() > 0)
                        byType[kind.Name] = kind.Value.GetDouble();

            return new WeaponStats(
                Num(damage, "DpsTotal") ?? 0,
                Num(damage, "Sustained") ?? 0,
                Num(damage, "AlphaTotal") ?? 0,
                Number(std, "Weapon", "RateOfFire"),
                Number(std, "Weapon", "EffectiveRange"),
                Number(std, "Ammunition", "Speed"),
                byType);
        }

        static ShieldStats? Shield(JsonElement std)
        {
            if (At(std, "Shield") is not { ValueKind: JsonValueKind.Object } shield)
                return null;

            var absorb = new Dictionary<string, double>(StringComparer.Ordinal);
            if (shield.TryGetProperty("Absorption", out var abs) && abs.ValueKind == JsonValueKind.Object)
                foreach (var kind in abs.EnumerateObject())
                    absorb[kind.Name] = Num(kind.Value, "Maximum") ?? 0;

            return new ShieldStats(
                Num(shield, "MaxShieldHealth") ?? 0,
                Num(shield, "MaxShieldRegen") ?? 0,
                Num(shield, "DownedDelay") ?? 0,
                Num(shield, "DamagedDelay") ?? 0,
                absorb);
        }

        static QuantumStats? Quantum(JsonElement std)
        {
            if (At(std, "QuantumDrive") is not { ValueKind: JsonValueKind.Object } qd)
                return null;

            return new QuantumStats(
                Number(qd, "StandardJump", "DriveSpeed"),
                Number(qd, "StandardJump", "SpoolUpTime"),
                Number(qd, "StandardJump", "CooldownTime"),
                Num(qd, "FuelRate") ?? 0,
                Num(qd, "DisconnectRange") ?? 0,
                Number(qd, "StandardJump", "StageOneAccelRate"),
                Number(qd, "StandardJump", "StageTwoAccelRate"));
        }

        static MissileStats? Missile(JsonElement std)
        {
            if (At(std, "Missile") is not { ValueKind: JsonValueKind.Object } ms)
                return null;

            // Damage is a per-type block; the sheet wants the hit as a whole.
            var damage = 0.0;
            if (ms.TryGetProperty("Damage", out var dmg) && dmg.ValueKind == JsonValueKind.Object)
                foreach (var kind in dmg.EnumerateObject())
                    if (kind.Value.ValueKind == JsonValueKind.Number) damage += kind.Value.GetDouble();

            return new MissileStats(
                damage,
                Number(ms, "GCS", "LinearSpeed"),
                Number(ms, "Targeting", "LockTime"),
                Num(ms, "Distance") ?? 0,
                At(ms, "Targeting", "TrackingSignalType") is { ValueKind: JsonValueKind.String } sig ? sig.GetString() : null);
        }

        static ArmorSignals? Armor(JsonElement std)
        {
            if (At(std, "Armor", "SignalMultipliers") is not { ValueKind: JsonValueKind.Object } sm)
                return null;

            return new ArmorSignals(
                Num(sm, "Electromagnetic") ?? 1,
                Num(sm, "Infrared") ?? 1,
                Num(sm, "CrossSection") ?? 1);
        }
    }

    /// <summary>
    /// Ship class name → base figures and loadout tree, from <c>ships.json</c>.
    /// </summary>
    /// <remarks>
    /// Every entry the sheet reads is kept: a port a pilot can change, a port
    /// holding a part with figures - the model reads the life support and the
    /// armour, which no pilot can change - or a port whose children are. Seats,
    /// doors, controllers and empty fixed slots are not part of the ship the
    /// sheet describes, and dropping them takes the digest from 9.7 MB to
    /// under half. What a port accepts is kept only where a swap could use
    /// it, and for the turret ports the gun classification reads.
    /// </remarks>
    /// <param name="parts">The part digest, so a port is kept when its part has figures.</param>
    public static Dictionary<string, ShipBase> DigestShipStats(string shipsJson, IReadOnlyDictionary<string, PartStats> parts)
    {
        var result = new Dictionary<string, ShipBase>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(shipsJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var s in doc.RootElement.EnumerateArray())
        {
            var className = Str(s, "ClassName");
            if (className is null || !s.TryGetProperty("Loadout", out var loadout))
                continue;

            var pools = new Dictionary<string, int>(StringComparer.Ordinal);
            if (s.TryGetProperty("PowerPools", out var pp) && pp.ValueKind == JsonValueKind.Object)
                foreach (var pool in pp.EnumerateObject())
                    if (Num(pool.Value, "Size") is { } size && size >= 0)
                        pools[pool.Name] = (int)size;

            var ifcs = At(s, "FlightCharacteristics", "IFCS");
            var agility = At(s, "Agility");

            result[className] = new ShipBase(
                className,
                Str(s, "Name") ?? className,
                At(s, "Manufacturer", "Name") is { ValueKind: JsonValueKind.String } m ? m.GetString() : null,
                Str(s, "Role"),
                Str(s, "Career"),
                (int)(Num(s, "Size") ?? 0),
                (int)(Num(s, "Crew") ?? 0),
                s.TryGetProperty("IsSpaceship", out var sp) && sp.ValueKind == JsonValueKind.True,
                Num(s, "Mass") ?? 0,
                Num(s, "MassLoadout") ?? 0,
                Num(s, "Health") ?? 0,
                new Vec3(Number(s, "CrossSection", "X"), Number(s, "CrossSection", "Y"), Number(s, "CrossSection", "Z")),
                new FlightStats(
                    Number(ifcs, "ScmSpeed"), Number(ifcs, "BoostSpeedForward"), Number(ifcs, "MaxSpeed"),
                    Number(agility, "Pitch"), Number(agility, "Yaw"), Number(agility, "Roll")),
                Number(s, "QuantumTravel", "FuelCapacity"),
                Number(s, "Propulsion", "FuelCapacity"),
                Num(s, "Cargo") ?? 0,
                pools,
                new DatasetTotals(
                    Number(s, "Emission", "EmShields"),
                    Number(s, "Emission", "EmQuantum"),
                    Number(s, "Emission", "IrShields"),
                    Number(s, "Emission", "IrQuantum"),
                    (int)Number(s, "Power", "GenerationSegments"),
                    Number(s, "Cooling", "GenerationSegments"),
                    Num(s, "ShieldHp") ?? 0,
                    Number(s, "Weaponry", "FixedWeapons", "DpsTotal"),
                    Number(s, "Weaponry", "TurretDps"),
                    Number(s, "QuantumTravel", "Range"),
                    Num(s, "MassTotal") ?? 0),
                Ports(loadout, parts),
                Grids(s));
        }

        return result;

        // The dump's CargoGrids block: one entry per grid the hull places,
        // which is the count Game2.dcb cannot give (a grid record is a type,
        // and the Spirit C1 places its one type twice). Metres, the SCU the
        // dump credits it with, and the smallest and largest box it accepts.
        // Summed over the block it equals the dump's own Cargo figure on every
        // hull that has one (149 of 318 on 2026-09-16), which is the check.
        static IReadOnlyList<CargoGrid> Grids(JsonElement ship)
        {
            if (!ship.TryGetProperty("CargoGrids", out var grids) || grids.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<CargoGrid>();
            foreach (var g in grids.EnumerateArray())
            {
                var cls = Str(g, "Class");
                if (cls is null) continue;
                list.Add(new CargoGrid(
                    cls,
                    Str(g, "UUID"),
                    Num(g, "X") ?? 0, Num(g, "Y") ?? 0, Num(g, "Z") ?? 0,
                    Num(g, "SCU") ?? 0,
                    new Vec3(Number(g, "MinSize", "X"), Number(g, "MinSize", "Y"), Number(g, "MinSize", "Z")),
                    new Vec3(Number(g, "MaxSize", "X"), Number(g, "MaxSize", "Y"), Number(g, "MaxSize", "Z")),
                    g.TryGetProperty("IsExternalContainer", out var ext) && ext.ValueKind == JsonValueKind.True));
            }
            return list;
        }

        static IReadOnlyList<FitPort> Ports(JsonElement ports, IReadOnlyDictionary<string, PartStats> parts)
        {
            if (ports.ValueKind != JsonValueKind.Array)
                return [];

            var list = new List<FitPort>();
            foreach (var port in ports.EnumerateArray())
            {
                var cls = Str(port, "ClassName");
                var editable = port.TryGetProperty("Editable", out var ed) && ed.ValueKind == JsonValueKind.True;
                var children = port.TryGetProperty("Loadout", out var sub) ? Ports(sub, parts) : [];

                var described = cls is not null && parts.ContainsKey(cls);
                var type = Str(port, "Type");

                var accepts = new List<string>();
                if (port.TryGetProperty("CompatibleTypes", out var types) && types.ValueKind == JsonValueKind.Array)
                    foreach (var t in types.EnumerateArray())
                        if (Str(t, "Type") is { } kind) accepts.Add(kind);

                // A turret is told by the port's type or by what it accepts - the
                // Valkyrie's door mounts are WeaponMount ports that accept a
                // TurretBase - and the gun classification needs either kept.
                var turretish = (type is not null && type.StartsWith("Turret", StringComparison.Ordinal))
                    || accepts.Any(a => a.StartsWith("Turret", StringComparison.Ordinal));

                // Editable means something to the bench only when a shop sells what
                // fits: twenty weapon cabinets and a life-support filter slot are
                // editable in the data and decisions for nobody.
                var bench = editable && accepts.Any(Shoppable.Contains);

                if (!bench && !described && children.Count == 0 && !turretish)
                    continue;

                if (!bench && !turretish)
                    accepts.Clear();

                var hardpoint = Str(port, "HardpointName") ?? "?";

                list.Add(new FitPort(
                    Str(port, "PortId") ?? hardpoint,
                    hardpoint,
                    cls,
                    type is null ? null : type.Split('.')[0],
                    bench,
                    (int)(Num(port, "MinSize") ?? 0),
                    (int)(Num(port, "MaxSize") ?? Num(port, "MinSize") ?? 0),
                    accepts,
                    children));
            }

            return list;
        }
    }

    /// <summary>A nested element by property path, or default when any step is missing.</summary>
    private static JsonElement At(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var step in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(step, out var next))
                return default;
            current = next;
        }

        return current;
    }

    /// <summary>A nested number by property path, or zero.</summary>
    private static double Number(JsonElement element, params string[] path)
    {
        var found = At(element, path);
        return found.ValueKind == JsonValueKind.Number ? found.GetDouble() : 0;
    }
}
