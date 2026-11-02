namespace Quantumwake.Core.GameData;

/// <summary>One mineable entity's radar signature, as the install gives it.</summary>
/// <param name="Class">The entity, <c>MineableRock_SurfaceCommon_Quantainium</c>.</param>
/// <param name="Composition">Its <c>MineableComposition</c>, bare; null when it names none.</param>
/// <param name="Signature">The resource signature the mining radar reads for one of it.</param>
/// <param name="Placed">Whether any <c>HarvestablePreset</c> names it as the entity it places; a rock none does is a test or legacy preset nobody meets.</param>
public sealed record GameRockSignature(string Class, string? Composition, double Signature, bool Placed = true);

/// <summary>One thing a radar signature can tell apart, as the Radar tab lists it.</summary>
/// <param name="Signature">What one of it reads.</param>
/// <param name="Kind">ship, asteroid, hand, ground or deposit.</param>
/// <param name="Name">The ore for a ship rock; otherwise the kind of rock, in English.</param>
/// <param name="Tier">Common to Legendary, for a ship rock; null otherwise.</param>
/// <param name="Ores">What it can hold. One for a ship rock; for the rest, the value does not say which.</param>
/// <param name="Surface">Found on planets and moons.</param>
/// <param name="Asteroid">Found in asteroid fields.</param>
public sealed record RadarRow(double Signature, string Kind, string Name, string? Tier, List<string> Ores, bool Surface, bool Asteroid)
{
    /// <summary>For an older deposit rock, the stones it is named for: Felsic, Granite...</summary>
    public List<string> Stones { get; init; } = [];
}

/// <summary>
/// Reads what each mineable rock reads on the mining radar.
/// </summary>
/// <remarks>
/// <para>
/// Every entity under <c>entities/mineable/</c> carries an
/// <c>SSCSignatureSystemParams</c> whose <c>radarProperties</c> hold
/// <c>baseSignatureParams.signatures</c>, eight floats, one per signature kind.
/// The fifth is the resource signature, the number the mining radar shows.
/// On 4.10.2, 260 entities carry one, 201 of them named by a preset, in 34
/// distinct values: one per ore among the tiered ship rocks, 15 apart within a
/// tier (Quantainium 3170, Stileron 3185, Savrilium 3200); one per asteroid
/// class, C to E from 4700 to 4900, whatever ore it holds; 3000 for every
/// hand-mining rock; and 4000 shared by ground-vehicle rocks, the older
/// deposit-type rocks (Felsic, Granite, Shale...) and every test preset.
/// </para>
/// <para>
/// So the value names an ore only for ship rocks. Elsewhere it names a kind of
/// rock, and <see cref="Radar"/> says that rather than offering an ore.
/// </para>
/// <para>
/// The files give one rock's figure. That a ping over a cluster reads that
/// figure times the rocks in it is how pilots read the radar, and is not in the
/// files; the page says so.
/// </para>
/// </remarks>
public static partial class GameRockSignatures
{
    /// <summary>The slot of <c>signatures</c> the mining radar shows.</summary>
    public const int ResourceSlot = 4;

    private const string Folder = "entities/mineable/";

    public static List<GameRockSignature> Read(DataCore core)
    {
        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        var signatureStruct = core.StructIndexOf("SSCSignatureSystemParams");
        var mineableStruct = core.StructIndexOf("MineableParams");
        if (signatureStruct < 0) return [];

        // A rock reaches the world through a preset that names it. The deposit
        // tables reach only some places (see GameSpawns), so every preset is
        // asked, not just the ones a table this app reads points at.
        var placed = new HashSet<Guid>();
        foreach (var record in byId.Values)
        {
            if (core.StructName(record.StructIndex) != "HarvestablePreset") continue;
            if (core.ReferenceAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "entityClass") is { } target)
                placed.Add(target);
        }

