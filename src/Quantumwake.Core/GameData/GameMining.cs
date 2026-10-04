namespace Quantumwake.Core.GameData;

/// <summary>
/// The percentage modifiers a laser, module or gadget applies to a rock, as
/// the files carry them: <c>-30</c> is thirty percent less. Zero means the
/// item says nothing about that figure.
/// </summary>
/// <param name="Instability">Laser instability - how much the charge wanders.</param>
/// <param name="WindowSize">The optimal charge window's width.</param>
/// <param name="Resistance">The rock's resistance, as the laser meets it.</param>
/// <param name="ShatterDamage">The damage a fracture does to the pieces.</param>
/// <param name="ClusterFactor">How many pieces a fracture makes.</param>
/// <param name="WindowRate">How fast the charge climbs inside the window.</param>
/// <param name="CatastrophicRate">How fast it climbs past it.</param>
public sealed record MiningModifiers(
    double Instability = 0,
    double WindowSize = 0,
    double Resistance = 0,
    double ShatterDamage = 0,
    double ClusterFactor = 0,
    double WindowRate = 0,
    double CatastrophicRate = 0)
{
    public static readonly MiningModifiers None = new();

    public bool IsEmpty => Instability == 0 && WindowSize == 0 && Resistance == 0 && ShatterDamage == 0
        && ClusterFactor == 0 && WindowRate == 0 && CatastrophicRate == 0;

    /// <summary>Two sets of percentage points, added - which is how the game stacks them.</summary>
    public MiningModifiers Plus(MiningModifiers other) => new(
        Instability + other.Instability, WindowSize + other.WindowSize, Resistance + other.Resistance,
        ShatterDamage + other.ShatterDamage, ClusterFactor + other.ClusterFactor,
        WindowRate + other.WindowRate, CatastrophicRate + other.CatastrophicRate);
}

/// <summary>A ship mining laser: the head on a Prospector, MOLE or Golem arm.</summary>
/// <param name="Power">The fracture beam's energy per second - what the tools call its wattage.</param>
/// <param name="ExtractionPower">The extraction beam's, once the rock is in pieces.</param>
/// <param name="FilterModifier">How much waste the head's own filter removes, in percent.</param>
/// <param name="ThrottleMinimum">The lowest the beam can be throttled, as a share of full power.</param>
/// <param name="Slots">Module ports on the head: the Arbor MH1 has one, most heads two.</param>
public sealed record MiningLaser(
    string Class,
    string Name,
    int Size,
    double Power,
    double ExtractionPower,
    double FilterModifier,
    double ThrottleMinimum,
    MiningModifiers Modifiers,
    string Manufacturer,
    int Slots = 0);

/// <summary>A module in a laser's slot: passive and always on, or active with charges and a lifetime.</summary>
/// <param name="PowerMultiplier">On the fracture beam's power; 1 is unchanged.</param>
/// <param name="ExtractionMultiplier">On the extraction beam's.</param>
/// <param name="Lifetime">Seconds an active module's effect lasts per charge; 0 for a passive.</param>
public sealed record MiningModule(
    string Class,
    string Name,
    bool Active,
    double Lifetime,
    int Charges,
    double PowerMultiplier,
    double ExtractionMultiplier,
    double FilterModifier,
    MiningModifiers Modifiers,
    string Manufacturer);

/// <summary>A gadget placed on the rock itself.</summary>
public sealed record MiningGadget(string Class, string Name, MiningModifiers Modifiers, string Manufacturer);

/// <summary>What a mineral does to the rock it is in: how it resists, how it wanders, where its window sits.</summary>
/// <param name="Resistance">The element's resistance, -1 to 1; the HUD's percentage is a function of the mix.</param>
/// <param name="Instability">The element's instability; Quantainium is 1000, iron 50.</param>
/// <param name="WindowMidpoint">Where in the charge the optimal window sits, 0 to 1.</param>
/// <param name="WindowRandomness">How far that midpoint wanders rock to rock.</param>
/// <param name="WindowThinness">How narrow the window is; negative is wide.</param>
/// <param name="ExplosionMultiplier">How hard the rock blows when overcharged.</param>
/// <param name="ClusterFactor">How many pieces it tends to break into.</param>
public sealed record MineralProfile(
    string Class,
    string Name,
    string Method,
    double Resistance,
    double Instability,
    double WindowMidpoint,
    double WindowRandomness,
    double WindowThinness,
    double ExplosionMultiplier,
    double ClusterFactor);

/// <summary>One element in a deposit's composition and its share.</summary>
public sealed record CompositionPart(string Element, double MinPercent, double MaxPercent, double Probability);

