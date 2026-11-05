namespace Quantumwake.Core.GameData;

/// <summary>
/// What a ship part does, as the install describes it: the figures its in-game
/// description leaves out. Each is null when the part is not that kind of
/// thing, or the install does not say.
/// </summary>
/// <param name="Class">The entity class - <c>SHLD_BEHR_S02_7MA_SCItem</c>.</param>
/// <param name="Type">The item type - Shield, Cooler, PowerPlant, QuantumDrive, WeaponGun, Missile.</param>
/// <param name="ShieldHp">A shield generator's pool.</param>
/// <param name="ShieldRegen">Points a second it regenerates.</param>
/// <param name="DamagedDelay">Seconds after a hit before regeneration starts.</param>
/// <param name="DownedDelay">Seconds after the pool is emptied before it starts.</param>
/// <param name="PowerOutput">A power plant's output, in the power segments the ship's power screen counts.</param>
/// <param name="CoolingOutput">A cooler's coolant a second, in the game's standard resource units.</param>
/// <param name="QuantumSpeed">A quantum drive's top speed, metres a second.</param>
/// <param name="SpoolTime">Seconds to spool.</param>
/// <param name="QuantumCooldown">Seconds before it can jump again.</param>
/// <param name="Alpha">A gun's damage per shot, all kinds and pellets added.</param>
/// <param name="DamageKind">The kind that damage mostly is - Energy, Physical, Distortion.</param>
/// <param name="RoundsPerMinute">The gun's first fire mode's rate.</param>
/// <param name="Dps">Damage a second while the trigger is held; null where the mode does not reduce to one figure.</param>
/// <param name="ProjectileSpeed">Metres a second.</param>
/// <param name="Range">How far a round flies before it is gone: speed times lifetime.</param>
/// <param name="MissileDamage">A missile's explosion.</param>
/// <param name="MissileSpeed">Its cruise speed, metres a second.</param>
/// <param name="LockTime">Seconds to lock.</param>
/// <param name="LockRangeMin">The nearest it locks, metres.</param>
/// <param name="LockRangeMax">The furthest.</param>
/// <param name="Tracking">What it locks on to - CrossSection, Infrared, Electromagnetic.</param>
/// <param name="FireKind">The gun's first fire mode - Auto, Semi, Burst, Charge, Beam.</param>
/// <param name="FireModes">How many modes its trigger flattens to; more than one is a gun whose rate depends on how it is used.</param>
/// <param name="DescriptionKey">
/// The text-table key the part's description is under - <c>item_Desc_SHLD_BEHR_S02_7MA</c> -
/// which is not always its class name: a Bengal's cannon borrows another gun's.
/// </param>
public sealed record ShipPartStats(
    string Class,
    string Type,
    double? ShieldHp = null,
    double? ShieldRegen = null,
    double? DamagedDelay = null,
    double? DownedDelay = null,
    double? PowerOutput = null,
    double? CoolingOutput = null,
    double? QuantumSpeed = null,
    double? SpoolTime = null,
    double? QuantumCooldown = null,
    double? Alpha = null,
    string? DamageKind = null,
    double? RoundsPerMinute = null,
    double? Dps = null,
    double? ProjectileSpeed = null,
    double? Range = null,
    double? MissileDamage = null,
    double? MissileSpeed = null,
    double? LockTime = null,
    double? LockRangeMin = null,
    double? LockRangeMax = null,
    string? Tracking = null,
    string? FireKind = null,
    int FireModes = 0,
    string? DescriptionKey = null);