        var rocks = new List<GameRockSignature>();
        foreach (var record in byId.Values)
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;
            var file = record.FileName.Replace('\\', '/');
            var at = file.IndexOf(Folder, StringComparison.OrdinalIgnoreCase);
            if (at < 0) continue;


            var instance = core.InstanceAt(record, record.VariantIndex);
            double? signature = null;
            string? composition = null;

            foreach (var component in core.PointerArrayAt(instance, record.StructIndex, "Components"))
            {
                var ca = core.InstanceAt(component);
                if (component.StructIndex == signatureStruct)
                {
                    var (radarAt, radarSi) = Nested(core, ca, component.StructIndex, "radarProperties");
                    if (radarAt < 0) continue;
                    var (baseAt, baseSi) = Nested(core, radarAt, radarSi, "baseSignatureParams");
                    if (baseAt < 0) continue;
                    var values = core.SingleArrayAt(baseAt, baseSi, "signatures");
                    if (values.Count > ResourceSlot && values[ResourceSlot] > 0) signature = Math.Round(values[ResourceSlot], 1);
                }
                else if (component.StructIndex == mineableStruct
                    && core.ReferenceAt(ca, component.StructIndex, "composition") is { } id && byId.TryGetValue(id, out var comp))
                {
                    composition = comp.Name.Contains('.') ? comp.Name[(comp.Name.IndexOf('.') + 1)..] : comp.Name;
                }
            }

