using System.Numerics;
using System.Text;

namespace Quantumwake.Core.GameData;

/// <summary>One node of a model: a piece of hull, or a named point something fits to.</summary>
/// <param name="Name">The node name - <c>nose</c>, <c>hardpoint_tail_turret</c>.</param>
/// <param name="Parent">The parent's index, or -1 for the root.</param>
/// <param name="Local">The transform relative to the parent, row-vector convention (translation in M41..M43).</param>
/// <param name="Helper">True for a point with no geometry of its own - every hardpoint is one.</param>
public sealed record IvoNode(string Name, int Parent, Matrix4x4 Local, bool Helper);

/// <summary>A run of triangles that share one material and belong to one node.</summary>
/// <param name="Node">The node the triangles belong to; their vertices are in that node's space.</param>
/// <param name="Material">The index into the material file's sub-materials.</param>
public sealed record IvoSubset(int Node, int Material, int FirstIndex, int IndexCount, int FirstVertex, int VertexCount);

/// <summary>
/// The geometry of one model the game ships as <c>#ivo</c>: a <c>.cga</c>/<c>.cgf</c>
/// holding the node tree, beside a <c>.cgam</c>/<c>.cgfm</c> holding the mesh.
/// </summary>
/// <remarks>
/// <para>
/// The layout follows the open-source CryEngine converter
/// (https://github.com/Markemp/Cryengine-Converter, see docs/credits.md), checked
/// against this install: the Corsair's <c>DRAK_Corsair.cga</c> carries 319 nodes,
/// 103 of them with geometry and 87 named <c>hardpoint_*</c>, and the vertices
/// decoded from its <c>.cgam</c> land inside the boxes the node table records.
/// </para>
/// <para>
/// The file is a chunk table - a 16-byte header (<c>#ivo</c>, version, count,
/// table offset) and 16-byte entries (type, version, 64-bit offset) whose sizes
/// are the gaps between offsets. Three chunks matter here: the node table
/// (<c>0x70697FDA</c>), the material file name (<c>0x83353333</c>, or
/// <c>0x8335674E</c> in older files) and the mesh (<c>0xB8757777</c>, or
/// <c>0xB875B2D9</c>).
/// </para>
/// <para>
/// Only the most detailed mesh is read. The files also carry four lower
/// levels of detail and a physics proxy, neither of which a viewer wants.
/// </para>
/// </remarks>
public sealed class IvoModel
{
    public const uint NodeChunk = 0x70697FDA;
    public const uint MaterialChunk = 0x83353333;
    public const uint MaterialChunkOld = 0x8335674E;
    public const uint MeshChunk = 0xB8757777;
    public const uint MeshChunkOld = 0xB875B2D9;
    public const uint BonesChunk = 0xC2011111;
    public const uint BonesChunkOld = 0xC201973C;

    private const uint StreamIndices = 0xEECDC168;
    private const uint StreamVertsUvs = 0x91329AE9;
    private const uint StreamVertsUvs2 = 0xB3A70D5E;
    private const uint StreamNormals = 0x9CF3F615;
    private const uint StreamNormals2 = 0x38A581FE;
    private const uint StreamTangents = 0xB95E9A1B;
    private const uint StreamQTangents = 0xEE057252;
    private const uint StreamColours = 0xD9EED421;

    public List<IvoNode> Nodes { get; } = [];

    /// <summary>True when <see cref="Nodes"/> are a skeleton's bones rather than a node table; no subset belongs to them.</summary>
    public bool Skeleton { get; private set; }

    /// <summary>The material file the model names - <c>objects/spaceships/ships/drak/corsair/drak_corsair</c> - without extension, as written.</summary>
    public string? MaterialFile { get; private set; }

    /// <summary>Positions, three floats a vertex, in the space of the node each subset names.</summary>
    public float[] Positions { get; private set; } = [];

    /// <summary>Unit normals, three floats a vertex; empty when the mesh stores none.</summary>
    public float[] Normals { get; private set; } = [];

    /// <summary>Texture coordinates, two floats a vertex.</summary>
    public float[] Uvs { get; private set; } = [];

