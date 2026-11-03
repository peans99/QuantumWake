using System.Globalization;
using System.Xml.Linq;

namespace Quantumwake.Core.GameData;

/// <summary>One layer of a layered hull material, with what the layer file says it looks like.</summary>
/// <param name="Name">The layer's role - <c>Primary</c>, <c>Wear</c>, <c>Secondary</c>.</param>
/// <param name="Tint">The colour the sub-material multiplies the layer by, linear RGB.</param>
/// <param name="PaletteTint">0 for none, or 1-3 for the ship's tint palette entry A, B or C - which is how a paint recolours a hull.</param>
/// <param name="GlossMult">Multiplies the layer's gloss.</param>
/// <param name="Tiling">How many times the layer repeats across the UVs: the sub-material's and the layer's own tiling together.</param>
/// <param name="Diffuse">The layer's colour texture, as the material names it, or null.</param>
/// <param name="Normal">The layer's normal map (<c>_ddna</c>, gloss beside it), or null.</param>
/// <param name="Colour">The layer file's own diffuse colour, linear RGB.</param>
/// <param name="Specular">The layer file's specular colour - its reflectance at head-on - linear RGB.</param>
/// <param name="Gloss">The layer file's gloss, 0-1.</param>
public sealed record ModelLayer(
    string Name, float[] Tint, int PaletteTint, float GlossMult, float Tiling,
    string? Diffuse, string? Normal, float[] Colour, float[] Specular, float Gloss);

/// <summary>One sub-material: what a run of triangles is drawn with.</summary>
/// <param name="Shader">The engine shader - <c>HardSurface</c>, <c>Illum</c>, <c>GlassPBR</c>, <c>MeshDecal</c>, <c>NoDraw</c>.</param>
/// <param name="Hidden">True for a material the game never draws: physics proxies, <c>NoDraw</c>.</param>
/// <param name="Diffuse">Linear RGB.</param>
/// <param name="Specular">Linear RGB.</param>
/// <param name="Emissive">Linear RGB, already scaled by the glow amount.</param>
/// <param name="Gloss">0-1, from the material's shininess out of 255.</param>
/// <param name="AlphaTest">The cut-out threshold, or 0 for none.</param>
/// <param name="Textures">Texture slot to file, as written: <c>TexSlot1</c> is the colour map, <c>TexSlot2</c> the normal map.</param>
/// <param name="Glass">True for glass, which the palette's glass colour tints.</param>
/// <param name="TintsGlass">True when the glass takes the palette's glass colour.</param>
/// <param name="Decal">True for a decal or stencil sheet drawn over the hull.</param>
public sealed record ModelSubMaterial(
    string Name, string Shader, bool Hidden,
    float[] Diffuse, float[] Specular, float[] Emissive, float Opacity, float Gloss, float AlphaTest,
    IReadOnlyDictionary<string, string> Textures, IReadOnlyList<ModelLayer> Layers,
    bool Glass, bool TintsGlass, bool Decal);

/// <summary>
/// Reads a material file - <c>.mtl</c>, binary or text XML - into what a
/// viewer needs to draw it.
/// </summary>
/// <remarks>
/// <para>
/// A model's triangles carry a sub-material index into its material file, and
/// that file is a list of sub-materials. The Corsair's
/// <c>DRAK_Corsair_Default_New.mtl</c> has 92: 39 <c>HardSurface</c>,
/// 21 <c>Illum</c>, 20 <c>NoDraw</c>, 5 glass, 4 decals.
/// </para>
/// <para>
/// <c>HardSurface</c> is the hull: it draws no texture of its own, but blends
/// layer files (<c>Materials/vehicles/layers/...</c>) - a tileable painted
/// panel, say, with its colour and normal maps - each tinted, and one of them
/// usually tinted by the palette (<c>PaletteTint="1"</c> is entry A). That is
/// the whole of how paints work: the Corsair's ten liveries are ten palettes
/// over the same material file. The layers are resolved here, so the page gets
/// one record per sub-material with the textures it needs to load.
/// </para>
/// <para>
/// Colours in material files are linear already; the palette's are 8-bit sRGB,
/// which the page converts.
/// </para>
/// </remarks>
public static class ModelMaterials
{
    /// <summary>Parses a material file; <paramref name="readLayer"/> fetches the layer files it names.</summary>
    public static List<ModelSubMaterial> Read(byte[] mtl, Func<string, byte[]?> readLayer)
    {
        var root = Parse(mtl);
        if (root is null) return [];

        var layers = new Dictionary<string, XElement?>(StringComparer.OrdinalIgnoreCase);
        XElement? Layer(string path)
        {
            if (!layers.TryGetValue(path, out var element))
            {
                element = readLayer(path) is { } bytes ? Parse(bytes) : null;
                layers[path] = element;
            }
            return element;
        }

        var subs = root.Element("SubMaterials")?.Elements("Material").ToList();
        var materials = subs is { Count: > 0 } ? subs : [root];
        return [.. materials.Select(m => Sub(m, Layer))];
    }

