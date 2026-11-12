using System.Text.Json;
using System.Text.Json.Serialization;

namespace Quantumwake.Core.GameData;

/// <summary>A livery a hull can wear: which palette, and sometimes a material of its own.</summary>
/// <param name="Tag">The paint's tag - <c>Paint_Corsair_Black_Black_Red</c> - which the paint item carries too.</param>
/// <param name="Palette">The tint palette's name, a key into <see cref="GameModelCatalogue.Palettes"/>.</param>
/// <param name="Material">A material file replacing the hull's, or null when the paint is only a palette.</param>
public sealed record ModelPaint(string Tag, string? Palette, string? Material);

/// <summary>What fills one port in a default loadout, and what fills the ports on that.</summary>
public sealed record ModelLoadout(string Port, string? Class, IReadOnlyList<ModelLoadout> Children);

/// <summary>A thing the game draws: its model, material, palette, liveries and stock loadout.</summary>
/// <param name="Class">The entity class - <c>DRAK_Corsair</c>, <c>BEHR_LaserCannon_S4</c>.</param>
/// <param name="Geometry">The model file as the record writes it.</param>
/// <param name="Material">A material file replacing the one the model names, or null.</param>
/// <param name="Palette">The default tint palette's name, or null.</param>
/// <param name="Type">The attach type and subtype - <c>WeaponGun/Gun</c> - or null for a vehicle.</param>
/// <param name="Vehicle">True for a ship or ground vehicle.</param>
/// <param name="Helpers">
/// Ports whose attachment point in the model has another name: an FPS
/// rifle's <c>magazine_attach</c> port hangs off its <c>magAttach</c> bone.
/// Ports not listed attach at the node of their own name, as every ship's do.
/// </param>
public sealed record ModelEntity(
    string Class, string Geometry, string? Material, string? Palette, string? Type, bool Vehicle,
    IReadOnlyList<ModelPaint> Paints, IReadOnlyList<ModelLoadout> Loadout,
    IReadOnlyDictionary<string, string>? Helpers = null);

/// <summary>One entry of a tint palette, 8-bit sRGB as the record stores it.</summary>
public sealed record ModelTint(int[] Tint, int[] Spec, int Gloss);

/// <summary>A tint palette: the three colours a hull's layers pick from, and the glass tint.</summary>
public sealed record ModelPalette(ModelTint A, ModelTint B, ModelTint C, int[] Glass);

/// <summary>
/// Every model the 3D view can open, read from the DataCore.
/// </summary>
/// <remarks>
/// <para>
/// A ship's vehicle file - <c>Scripts/Entities/Vehicles/Implementations/Xml/DRAK_Corsair.xml</c>,
/// which lays its parts out - is one of the archive's encrypted entries, and
/// this does not open it. It does not need to. The entity's
/// <c>SGeometryResourceParams</c> names the hull model, and the hull model
/// carries every hardpoint as a named node with its transform: the Corsair's
/// has 87. The default loadout says what fills each of those ports, and each
/// item's own record names its model. Turrets and racks have loadouts of
/// their own - the Corsair's manned turret holds two S2 gimbals, each holding
/// a Klaus &amp; Werner repeater - so the loadout is kept as a tree.
/// </para>
/// <para>
/// Paints are <c>SubGeometry</c> entries on the same component: a tag, and a
/// palette. The Corsair's ten are ten palettes over one material file.
/// </para>
/// <para>
/// Only entities whose model sits under a folder <see cref="ModelArchive"/>
/// indexes are kept - ships, ground vehicles, ship parts, FPS weapons and armour - so
/// every entry here is one the view can actually open.
/// </para>
/// </remarks>
public sealed class GameModelCatalogue
{
    public const int CacheVersion = 5;

    public static readonly GameModelCatalogue Empty = new([], []);

    public Dictionary<string, ModelEntity> Entities { get; }
    public Dictionary<string, ModelPalette> Palettes { get; }