    /// <summary>Vertex colours, four bytes a vertex; the game paints wear and dirt masks into them.</summary>
    public byte[] Colours { get; private set; } = [];

    public uint[] Indices { get; private set; } = [];

    public List<IvoSubset> Subsets { get; } = [];

    public static bool IsIvo(ReadOnlySpan<byte> data) =>
        data.Length >= 16 && data[0] == (byte)'#' && data[1] == (byte)'i' && data[2] == (byte)'v' && data[3] == (byte)'o';

    /// <summary>
    /// Reads a model from its node file and, when the mesh lives apart, its mesh file.
    /// Null when the node file is not <c>#ivo</c>.
    /// </summary>
    public static IvoModel? Read(byte[] nodes, byte[]? mesh)
    {
        if (!IsIvo(nodes)) return null;

        var model = new IvoModel();
        var nodeChunks = Chunks(nodes);

        // The per-node boxes are what the compressed positions are scaled to.
        var boxes = new List<(Vector3 Min, Vector3 Max)>();
        foreach (var (type, offset, size) in nodeChunks)
        {
            if (type == NodeChunk) model.ReadNodes(nodes, offset, size, boxes);
            else if (type is MaterialChunk or MaterialChunkOld) model.MaterialFile ??= ReadMaterialName(nodes, offset);
        }

        // A character (.chr, .skin) has a skeleton instead of a node table,
        // and its bones are where attachments go: an FPS rifle's magazine
        // hangs off magazine_attach.
        if (model.Nodes.Count == 0)
        {
            foreach (var (type, offset, size) in nodeChunks)
            {
                if (type is BonesChunk or BonesChunkOld)
                {
                    model.ReadBones(nodes, offset, size);
                    break;
                }
            }
        }

        foreach (var source in mesh is not null && IsIvo(mesh) ? [mesh, nodes] : new[] { nodes })
        {
            foreach (var (type, offset, size) in Chunks(source))
            {
                if (type is MeshChunk or MeshChunkOld)
                {
                    model.ReadMesh(source, offset, size, boxes);
                    return model;
                }
            }
        }

        return model;
    }

    private static List<(uint Type, int Offset, int Size)> Chunks(byte[] data)
    {
        var count = BitConverter.ToInt32(data, 8);
        var table = BitConverter.ToInt32(data, 12);
        var list = new List<(uint, int, int)>(count);
        if (count < 0 || table < 0 || table + (long)count * 16 > data.Length) return list;

        var offsets = new long[count];
        for (var i = 0; i < count; i++) offsets[i] = (long)BitConverter.ToUInt64(data, table + i * 16 + 8);

        for (var i = 0; i < count; i++)
        {
            var type = BitConverter.ToUInt32(data, table + i * 16);
            var start = offsets[i];
            // Sizes are not stored: a chunk runs to the next one, and the last
            // to the table (or the end, when the table comes first).
            var end = i + 1 < count ? offsets[i + 1] : (table > start ? table : data.Length);
            if (start < 0 || end > data.Length || end < start) continue;
            list.Add((type, (int)start, (int)(end - start)));
        }
        return list;
    }