/// <summary>What a kind of deposit is made of.</summary>
/// <param name="Class">The preset's own name - <c>Asteroid_PType_Copper</c>, <c>QuantaniumDeposit</c>.</param>
/// <param name="Name">What the HUD calls it, when the files say; the class otherwise.</param>
public sealed record MineralComposition(string Class, string Name, int MinimumDistinctElements, IReadOnlyList<CompositionPart> Parts);

/// <summary>The constants the game's rock model runs on, from <c>MiningGlobalParams</c>.</summary>
/// <param name="PowerCapacityPerMass">Energy a rock holds per kilogram - the charge gauge's full scale.</param>
/// <param name="DecayPerMass">Energy a rock sheds per second per kilogram.</param>
/// <param name="OptimalWindowSize">The optimal window's base width, as a share of the gauge.</param>
/// <param name="OptimalWindowMaxSize">The widest the window can be made.</param>
/// <param name="ResistanceCurveFactor">The curve resistance is applied through.</param>
/// <param name="ControlledBreakingFillRate">How fast the fracture completes inside the window, per second.</param>
/// <param name="DangerBreakingFillRate">How fast the overcharge fills past it.</param>
/// <param name="AbsorbableVolumeThreshold">Below this volume a piece can be extracted rather than broken.</param>
/// <param name="CentiScuPerVolume">Cargo per unit of rock volume.</param>
public sealed record MiningConstants(
    double PowerCapacityPerMass,
    double DecayPerMass,
    double OptimalWindowSize,
    double OptimalWindowFactor,
    double OptimalWindowMaxSize,
    double ResistanceCurveFactor,
    double OptimalWindowThinnessCurveFactor,
    double ControlledBreakingFillRate,
    double ControlledBreakingDecayRate,
    double DangerBreakingFillRate,
    double DangerBreakingDecayRate,
    double AbsorbableVolumeThreshold,
    double CentiScuPerVolume);

/// <summary>Everything the install says about mining, read once with the rest of the game data.</summary>
public sealed record GameMiningData(
    MiningConstants? Constants,
    List<MiningLaser> Lasers,
    List<MiningModule> Modules,
    List<MiningGadget> Gadgets,
    List<MineralProfile> Minerals,
    List<MineralComposition> Compositions)
{
    public static readonly GameMiningData Empty = new(null, [], [], [], [], []);
}

/// <summary>
/// Reads the mining model out of the install: the constants, the minerals,
/// the lasers, the modules and the gadgets.
/// </summary>
/// <remarks>
/// <para>
/// None of this is in the community dataset the Garage runs on - its part
/// digest carries a mining laser's mass and health and nothing it does to a
/// rock - and none of it is in the logs, which record nothing of a rock. It
/// is all in <c>Game2.dcb</c>, under structs that name themselves:
/// <c>MiningGlobalParams</c>, <c>MineableElement</c>,
/// <c>MineableComposition</c>, <c>SEntityComponentMiningLaserParams</c>,
/// <c>ItemMiningModifierParams</c>, <c>ItemMineableRockModifierParams</c>.
/// </para>
/// <para>
/// A laser's power is the fracture beam's <c>damagePerSecond.DamageEnergy</c>;
/// the figures the community tools print as wattage - Helix I 3,900, Arbor
/// MH1 2,340, Klein-S1 3,120 - are exactly these, checked on this install
/// against scminer.rocks on 2026-09-15. A module carries two weapon
/// modifiers, the first on the fracture beam and the second (marked
/// <c>fireActionIndex=1</c>) on extraction, and a mining modifier for the
/// rest; a gadget carries a rock modifier only. The percentages are kept as
/// the files have them, so the page can say "-30% resistance" in the game's
/// own numbers rather than a rounding of them.
/// </para>
/// </remarks>
public static class GameMining
{
    public static GameMiningData Read(
        DataCore core,
        IReadOnlyDictionary<string, string> text,
        IReadOnlyDictionary<string, GameItem> facts,
        IReadOnlyDictionary<string, string> resourceNames)
    {
        var data = new GameMiningData(null, [], [], [], [], []);

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        var elementNames = new Dictionary<Guid, string>();

        foreach (var record in core.Records())
        {
            if (record.Name.StartsWith("MiningGlobalParams.MiningGlobalParams", StringComparison.OrdinalIgnoreCase)
                && record.Name.Equals("MiningGlobalParams.MiningGlobalParams", StringComparison.OrdinalIgnoreCase))
            {
                data = data with { Constants = Constants(core, record) };
                continue;
            }

            if (record.Name.StartsWith("MineableElement.", StringComparison.OrdinalIgnoreCase))
            {
                if (Mineral(core, record, resourceNames) is { } mineral)
                {
                    data.Minerals.Add(mineral);
                    elementNames[record.Hash] = mineral.Class;
                }
                continue;
            }

            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.OrdinalIgnoreCase)) continue;
            var cls = record.Name["EntityClassDefinition.".Length..];