    [JsonConstructor]
    public GameModelCatalogue(Dictionary<string, ModelEntity> entities, Dictionary<string, ModelPalette> palettes)
    {
        Entities = new(entities, StringComparer.OrdinalIgnoreCase);
        Palettes = new(palettes, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The entity by class, or its <c>_GS</c> rework when the patch deleted it (<see cref="GoldStandard"/>).</summary>
    public ModelEntity? Find(string cls) =>
        Entities.GetValueOrDefault(cls)
        ?? (GoldStandard.Successor(cls, Entities.ContainsKey) is { } rework ? Entities[rework] : null);

    private static readonly string[] Folders = ["objects/spaceships/", "objects/vehicles/", "objects/fps_weapons/", "objects/characters/"];

    /// <summary>Reads the catalogue, or loads it from <paramref name="cachePath"/> when the archive has not changed.</summary>
    public static GameModelCatalogue Load(string archivePath, string cachePath)
    {
        if (!File.Exists(archivePath)) return Empty;
        var stamp = $"{CacheVersion}:{new FileInfo(archivePath).LastWriteTimeUtc.Ticks}";

        try
        {
            if (File.Exists(cachePath))
            {
                using var stream = File.OpenRead(cachePath);
                var cached = JsonSerializer.Deserialize(stream, ModelJson.Default.CachedCatalogue);
                if (cached?.Stamp == stamp && cached.Catalogue is not null) return cached.Catalogue;
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // A torn or old cache is read again from the archive.
        }

        var p4k = new P4kArchive(archivePath);
        var blob = p4k.TryRead(@"Data\Game2.dcb");
        if (blob is null) return Empty;
        var catalogue = Read(new DataCore(blob));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(cachePath))!);
            var temp = cachePath + ".tmp";
            using (var stream = File.Create(temp))
                JsonSerializer.Serialize(stream, new CachedCatalogue(stamp, catalogue), ModelJson.Default.CachedCatalogue);
            File.Move(temp, cachePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }

        return catalogue;
    }

    public static GameModelCatalogue Read(DataCore core)
    {
        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        var geometry = core.StructIndexOf("SGeometryResourceParams");
        var attach = core.StructIndexOf("SAttachableComponentParams");
        var loadout = core.StructIndexOf("SEntityComponentDefaultLoadoutParams");
        var vehicle = core.StructIndexOf("VehicleComponentParams");
        var ports = core.StructIndexOf("SItemPortContainerComponentParams");

        var palettes = new Dictionary<string, ModelPalette>(StringComparer.OrdinalIgnoreCase);
        var paletteNames = new Dictionary<Guid, string>();
        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("TintPaletteTree.", StringComparison.Ordinal)) continue;
            var name = record.Name["TintPaletteTree.".Length..];
            paletteNames[record.Hash] = name;
            if (Palette(core, record) is { } palette) palettes[name] = palette;
        }

        string? PaletteOf(Guid? id) => id is { } g && paletteNames.TryGetValue(g, out var n) ? n : null;

        var entities = new Dictionary<string, ModelEntity>(StringComparer.OrdinalIgnoreCase);
        if (geometry < 0) return new GameModelCatalogue(entities, palettes);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;

            string? path = null, material = null, palette = null, type = null;
            var paints = new List<ModelPaint>();
            var tree = new List<ModelLoadout>();
            Dictionary<string, string>? helpers = null;
            var isVehicle = false;

