namespace Quantumwake.Core.GameData;

/// <summary>Damage by the six kinds the game tracks - one hit, or one second of a beam.</summary>
public sealed record DamageKinds(
    double Physical = 0,
    double Energy = 0,
    double Distortion = 0,
    double Thermal = 0,
    double Biochemical = 0,
    double Stun = 0)
{
    public static readonly DamageKinds None = new();

    public double Total => Physical + Energy + Distortion + Thermal + Biochemical + Stun;
    public bool IsEmpty => Total <= 0;

    /// <summary>The kind most of it is - "physical", "energy" - or "mixed" when no kind is two thirds of the whole.</summary>
    public string Dominant
    {
        get
        {
            var total = Total;
            if (total <= 0) return "";
            var top = new[] { ("physical", Physical), ("energy", Energy), ("distortion", Distortion), ("thermal", Thermal), ("biochemical", Biochemical), ("stun", Stun) }
                .MaxBy(k => k.Item2);
            return top.Item2 >= total * 2 / 3 ? top.Item1 : "mixed";
        }
    }

    public DamageKinds Times(double factor) =>
        new(Physical * factor, Energy * factor, Distortion * factor, Thermal * factor, Biochemical * factor, Stun * factor);
}

/// <summary>What a gun's projectile does when it goes off rather than when it hits: a rocket, a grenade round.</summary>
/// <param name="Radius">The blast's full-damage radius, metres.</param>
/// <param name="OuterRadius">Where the blast reaches nothing, metres.</param>
public sealed record Explosion(DamageKinds Damage, double Radius, double OuterRadius);

/// <summary>
/// One way a gun fires, as the files lay it out: the HUD's word for it, the
/// cyclic rate, how many projectiles a pull sends, and what a charge or a
/// beam does instead of a shot.
/// </summary>
/// <param name="Name">What the HUD prints - AUTO, SEMI, BURST, CHARGE, BEAM.</param>
/// <param name="Kind">Auto, Semi, Burst, Charge or Beam.</param>
/// <param name="RoundsPerMinute">The cyclic rate: within a burst for a burst, the cap on the trigger for semi.</param>
/// <param name="Pellets">Projectiles per shot - a shotgun's spread, or a parallel-barrelled pistol's.</param>
/// <param name="AmmoPerShot">Rounds a shot takes from the magazine.</param>
/// <param name="BurstShots">Rounds one pull sends, 0 when not a burst.</param>
/// <param name="BurstCooldown">Seconds after a burst before the next can start.</param>
/// <param name="ChargeSeconds">Seconds to full charge, 0 when not a charge.</param>
/// <param name="ChargeDamageMultiplier">What a full charge does to the hit; 1 is unchanged.</param>
/// <param name="ChargeAmmoMultiplier">What a full charge costs in rounds; 1 is unchanged.</param>
/// <param name="BeamDamagePerSecond">A beam's damage a second at full-damage range; null when not a beam.</param>
/// <param name="BeamFullRange">Metres to which the beam does all of it.</param>
/// <param name="BeamZeroRange">Metres past which it does none.</param>
/// <param name="BeamAmmoPerSecond">Rounds a second a beam draws, at most.</param>
/// <param name="ChargePellets">Projectiles a full charge sends instead, 0 when unchanged - the Salvo's slug becomes seven fragments.</param>
/// <param name="HeatPerShot">Heat a shot adds; energy guns overheat, ballistic ones read 0.</param>
/// <param name="Condition">When this mode takes over from another - "heat at 40% or more" - or empty.</param>
/// <param name="SecondaryAmmo">True when the mode fires the magazine's second load - the Arlington's buckshot beside its slug.</param>
/// <param name="Hit">One projectile's damage when it is not the gun's own - the second load's - or null.</param>
public sealed record FireMode(
    string Name,
    string Kind,
    double RoundsPerMinute,
    int Pellets = 1,
    int AmmoPerShot = 1,
    int BurstShots = 0,
    double BurstCooldown = 0,
    double ChargeSeconds = 0,
    double ChargeDamageMultiplier = 1,
    double ChargeAmmoMultiplier = 1,
    DamageKinds? BeamDamagePerSecond = null,
    double BeamFullRange = 0,
    double BeamZeroRange = 0,
    double BeamAmmoPerSecond = 0,
    double ChargePellets = 0,
    double HeatPerShot = 0,
    string Condition = "",
    bool SecondaryAmmo = false,
    DamageKinds? Hit = null);

/// <summary>A gun a pilot carries, as the install describes it.</summary>
/// <param name="Class">The entity class - <c>behr_rifle_ballistic_01</c>.</param>
/// <param name="Name">The game's display name - P4-AR Rifle.</param>
/// <param name="Kind">The shop's word for it - Rifle, Pistol, Sniper Rifle, Shotgun, LMG.</param>
/// <param name="Weight">Small, Medium or Large - the holster it takes.</param>
/// <param name="Damage">One projectile's damage at the muzzle.</param>
/// <param name="ProjectileSpeed">Metres a second.</param>
/// <param name="ProjectileLifetime">Seconds a projectile flies before it is gone.</param>
/// <param name="DropStart">Metres before the damage starts to fall; 0 when it never does.</param>
/// <param name="DropPerMetre">Damage lost per metre past that.</param>
/// <param name="DropFloor">The least a projectile does however far it flew.</param>
/// <param name="Magazine">Rounds in the magazine it ships with.</param>
/// <param name="MagazineClass">That magazine's class, for finding the ammo in a shop.</param>
/// <param name="Mass">Kilograms.</param>
/// <param name="BaseClass">The class this is a finish of, or null for the plain gun.</param>
/// <param name="Explosion">What the round does on arrival, for a launcher; null for a bullet.</param>
public sealed record PersonalWeapon(
    string Class,
    string Name,
    string Kind,
    string Weight,
    int Size,
    string Manufacturer,
    DamageKinds Damage,
    double ProjectileSpeed,
    double ProjectileLifetime,
    double DropStart,
    double DropPerMetre,
    double DropFloor,
    int Magazine,
    string MagazineClass,
    double Mass,
    IReadOnlyList<FireMode> Modes,
    string? BaseClass = null,
    Explosion? Explosion = null);

/// <summary>How a piece of armour scales what hits it: 0.7 is thirty percent off.</summary>
/// <param name="Macro">The shared table it reads from - <c>MediumArmor</c>; every piece of a class shares one.</param>
/// <param name="Impact">The multiplier on the force of an impact.</param>
public sealed record DamageResistances(
    string Macro,
    double Physical,
    double Energy,
    double Distortion,
    double Thermal,
    double Biochemical,
    double Stun,
    double Impact);

/// <summary>A piece of armour, as the install describes it.</summary>
/// <param name="Slot">Helmet, Core, Arms, Legs, Undersuit or Backpack.</param>
/// <param name="Weight">Light, Medium or Heavy; empty for an undersuit or a backpack.</param>
/// <param name="Kind">The shop's word - "Armor: Core".</param>
/// <param name="Family">The set's name, so colours of one set sit together - "Testudo" for Testudo Core Deathblow.</param>
/// <param name="Resistances">Null for a backpack, which stops nothing.</param>
/// <param name="Protects">The body parts it covers, as the game names them.</param>
/// <param name="TemperatureMin">The coldest it keeps you comfortable in, °C.</param>
/// <param name="TemperatureMax">The hottest.</param>
/// <param name="RadiationCapacity">How much radiation it soaks before you take it.</param>
/// <param name="RadiationDissipation">How fast it sheds what it soaked, a second.</param>
/// <param name="GForceResistance">The undersuit's g tolerance; 0 where the piece says nothing.</param>
/// <param name="CapacityMicroScu">What it carries, in millionths of an SCU; 0 for none.</param>
/// <param name="EmSignature">Electromagnetic emission the piece adds.</param>
/// <param name="IrSignature">Infrared.</param>
/// <param name="MotionPenalty">How much a broken piece slows you, 0 to 1.</param>
/// <param name="ViewPenalty">How much it narrows the view when broken.</param>
public sealed record ArmourPiece(
    string Class,
    string Name,
    string Slot,
    string Weight,
    string Kind,
    string Manufacturer,
    string Family,
    DamageResistances? Resistances,
    IReadOnlyList<string> Protects,
    double TemperatureMin,
    double TemperatureMax,
    double RadiationCapacity,
    double RadiationDissipation,
    double GForceResistance,
    long CapacityMicroScu,
    double EmSignature,
    double IrSignature,
    double MotionPenalty,
    double ViewPenalty,
    double Mass);