    private static XElement? Parse(byte[] bytes)
    {
        try
        {
            return CryXml.Parse(bytes).Root;
        }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or ArgumentException)
        {
            return null;
        }
    }

    private static ModelSubMaterial Sub(XElement m, Func<string, XElement?> layer)
    {
        var shader = (string?)m.Attribute("Shader") ?? "";
        var name = (string?)m.Attribute("Name") ?? "";
        var mask = (string?)m.Attribute("StringGenMask") ?? "";
        var hidden = shader.Equals("NoDraw", StringComparison.OrdinalIgnoreCase)
            || name.Contains("proxy", StringComparison.OrdinalIgnoreCase);

        var publics = m.Element("PublicParams");
        var glow = Number(publics, "GlowAmount") ?? Number(publics, "EmissiveIntensity") ?? 1f;
        var emissive = Colour(m, "Emissive").Select(c => c * glow).ToArray();

        var layers = new List<ModelLayer>();
        foreach (var l in m.Element("MatLayers")?.Elements("Layer") ?? [])
        {
            var path = (string?)l.Attribute("Path");
            if (string.IsNullOrWhiteSpace(path)) continue;
            var file = layer(path);
            var sub = (string?)l.Attribute("Submtl");
            if (file is not null && !string.IsNullOrEmpty(sub)
                && file.Element("SubMaterials")?.Elements("Material")
                    .FirstOrDefault(x => string.Equals((string?)x.Attribute("Name"), sub, StringComparison.OrdinalIgnoreCase)) is { } picked)
                file = picked;

            var textures = file is null ? new Dictionary<string, string>() : Textures(file);
            var layerTiling = Number(file?.Element("PublicParams"), "LayerTiling") ?? 1f;
            layers.Add(new ModelLayer(
                (string?)l.Attribute("Name") ?? "",
                Vector(l, "TintColor", 1f),
                (int)(Number(l, "PaletteTint") ?? 0),
                Number(l, "GlossMult") ?? 1f,
                (Number(l, "UVTiling") ?? 1f) * layerTiling,
                textures.GetValueOrDefault("TexSlot1") ?? textures.GetValueOrDefault("Diffuse"),
                textures.GetValueOrDefault("TexSlot2") ?? textures.GetValueOrDefault("Bumpmap"),
                file is null ? [1, 1, 1] : Colour(file, "Diffuse", 1f),
                file is null ? [0.04f, 0.04f, 0.04f] : Colour(file, "Specular", 0.04f),
                file is null ? 0.5f : (Number(file, "Shininess") ?? 128f) / 255f));
        }

        var glass = shader.StartsWith("Glass", StringComparison.OrdinalIgnoreCase);
        return new ModelSubMaterial(
            name, shader, hidden,
            Colour(m, "Diffuse", 1f), Colour(m, "Specular", 0.04f), emissive,
            Number(m, "Opacity") ?? 1f,
            (Number(m, "Shininess") ?? 128f) / 255f,
            Number(m, "AlphaTest") ?? 0f,
            Textures(m), layers,
            glass, glass && mask.Contains("USE_TINT_PALETTE", StringComparison.OrdinalIgnoreCase),
            shader.Contains("Decal", StringComparison.OrdinalIgnoreCase)
                || mask.Contains("DECAL", StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, string> Textures(XElement m)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in m.Element("Textures")?.Elements("Texture") ?? [])
        {
            var slot = (string?)t.Attribute("Map");
            var file = (string?)t.Attribute("File");
            // $-names are engine placeholders (render targets, the tint
            // palette's own decal sheet), not files.
            if (string.IsNullOrEmpty(slot) || string.IsNullOrWhiteSpace(file) || file.StartsWith('$')) continue;
            map.TryAdd(slot, file);
        }
        return map;
    }

    private static float? Number(XElement? e, string name) =>
        e?.Attribute(name) is { } a && float.TryParse(a.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    private static float[] Colour(XElement e, string name, float fallback = 0f) => Vector(e, name, fallback);

    private static float[] Vector(XElement e, string name, float fallback)
    {
        var raw = (string?)e.Attribute(name);
        var parts = raw?.Split(',') ?? [];
        var v = new float[3];
        for (var i = 0; i < 3; i++)
            v[i] = i < parts.Length && float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : fallback;
        return v;
    }
}