            foreach (var component in core.PointerArray(record, "Components"))
            {
                var at = core.InstanceAt(component);
                if (component.StructIndex == geometry)
                {
                    var (node, nodeField) = core.FieldAt(at, geometry, "Geometry");
                    if (node < 0 || nodeField is null) continue;
                    (path, material, palette) = Data(core, node, nodeField.StructIndex, PaletteOf);
                    // A colourway is often the component's material over a shared model.
                    var componentMaterial = ComponentMaterial(core, at, geometry);
                    material = componentMaterial ?? material;
                    string? worn = null, wornMaterial = null, bodyMaterial = null;
                    foreach (var sub in core.ClassArrayAt(node, nodeField.StructIndex, "SubGeometry"))
                    {
                        var subAt = core.InstanceAt(sub);
                        var tags = core.StringAt(subAt, sub.StructIndex, "Tags");
                        if (string.IsNullOrWhiteSpace(tags)) continue;
                        var (subPath, subMaterial, subPalette) = Data(core, subAt, sub.StructIndex, PaletteOf);
                        var words = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries);

                        // Body armour's own geometry is the crate it is carried
                        // in; the suit as worn is the sub-geometry tagged for a
                        // body - cds_armor_heavy_core_01_01_01's is
                        // m_cds_heavy_armor_01_core.skin under "Male". The male
                        // body is the one the view draws.
                        var male = words.Contains("Male", StringComparer.OrdinalIgnoreCase);
                        if (male || words.Contains("Female", StringComparer.OrdinalIgnoreCase))
                            bodyMaterial ??= subMaterial;
                        if (male && subPath is not null)
                        {
                            worn = subPath;
                            wornMaterial = subMaterial;
                            palette = subPalette ?? palette;
                            continue;
                        }
                        if (words.Any(w => NotPaints.Contains(w))) continue;
                        paints.Add(new ModelPaint(tags.Trim(), subPalette, subMaterial));
                    }

                    // The colourway's material is often written on one body
                    // only - that torso's names m_cds_heavy_armor_01_01_01.mtl
                    // under Female and nothing under Male - and both bodies
                    // wear it. Never the crate's material.
                    if (worn is not null)
                    {
                        path = worn;
                        material = componentMaterial ?? wornMaterial ?? bodyMaterial;
                    }
                }
                else if (component.StructIndex == attach)
                {
                    var (def, defField) = core.FieldAt(at, attach, "AttachDef");
                    if (def >= 0 && defField is not null)
                        type = $"{core.EnumAt(def, defField.StructIndex, "Type")}/{core.EnumAt(def, defField.StructIndex, "SubType")}";
                }
                else if (component.StructIndex == loadout)
                {
                    if (core.PointerAt(at, loadout, "loadout") is { } manual
                        && core.StructName(manual.StructIndex) == "SItemPortLoadoutManualParams")
                        tree = Entries(core, core.InstanceAt(manual), manual.StructIndex, byId, 0);
                }
                else if (component.StructIndex == ports)
                {
                    helpers = Helpers(core, at, ports);
                }
                else if (component.StructIndex == vehicle)
                {
                    isVehicle = true;
                }
            }

            if (path is null || !Folders.Any(f => path.Replace('\\', '/').StartsWith(f, StringComparison.OrdinalIgnoreCase)))
                continue;

            var cls = record.Name["EntityClassDefinition.".Length..];
            entities[cls] = new ModelEntity(cls, path, material, palette, isVehicle ? null : type, isVehicle, paints, tree, helpers);
        }

        return new GameModelCatalogue(entities, palettes);
    }

    /// <summary>
    /// Sub-geometry tags that are a body, a state or a place the item is held,
    /// not a livery: armour's Male and Female, the crate a suit is carried in.
    /// </summary>
    private static readonly HashSet<string> NotPaints = new(StringComparer.OrdinalIgnoreCase)
    {
        "Male", "Female", "heldEntity", "inventoryStoredEntity", "shouldered", "Holstered", "Stowed", "FirstPerson", "ThirdPerson",
    };

    /// <summary>The material the geometry component names for the whole entity, as armour's colourways do.</summary>
    private static string? ComponentMaterial(DataCore core, long at, int geometry)
    {
        var (node, nodeField) = core.FieldAt(at, geometry, "Material");
        if (node < 0 || nodeField is null) return null;
        var (inner, innerField) = core.FieldAt(node, nodeField.StructIndex, "Material");
        if (inner < 0 || innerField is null) return null;
        var p = core.StringAt(inner, innerField.StructIndex, "path");
        return string.IsNullOrWhiteSpace(p) ? null : p;
    }

    private static (string? Path, string? Material, string? Palette) Data(
        DataCore core, long node, int nodeStruct, Func<Guid?, string?> paletteOf)
    {
        var (data, dataField) = core.FieldAt(node, nodeStruct, "Geometry");
        if (data < 0 || dataField is null) return (null, null, null);
        var ds = dataField.StructIndex;

        string? Path(string field)
        {
            var (at, f) = core.FieldAt(data, ds, field);
            var p = at >= 0 && f is not null ? core.StringAt(at, f.StructIndex, "path") : null;
            return string.IsNullOrWhiteSpace(p) ? null : p;
        }

        var (pal, palField) = core.FieldAt(data, ds, "Palette");
        var palette = pal >= 0 && palField is not null ? paletteOf(core.ReferenceAt(pal, palField.StructIndex, "RootRecord")) : null;
        return (Path("Geometry"), Path("Material"), palette);
    }

    /// <summary>
    /// The ports whose attachment implementation names a helper or bone other
    /// than the port: <c>AttachmentImplementation</c> -> <c>Helper</c> -> <c>Helper</c> -> <c>Name</c>.
    /// </summary>
    private static Dictionary<string, string>? Helpers(DataCore core, long at, int ports)
    {
        Dictionary<string, string>? map = null;
        foreach (var port in core.ClassArrayAt(at, ports, "Ports"))
        {
            var p = core.InstanceAt(port);
            var name = core.StringAt(p, port.StructIndex, "Name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (core.PointerAt(p, port.StructIndex, "AttachmentImplementation") is not { } impl) continue;
            var (h1, f1) = core.FieldAt(core.InstanceAt(impl), impl.StructIndex, "Helper");
            if (h1 < 0 || f1 is null) continue;
            var (h2, f2) = core.FieldAt(h1, f1.StructIndex, "Helper");
            if (h2 < 0 || f2 is null) continue;
            var helper = core.StringAt(h2, f2.StructIndex, "Name");
            if (string.IsNullOrWhiteSpace(helper) || helper.Equals(name, StringComparison.OrdinalIgnoreCase)) continue;
            (map ??= new(StringComparer.OrdinalIgnoreCase))[name] = helper;
        }
        return map;
    }

    private static List<ModelLoadout> Entries(DataCore core, long at, int structIndex, Dictionary<Guid, DataRecord> byId, int depth)
    {
        var list = new List<ModelLoadout>();
        if (depth > 6) return list;
        foreach (var entry in core.ClassArrayAt(at, structIndex, "entries"))
        {
            var e = core.InstanceAt(entry);
            var port = core.StringAt(e, entry.StructIndex, "itemPortName");
            if (string.IsNullOrWhiteSpace(port)) continue;

            string? cls = null;
            if (core.ReferenceAt(e, entry.StructIndex, "entityClassReference") is { } id
                && byId.TryGetValue(id, out var item)
                && item.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal))
                cls = item.Name["EntityClassDefinition.".Length..];
            cls ??= core.StringAt(e, entry.StructIndex, "entityClassName") is { Length: > 0 } named ? named : null;

            var children = core.PointerAt(e, entry.StructIndex, "loadout") is { } sub
                && core.StructName(sub.StructIndex) == "SItemPortLoadoutManualParams"
                ? Entries(core, core.InstanceAt(sub), sub.StructIndex, byId, depth + 1)
                : [];

            if (cls is null && children.Count == 0) continue;
            list.Add(new ModelLoadout(port, cls, children));
        }
        return list;
    }

    private static ModelPalette? Palette(DataCore core, DataRecord record)
    {
        var at = core.InstanceAt(record, record.VariantIndex);
        var (root, rootField) = core.FieldAt(at, record.StructIndex, "root");
        if (root < 0 || rootField is null) return null;
        var rs = rootField.StructIndex;

        int[] Rgb(long inst, int si, string name)
        {
            var (c, f) = core.FieldAt(inst, si, name);
            if (c < 0 || f is null) return [0, 0, 0];
            return [core.ByteAt(c, f.StructIndex, "r") ?? 0, core.ByteAt(c, f.StructIndex, "g") ?? 0, core.ByteAt(c, f.StructIndex, "b") ?? 0];
        }

        ModelTint Entry(string name)
        {
            var (e, f) = core.FieldAt(root, rs, name);
            if (e < 0 || f is null) return new ModelTint([0, 0, 0], [10, 10, 10], 128);
            return new ModelTint(Rgb(e, f.StructIndex, "tintColor"), Rgb(e, f.StructIndex, "specColor"),
                core.ByteAt(e, f.StructIndex, "glossiness") ?? 128);
        }

        return new ModelPalette(Entry("entryA"), Entry("entryB"), Entry("entryC"), Rgb(root, rs, "glassColor"));
    }
}

/// <summary>The catalogue as it sits in the cache, with the archive stamp it was read from.</summary>
public sealed record CachedCatalogue(string Stamp, GameModelCatalogue Catalogue);

[JsonSerializable(typeof(CachedCatalogue))]
internal partial class ModelJson : JsonSerializerContext;