/// <summary>
/// A knife. The blade's figures come from a <c>MeleeCombatConfig</c> record the
/// knife points at, and on this install every knife but the three gun-game
/// variants points at the same one - which is the finding, and the page says
/// it rather than printing 30 down a column.
/// </summary>
/// <param name="Slash">One slash's damage.</param>
/// <param name="Stab">One stab's damage.</param>
/// <param name="Impulse">The push a hit gives, in the game's own unit.</param>
/// <param name="Config">The record the figures come from - <c>KnifeMeleeCombat</c>.</param>
public sealed record MeleeWeapon(
    string Class,
    string Name,
    string Manufacturer,
    double Mass,
    DamageKinds Slash,
    DamageKinds Stab,
    double Impulse,
    string Config,
    string? BaseClass = null);

/// <summary>A lingering area a grenade leaves: so much damage every so often within a radius.</summary>
public sealed record Hazard(DamageKinds PerHit, double PeriodSeconds, double Radius);

/// <summary>
/// A grenade. What sets it off is a trigger on its triggerable-devices
/// component - a timer with a duration, or impact - and what it does is the
/// explosion behaviour behind that trigger; a grenade that leaves something
/// behind spawns a hazard-zone entity, read here as <see cref="Hazard"/>.
/// </summary>
/// <param name="Trigger">"timer" or "impact".</param>
/// <param name="FuseSeconds">The timer's duration; 0 on impact.</param>
/// <param name="Blast">The explosion, or null when the grenade only leaves a zone.</param>
/// <param name="Pressure">The blast's pressure figure, the game's own unit; what throws things.</param>
public sealed record Throwable(
    string Class,
    string Name,
    string Manufacturer,
    double Mass,
    string Trigger,
    double FuseSeconds,
    Explosion? Blast,
    double Pressure,
    Hazard? Hazard,
    string? BaseClass = null);

/// <summary>
/// What an attachment does to the gun it sits on: multipliers on the gun's
/// own figures, 1 being no change. Read from the <c>SWeaponStats</c> block on
/// the attachment's <c>SWeaponModifierComponentParams</c>.
/// </summary>
/// <param name="Damage">On every hit.</param>
/// <param name="FireRate">On the cyclic rate.</param>
/// <param name="Spread">On the cone, at rest and firing; a suppressor widens it, a laser narrows it.</param>
/// <param name="RecoilStrength">On the kick.</param>
/// <param name="RecoilTime">On how long the kick lasts.</param>
/// <param name="Sound">On how far a shot is heard.</param>
/// <param name="Heat">On the heat a shot makes.</param>
/// <param name="AmmoCost">On rounds per shot.</param>
/// <param name="ChargeTime">On a charged mode's charge.</param>
/// <param name="ProjectileSpeed">On the round's speed.</param>
/// <param name="Pellets">Added to a shot's pellets; negative takes them away.</param>
/// <param name="BurstShots">Added to a burst's shots.</param>
/// <param name="Zoom">A sight's magnification; 0 for no sight.</param>
/// <param name="SecondZoom">The sight's second magnification, for one that switches; 0 when it has none.</param>
/// <param name="ZoomTime">On how long aiming down it takes.</param>
/// <param name="ZeroingMax">Metres the sight can be zeroed out to; 0 when it cannot.</param>
/// <param name="ZeroingStep">Metres per zeroing click.</param>
public sealed record AttachmentEffect(
    double Damage = 1,
    double FireRate = 1,
    double Spread = 1,
    double RecoilStrength = 1,
    double RecoilTime = 1,
    double Sound = 1,
    double Heat = 1,
    double AmmoCost = 1,
    double ChargeTime = 1,
    double ProjectileSpeed = 1,
    int Pellets = 0,
    int BurstShots = 0,
    double Zoom = 0,
    double SecondZoom = 0,
    double ZoomTime = 1,
    double ZeroingMax = 0,
    double ZeroingStep = 0)
{
    public static readonly AttachmentEffect None = new();

    /// <summary>True when the block changes nothing the page can show - a flashlight.</summary>
    public bool IsNone => this == None;
}

/// <summary>
/// A sight, a barrel piece or an underbarrel piece for a personal weapon.
/// </summary>
/// <param name="Kind">Sight, Barrel or Underbarrel - the slot it goes in.</param>
/// <param name="Family">What it is within the slot: Suppressor, Compensator, Stabilizer, Telescopic, Holographic, Reflex, Monitor, Laser pointer, Flashlight.</param>
/// <param name="Size">The slot size it takes, 1 to 3.</param>
/// <param name="Icon">
/// The game's own picture of it, as an archive entry under
/// <c>Data\UI\Textures\PlayerUI\WeaponAttachment\Icons\</c>, or null. The
/// folder holds 40 on this install, named by class: the plain barrels and
/// most sights, none of the Vera/Torrent/Stark/Escalate variants. A finish
/// wears its plain item's.
/// </param>
public sealed record WeaponAttachment(
    string Class,
    string Name,
    string Kind,
    string Family,
    int Size,
    string Manufacturer,
    double Mass,
    AttachmentEffect Effect,
    string? BaseClass = null,
    string? Icon = null);

/// <summary>Everything the install says about what a pilot wears and carries.</summary>
public sealed record GameArmouryData(
    List<PersonalWeapon> Weapons,
    List<ArmourPiece> Armour,
    List<MeleeWeapon>? Melee = null,
    List<Throwable>? Throwables = null,
    List<WeaponAttachment>? Attachments = null)
{
    public static readonly GameArmouryData Empty = new([], [], [], [], []);

    // The three came later than the two, and a cache written before them
    // deserialises with nulls; empty lists keep every caller simple.
    public List<MeleeWeapon> Melee { get; init; } = Melee ?? [];
    public List<Throwable> Throwables { get; init; } = Throwables ?? [];
    public List<WeaponAttachment> Attachments { get; init; } = Attachments ?? [];
}

/// <summary>
/// Reads personal weapons and armour out of the install.
/// </summary>
/// <remarks>
/// <para>
/// A gun's damage is not on the gun. <c>SCItemWeaponComponentParams</c>
/// names the magazine it ships with (<c>ammoContainerRecord</c>), the
/// magazine's <c>SAmmoContainerComponentParams</c> names an
/// <c>AmmoParams</c> record, and that carries the projectile: speed,
/// lifetime, a <c>DamageInfo</c> per hit and a distance-drop block. The
/// gun keeps the fire modes. Checked on this install against the community
/// tables on 2026-09-16: the P4-AR reads 12 physical at 810 a minute from a
/// 40-round magazine, the P6-LR 100 at 55 from 8, the S-38 22.5 at 450.
/// </para>
/// <para>
/// The fire modes come in a small zoo of structs. Rapid, Single, Burst,
/// Charged and Beam are the leaves; Sequence, Parallel and DynamicCondition
/// wrap them - a Scalpel's two barrels are a Sequence fired "Automatically"
/// (both per pull) or "Individually" (one per pull), a Tripledown's three are
/// a Parallel, a Parallax turns its rapid fire into a beam when its heat
/// passes 40%. Each wrapper is flattened to what the trigger does, so the
/// page can compare a rifle to a rifle without knowing the tree.
/// </para>
/// <para>
/// Armour's resistances are not per item either. Every
/// <c>SCItemSuitArmorParams</c> points at one of twelve
/// <c>DamageResistanceMacro</c> records, and every medium piece points at
/// the same one: 0.7 on every kind, 0.55 on stun. What differs between two
/// medium cores is the rest of the block - temperature range, radiation,
/// capacity, signature, mass - which is what the table shows, and the page
/// says so rather than letting a column of identical 30%s look like a
/// finding.
/// </para>
/// </remarks>
public static class GameArmoury
{
    private static readonly string[] SlotWords = ["Helmet", "Core", "Arms", "Legs", "Pants", "Backpack", "Undersuit", "Flight Suit", "Flightsuit", "Suit", "Torso"];