            if (signature is { } s)
                rocks.Add(new GameRockSignature(record.Name["EntityClassDefinition.".Length..], composition, s, placed.Contains(record.Hash)));
        }

        return [.. rocks.OrderBy(r => r.Signature).ThenBy(r => r.Class, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// The rocks a pilot can meet, one row per thing a signature can tell apart.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A ship rock is one row per ore, with the tier the game files it under
    /// and whether it is found on surfaces, in asteroid fields or both: the
    /// surface and asteroid versions of an ore read the same. Every other kind
    /// is one row per value, listing what it can hold, because the value does
    /// not say which.
    /// </para>
    /// <para>
    /// Left out: anything no preset places, and the game's own templates and
    /// test rocks. <c>MineableRock_FPS_TEMPLATE</c> is named by a preset, so
    /// being placed is not enough on its own.
    /// </para>
    /// </remarks>
    public static List<RadarRow> Radar(IEnumerable<GameRockSignature> rocks)
    {
        var rows = new List<RadarRow>();
        var met = rocks.Where(r => r.Placed && !Unmet(r.Class)).ToList();

        foreach (var g in met.Select(r => (Rock: r, Match: ShipRock().Match(r.Class))).Where(x => x.Match.Success)
                     .GroupBy(x => (x.Rock.Signature, Ore: x.Match.Groups["ore"].Value)))
        {
            var places = g.Select(x => x.Match.Groups["where"].Value).ToHashSet();
            rows.Add(new RadarRow(g.Key.Signature, "ship", g.Key.Ore, g.First().Match.Groups["tier"].Value,
                [g.Key.Ore], places.Contains("Surface"), places.Contains("Asteroid")));
        }

        foreach (var g in met.Select(r => (Rock: r, Match: AsteroidClass().Match(r.Class))).Where(x => x.Match.Success)
                     .GroupBy(x => (x.Rock.Signature, Type: x.Match.Groups["type"].Value)))
        {
            rows.Add(new RadarRow(g.Key.Signature, "asteroid", $"{g.Key.Type}-type asteroid", null,
                Ores(g.Select(x => x.Match.Groups["ore"].Value)), false, true));
        }

        foreach (var (kind, pattern, name) in new[]
                 {
                     ("hand", HandRock(), "Hand-mining deposit"),
                     ("ground", GroundRock(), "Ground-vehicle deposit"),
                 })
        {
            foreach (var g in met.Select(r => (Rock: r, Match: pattern.Match(r.Class))).Where(x => x.Match.Success)
                         .GroupBy(x => x.Rock.Signature))
            {
                rows.Add(new RadarRow(g.Key, kind, name, null,
                    Ores(g.Select(x => x.Match.Groups["ore"].Value)), true, false));
            }
        }

        // Felsic, Gneiss, Granite and the rest: named for the stone, holding one
        // of a handful of common ores each. QuantaniumMineableRock is one of
        // these too, spelt as the class spells it.
        foreach (var g in met.Select(r => (Rock: r, Match: DepositRock().Match(r.Class))).Where(x => x.Match.Success)
                     .GroupBy(x => x.Rock.Signature))
        {
            var ores = Ores(g.Select(x => x.Match.Groups["ore"].Success ? x.Match.Groups["ore"].Value : x.Match.Groups["stone"].Value));
            var stones = Ores(g.Select(x => x.Match.Groups["stone"].Value));
            rows.Add(new RadarRow(g.Key, "deposit", "Older deposit rock", null, ores, true, false) { Stones = stones });
        }

        return [.. rows.OrderBy(r => r.Signature).ThenBy(r => KindOrder(r.Kind)).ThenBy(r => r.Name, StringComparer.OrdinalIgnoreCase)];

        static List<string> Ores(IEnumerable<string> names) =>
            [.. names.Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];

        static int KindOrder(string kind) => kind switch
        {
            "ship" => 0, "asteroid" => 1, "hand" => 2, "ground" => 3, _ => 4,
        };
    }

    /// <summary>The game's own scaffolding: templates, test rocks, model-tag fixtures.</summary>
    private static bool Unmet(string cls) =>
        cls.Contains("TEMPLATE", StringComparison.OrdinalIgnoreCase)
        || cls.Contains("test", StringComparison.OrdinalIgnoreCase);

    // Ship rocks: MineableRock_SurfaceCommon_Iron, MineableRock_AsteroidLegendary_Savrilium_RCD_large.
    [System.Text.RegularExpressions.GeneratedRegex(@"^MineableRock_(?<where>Surface|Asteroid)(?<tier>Common|Uncommon|Rare|Epic|Legendary)_(?<ore>[A-Za-z]+)(_|$)")]
    private static partial System.Text.RegularExpressions.Regex ShipRock();

    // AsteroidCTypeMineableRock_Copper; the bare class with no ore is a template no preset places.
    [System.Text.RegularExpressions.GeneratedRegex(@"^Asteroid(?<type>[A-Z])TypeMineableRock(_(?<ore>[A-Za-z]+))?$")]
    private static partial System.Text.RegularExpressions.Regex AsteroidClass();

    [System.Text.RegularExpressions.GeneratedRegex(@"^MineableRock_FPS_(?<ore>[A-Za-z]+)(_|$)")]
    private static partial System.Text.RegularExpressions.Regex HandRock();

    [System.Text.RegularExpressions.GeneratedRegex(@"^MineableRock_GroundVehicle_(?<ore>[A-Za-z]+)(_|$)")]
    private static partial System.Text.RegularExpressions.Regex GroundRock();

    [System.Text.RegularExpressions.GeneratedRegex(@"^(?<stone>[A-Z][a-z]+)MineableRock(_(?<ore>[A-Za-z]+))?$")]
    private static partial System.Text.RegularExpressions.Regex DepositRock();

    /// <summary>A struct field that may be laid inline or pointed at, as its instance and struct.</summary>
    private static (long At, int StructIndex) Nested(DataCore core, long at, int structIndex, string name)
    {
        var field = core.StructProperties(structIndex).FirstOrDefault(p => p.Name == name);
        if (field is null) return (-1, -1);
        if (field.DataType == 0x0010 && field.ConversionType == 0)
        {
            var (fieldAt, _) = core.FieldAt(at, structIndex, name);
            return (fieldAt, field.StructIndex);
        }
        return core.PointerAt(at, structIndex, name) is { StructIndex: >= 0 } pointer
            ? (core.InstanceAt(pointer), pointer.StructIndex)
            : (-1, -1);
    }
}