/// <summary>
/// Reads the figures a ship part's description does not print.
/// </summary>
/// <remarks>
/// <para>
/// The game's own description of a cooler says its maker, size, grade and
/// class and nothing about how much it cools. Those numbers are not derived the
/// way a ship's totals are (see docs/datacore.md, "Ships") - each sits on the
/// part's own entity - so they are a lookup, not a reimplementation.
/// </para>
/// <para>
/// Checked against the community dataset on this install rather than assumed:
/// the 7MA 'Lorica' reads 10,000 HP, 2,200 a second, 4.58 s and 9.17 s; the
/// Bracer 34 coolant; the StarHeart 16 power; the Eos 199,272,700 m/s, 5.1 s,
/// 10.8 s; the Tempest II 1,700 damage, 1,029 m/s, 0.1 s. Every one agrees.
/// The dataset's missile "range" is speed times lifetime - 30,870 m for a
/// missile that locks at 10,000 - so the lock range is what is read here.
/// </para>
/// <para>
/// Power and cooling are on <c>ItemResourceComponentParams</c>, as the
/// generation half of a delta in its first state: a power plant generates
/// <c>Power</c> in <c>SPowerSegmentResourceUnit.units</c>, a cooler generates
/// <c>Coolant</c> in <c>SStandardResourceUnit.standardResourceUnits</c>.
/// </para>
/// </remarks>
public static class GameShipParts
{
    private static readonly HashSet<string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        "Shield", "Cooler", "PowerPlant", "QuantumDrive", "WeaponGun", "Missile",
    };

    public static Dictionary<string, ShipPartStats> Read(DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, GameItem> facts)
    {
        var result = new Dictionary<string, ShipPartStats>(StringComparer.OrdinalIgnoreCase);

        var shield = core.StructIndexOf("SCItemShieldGeneratorParams");
        var resource = core.StructIndexOf("ItemResourceComponentParams");
        var quantum = core.StructIndexOf("SCItemQuantumDriveParams");
        var weapon = core.StructIndexOf("SCItemWeaponComponentParams");
        var ammoContainer = core.StructIndexOf("SAmmoContainerComponentParams");
        var missile = core.StructIndexOf("SCItemMissileParams");
        var attach = core.StructIndexOf("SAttachableComponentParams");

        Dictionary<Guid, DataRecord>? byId = null;

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.OrdinalIgnoreCase)) continue;
            var cls = record.Name["EntityClassDefinition.".Length..];
            if (!facts.TryGetValue(cls, out var item) || !Types.Contains(item.Type)) continue;

            var stats = new ShipPartStats(cls, item.Type);
            long weaponAt = -1, ammoAt = -1;

            foreach (var component in core.PointerArray(record, "Components"))
            {
                var at = core.InstanceAt(component);
                var s = component.StructIndex;

                if (s == shield)
                {
                    stats = stats with
                    {
                        ShieldHp = Positive(core.SingleAt(at, s, "MaxShieldHealth")),
                        ShieldRegen = Positive(core.SingleAt(at, s, "MaxShieldRegen")),
                        DamagedDelay = Positive(core.SingleAt(at, s, "DamagedRegenDelay")),
                        DownedDelay = Positive(core.SingleAt(at, s, "DownedRegenDelay")),
                    };
                }
                else if (s == resource)
                {
                    if (item.Type.Equals("PowerPlant", StringComparison.OrdinalIgnoreCase))
                        stats = stats with { PowerOutput = Generated(core, at, s, "Power") };
                    else if (item.Type.Equals("Cooler", StringComparison.OrdinalIgnoreCase))
                        stats = stats with { CoolingOutput = Generated(core, at, s, "Coolant") };
                }
                else if (s == quantum && GameMining.Nested(core, at, s, "params") is { } q)
                {
                    stats = stats with
                    {
                        QuantumSpeed = Positive(core.SingleAt(q.At, q.StructIndex, "driveSpeed")),
                        SpoolTime = Positive(core.SingleAt(q.At, q.StructIndex, "spoolUpTime")),
                        QuantumCooldown = Positive(core.SingleAt(q.At, q.StructIndex, "cooldownTime")),
                    };
                }
                else if (s == weapon) weaponAt = at;
                else if (s == ammoContainer) ammoAt = at;
                else if (s == missile) stats = Missile(core, at, s, stats);
                else if (s == attach) stats = stats with { DescriptionKey = DescriptionKey(core, at, s) };
            }

            if (weaponAt >= 0)
            {
                byId ??= ById(core);
                stats = Gun(core, text, byId, weaponAt, weapon, ammoAt, ammoContainer, stats);
            }

            if (Says(stats)) result[cls] = stats;
        }

        return result;
    }

    /// <summary>The key under <c>AttachDef.Localization.Description</c>, without its leading <c>@</c>.</summary>
    private static string? DescriptionKey(DataCore core, long at, int s)
    {
        var (def, field) = core.FieldAt(at, s, "AttachDef");
        if (def < 0 || field is null) return null;
        var (loc, locField) = core.FieldAt(def, field.StructIndex, "Localization");
        if (loc < 0 || locField is null) return null;
        var key = core.StringAt(loc, locField.StructIndex, "Description")?.TrimStart('@');
        return key is { Length: > 0 } ? key : null;
    }

    private static Dictionary<Guid, DataRecord> ById(DataCore core)
    {
        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);
        return byId;
    }

    /// <summary>Whether a part came out with anything to print.</summary>
    private static bool Says(ShipPartStats s) =>
        s.ShieldHp is not null || s.PowerOutput is not null || s.CoolingOutput is not null || s.QuantumSpeed is not null
        || s.Alpha is not null || s.ProjectileSpeed is not null || s.Dps is not null || s.MissileDamage is not null;

    /// <summary>
    /// What the part's first resource state generates of one resource, or null.
    /// </summary>
    /// <remarks>
    /// The first state is the one the part is in when it is simply switched on;
    /// later states are what a power-saving mode or a misfire puts it in.
    /// </remarks>
    private static double? Generated(DataCore core, long at, int s, string resourceName)
    {
        var states = Items(core, at, s, "states");
        if (states.Count == 0) return null;
        var state = states[0];
        var sat = core.InstanceAt(state);

        foreach (var delta in Items(core, sat, state.StructIndex, "deltas"))
        {
            var dat = core.InstanceAt(delta);
            if (GameMining.Nested(core, dat, delta.StructIndex, "generation") is not { } generation) continue;
            if (!string.Equals(core.EnumAt(generation.At, generation.StructIndex, "resource"), resourceName, StringComparison.OrdinalIgnoreCase)) continue;
            if (core.PointerAt(generation.At, generation.StructIndex, "resourceAmountPerSecond") is not { } unit) continue;

            var uat = core.InstanceAt(unit);
            var value = core.SingleAt(uat, unit.StructIndex, "units") ?? core.SingleAt(uat, unit.StructIndex, "standardResourceUnits")
                ?? (double?)core.Int32At(uat, unit.StructIndex, "units") ?? core.Int32At(uat, unit.StructIndex, "standardResourceUnits");
            return value is > 0 ? Math.Round(value.Value, 3) : null;
        }

        return null;
    }

    /// <summary>An array property's entries, whichever of the two array shapes it is stored as.</summary>
    private static IReadOnlyList<DataCore.Pointer> Items(DataCore core, long at, int s, string name)
    {
        var property = core.StructProperties(s).FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (property is null) return [];
        return property.DataType == 0x0010 ? core.ClassArrayAt(at, s, name) : core.PointerArrayAt(at, s, name);
    }

    /// <summary>
    /// A ship gun: what one shot does, how often, how fast and how far.
    /// </summary>
    /// <remarks>
    /// The fire modes are the personal-weapon reader's own, because a ship gun
    /// is built from the same structs - a Sequence of Single shots at so many
    /// RPM for the M4A. A ship gun carries its ammo on itself rather than in a
    /// magazine record, so the gun's own ammo container is tried first. Damage a
    /// second is given only where it is one figure: a held rapid or single
    /// trigger is the shot times the rate, a beam says it outright, and a burst
    /// or a charge depends on how the trigger is used, so those say only the shot.
    /// </remarks>
    private static ShipPartStats Gun(
        DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<Guid, DataRecord> byId,
        long weaponAt, int weapon, long ammoAt, int ammoContainer, ShipPartStats stats)
    {
        DataRecord? ammo = null;
        if (ammoAt >= 0 && core.ReferenceAt(ammoAt, ammoContainer, "ammoParamsRecord") is { } own && byId.TryGetValue(own, out var found))
            ammo = found;
        else if (core.ReferenceAt(weaponAt, weapon, "ammoContainerRecord") is { } mid && byId.TryGetValue(mid, out var magazine))
        {
            foreach (var component in core.PointerArray(magazine, "Components"))
            {
                if (component.StructIndex != ammoContainer) continue;
                if (core.ReferenceAt(core.InstanceAt(component), ammoContainer, "ammoParamsRecord") is { } aid && byId.TryGetValue(aid, out var a)) ammo = a;
            }
        }

        var modes = new List<FireMode>();
        foreach (var action in core.PointerArrayAt(weaponAt, weapon, "fireActions"))
            modes.AddRange(GameArmoury.Modes(core, text, action, ""));
        if (modes.Count == 0) return stats;
        var mode = modes[0];
        stats = stats with { FireKind = mode.Kind, FireModes = modes.Count };

        if (mode.BeamDamagePerSecond is { IsEmpty: false } beam)
            return stats with { Dps = Math.Round(beam.Total, 1), DamageKind = Dominant(beam), Range = Positive(mode.BeamZeroRange) };

        if (ammo is null) return stats;
        var (hit, speed, lifetime, _, _, _, blast) = GameArmoury.Projectile(core, ammo);
        var shot = mode.Hit is { IsEmpty: false } own2 ? own2 : hit;
        if (shot.IsEmpty) return stats;

        // A charge's shot depends on how long it was held, and a distortion
        // round's damage is not on the projectile where every other one keeps
        // it - all 11 distortion guns read under one point a shot here and 25
        // to 1,040 in the community dataset - so neither gets a figure. Nor does
        // a shell that also explodes: the Nova's 18,500 on contact is 32,000
        // there, and how much of a blast lands is not a property of the gun.
        var raw = shot.Total * Math.Max(1, mode.Pellets);
        var alpha = Math.Round(raw, 1, MidpointRounding.AwayFromZero);
        var plain = mode.Kind is "Auto" or "Semi" && alpha >= 1 && blast is null;
        var rate = plain && mode.RoundsPerMinute > 0 && Settled(core, weaponAt, weapon) ? mode.RoundsPerMinute : (double?)null;

        return stats with
        {
            Alpha = plain ? alpha : null,
            DamageKind = plain ? Dominant(shot) : null,
            RoundsPerMinute = rate,
            Dps = rate is { } rpm ? Math.Round(raw * rpm / 60, 1) : null,
            ProjectileSpeed = Positive(speed),
            Range = speed > 0 && lifetime > 0 ? Math.Round(speed * lifetime) : null,
        };
    }

    /// <summary>
    /// Whether the gun's rate of fire is one number in the files.
    /// </summary>
    /// <remarks>
    /// Most ship guns fire through a looping Sequence: each entry a step at so
    /// many RPM, wrapping a Single shot that has a fire rate of its own. Where
    /// the two agree - the M4A's 100 and 100 - there is nothing to decide. Where
    /// they do not - the Behring ballistic repeater steps at 900 around shots
    /// rated 500 - which one the game obeys is not something the files say, and
    /// the community dataset took the other: 34 of 181 guns read a different
    /// rate there, every one of them a gun like this. Those print no rate and no
    /// damage a second rather than a figure that is wrong by half for one side.
    /// </remarks>
    private static bool Settled(DataCore core, long weaponAt, int weapon)
    {
        foreach (var action in core.PointerArrayAt(weaponAt, weapon, "fireActions"))
        {
            var at = core.InstanceAt(action);
            var s = action.StructIndex;
            switch (core.StructName(s))
            {
                case "SWeaponActionFireRapidParams" or "SWeaponActionFireSingleParams":
                    continue;
                case "SWeaponActionSequenceParams":
                    foreach (var entry in Items(core, at, s, "sequenceEntries"))
                    {
                        var eat = core.InstanceAt(entry);
                        var es = entry.StructIndex;
                        if (!string.Equals(core.EnumAt(eat, es, "unit"), "RPM", StringComparison.OrdinalIgnoreCase)) return false;
                        var step = core.SingleAt(eat, es, "delay") ?? core.Int32At(eat, es, "delay");
                        if (core.PointerAt(eat, es, "weaponAction") is not { } inner) return false;
                        var iat = core.InstanceAt(inner);
                        var shot = core.Int32At(iat, inner.StructIndex, "fireRate") ?? core.SingleAt(iat, inner.StructIndex, "fireRate");
                        if (step is null || shot is null || Math.Abs(step.Value - shot.Value) > 0.5) return false;
                    }
                    continue;
                default:
                    return false;
            }
        }
        return true;
    }

    private static ShipPartStats Missile(DataCore core, long at, int s, ShipPartStats stats)
    {
        double? damage = null;
        if (GameMining.Nested(core, at, s, "explosionParams") is { } blast)
            damage = Positive(GameArmoury.Damage(core, core.PointerAt(blast.At, blast.StructIndex, "damage")).Total);

        double? speed = GameMining.Nested(core, at, s, "GCSParams") is { } gcs ? Positive(core.SingleAt(gcs.At, gcs.StructIndex, "linearSpeed")) : null;

        double? lockTime = null, min = null, max = null;
        string? tracking = null;
        if (GameMining.Nested(core, at, s, "targetingParams") is { } t)
        {
            lockTime = Positive(core.SingleAt(t.At, t.StructIndex, "lockTime"));
            min = Positive(core.SingleAt(t.At, t.StructIndex, "lockRangeMin"));
            max = Positive(core.SingleAt(t.At, t.StructIndex, "lockRangeMax"));
            tracking = core.EnumAt(t.At, t.StructIndex, "trackingSignalType");
        }

        return stats with { MissileDamage = damage, MissileSpeed = speed, LockTime = lockTime, LockRangeMin = min, LockRangeMax = max, Tracking = tracking };
    }

    private static string? Dominant(DamageKinds d)
    {
        var kinds = new (string Name, double Value)[]
        {
            ("Physical", d.Physical), ("Energy", d.Energy), ("Distortion", d.Distortion),
            ("Thermal", d.Thermal), ("Biochemical", d.Biochemical), ("Stun", d.Stun),
        };
        var top = kinds.MaxBy(k => k.Value);
        return top.Value > 0 ? top.Name : null;
    }

    private static double? Positive(float? value) => value is > 0 ? Math.Round((double)value.Value, 3) : null;

    private static double? Positive(double value) => value > 0 ? Math.Round(value, 3) : null;
}