    private void ReadNodes(byte[] data, int offset, int size, List<(Vector3, Vector3)> boxes)
    {
        using var r = new BinaryReader(new MemoryStream(data, offset, size));
        r.ReadInt32();
        var count = r.ReadInt32();
        r.ReadInt32();                 // meshes
        var unknownIndices = r.ReadInt32();
        var materialIndices = r.ReadInt32();
        var strings = r.ReadInt32();
        r.ReadInt32();
        r.ReadInt32();
        r.ReadBytes(32);

        var records = new List<(Matrix4x4 Local, int Parent, bool Helper)>(count);
        for (var i = 0; i < count; i++)
        {
            r.ReadBytes(48);           // world to bone
            var m = new float[12];
            for (var k = 0; k < 12; k++) m[k] = r.ReadSingle();
            r.ReadBytes(12);           // scale
            r.ReadUInt32();            // id
            r.ReadUInt32();
            var parent = r.ReadUInt16();
            var type = r.ReadUInt16();
            var min = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var max = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            r.ReadBytes(16);
            r.ReadUInt32();            // vertex count
            r.ReadUInt16();            // children
            r.ReadUInt16();            // mesh chunk
            r.ReadBytes(40);

            // Stored as a 3x4 with the translation in the last column; the
            // row-vector form System.Numerics composes wants it transposed.
            var local = new Matrix4x4(
                m[0], m[4], m[8], 0,
                m[1], m[5], m[9], 0,
                m[2], m[6], m[10], 0,
                m[3], m[7], m[11], 1);
            records.Add((local, parent == 0xFFFF ? -1 : parent, type != 0));
            boxes.Add((min, max));
        }

        r.ReadBytes(unknownIndices * 2 + materialIndices * 2);
        var names = Encoding.ASCII.GetString(r.ReadBytes(strings)).Split('\0');

        for (var i = 0; i < count; i++)
        {
            var (local, parent, helper) = records[i];
            Nodes.Add(new IvoNode(i < names.Length ? names[i] : $"node_{i}", parent < count ? parent : -1, local, helper));
        }
    }

    /// <summary>
    /// A skeleton, kept as one helper node per bone in model space - the
    /// skinned parts beside it are in the same space at rest, so a bone needs
    /// no parent to be placed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two layouts. The one the CryEngine converter documents: a count, a name
    /// table size, two flag words and 32 bytes of padding, sixteen bytes a
    /// bone, the names, then every bone's local and every bone's model-space
    /// transform (quaternion and translation each).
    /// </para>
    /// <para>
    /// The one this install's FPS weapons use (<c>brfl_fps_behr_p4ar.chr</c>):
    /// a count, then 68 bytes a bone - controller id, a second id, the parent's
    /// index, the local transform and the model-space transform - and the names
    /// last. The controller id is the CRC-32 of the lowercased name; the root's
    /// is 0x16F4F95B, which is "root", and that is how the layout was told
    /// apart from the other one.
    /// </para>
    /// </remarks>
    private void ReadBones(byte[] data, int offset, int size)
    {
        if (size < 8) return;
        var count = BitConverter.ToInt32(data, offset);
        if (count <= 0 || count > 4096) return;

        var second = BitConverter.ToInt32(data, offset + 4);
        var packed = 4 + (long)count * 68;
        if (second > 0 && second < size && 48 + (long)count * (16 + 56) + second <= size)
            ReadBonesTabled(data, offset, size, count, second);
        else if (packed < size)
            ReadBonesPacked(data, offset, size, count);
    }

    private void ReadBonesTabled(byte[] data, int offset, int size, int count, int strings)
    {
        using var r = new BinaryReader(new MemoryStream(data, offset, size));
        r.ReadBytes(16 + 32);
        r.ReadBytes(count * 16);
        var names = Encoding.ASCII.GetString(r.ReadBytes(strings)).Split('\0');
        r.ReadBytes(count * 28);    // local transforms
        var bones = new List<IvoNode>(count);
        for (var i = 0; i < count; i++)
            bones.Add(new IvoNode(i < names.Length ? names[i] : $"bone_{i}", -1, Transform(r), true));
        Nodes.AddRange(bones);
        Skeleton = true;
    }

    private void ReadBonesPacked(byte[] data, int offset, int size, int count)
    {
        using var r = new BinaryReader(new MemoryStream(data, offset, size));
        r.ReadInt32();
        var transforms = new List<Matrix4x4>(count);
        for (var i = 0; i < count; i++)
        {
            r.ReadBytes(12);            // id, second id, parent
            r.ReadBytes(28);            // local transform
            transforms.Add(Transform(r));
        }

        var tail = data.AsSpan(offset + 4 + count * 68, size - 4 - count * 68);
        var names = Encoding.ASCII.GetString(tail).Split('\0');
        if (names.Length < count) return;
        for (var i = 0; i < count; i++)
            Nodes.Add(new IvoNode(names[i], -1, transforms[i], true));
        Skeleton = true;
    }

