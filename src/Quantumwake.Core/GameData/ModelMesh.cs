using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Quantumwake.Core.GameData;

/// <summary>A node as the page receives it.</summary>
/// <param name="Name">The node name.</param>
/// <param name="Parent">The parent's index, or -1.</param>
/// <param name="Matrix">The local transform, sixteen floats in the order WebGL reads (column-major).</param>
/// <param name="Helper">True for a point with no geometry: a hardpoint.</param>
public sealed record MeshNode(
    [property: JsonPropertyName("n")] string Name,
    [property: JsonPropertyName("p")] int Parent,
    [property: JsonPropertyName("m")] float[] Matrix,
    [property: JsonPropertyName("h")] bool Helper);

/// <summary>A run of triangles as the page receives it.</summary>
/// <param name="Node">Which node's space the vertices are in.</param>
/// <param name="File">An index into <see cref="MeshHeader.Materials"/>.</param>
/// <param name="Material">The sub-material within that file.</param>
/// <param name="First">The first index in the index buffer.</param>
/// <param name="Count">How many indices.</param>
public sealed record MeshSubset(
    [property: JsonPropertyName("node")] int Node,
    [property: JsonPropertyName("file")] int File,
    [property: JsonPropertyName("mat")] int Material,
    [property: JsonPropertyName("first")] int First,
    [property: JsonPropertyName("count")] int Count);

/// <summary>What precedes the buffers.</summary>
public sealed record MeshHeader(
    [property: JsonPropertyName("vertices")] int Vertices,
    [property: JsonPropertyName("indices")] int Indices,
    [property: JsonPropertyName("materials")] IReadOnlyList<string> Materials,
    [property: JsonPropertyName("nodes")] IReadOnlyList<MeshNode> Nodes,
    [property: JsonPropertyName("subsets")] IReadOnlyList<MeshSubset> Subsets);

/// <summary>
/// A model packed for the page: one header and four flat buffers WebGL can
/// take without conversion.
/// </summary>
/// <remarks>
/// <para>
/// Layout: <c>QWM1</c>, a 32-bit header length, the header as JSON padded to
/// four bytes, then positions (three 32-bit floats a vertex), normals (four
/// signed bytes a vertex, the fourth unused, so every vertex starts aligned),
/// texture coordinates (two floats) and 32-bit indices.
/// </para>
/// <para>
/// What the game never draws is left out here rather than on the page: the
/// damaged variants of every panel (the Corsair's hull carries nine
/// <c>_damage</c> nodes, 75,000 triangles that only show once it is shot up),
/// physics proxies, and sub-materials whose shader is <c>NoDraw</c>. The
/// vertices only those used are dropped with them.
/// </para>
/// <para>
/// A <c>.cdf</c> is a character - a skeleton and skinned parts, which is how
/// the Corsair's main thrusters are built. Its parts are drawn in their rest
/// pose, each under a node of its own named for the attachment.
/// </para>
/// </remarks>
public static class ModelMesh
{
    /// <summary>
    /// Raised whenever what <see cref="Build"/> writes changes, so meshes packed
    /// by an older build are packed again rather than served from disk.
    /// </summary>
    public const int Version = 2;