    public static GameArmouryData Read(DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, GameItem> facts)
    {
        var data = new GameArmouryData([], [], [], [], []);

        var attach = core.StructIndexOf("SAttachableComponentParams");
        var weapon = core.StructIndexOf("SCItemWeaponComponentParams");
        var melee = core.StructIndexOf("SMeleeWeaponComponentParams");
        var devices = core.StructIndexOf("EntityComponentTriggerableDevicesParams");
        var hazard = core.StructIndexOf("HazardComponentParams");
        var modifier = core.StructIndexOf("SWeaponModifierComponentParams");
        var ammoContainer = core.StructIndexOf("SAmmoContainerComponentParams");
        var suit = core.StructIndexOf("SCItemSuitArmorParams");
        var helmet = core.StructIndexOf("SCItemSuitHelmetParams");
        var clothing = core.StructIndexOf("SCItemClothingParams");
        var inventory = core.StructIndexOf("SCItemInventoryContainerComponentParams");
        var purchasable = core.StructIndexOf("SCItemPurchasableParams");
        var physics = core.StructIndexOf("SEntityPhysicsControllerParams");
        if (attach < 0 || weapon < 0) return data;

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.OrdinalIgnoreCase)) continue;
            var cls = record.Name["EntityClassDefinition.".Length..];
            if (!facts.TryGetValue(cls, out var item)) continue;
            // An item the game has not named is one nobody can buy or be sold.
            if (item.Name.Length == 0 || item.Name == cls) continue;

            if (item.Type.Equals("WeaponPersonal", StringComparison.OrdinalIgnoreCase))
            {
                if (item.SubType.Equals("Knife", StringComparison.OrdinalIgnoreCase))
                {
                    if (Knife(core, record, cls, item, byId, melee, physics) is { } knife) data.Melee.Add(knife);
                }
                else if (item.SubType.Equals("Grenade", StringComparison.OrdinalIgnoreCase))
                {
                    if (Grenade(core, record, cls, item, byId, devices, hazard, physics) is { } grenade) data.Throwables.Add(grenade);
                }
                else if (Weapon(core, text, record, cls, item, byId, weapon, ammoContainer, purchasable, physics) is { } gun) data.Weapons.Add(gun);
            }
            else if (item.Type.Equals("WeaponAttachment", StringComparison.OrdinalIgnoreCase))
            {
                if (Attachment(core, record, cls, item, modifier, physics) is { } piece) data.Attachments.Add(piece);
            }
            else if (item.Type.StartsWith("Char_Armor_", StringComparison.OrdinalIgnoreCase))
            {
                if (Armour(core, text, record, cls, item, byId, suit, helmet, clothing, inventory, purchasable, physics) is { } piece) data.Armour.Add(piece);
            }
        }

        // A finish is the gun whose class it extends: behr_rifle_ballistic_01_black01
        // is the P4-AR in black. The longest such prefix that is itself a gun
        // is the base, so a finish of a finish still lands on the plain one.
        var classes = data.Weapons.Select(w => w.Class).ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < data.Weapons.Count; i++)
        {
            var w = data.Weapons[i];
            string? best = null;
            foreach (var candidate in classes)
            {
                if (candidate.Length >= w.Class.Length || !w.Class.StartsWith(candidate + "_", StringComparison.OrdinalIgnoreCase)) continue;
                if (best is null || candidate.Length > best.Length) best = candidate;
            }
            if (best is not null) data.Weapons[i] = w with { BaseClass = best };
        }

        // A finish whose class does not extend the plain gun's - the Yubarev
        // "Deadeye" is lbco_pistol_energy_cen01 beside lbco_pistol_energy_01 -
        // is still one when its name without the quoted finish is the plain
        // gun's name and its figures are the plain gun's figures.
        var plain = data.Weapons.Where(w => w.BaseClass is null).ToList();
        for (var i = 0; i < data.Weapons.Count; i++)
        {
            var w = data.Weapons[i];
            if (w.BaseClass is not null) continue;
            var stem = Unquoted(w.Name);
            if (stem == w.Name) continue;
            var twin = plain.FirstOrDefault(p => p.Class != w.Class && p.Name == stem && p.Magazine == w.Magazine
                && p.Damage == w.Damage && p.ProjectileSpeed == w.ProjectileSpeed);
            if (twin is not null) data.Weapons[i] = w with { BaseClass = twin.Class };
        }

        // Knives and attachments have finishes the way guns do - the Sawtooth
        // "Sirocco" is ksar_melee_01_brown01 - and the same prefix rule finds them.
        // The name has to agree as well as the class: arma_barrel_stab_s1_02 is
        // the Escalate beside arma_barrel_stab_s1's Emod, a different barrel
        // with different figures, where ksar_melee_01_brown01 is the Sawtooth
        // "Sirocco", the Sawtooth in brown.
        var knives = data.Melee.Select(m => (m.Class, m.Name)).ToList();
        for (var i = 0; i < data.Melee.Count; i++)
            if (BaseOf(data.Melee[i].Class, data.Melee[i].Name, knives) is { } b) data.Melee[i] = data.Melee[i] with { BaseClass = b };
        var pieces = data.Attachments.Select(a => (a.Class, a.Name)).ToList();
        for (var i = 0; i < data.Attachments.Count; i++)
            if (BaseOf(data.Attachments[i].Class, data.Attachments[i].Name, pieces) is { } b) data.Attachments[i] = data.Attachments[i] with { BaseClass = b };

        data.Weapons.Sort((a, b) => string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase) is var k && k != 0 ? k : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Armour.Sort((a, b) => string.Compare(a.Slot, b.Slot, StringComparison.OrdinalIgnoreCase) is var s && s != 0 ? s : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Melee.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Throwables.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Attachments.Sort((a, b) => string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase) is var k && k != 0 ? k
            : a.Size != b.Size ? a.Size.CompareTo(b.Size) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return data;
    }

    /// <summary>Where the game keeps an attachment's picture; the class name is the file name.</summary>
    public const string AttachmentIconFolder = @"Data\UI\Textures\PlayerUI\WeaponAttachment\Icons\";

    /// <summary>
    /// Stamps each attachment with the archive's picture of it, given the
    /// entries the folder holds. Read separately from <see cref="Read"/>
    /// because the DataCore does not name these files; only the archive's
    /// own listing says which exist.
    /// </summary>
    public static void StampIcons(GameArmouryData data, IEnumerable<string> entries)
    {
        var have = entries
            .Where(e => e.StartsWith(AttachmentIconFolder, StringComparison.OrdinalIgnoreCase) && e.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
            .Select(e => P4kArchive.EntryStem(e))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < data.Attachments.Count; i++)
        {
            var a = data.Attachments[i];
            var cls = have.Contains(a.Class) ? a.Class : a.BaseClass is not null && have.Contains(a.BaseClass) ? a.BaseClass : null;
            if (cls is not null) data.Attachments[i] = a with { Icon = AttachmentIconFolder + cls + ".dds" };
        }
    }

    /// <summary>
    /// The longest other class this one extends with an underscore and is named
    /// as, quoted finish aside, or null: a finish's plain item.
    /// </summary>
    private static string? BaseOf(string cls, string name, IEnumerable<(string Class, string Name)> items)
    {
        var stem = Unquoted(name);
        string? best = null;
        foreach (var (candidate, candidateName) in items)
        {
            if (candidate.Length >= cls.Length || !cls.StartsWith(candidate + "_", StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.Equals(Unquoted(candidateName), stem, StringComparison.OrdinalIgnoreCase)) continue;
            if (best is null || candidate.Length > best.Length) best = candidate;
        }
        return best;
    }

    /// <summary>The same things the gun reader leaves out: templates, test rigs, the developers' folder, the spawners.</summary>
    private static bool Scaffolding(DataRecord record, string cls) =>
        cls.Contains("template", StringComparison.OrdinalIgnoreCase) || cls.Contains("_test", StringComparison.OrdinalIgnoreCase)
        || cls.Contains("_reference", StringComparison.OrdinalIgnoreCase) || cls.Contains("_toy_", StringComparison.OrdinalIgnoreCase)
        || cls.StartsWith("EntitySpawner", StringComparison.OrdinalIgnoreCase)
        || record.FileName.Contains("/dev/", StringComparison.OrdinalIgnoreCase);

    private static MeleeWeapon? Knife(
        DataCore core, DataRecord record, string cls, GameItem item,
        IReadOnlyDictionary<Guid, DataRecord> byId, int melee, int physics)
    {
        if (melee < 0 || Scaffolding(record, cls)) return null;

        long meleeAt = -1;
        double mass = 0;
        foreach (var component in core.PointerArray(record, "Components"))
        {
            var at = core.InstanceAt(component);
            if (component.StructIndex == melee) meleeAt = at;
            else if (component.StructIndex == physics) mass = Mass(core, at, physics);
        }
        if (meleeAt < 0) return null;

        // The blade's figures are on a MeleeCombatConfig record the knife
        // points at, as an array of attack categories - BladeSlash, BladeStab -
        // each with an inline DamageInfo.
        if (core.ReferenceAt(meleeAt, melee, "meleeCombatConfig") is not { } id || !byId.TryGetValue(id, out var config)) return null;
        var configAt = core.InstanceAt(config, config.VariantIndex);
        var slash = DamageKinds.None;
        var stab = DamageKinds.None;
        double impulse = 0;
        foreach (var category in core.ClassArrayAt(configAt, config.StructIndex, "attackCategoryParams"))
        {
            var at = core.InstanceAt(category);
            var s = category.StructIndex;
            var damage = InlineDamage(core, at, s, "damageInfo");
            var action = core.EnumAt(at, s, "actionCategory") ?? "";
            if (action.Contains("Slash", StringComparison.OrdinalIgnoreCase)) slash = damage;
            else if (action.Contains("Stab", StringComparison.OrdinalIgnoreCase)) stab = damage;
            impulse = Math.Max(impulse, R(core.SingleAt(at, s, "attackImpulse")));
        }
        if (slash.IsEmpty && stab.IsEmpty) return null;

        var configName = config.Name.Contains('.') ? config.Name[(config.Name.LastIndexOf('.') + 1)..] : config.Name;
        return new MeleeWeapon(cls, item.Name, item.Manufacturer, mass, slash, stab, impulse, configName);
    }

    private static Throwable? Grenade(
        DataCore core, DataRecord record, string cls, GameItem item,
        IReadOnlyDictionary<Guid, DataRecord> byId, int devices, int hazard, int physics)
    {
        if (devices < 0 || Scaffolding(record, cls)) return null;

        long devicesAt = -1;
        double mass = 0;
        foreach (var component in core.PointerArray(record, "Components"))
        {
            var at = core.InstanceAt(component);
            if (component.StructIndex == devices) devicesAt = at;
            else if (component.StructIndex == physics) mass = Mass(core, at, physics);
        }
        // A glowstick is filed as a grenade and has no triggers: it is not one.
        if (devicesAt < 0) return null;

        var trigger = "";
        double fuse = 0, pressure = 0;
        Explosion? blast = null;
        Hazard? zone = null;
        foreach (var pointer in core.PointerArrayAt(devicesAt, devices, "triggers"))
        {
            var at = core.InstanceAt(pointer);
            var s = pointer.StructIndex;
            var kind = core.StructName(s);
            if (core.PointerAt(at, s, "behavior") is not { } behaviour) continue;
            var bat = core.InstanceAt(behaviour);
            var bs = behaviour.StructIndex;
            var doing = core.StructName(bs);

            if (doing.Contains("Explosion", StringComparison.OrdinalIgnoreCase)
                && GameMining.Nested(core, bat, bs, "explosionParams") is { } explosion)
            {
                var damage = Damage(core, core.PointerAt(explosion.At, explosion.StructIndex, "damage"));
                blast = new Explosion(damage, R(core.SingleAt(explosion.At, explosion.StructIndex, "minRadius")), R(core.SingleAt(explosion.At, explosion.StructIndex, "maxRadius")));
                pressure = R(core.SingleAt(explosion.At, explosion.StructIndex, "pressure"));
                // The trigger in front of the explosion is what sets the grenade off.
                trigger = kind.Contains("Timer", StringComparison.OrdinalIgnoreCase) ? "timer"
                    : kind.Contains("Impact", StringComparison.OrdinalIgnoreCase) ? "impact" : "";
                if (trigger == "timer") fuse = R(core.SingleAt(at, s, "duration"));
            }
            else if (doing.Contains("SpawnEntity", StringComparison.OrdinalIgnoreCase)
                && core.ReferenceAt(bat, bs, "entityToSpawn") is { } spawnId && byId.TryGetValue(spawnId, out var spawned))
            {
                zone = HazardOf(core, spawned, hazard);
            }
        }
        // A blast of nothing - the plasma grenade's 2 thermal is a fuse, not a
        // weapon - still counts when a zone follows it; a grenade with neither
        // is a flare.
        if (blast is null && zone is null) return null;
        if (blast is { Damage.IsEmpty: true }) blast = null;

        return new Throwable(cls, item.Name, item.Manufacturer, mass, trigger, fuse, blast, pressure, zone);
    }

    /// <summary>The damaging area a spawned entity is, or null when it is not one.</summary>
    private static Hazard? HazardOf(DataCore core, DataRecord spawned, int hazard)
    {
        if (hazard < 0) return null;
        foreach (var component in core.PointerArray(spawned, "Components"))
        {
            if (component.StructIndex != hazard) continue;
            var at = core.InstanceAt(component);
            var perHit = Damage(core, core.PointerAt(at, hazard, "damagePerHit"));
            if (perHit.IsEmpty) return null;
            var period = R(core.SingleAt(at, hazard, "damagePeriod"));
            double radius = 0;
            if (core.PointerAt(at, hazard, "hazardAreaShape") is { } shape)
                radius = R(core.SingleAt(core.InstanceAt(shape), shape.StructIndex, "radius"));
            return new Hazard(perHit, period, radius);
        }
        return null;
    }

    private static WeaponAttachment? Attachment(DataCore core, DataRecord record, string cls, GameItem item, int modifier, int physics)
    {
        // Sights, barrels and underbarrel pieces; the rest of the type is a
        // gun's own internals - firing mechanism, power array, ventilation -
        // and the ship-weapon barrels, which live outside weapon_modifier/.
        var kind = item.SubType switch
        {
            "IronSight" => "Sight",
            "Barrel" => "Barrel",
            "BottomAttachment" => "Underbarrel",
            _ => null,
        };
        if (kind is null || modifier < 0 || Scaffolding(record, cls)) return null;
        if (!record.FileName.Contains("/weapon_modifier/", StringComparison.OrdinalIgnoreCase)) return null;
        // The binoculars carry a fake optic so they can be aimed, and a mount
        // wears a sight for the hologram; neither goes on a rifle.
        if (cls.Contains("FakeOptic", StringComparison.OrdinalIgnoreCase) || cls.StartsWith("weaponMount_", StringComparison.OrdinalIgnoreCase)) return null;

        long modifierAt = -1;
        double mass = 0;
        foreach (var component in core.PointerArray(record, "Components"))
        {
            var at = core.InstanceAt(component);
            if (component.StructIndex == modifier) modifierAt = at;
            else if (component.StructIndex == physics) mass = Mass(core, at, physics);
        }
        if (modifierAt < 0) return null;

        var effect = AttachmentEffect.None;
        if (GameMining.Nested(core, modifierAt, modifier, "modifier") is { } block
            && GameMining.Nested(core, block.At, block.StructIndex, "weaponStats") is { } stats)
        {
            var (at, s) = stats;
            double M(string name) => R(core.SingleAt(at, s, name), 1);
            double zoom = 0, secondZoom = 0, zoomTime = 1, spread = 1, recoilStrength = 1, recoilTime = 1;
            if (GameMining.Nested(core, at, s, "aimModifier") is { } aim)
            {
                zoom = R(core.SingleAt(aim.At, aim.StructIndex, "zoomScale"));
                secondZoom = R(core.SingleAt(aim.At, aim.StructIndex, "secondZoomScale"));
                zoomTime = R(core.SingleAt(aim.At, aim.StructIndex, "zoomTimeScale"), 1);
            }
            // The spread block scales the cone at rest and while firing by the
            // same factor on every attachment on this install; one number says it.
            if (GameMining.Nested(core, at, s, "spreadModifier") is { } cone)
                spread = R(core.SingleAt(cone.At, cone.StructIndex, "attackMultiplier"), 1);
            if (GameMining.Nested(core, at, s, "recoilModifier") is { } kick)
            {
                recoilStrength = R(core.SingleAt(kick.At, kick.StructIndex, "fireRecoilStrengthMultiplier"), 1);
                recoilTime = R(core.SingleAt(kick.At, kick.StructIndex, "fireRecoilTimeMultiplier"), 1);
            }
            double zeroMax = 0, zeroStep = 0;
            if (core.PointerAt(modifierAt, modifier, "zeroingParams") is { } zeroing)
            {
                var zat = core.InstanceAt(zeroing);
                zeroMax = R(core.SingleAt(zat, zeroing.StructIndex, "maxRange"));
                zeroStep = R(core.SingleAt(zat, zeroing.StructIndex, "rangeIncrement"));
            }
            // Only a sight magnifies; a barrel's block carries a 1 that means
            // nothing. A sight's second zoom is one only when it differs from
            // the first and is more than none.
            if (kind != "Sight") { zoom = 0; secondZoom = 0; }
            else { zoom = Math.Max(zoom, 1); if (secondZoom <= 1 || secondZoom == zoom) secondZoom = 0; }
            effect = new AttachmentEffect(
                Damage: M("damageMultiplier"), FireRate: M("fireRateMultiplier"), Spread: spread,
                RecoilStrength: recoilStrength, RecoilTime: recoilTime, Sound: M("soundRadiusMultiplier"),
                Heat: M("heatGenerationMultiplier"), AmmoCost: M("ammoCostMultiplier"), ChargeTime: M("chargeTimeMultiplier"),
                ProjectileSpeed: M("projectileSpeedMultiplier"),
                Pellets: core.Int32At(at, s, "pellets") ?? 0, BurstShots: core.Int32At(at, s, "burstShots") ?? 0,
                Zoom: zoom, SecondZoom: secondZoom, ZoomTime: zoomTime,
                ZeroingMax: zeroMax, ZeroingStep: zeroStep);
        }

        return new WeaponAttachment(cls, item.Name, kind, AttachmentFamily(cls, item.Name, kind), item.Size, item.Manufacturer, mass, effect);
    }

    /// <summary>
    /// What an attachment is within its slot, from the class token - <c>supp</c>,
    /// <c>comp</c>, <c>stab</c>, <c>tsco</c>, <c>holo</c>, <c>rdot</c>, <c>disp</c>,
    /// <c>lasr</c>, <c>flsh</c> - with the display name's bracketed word as the fallback.
    /// </summary>
    public static string AttachmentFamily(string cls, string name, string kind)
    {
        var lower = cls.ToLowerInvariant();
        if (lower.Contains("_supp_")) return "Suppressor";
        if (lower.Contains("_comp_")) return "Compensator";
        if (lower.Contains("_stab_")) return "Stabilizer";
        if (lower.Contains("_flhd_")) return "Flash hider";
        if (lower.Contains("_tsco_")) return "Telescopic";
        if (lower.Contains("_holo_")) return "Holographic";
        if (lower.Contains("_rdot_")) return "Reflex";
        if (lower.Contains("_disp_")) return "Monitor";
        if (lower.Contains("_lasr_")) return "Laser pointer";
        if (lower.Contains("_flsh_")) return "Flashlight";
        var open = name.IndexOf('(');
        var close = open >= 0 ? name.IndexOf(')', open) : -1;
        if (open >= 0 && close > open)
        {
            var inside = name[(open + 1)..close];
            var space = inside.IndexOf(' ');
            if (space > 0) return inside[(space + 1)..].Trim();
        }
        return kind;
    }

    /// <summary>A DamageInfo written inline in its parent rather than pointed at.</summary>
    private static DamageKinds InlineDamage(DataCore core, long at, int s, string name)
    {
        if (core.PointerAt(at, s, name) is { } pointer) return Damage(core, pointer);
        var (fat, field) = core.FieldAt(at, s, name);
        if (fat < 0 || field is null) return DamageKinds.None;
        double F(string n) => R(core.SingleAt(fat, field.StructIndex, n));
        return new DamageKinds(F("DamagePhysical"), F("DamageEnergy"), F("DamageDistortion"), F("DamageThermal"), F("DamageBiochemical"), F("DamageStun"));
    }

    private static PersonalWeapon? Weapon(
        DataCore core, IReadOnlyDictionary<string, string> text, DataRecord record, string cls, GameItem item,
        IReadOnlyDictionary<Guid, DataRecord> byId, int weapon, int ammoContainer, int purchasable, int physics)
    {
        // Templates, test rigs, the developers' reference guns and the spawners
        // that place a gun in the world wear the same type as the gun; so do
        // the tools, knives and grenades, which the sub-type tells apart - a
        // multi-tool's beam and a toy pistol's dart are not what a holster
        // compares.
        if (item.SubType is not ("Small" or "Medium" or "Large")) return null;
        if (cls.Contains("template", StringComparison.OrdinalIgnoreCase) || cls.Contains("_test", StringComparison.OrdinalIgnoreCase)
            || cls.Contains("_reference", StringComparison.OrdinalIgnoreCase) || cls.Contains("_toy_", StringComparison.OrdinalIgnoreCase)
            || cls.StartsWith("EntitySpawner", StringComparison.OrdinalIgnoreCase)
            || record.FileName.Contains("/dev/", StringComparison.OrdinalIgnoreCase))
            return null;

        long weaponAt = -1;
        string kind = "";
        double mass = 0;
        foreach (var component in core.PointerArray(record, "Components"))
        {
            var at = core.InstanceAt(component);
            if (component.StructIndex == weapon) weaponAt = at;
            else if (component.StructIndex == purchasable) kind = DisplayType(core, text, at, purchasable);
            else if (component.StructIndex == physics) mass = Mass(core, at, physics);
        }
        if (weaponAt < 0) return null;

        // The magazine, then the ammo, then the projectile.
        var magazineId = core.ReferenceAt(weaponAt, weapon, "ammoContainerRecord");
        if (magazineId is not { } mid || !byId.TryGetValue(mid, out var magazine)) return null;
        var magazineClass = magazine.Name["EntityClassDefinition.".Length..];

        var rounds = 0;
        DataRecord? ammo = null, secondary = null;
        foreach (var component in core.PointerArray(magazine, "Components"))
        {
            if (component.StructIndex != ammoContainer) continue;
            var at = core.InstanceAt(component);
            rounds = core.Int32At(at, ammoContainer, "maxAmmoCount") ?? 0;
            if (core.ReferenceAt(at, ammoContainer, "ammoParamsRecord") is { } aid && byId.TryGetValue(aid, out var found)) ammo = found;
            if (core.ReferenceAt(at, ammoContainer, "secondaryAmmoParamsRecord") is { } sid && byId.TryGetValue(sid, out var second)) secondary = second;
        }
        if (ammo is null) return null;

        var (hit, speed, lifetime, dropStart, dropPerMetre, dropFloor, explosion) = Projectile(core, ammo);
        var secondHit = secondary is not null ? Projectile(core, secondary).Hit : null;

        var modes = new List<FireMode>();
        foreach (var action in core.PointerArrayAt(weaponAt, weapon, "fireActions"))
            modes.AddRange(Modes(core, text, action, ""));
        // A mode that fires the magazine's second load carries that load's damage.
        for (var i = 0; i < modes.Count; i++)
            if (modes[i].SecondaryAmmo && secondHit is not null) modes[i] = modes[i] with { Hit = secondHit };

        // A beam does its damage by the second and needs no projectile figure;
        // anything else without one is a tool - a multi-tool, a medical gun -
        // and not a weapon to compare.
        if (hit.IsEmpty && explosion is null && modes.All(m => m.BeamDamagePerSecond is null)) return null;
        if (modes.Count == 0) return null;

        return new PersonalWeapon(cls, item.Name, KindOf(cls, kind), item.SubType, item.Size, item.Manufacturer,
            hit, speed, lifetime, dropStart, dropPerMetre, dropFloor, rounds, magazineClass, mass, modes, null, explosion);
    }

    /// <summary>
    /// The holster's word for a gun, from the class name's own token -
    /// <c>behr_rifle_ballistic_01</c> is a rifle. The shop's display type is
    /// the fallback: it calls the Lumin V SMG a rifle and the Vendetta HMG
    /// "Large", so it is not the first choice.
    /// </summary>
    private static string KindOf(string cls, string shopKind)
    {
        var tokens = cls.Split('_');
        foreach (var token in tokens)
        {
            switch (token.ToLowerInvariant())
            {
                case "pistol": return "Pistol";
                case "smg": return "SMG";
                case "rifle": return "Rifle";
                case "sniper": return "Sniper rifle";
                case "crossbow": return "Crossbow";
                case "shotgun": return "Shotgun";
                case "lmg": return "LMG";
                case "hmg": return "Heavy";
                case "special": return "Heavy";
                case "glauncher": return "Grenade launcher";
            }
        }
        return shopKind;
    }

    /// <summary>What an ammo record's projectile does: its hit, flight and drop, and its blast if it has one.</summary>
    private static (DamageKinds Hit, double Speed, double Lifetime, double DropStart, double DropPerMetre, double DropFloor, Explosion? Explosion) Projectile(DataCore core, DataRecord ammo)
    {
        var ammoAt = core.InstanceAt(ammo, ammo.VariantIndex);
        var speed = R(core.SingleAt(ammoAt, ammo.StructIndex, "speed"));
        var lifetime = R(core.SingleAt(ammoAt, ammo.StructIndex, "lifetime"));

        var hit = DamageKinds.None;
        double dropStart = 0, dropPerMetre = 0, dropFloor = 0;
        Explosion? explosion = null;
        if (core.PointerAt(ammoAt, ammo.StructIndex, "projectileParams") is { } projectile)
        {
            var pat = core.InstanceAt(projectile);
            var ps = projectile.StructIndex;
            hit = Damage(core, core.PointerAt(pat, ps, "damage"));
            if (core.PointerAt(pat, ps, "damageDropParams") is { } drop)
            {
                var dat = core.InstanceAt(drop);
                var ds = drop.StructIndex;
                // The drop is written per damage kind, but only the kinds the
                // projectile does are filled in, so the start is the largest
                // and the rate and floor are the sums. A block with a start
                // and no rate - the Zenith's 500 m - is no drop at all.
                dropPerMetre = Damage(core, core.PointerAt(dat, ds, "damageDropPerMeter")).Total;
                if (dropPerMetre > 0)
                {
                    dropStart = Max(Damage(core, core.PointerAt(dat, ds, "damageDropMinDistance")));
                    dropFloor = Damage(core, core.PointerAt(dat, ds, "damageDropMinDamage")).Total;
                }
                else dropPerMetre = 0;
            }
            if (core.PointerAt(pat, ps, "detonationParams") is { } detonation
                && GameMining.Nested(core, core.InstanceAt(detonation), detonation.StructIndex, "explosionParams") is { } blast)
            {
                var burst = Damage(core, core.PointerAt(blast.At, blast.StructIndex, "damage"));
                if (!burst.IsEmpty)
                    explosion = new Explosion(burst, R(core.SingleAt(blast.At, blast.StructIndex, "minRadius")), R(core.SingleAt(blast.At, blast.StructIndex, "maxRadius")));
            }
        }
        return (hit, speed, lifetime, dropStart, dropPerMetre, dropFloor, explosion);
    }

    /// <summary>
    /// The fire modes under one action, flattened: a wrapper yields what its
    /// leaves do, in the wrapper's name.
    /// </summary>
    private static IEnumerable<FireMode> Modes(DataCore core, IReadOnlyDictionary<string, string> text, DataCore.Pointer action, string condition)
    {
        var at = core.InstanceAt(action);
        var s = action.StructIndex;
        var kind = core.StructName(s);
        var name = ModeName(core, text, at, s);

        switch (kind)
        {
            case "SWeaponActionFireRapidParams":
            {
                var (pellets, cost, second) = Launch(core, at, s);
                yield return new FireMode(name.Length > 0 ? name : "AUTO", "Auto", core.Int32At(at, s, "fireRate") ?? 0, pellets, cost,
                    HeatPerShot: R(core.SingleAt(at, s, "heatPerShot")), Condition: condition, SecondaryAmmo: second);
                break;
            }
            case "SWeaponActionFireSingleParams":
            {
                var (pellets, cost, second) = Launch(core, at, s);
                yield return new FireMode(name.Length > 0 ? name : "SEMI", "Semi", core.Int32At(at, s, "fireRate") ?? 0, pellets, cost,
                    HeatPerShot: R(core.SingleAt(at, s, "heatPerShot")), Condition: condition, SecondaryAmmo: second);
                break;
            }
            case "SWeaponActionFireBurstParams":
            {
                var (pellets, cost, second) = Launch(core, at, s);
                yield return new FireMode(name.Length > 0 ? name : "BURST", "Burst", core.Int32At(at, s, "fireRate") ?? 0, pellets, cost,
                    core.ByteAt(at, s, "shotCount") ?? 0, R(core.SingleAt(at, s, "cooldownTime")),
                    HeatPerShot: R(core.SingleAt(at, s, "heatPerShot")), Condition: condition, SecondaryAmmo: second);
                break;
            }
            case "SWeaponActionFireBeamParams":
            {
                yield return new FireMode(name.Length > 0 ? name : "BEAM", "Beam", 0, 1, 0,
                    BeamDamagePerSecond: Damage(core, core.PointerAt(at, s, "damagePerSecond")),
                    BeamFullRange: R(core.SingleAt(at, s, "fullDamageRange")),
                    BeamZeroRange: R(core.SingleAt(at, s, "zeroDamageRange")),
                    BeamAmmoPerSecond: R(core.SingleAt(at, s, "maxEnergyDraw")),
                    HeatPerShot: R(core.SingleAt(at, s, "heatPerSecond")), Condition: condition);
                break;
            }
            case "SWeaponActionFireChargedParams":
            {
                // The charge wraps the shot it releases; the shot's rate and
                // pellets are the mode's, and the charge's own figures ride on top.
                var inner = core.PointerAt(at, s, "weaponAction") is { } shot ? Modes(core, text, shot, condition).FirstOrDefault() : null;
                var (damageMul, ammoMul, chargePellets) = GameMining.Nested(core, at, s, "maxChargeModifier") is { } max
                    ? (R(core.SingleAt(max.At, max.StructIndex, "damageMultiplier"), 1), R(core.SingleAt(max.At, max.StructIndex, "ammoCostMultiplier"), 1), core.Int32At(max.At, max.StructIndex, "pellets") ?? 0)
                    : (1, 1, 0);
                yield return new FireMode(name.Length > 0 ? name : "CHARGE", "Charge", inner?.RoundsPerMinute ?? 0, inner?.Pellets ?? 1, inner?.AmmoPerShot ?? 1,
                    ChargeSeconds: R(core.SingleAt(at, s, "chargeTime")),
                    ChargeDamageMultiplier: damageMul, ChargeAmmoMultiplier: ammoMul, ChargePellets: chargePellets,
                    HeatPerShot: inner?.HeatPerShot ?? 0, Condition: condition, SecondaryAmmo: inner?.SecondaryAmmo ?? false);
                break;
            }
            case "SWeaponActionSequenceParams":
            {
                foreach (var mode in Sequence(core, text, at, s, name, condition)) yield return mode;
                break;
            }
            case "SWeaponActionParallelParams":
            {
                // Every barrel fires on the pull: one mode with the pellets and
                // the cost added up, at the rate of the first.
                FireMode? merged = null;
                foreach (var barrel in core.PointerArrayAt(at, s, "weaponActions"))
                {
                    foreach (var mode in Modes(core, text, barrel, condition))
                    {
                        merged = merged is null ? mode : merged with { Pellets = merged.Pellets + mode.Pellets, AmmoPerShot = merged.AmmoPerShot + mode.AmmoPerShot };
                    }
                }
                if (merged is not null) yield return name.Length > 0 ? merged with { Name = name } : merged;
                break;
            }
            case "SWeaponActionDynamicConditionParams":
            {
                if (core.PointerAt(at, s, "defaultWeaponAction") is { } fallback)
                    foreach (var mode in Modes(core, text, fallback, condition)) yield return mode;
                foreach (var entry in core.ClassArrayAt(at, s, "conditionalWeaponActions"))
                {
                    var eat = core.InstanceAt(entry);
                    var when = Condition(core, core.PointerAt(eat, entry.StructIndex, "condition"));
                    if (core.PointerAt(eat, entry.StructIndex, "weaponAction") is { } then)
                        foreach (var mode in Modes(core, text, then, when.Length > 0 ? when : condition)) yield return mode;
                }
                break;
            }
        }
    }

    /// <summary>
    /// A sequence of leaf actions, flattened to what the trigger does.
    /// </summary>
    /// <remarks>
    /// <c>Looping</c> cycles the entries while the trigger is held, each
    /// followed by its delay - a Killshot's two barrels alternating at 535 a
    /// minute, a Lumin's three-round bursts with a pause between. The mode
    /// keeps the leaf's cyclic rate and folds the delay into the burst's
    /// cooldown, so the sustained rate the page derives is the loop's.
    /// <c>Automatically</c> sends every entry on one pull - a Scalpel's two
    /// barrels together - which is a burst of that many. <c>Individually</c>
    /// sends one entry a pull, which is semi-automatic at the leaf's rate.
    /// </remarks>
    private static IEnumerable<FireMode> Sequence(DataCore core, IReadOnlyDictionary<string, string> text, long at, int s, string name, string condition)
    {
        var mode = core.EnumAt(at, s, "mode") ?? "";
        var entries = core.ClassArrayAt(at, s, "sequenceEntries");
        var leaves = new List<(FireMode Mode, int Repetitions, double Delay)>();
        foreach (var entry in entries)
        {
            var eat = core.InstanceAt(entry);
            var es = entry.StructIndex;
            if (core.PointerAt(eat, es, "weaponAction") is not { } action) continue;
            var leaf = Modes(core, text, action, condition).FirstOrDefault();
            if (leaf is null) continue;
            var delay = R(core.SingleAt(eat, es, "delay"));
            var unit = core.EnumAt(eat, es, "unit") ?? "Seconds";
            var seconds = unit.Equals("RPM", StringComparison.OrdinalIgnoreCase) ? (delay > 0 ? 60 / delay : 0) : delay;
            leaves.Add((leaf, Math.Max(1, core.Int32At(eat, es, "repetitions") ?? 1), seconds));
        }
        if (leaves.Count == 0) yield break;

        var first = leaves[0].Mode;
        var label = name.Length > 0 ? name : first.Name;
        // A wrapper that changes what the trigger does takes the HUD word for
        // what it became, not the leaf's.
        var own = name.Length > 0;
        var shots = leaves.Sum(l => l.Repetitions * Math.Max(1, l.Mode.BurstShots));

        if (mode.Equals("Individually", StringComparison.OrdinalIgnoreCase))
        {
            yield return first with { Name = own ? label : "SEMI", Kind = "Semi", BurstShots = 0, BurstCooldown = 0 };
        }
        else if (mode.Equals("Automatically", StringComparison.OrdinalIgnoreCase))
        {
            yield return first with { Name = own || shots <= 1 ? label : "BURST", Kind = shots > 1 ? "Burst" : first.Kind, BurstShots = shots > 1 ? shots : 0 };
        }
        else
        {
            // Looping. A loop of single shots is automatic fire at the loop's
            // pace; a loop of bursts is bursts with the delay in the gap. The
            // leaf's own rate is the rate within; the delay between entries
            // is the loop's, and the two are what the sustained figure adds.
            var between = leaves.Sum(l => l.Repetitions * l.Delay);
            if (first.BurstShots > 0)
            {
                yield return first with { Name = label, BurstCooldown = first.BurstCooldown + between / Math.Max(1, leaves.Sum(l => l.Repetitions)) };
            }
            else
            {
                var cycle = between;
                yield return first with { Name = name.Length > 0 ? label : "AUTO", Kind = "Auto", RoundsPerMinute = cycle > 0 ? Math.Round(shots * 60 / cycle) : first.RoundsPerMinute, BurstShots = 0, BurstCooldown = 0 };
            }
        }
    }

    private static string Condition(DataCore core, DataCore.Pointer? condition)
    {
        if (condition is not { } c) return "";
        var at = core.InstanceAt(c);
        return core.StructName(c.StructIndex) switch
        {
            "SWeaponConditionHeatLevel" when GameMining.Nested(core, at, c.StructIndex, "comparer") is { } cmp =>
                $"heat {Comparer(core.EnumAt(cmp.At, cmp.StructIndex, "mode"))} {Math.Round((R(core.SingleAt(cmp.At, cmp.StructIndex, "value"))) * 100)}%",
            var other => other.Replace("SWeaponCondition", "", StringComparison.Ordinal),
        };
    }

    private static string Comparer(string? mode) => mode switch
    {
        "GreaterOrEqual" => "at or above",
        "Greater" => "above",
        "LessOrEqual" => "at or below",
        "Less" => "below",
        _ => "at",
    };

    private static readonly HashSet<string> GenericModeNames = new(StringComparer.OrdinalIgnoreCase)
        { "Rapid", "Single", "Burst", "Charge", "Beam", "Damage Beam", "Sequence", "RapidBeam", "Parallel", "" };

    /// <summary>
    /// The HUD's word for the mode - [AUTO] without its brackets - with the
    /// files' own label after it when that says more: a Prism's slug and its
    /// spread are both SEMI on the HUD. Empty when the HUD has no word.
    /// </summary>
    private static string ModeName(DataCore core, IReadOnlyDictionary<string, string> text, long at, int s)
    {
        var hud = "";
        var key = core.StringAt(at, s, "localisedName");
        if (key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var english) && !GameItems.Unwritten(english))
            hud = english.Trim('[', ']', ' ').ToUpperInvariant();
        var own = core.StringAt(at, s, "name") ?? "";
        return hud.Length > 0 && !GenericModeNames.Contains(own) && !own.Equals(hud, StringComparison.OrdinalIgnoreCase)
            ? $"{hud} {own.ToLowerInvariant()}"
            : hud;
    }

    private static (int Pellets, int Cost, bool Secondary) Launch(DataCore core, long at, int s)
    {
        if (core.PointerAt(at, s, "launchParams") is not { } launch) return (1, 1, false);
        var lat = core.InstanceAt(launch);
        var ls = launch.StructIndex;
        return (Math.Max(1, core.Int32At(lat, ls, "pelletCount") ?? 1), Math.Max(0, core.Int32At(lat, ls, "ammoCost") ?? 1),
            string.Equals(core.EnumAt(lat, ls, "projectileType"), "Secondary", StringComparison.OrdinalIgnoreCase));
    }

    private static DamageKinds Damage(DataCore core, DataCore.Pointer? pointer)
    {
        if (pointer is not { } p) return DamageKinds.None;
        var at = core.InstanceAt(p);
        var s = p.StructIndex;
        double F(string name) => R(core.SingleAt(at, s, name));
        return new DamageKinds(F("DamagePhysical"), F("DamageEnergy"), F("DamageDistortion"), F("DamageThermal"), F("DamageBiochemical"), F("DamageStun"));
    }

    /// <summary>A name with its quoted finish taken out: Yubarev "Deadeye" Pistol is Yubarev Pistol.</summary>
    public static string Unquoted(string name)
    {
        var open = name.IndexOf('"');
        var close = open >= 0 ? name.IndexOf('"', open + 1) : -1;
        if (open < 0 || close < 0) return name;
        return string.Join(' ', (name[..open] + name[(close + 1)..]).Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static double Max(DamageKinds d) => new[] { d.Physical, d.Energy, d.Distortion, d.Thermal, d.Biochemical, d.Stun }.Max();

    private static ArmourPiece? Armour(
        DataCore core, IReadOnlyDictionary<string, string> text, DataRecord record, string cls, GameItem item,
        IReadOnlyDictionary<Guid, DataRecord> byId, int suit, int helmet, int clothing, int inventory, int purchasable, int physics)
    {
        // Invisible stand-ins, placeholders and the NPC-only sets are not for sale.
        if (cls.StartsWith("nodraw_", StringComparison.OrdinalIgnoreCase) || cls.StartsWith("volume_", StringComparison.OrdinalIgnoreCase)
            || item.Name.StartsWith("TEST ", StringComparison.Ordinal)) return null;

        var slot = item.Type["Char_Armor_".Length..] switch
        {
            "Torso" => "Core",
            var other => other,
        };
        var weight = item.SubType is "Light" or "Medium" or "Heavy" ? item.SubType : "";

        string kind = "";
        double mass = 0;
        DamageResistances? resistances = null;
        var protects = new List<string>();
        double tMin = 0, tMax = 0, radCap = 0, radRate = 0, g = 0, em = 0, ir = 0, motion = 0, view = 0;
        long capacity = 0;
        var sawArmour = false;

        foreach (var component in core.PointerArray(record, "Components"))
        {
            var at = core.InstanceAt(component);
            var s = component.StructIndex;
            if (s == purchasable) kind = DisplayType(core, text, at, purchasable);
            else if (s == physics) mass = Mass(core, at, physics);
            else if (s == suit)
            {
                sawArmour = true;
                if (core.ReferenceAt(at, s, "damageResistance") is { } macroId && byId.TryGetValue(macroId, out var macro))
                    resistances = Resistances(core, macro);
                foreach (var part in core.ReferenceArrayAt(at, s, "protectedBodyParts"))
                    if (byId.TryGetValue(part, out var body)) protects.Add(body.Name["BodyPart.".Length..]);
                foreach (var sig in core.ClassArrayAt(at, s, "signatureParams"))
                {
                    var sat = core.InstanceAt(sig);
                    var type = core.EnumAt(sat, sig.StructIndex, "signatureType") ?? "";
                    var emission = R(core.SingleAt(sat, sig.StructIndex, "signatureEmission"));
                    if (type.Equals("Electromagnetic", StringComparison.OrdinalIgnoreCase)) em += emission;
                    else if (type.Equals("Infrared", StringComparison.OrdinalIgnoreCase)) ir += emission;
                }
                if (core.ReferenceAt(at, s, "restrictedMoveViewPenalty") is { } penaltyId && byId.TryGetValue(penaltyId, out var penalty))
                {
                    var pat = core.InstanceAt(penalty, penalty.VariantIndex);
                    motion = R(core.SingleAt(pat, penalty.StructIndex, "restrictedMotionPenalty"));
                    view = R(core.SingleAt(pat, penalty.StructIndex, "restrictedViewPenalty"));
                }
            }
            else if (s == clothing)
            {
                if (GameMining.Nested(core, at, s, "TemperatureResistance") is { } temp)
                {
                    tMin = R(core.SingleAt(temp.At, temp.StructIndex, "MinResistance"));
                    tMax = R(core.SingleAt(temp.At, temp.StructIndex, "MaxResistance"));
                }
                if (GameMining.Nested(core, at, s, "RadiationResistance") is { } rad)
                {
                    radCap = R(core.SingleAt(rad.At, rad.StructIndex, "MaximumRadiationCapacity"));
                    radRate = R(core.SingleAt(rad.At, rad.StructIndex, "RadiationDissipationRate"));
                }
                if (GameMining.Nested(core, at, s, "Flight") is { } flight)
                    g = R(core.SingleAt(flight.At, flight.StructIndex, "gForceResistance"));
            }
            else if (s == inventory)
            {
                if (core.ReferenceAt(at, s, "containerParams") is { } containerId && byId.TryGetValue(containerId, out var container))
                    capacity = Capacity(core, container);
            }
        }

        // A backpack carries and stops nothing; anything else without the
        // armour block is a costume.
        if (!sawArmour && !slot.Equals("Backpack", StringComparison.OrdinalIgnoreCase)) return null;
        if (slot.Equals("Backpack", StringComparison.OrdinalIgnoreCase) && capacity == 0) return null;
        if (resistances?.Macro.Contains("_ai_", StringComparison.OrdinalIgnoreCase) == true) return null;

        return new ArmourPiece(cls, item.Name, slot, weight, kind, item.Manufacturer, Family(item.Name),
            resistances, protects, tMin, tMax, radCap, radRate, g, capacity, em, ir, motion, view, mass);
    }

    /// <summary>
    /// The set's name: the words before the slot word, so "Testudo Core
    /// Deathblow" and "Testudo Core Clanguard" are both Testudo. A name with
    /// no slot word in it is its own family.
    /// </summary>
    public static string Family(string name)
    {
        foreach (var word in SlotWords)
        {
            var at = name.IndexOf(" " + word, StringComparison.OrdinalIgnoreCase);
            if (at > 0 && (at + word.Length + 1 == name.Length || !char.IsLetter(name[at + word.Length + 1])))
                return Unsuffixed(name[..at]);
        }
        return Unsuffixed(name);
    }

    /// <summary>"Geist Armor" and "Geist" are one set; the word is the shop's, not the maker's.</summary>
    private static string Unsuffixed(string family)
    {
        var trimmed = family.Trim();
        return trimmed.EndsWith(" Armor", StringComparison.OrdinalIgnoreCase) && trimmed.Length > 6 ? trimmed[..^6].Trim() : trimmed;
    }

    private static DamageResistances Resistances(DataCore core, DataRecord macro)
    {
        var at = core.InstanceAt(macro, macro.VariantIndex);
        var s = macro.StructIndex;
        double Entry(string name)
        {
            if (GameMining.Nested(core, at, s, "damageResistance") is not { } block) return 1;
            return GameMining.Nested(core, block.At, block.StructIndex, name) is { } e ? R(core.SingleAt(e.At, e.StructIndex, "Multiplier"), 1) : 1;
        }
        var impact = GameMining.Nested(core, at, s, "impactForceResistance") is { } i ? R(core.SingleAt(i.At, i.StructIndex, "impactForceResistance"), 1) : 1;
        return new DamageResistances(macro.Name["DamageResistanceMacro.".Length..],
            Entry("PhysicalResistance"), Entry("EnergyResistance"), Entry("DistortionResistance"),
            Entry("ThermalResistance"), Entry("BiochemicalResistance"), Entry("StunResistance"), impact);
    }

    private static long Capacity(DataCore core, DataRecord container)
    {
        var at = core.InstanceAt(container, container.VariantIndex);
        if (core.PointerAt(at, container.StructIndex, "inventoryType") is not { } type) return 0;
        var tat = core.InstanceAt(type);
        if (core.PointerAt(tat, type.StructIndex, "capacity") is not { } cap) return 0;
        var cat = core.InstanceAt(cap);
        return core.Int32At(cat, cap.StructIndex, "microSCU") ?? (long)R(core.SingleAt(cat, cap.StructIndex, "microSCU"));
    }

    /// <summary>The shop's word for the item - "Rifle", "Armor: Core" - or empty.</summary>
    private static string DisplayType(DataCore core, IReadOnlyDictionary<string, string> text, long at, int s)
    {
        var key = core.StringAt(at, s, "displayType");
        return key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var english) && !GameItems.Unwritten(english) ? english : "";
    }

    private static double Mass(DataCore core, long at, int s)
    {
        if (core.PointerAt(at, s, "PhysType") is not { } phys) return 0;
        return R(core.SingleAt(core.InstanceAt(phys), phys.StructIndex, "Mass"));
    }

    /// <summary>
    /// A file single as a double, rounded to what was written: 0.05 in the
    /// file is 0.05000000074505806 widened, and the page would print it.
    /// </summary>
    private static double R(float? value, double fallback = 0) =>
        value is { } v ? Math.Round(v, 5) : fallback;
}