    private static Matrix4x4 Transform(BinaryReader r)
    {
        var q = new Quaternion(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        var t = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        var length = q.Length();
        q = length > 1e-6f ? Quaternion.Normalize(q) : Quaternion.Identity;
        return Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(t);
    }

    private static string? ReadMaterialName(byte[] data, int offset)
    {
        if (offset + 128 > data.Length) return null;
        var span = data.AsSpan(offset, 128);
        var end = span.IndexOf((byte)0);
        var name = Encoding.ASCII.GetString(end < 0 ? span : span[..end]).Trim();
        return name.Length == 0 ? null : name;
    }

    private void ReadMesh(byte[] data, int offset, int size, List<(Vector3 Min, Vector3 Max)> boxes)
    {
        using var r = new BinaryReader(new MemoryStream(data, offset, size));
        r.ReadUInt32();                // flags
        r.ReadUInt32();                // flags 2
        var vertices = r.ReadInt32();
        var indices = r.ReadInt32();
        var subsets = r.ReadInt32();
        r.ReadInt32();
        r.ReadBytes(24);               // bounding box
        var scaleMin = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        var scaleMax = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
        r.ReadUInt32();                // vertex format
        r.ReadBytes(92);

        for (var i = 0; i < subsets; i++)
        {
            var material = r.ReadUInt16();
            var node = r.ReadUInt16();
            var firstIndex = r.ReadInt32();
            var indexCount = r.ReadInt32();
            var firstVertex = r.ReadInt32();
            r.ReadInt32();
            var vertexCount = r.ReadInt32();
            r.ReadBytes(24);           // radius, centre, two unknowns
            Subsets.Add(new IvoSubset(node, material, firstIndex, indexCount, firstVertex, vertexCount));
        }

        Positions = new float[vertices * 3];
        Uvs = new float[vertices * 2];
        var haveIndices = false;

        while (r.BaseStream.Position + 8 <= r.BaseStream.Length)
        {
            var stream = r.ReadUInt32();
            var stride = r.ReadInt32();
            if (stride <= 0 || stride > 64) break;
            var start = r.BaseStream.Position;
            var length = (long)stride * (stream == StreamIndices ? indices : vertices);
            if (start + length > r.BaseStream.Length) break;

            switch (stream)
            {
                case StreamIndices:
                    if (haveIndices) return;   // a second index stream belongs to a lower level of detail
                    haveIndices = true;
                    Indices = new uint[indices];
                    for (var i = 0; i < indices; i++) Indices[i] = stride == 2 ? r.ReadUInt16() : r.ReadUInt32();
                    Rebase();
                    break;

                case StreamVertsUvs:
                case StreamVertsUvs2:
                    Colours = new byte[vertices * 4];
                    for (var i = 0; i < vertices; i++)
                    {
                        if (stride == 20)
                        {
                            Positions[i * 3] = r.ReadSingle();
                            Positions[i * 3 + 1] = r.ReadSingle();
                            Positions[i * 3 + 2] = r.ReadSingle();
                        }
                        else
                        {
                            Positions[i * 3] = r.ReadInt16() / 32767f;
                            Positions[i * 3 + 1] = r.ReadInt16() / 32767f;
                            Positions[i * 3 + 2] = r.ReadInt16() / 32767f;
                            r.ReadInt16();
                        }
                        r.Read(Colours, i * 4, 4);
                        Uvs[i * 2] = (float)r.ReadHalf();
                        Uvs[i * 2 + 1] = (float)r.ReadHalf();
                        r.BaseStream.Position = start + (long)(i + 1) * stride;
                    }
                    if (stride != 20) Unpack(scaleMin, scaleMax);
                    break;

                case StreamNormals:
                case StreamNormals2:
                    if (stride is 4 or 12)
                    {
                        Normals = new float[vertices * 3];
                        for (var i = 0; i < vertices; i++)
                        {
                            var n = stride == 4 ? PackedUnit(r.ReadUInt32()) : new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                            SetNormal(i, n);
                        }
                    }
                    break;

                case StreamQTangents when Normals.Length == 0 && stride == 8:
                    Normals = new float[vertices * 3];
                    for (var i = 0; i < vertices; i++)
                    {
                        var t = r.ReadUInt32();
                        var b = r.ReadUInt32();
                        var sign = (t & 0x80000000) != 0 ? -1f : 1f;
                        SetNormal(i, Vector3.Cross(PackedUnit(t), PackedUnit(b)) * sign);
                    }
                    break;

                case StreamTangents when Normals.Length == 0 && stride == 8:
                    Normals = new float[vertices * 3];
                    for (var i = 0; i < vertices; i++)
                    {
                        float qx = r.ReadInt16() / 32767f, qy = r.ReadInt16() / 32767f,
                              qz = r.ReadInt16() / 32767f, qw = r.ReadInt16() / 32767f;
                        var n = new Vector3(2 * (qx * qz + qw * qy), 2 * (qy * qz - qw * qx), 1 - 2 * (qx * qx + qy * qy));
                        SetNormal(i, qw < 0 ? -n : n);
                    }
                    break;
            }

            r.BaseStream.Position = start + length;
            // Every stream is padded to eight bytes of the file, not of the chunk.
            var pad = (8 - (offset + r.BaseStream.Position) % 8) % 8;
            r.BaseStream.Position += pad;
        }
    }

    /// <summary>
    /// Makes every index absolute. Sixteen-bit indices cannot reach past 65,535,
    /// so a big mesh restarts them: on the Corsair the subsets up to vertex
    /// 61,362 count from zero, and the next one counts from its own first vertex
    /// again. A subset's smallest index is its first vertex, which gives the base.
    /// </summary>
    private void Rebase()
    {
        foreach (var s in Subsets)
        {
            if (s.IndexCount <= 0 || s.FirstIndex < 0 || s.FirstIndex + s.IndexCount > Indices.Length) continue;
            var min = uint.MaxValue;
            for (var i = s.FirstIndex; i < s.FirstIndex + s.IndexCount; i++) min = Math.Min(min, Indices[i]);
            var delta = (long)s.FirstVertex - min;
            if (delta == 0) continue;
            for (var i = s.FirstIndex; i < s.FirstIndex + s.IndexCount; i++) Indices[i] = (uint)(Indices[i] + delta);
        }
    }

    /// <summary>
    /// Compressed positions are signed-normalised within the mesh's scaling box:
    /// half its extent either side of its centre, each axis held to at least a
    /// metre, which is how the engine avoids a degenerate axis on a flat part.
    /// </summary>
    private void Unpack(Vector3 min, Vector3 max)
    {
        var half = Vector3.Abs(max - min) / 2f;
        half = Vector3.Max(half, Vector3.One);
        var centre = (max + min) / 2f;
        for (var i = 0; i < Positions.Length; i += 3)
        {
            Positions[i] = Positions[i] * half.X + centre.X;
            Positions[i + 1] = Positions[i + 1] * half.Y + centre.Y;
            Positions[i + 2] = Positions[i + 2] * half.Z + centre.Z;
        }
    }

    private void SetNormal(int i, Vector3 n)
    {
        var length = n.Length();
        n = length > 1e-8f ? n / length : Vector3.UnitZ;
        Normals[i * 3] = n.X;
        Normals[i * 3 + 1] = n.Y;
        Normals[i * 3 + 2] = n.Z;
    }

    /// <summary>A unit vector packed 15-15-1-1: x and y biased by 0x3FFF, then z's sign.</summary>
    private static Vector3 PackedUnit(uint v)
    {
        const float scale = 1f / 16383f;
        var x = ((int)(v & 0x7FFF) - 0x3FFF) * scale;
        var y = ((int)((v >> 15) & 0x7FFF) - 0x3FFF) * scale;
        var z = MathF.Sqrt(MathF.Max(0, 1 - x * x - y * y));
        return new Vector3(x, y, (v >> 30 & 1) != 0 ? -z : z);
    }

    /// <summary>A node's transform to model space, composed up its parents.</summary>
    public Matrix4x4 World(int node)
    {
        var m = Matrix4x4.Identity;
        for (var guard = 0; node >= 0 && guard < 256; guard++)
        {
            m *= Nodes[node].Local;
            node = Nodes[node].Parent;
        }
        return m;
    }
}