    /// <summary>
    /// Packs the model at <paramref name="geometry"/>, or null when the archive
    /// does not hold it or it is not a format read here.
    /// </summary>
    /// <param name="materials">Looks up a material file's sub-materials, to leave hidden ones out.</param>
    public static byte[]? Build(ModelArchive archive, string geometry, Func<string, IReadOnlyList<ModelSubMaterial>?> materials)
    {
        var parts = Parts(archive, geometry);
        if (parts.Count == 0) return null;

        var files = new List<string>();
        var nodes = new List<MeshNode>();
        var subsets = new List<MeshSubset>();
        var positions = new List<float>();
        var normals = new List<sbyte>();
        var uvs = new List<float>();
        var indices = new List<uint>();

        foreach (var (name, model, materialOverride) in parts)
        {
            var file = materialOverride ?? model.MaterialFile;
            var fileIndex = -1;
            IReadOnlyList<ModelSubMaterial>? subMaterials = null;
            if (file is not null)
            {
                file = WithExtension(file);
                fileIndex = files.FindIndex(f => f.Equals(file, StringComparison.OrdinalIgnoreCase));
                if (fileIndex < 0)
                {
                    files.Add(file);
                    fileIndex = files.Count - 1;
                }
                subMaterials = materials(file);
            }

            // A part from a character has no node table; give it one node so
            // the page has something to hang it on.
            var nodeBase = nodes.Count;
            var rooted = model.Nodes.Count == 0 || model.Skeleton || parts.Count > 1;
            if (rooted)
            {
                nodes.Add(new MeshNode(name, -1, Identity, false));
            }
            var partRoot = rooted ? nodeBase : -1;
            var offset = nodes.Count;
            foreach (var n in model.Nodes)
                nodes.Add(new MeshNode(n.Name, n.Parent < 0 ? partRoot : n.Parent + offset, ColumnMajor(n.Local), n.Helper));

            var copied = new Dictionary<int, int>();
            foreach (var s in model.Subsets)
            {
                if (s.IndexCount <= 0 || s.VertexCount <= 0) continue;
                if (s.FirstIndex + s.IndexCount > model.Indices.Length) continue;
                if (s.FirstVertex + s.VertexCount > model.Positions.Length / 3) continue;
                var byNode = model.Nodes.Count > 0 && !model.Skeleton;
                if (byNode && (s.Node >= model.Nodes.Count || Hidden(model.Nodes[s.Node].Name))) continue;
                if (subMaterials is not null && s.Material < subMaterials.Count && subMaterials[s.Material].Hidden) continue;

                var inRange = true;
                for (var i = s.FirstIndex; i < s.FirstIndex + s.IndexCount && inRange; i++)
                    inRange = model.Indices[i] >= s.FirstVertex && model.Indices[i] < s.FirstVertex + s.VertexCount;
                if (!inRange) continue;

                if (!copied.TryGetValue(s.FirstVertex, out var start))
                {
                    start = positions.Count / 3;
                    copied[s.FirstVertex] = start;
                    for (var v = s.FirstVertex; v < s.FirstVertex + s.VertexCount; v++)
                    {
                        positions.Add(model.Positions[v * 3]);
                        positions.Add(model.Positions[v * 3 + 1]);
                        positions.Add(model.Positions[v * 3 + 2]);
                        if (model.Normals.Length > 0)
                        {
                            normals.Add(Snorm(model.Normals[v * 3]));
                            normals.Add(Snorm(model.Normals[v * 3 + 1]));
                            normals.Add(Snorm(model.Normals[v * 3 + 2]));
                        }
                        else
                        {
                            normals.Add(0); normals.Add(0); normals.Add(127);
                        }
                        normals.Add(0);
                        uvs.Add(model.Uvs[v * 2]);
                        uvs.Add(model.Uvs[v * 2 + 1]);
                    }
                }

                var first = indices.Count;
                for (var i = s.FirstIndex; i < s.FirstIndex + s.IndexCount; i++)
                    indices.Add((uint)(model.Indices[i] - s.FirstVertex + start));

                var node = byNode ? s.Node + offset : nodeBase;
                subsets.Add(new MeshSubset(node, fileIndex, s.Material, first, s.IndexCount));
            }
        }

        if (subsets.Count == 0 && nodes.Count == 0) return null;
        return Encode(new MeshHeader(positions.Count / 3, indices.Count, files, nodes, subsets),
            positions, normals, uvs, indices);
    }

    /// <summary>Nodes the game draws only in some state the view does not show.</summary>
    internal static bool Hidden(string node)
    {
        var n = node.ToLowerInvariant();
        return n.Contains("_damage") || n.Contains("damaged") || n.Contains("_dmg") || n.Contains("destroyed")
            || n.Contains("proxy") || n.Contains("physics") || n.EndsWith("_broken", StringComparison.Ordinal);
    }