            if (cls.StartsWith("Mining_Laser_", StringComparison.OrdinalIgnoreCase))
            {
                if (Laser(core, record, cls, facts) is { } laser) data.Lasers.Add(laser);
            }
            else if (cls.StartsWith("Mining_Modules_", StringComparison.OrdinalIgnoreCase))
            {
                if (Module(core, record, cls, facts) is { } module) data.Modules.Add(module);
            }
            else if (cls.StartsWith("Mining_Gadget_", StringComparison.OrdinalIgnoreCase))
            {
                if (Gadget(core, record, cls, facts) is { } gadget) data.Gadgets.Add(gadget);
            }
        }

        // Compositions refer to elements by hash, so they wait for the elements.
        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("MineableComposition.", StringComparison.OrdinalIgnoreCase)) continue;
            if (Composition(core, record, text, elementNames) is { } composition) data.Compositions.Add(composition);
        }

        data.Lasers.Sort((a, b) => a.Size != b.Size ? a.Size.CompareTo(b.Size) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Modules.Sort((a, b) => a.Active != b.Active ? a.Active.CompareTo(b.Active) : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Gadgets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        data.Minerals.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return data;
    }

    private static MiningConstants Constants(DataCore core, DataRecord record)
    {
        var at = core.InstanceAt(record, record.VariantIndex);
        var s = record.StructIndex;
        double F(string name) => core.SingleAt(at, s, name) ?? 0;
        return new MiningConstants(
            F("powerCapacityPerMass"), F("decayPerMass"), F("optimalWindowSize"), F("optimalWindowFactor"),
            F("optimalWindowMaxSize"), F("resistanceCurveFactor"), F("optimalWindowThinnessCurveFactor"),
            F("controlledBreakingFillRate"), F("controlledBreakingDecayRate"),
            F("dangerBreakingFillRate"), F("dangerBreakingDecayRate"),
            F("absorbableVolumeThreshold"), F("cSCUPerVolume"));
    }

    private static MineralProfile? Mineral(DataCore core, DataRecord record, IReadOnlyDictionary<string, string> resourceNames)
    {
        var cls = record.Name["MineableElement.".Length..];
        // The templates and the balance test are not minerals anybody meets.
        if (cls.Contains("template", StringComparison.OrdinalIgnoreCase) || cls.StartsWith("TestElement", StringComparison.OrdinalIgnoreCase))
            return null;

        var at = core.InstanceAt(record, record.VariantIndex);
        var s = record.StructIndex;
        double F(string name) => core.SingleAt(at, s, name) ?? 0;

        var resource = core.ReferenceAt(at, s, "resourceType");
        var name = resource is { } id && resourceNames.TryGetValue(id.ToString(), out var english) ? english : Words(cls);

        // The class names the method: MinableElement_FPS_Hadanite is hand
        // mining, _GroundVehicle_ the ROC, and a bare ore or raw is a ship rock.
        var method = cls.Contains("_FPS_", StringComparison.OrdinalIgnoreCase) ? "FPS"
            : cls.Contains("_GroundVehicle_", StringComparison.OrdinalIgnoreCase) ? "Ground"
            : "Ship";

        return new MineralProfile(cls, name, method,
            F("elementResistance"), F("elementInstability"),
            F("elementOptimalWindowMidpoint"), F("elementOptimalWindowMidpointRandomness"), F("elementOptimalWindowThinness"),
            F("elementExplosionMultiplier"), F("elementClusterFactor"));
    }

    private static MineralComposition? Composition(DataCore core, DataRecord record, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<Guid, string> elements)
    {
        var cls = record.Name["MineableComposition.".Length..];
        if (cls.Contains("template", StringComparison.OrdinalIgnoreCase) || cls.Contains("test", StringComparison.OrdinalIgnoreCase))
            return null;

        var at = core.InstanceAt(record, record.VariantIndex);
        var s = record.StructIndex;
        // The name is a localisation key - @hud_mining_asteroid_name_1 - that
        // several presets share; the class is what tells a copper P-type from
        // a tin one.
        var key = core.StringAt(at, s, "depositName") ?? "";
        var name = key.Length > 0 && text.TryGetValue(key.TrimStart('@'), out var english) ? english : cls;
        var minimum = core.Int32At(at, s, "minimumDistinctElements") ?? 0;

        var parts = new List<CompositionPart>();
        // The parts are laid inline - a class array, count and first index -
        // rather than pointed at, so the pointer-array read finds nothing.
        var entries = core.PointerArrayAt(at, s, "compositionArray");
        if (entries.Count == 0) entries = core.ClassArrayAt(at, s, "compositionArray");
        foreach (var pointer in entries)
        {
            var pat = core.InstanceAt(pointer);
            var ps = pointer.StructIndex;
            var element = core.ReferenceAt(pat, ps, "mineableElement");
            if (element is not { } id || !elements.TryGetValue(id, out var elementClass)) continue;
            parts.Add(new CompositionPart(elementClass,
                core.SingleAt(pat, ps, "minPercentage") ?? 0,
                core.SingleAt(pat, ps, "maxPercentage") ?? 0,
                core.SingleAt(pat, ps, "probability") ?? 0));
        }

        return parts.Count == 0 ? null : new MineralComposition(cls, name, minimum, parts);
    }

    private static MiningLaser? Laser(DataCore core, DataRecord record, string cls, IReadOnlyDictionary<string, GameItem> facts)
    {
        // Templates and test heads are not things a ship can mount.
        if (cls.Contains("Template", StringComparison.OrdinalIgnoreCase) || cls.Contains("TEST", StringComparison.Ordinal)
            || cls.Contains("_Test_", StringComparison.OrdinalIgnoreCase))
            return null;

        double power = 0, extraction = 0, filter = 0, throttleMin = 0;
        var modifiers = MiningModifiers.None;
        var seen = false;
        var slots = 0;

        foreach (var component in core.PointerArray(record, "Components"))
        {
            var name = core.StructName(component.StructIndex);
            var at = core.InstanceAt(component);
            var s = component.StructIndex;

            if (name == "SCItemWeaponComponentParams")
            {
                // The first fire action is the fracture beam, the second the
                // extraction beam; the ElectricArc hit type marks the first.
                var actions = core.PointerArrayAt(at, s, "fireActions");
                for (var i = 0; i < actions.Count; i++)
                {
                    if (Nested(core, core.InstanceAt(actions[i]), actions[i].StructIndex, "damagePerSecond") is not { } damage) continue;
                    var energy = core.SingleAt(damage.At, damage.StructIndex, "DamageEnergy") ?? 0;
                    if (i == 0) power = energy; else if (i == 1) extraction = energy;
                }
            }
            else if (name == "SItemPortContainerComponentParams")
            {
                // The module slots: item ports that take a MiningModifier. The
                // VEN port beside them takes a weapon attachment and is not one.
                var ports = core.ClassArrayAt(at, s, "Ports");
                foreach (var port in ports)
                {
                    var pat = core.InstanceAt(port);
                    var types = core.ClassArrayAt(pat, port.StructIndex, "Types");
                    if (types.Any(t => string.Equals(core.EnumAt(core.InstanceAt(t), t.StructIndex, "Type"), "MiningModifier", StringComparison.Ordinal)))
                        slots++;
                }
            }
            else if (name == "SEntityComponentMiningLaserParams")
            {
                seen = true;
                throttleMin = core.SingleAt(at, s, "throttleMinimum") ?? 0;
                modifiers = Modifiers(core, at, s, "miningLaserModifiers");
                filter = Filter(core, at, s);
            }
        }

        if (!seen || power <= 0) return null;

        facts.TryGetValue(cls, out var item);
        return new MiningLaser(cls, item?.Name is { Length: > 0 } n && n != cls ? n : Words(cls),
            item?.Size ?? SizeOf(cls), power, extraction, filter, throttleMin, modifiers, item?.Manufacturer ?? "", slots);
    }

    private static MiningModule? Module(DataCore core, DataRecord record, string cls, IReadOnlyDictionary<string, GameItem> facts)
    {
        // The bare class and the vehicle mods are not slot modules.
        if (cls.Equals("Mining_Modules", StringComparison.OrdinalIgnoreCase) || cls.Contains("VehicleMod", StringComparison.OrdinalIgnoreCase))
            return null;

        foreach (var component in core.PointerArray(record, "Components"))
        {
            if (core.StructName(component.StructIndex) != "EntityComponentAttachableModifierParams") continue;
            var at = core.InstanceAt(component);
            var s = component.StructIndex;

            var active = string.Equals(core.EnumAt(at, s, "activationMethod"), "ActivateOnDemand", StringComparison.OrdinalIgnoreCase);
            var charges = core.Int32At(at, s, "charges") ?? 0;
            double powerMul = 1, extractionMul = 1, filter = 0, lifetime = 0;
            var modifiers = MiningModifiers.None;

            foreach (var pointer in core.PointerArrayAt(at, s, "modifiers"))
            {
                var mat = core.InstanceAt(pointer);
                var ms = pointer.StructIndex;
                var kind = core.StructName(ms);

                if (kind == "ItemWeaponModifiersParams")
                {
                    var index = core.Int32At(mat, ms, "fireActionIndex") ?? 0;
                    if (Nested(core, mat, ms, "weaponModifier") is not { } weapon) continue;
                    if (Nested(core, weapon.At, weapon.StructIndex, "weaponStats") is not { } stats) continue;
                    var mul = core.SingleAt(stats.At, stats.StructIndex, "damageMultiplier") ?? 1;
                    if (index == 0) powerMul = mul; else if (index == 1) extractionMul = mul;
                }
                else if (kind == "ItemMiningModifierParams")
                {
                    modifiers = Modifiers(core, mat, ms, "MiningLaserModifier");
                    if (Nested(core, mat, ms, "modifierLifetime") is { } life) lifetime = core.SingleAt(life.At, life.StructIndex, "lifetime") ?? 0;
                }
                else if (kind == "MiningFilterItemModifierParams")
                {
                    filter = Filter(core, mat, ms);
                }
            }

            facts.TryGetValue(cls, out var item);
            return new MiningModule(cls, item?.Name is { Length: > 0 } n && n != cls ? n : Words(cls),
                active, active ? lifetime : 0, active ? charges : 0, powerMul, extractionMul, filter, modifiers, item?.Manufacturer ?? "");
        }

        return null;
    }

    private static MiningGadget? Gadget(DataCore core, DataRecord record, string cls, IReadOnlyDictionary<string, GameItem> facts)
    {
        foreach (var component in core.PointerArray(record, "Components"))
        {
            if (core.StructName(component.StructIndex) != "EntityComponentAttachableModifierParams") continue;
            var at = core.InstanceAt(component);
            var s = component.StructIndex;

            foreach (var pointer in core.PointerArrayAt(at, s, "modifiers"))
            {
                if (core.StructName(pointer.StructIndex) != "ItemMineableRockModifierParams") continue;
                var modifiers = Modifiers(core, core.InstanceAt(pointer), pointer.StructIndex, "MiningLaserModifier");
                facts.TryGetValue(cls, out var item);
                return new MiningGadget(cls, item?.Name is { Length: > 0 } n && n != cls ? n : Words(cls), modifiers, item?.Manufacturer ?? "");
            }
        }

        return null;
    }

    /// <summary>
    /// A struct-valued field, wherever the file put it: some are laid inline
    /// in the parent and some sit behind a pointer, and the same name is one
    /// or the other on different records. Null when the field is absent.
    /// </summary>
    internal static (long At, int StructIndex)? Nested(DataCore core, long at, int structIndex, string name)
    {
        if (core.PointerAt(at, structIndex, name) is { } pointer)
            return (core.InstanceAt(pointer), pointer.StructIndex);

        var (fat, field) = core.FieldAt(at, structIndex, name);
        return fat >= 0 && field is { ConversionType: 0, DataType: 0x0010 } ? (fat, field.StructIndex) : null;
    }

    /// <summary>The <c>MiningLaserModifiers</c> block: seven structs, each a <c>value</c> in percent.</summary>
    private static MiningModifiers Modifiers(DataCore core, long at, int structIndex, string field)
    {
        if (Nested(core, at, structIndex, field) is not { } block) return MiningModifiers.None;

        double V(string name) =>
            Nested(core, block.At, block.StructIndex, name) is { } v ? core.SingleAt(v.At, v.StructIndex, "value") ?? 0 : 0;

        return new MiningModifiers(
            V("laserInstability"), V("optimalChargeWindowSizeModifier"), V("resistanceModifier"),
            V("shatterdamageModifier"), V("clusterFactorModifier"),
            V("optimalChargeWindowRateModifier"), V("catastrophicChargeWindowRateModifier"));
    }

    private static double Filter(DataCore core, long at, int structIndex)
    {
        if (Nested(core, at, structIndex, "filterParams") is not { } filter) return 0;
        return Nested(core, filter.At, filter.StructIndex, "filterModifier") is { } v ? core.SingleAt(v.At, v.StructIndex, "value") ?? 0 : 0;
    }

    private static int SizeOf(string cls)
    {
        var tail = cls[(cls.LastIndexOf('_') + 1)..];
        return tail.Length == 2 && tail[0] == 'S' && char.IsDigit(tail[1]) ? tail[1] - '0' : 1;
    }

    private static string Words(string cls) => cls.Replace('_', ' ');
}