    private static List<(string Name, IvoModel Model, string? Material)> Parts(ModelArchive archive, string geometry)
    {
        var list = new List<(string, IvoModel, string?)>();
        var path = geometry.Replace('\\', '/');
        var ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext == ".cdf")
        {
            if (archive.TryRead(path) is not { } cdf) return list;
            XElement? root;
            try { root = CryXml.Parse(cdf).Root; }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException) { return list; }
            if (root is null) return list;

            var model = root.Element("Model");
            if ((string?)model?.Attribute("File") is { Length: > 0 } chr && Part(archive, chr) is { } body)
                list.Add(("body", body, (string?)model.Attribute("Material")));

            foreach (var a in root.Element("AttachmentList")?.Elements("Attachment") ?? [])
            {
                var type = (string?)a.Attribute("Type");
                var binding = (string?)a.Attribute("Binding");
                if (type is not ("CA_SKIN" or "CA_BONE") || string.IsNullOrWhiteSpace(binding)) continue;
                var bext = Path.GetExtension(binding).ToLowerInvariant();
                if (bext is not (".skin" or ".cgf" or ".cga")) continue;
                if (Part(archive, binding) is { } part)
                    list.Add(((string?)a.Attribute("AName") ?? Path.GetFileNameWithoutExtension(binding), part, (string?)a.Attribute("Material")));
            }
            return list;
        }

        if (Part(archive, path) is { } single)
            list.Add((Path.GetFileNameWithoutExtension(path), single, null));
        return list;
    }

    private static IvoModel? Part(ModelArchive archive, string path)
    {
        var bytes = archive.TryRead(path);
        if (bytes is null) return null;
        // The mesh sits beside the node file with an "m" on the extension:
        // .cga/.cgam, .cgf/.cgfm, .skin/.skinm, .chr/.chrm.
        var mesh = archive.TryRead(path + "m");
        try
        {
            return IvoModel.Read(bytes, mesh);
        }
        catch (Exception e) when (e is EndOfStreamException or ArgumentException or IOException)
        {
            return null;
        }
    }

    private static string WithExtension(string material) =>
        material.EndsWith(".mtl", StringComparison.OrdinalIgnoreCase) ? material : material + ".mtl";

    private static readonly float[] Identity = [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];

    /// <summary>
    /// System.Numerics keeps row vectors, translation in the bottom row; read
    /// out row by row, that is exactly the column-major order WebGL wants for
    /// the column-vector form of the same transform.
    /// </summary>
    private static float[] ColumnMajor(Matrix4x4 m) =>
        [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];

    private static sbyte Snorm(float v) => (sbyte)Math.Clamp(MathF.Round(v * 127f), -127, 127);

    private static byte[] Encode(MeshHeader header, List<float> positions, List<sbyte> normals, List<float> uvs, List<uint> indices)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(header, MeshJson.Default.MeshHeader);
        var padded = (json.Length + 3) & ~3;
        var size = 8 + padded + positions.Count * 4 + normals.Count + uvs.Count * 4 + indices.Count * 4;
        var bytes = new byte[size];
        Encoding.ASCII.GetBytes("QWM1").CopyTo(bytes, 0);
        BitConverter.TryWriteBytes(bytes.AsSpan(4), padded);
        json.CopyTo(bytes, 8);
        for (var i = json.Length; i < padded; i++) bytes[8 + i] = (byte)' ';

        var at = 8 + padded;
        foreach (var f in positions) { BitConverter.TryWriteBytes(bytes.AsSpan(at), f); at += 4; }
        foreach (var n in normals) bytes[at++] = (byte)n;
        foreach (var f in uvs) { BitConverter.TryWriteBytes(bytes.AsSpan(at), f); at += 4; }
        foreach (var i in indices) { BitConverter.TryWriteBytes(bytes.AsSpan(at), i); at += 4; }
        return bytes;
    }
}

[JsonSerializable(typeof(MeshHeader))]
internal partial class MeshJson : JsonSerializerContext;
