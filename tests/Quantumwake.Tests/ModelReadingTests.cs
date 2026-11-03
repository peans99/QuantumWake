using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// The pieces the 3D view reads out of the install: the <c>#ivo</c> model
/// files, their material files and their split textures. Each fixture is
/// written here byte by byte in the layout the install's own files have, so a
/// change to the reader that would misplace a hull is caught without the
/// 100 GB archive.
/// </summary>
public class ModelReadingTests
{
    // --- #ivo writing ---------------------------------------------------------

    private static byte[] Ivo(params (uint Type, byte[] Body)[] chunks)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("#ivo"));
        w.Write(0x900u);
        w.Write(chunks.Length);
        w.Write(0);                        // table offset, patched below
        var offsets = new List<long>();
        foreach (var (_, body) in chunks)
        {
            offsets.Add(ms.Position);
            w.Write(body);
            while (ms.Position % 8 != 0) w.Write((byte)0);
        }
        var table = ms.Position;
        for (var i = 0; i < chunks.Length; i++)
        {
            w.Write(chunks[i].Type);
            w.Write(0x900u);
            w.Write((ulong)offsets[i]);
        }
        var bytes = ms.ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12), (int)table);
        return bytes;
    }

    private sealed record Node(string Name, int Parent, Vector3 At, bool Helper);

    private static byte[] NodeTable(params Node[] nodes)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        var names = Encoding.ASCII.GetBytes(string.Join('\0', nodes.Select(n => n.Name)) + "\0");
        w.Write(0); w.Write(nodes.Length); w.Write(nodes.Count(n => !n.Helper));
        w.Write(0); w.Write(0); w.Write(names.Length); w.Write(0); w.Write(0);
        w.Write(new byte[32]);
        foreach (var n in nodes)
        {
            w.Write(new byte[48]);                                 // world to bone
            float[] m = [1, 0, 0, n.At.X, 0, 1, 0, n.At.Y, 0, 0, 1, n.At.Z];
            foreach (var f in m) w.Write(f);                       // 3x4, translation in the last column
            w.Write(new byte[12]);
            w.Write(0u); w.Write(0u);
            w.Write((ushort)(n.Parent < 0 ? 0xFFFF : n.Parent));
            w.Write((ushort)(n.Helper ? 2 : 0));
            w.Write(new byte[24 + 16]);
            w.Write(0u); w.Write((ushort)0); w.Write((ushort)0);
            w.Write(new byte[40]);
        }
        w.Write(names);
        return ms.ToArray();
    }

    private static byte[] MaterialName(string name)
    {
        var body = new byte[128 + 4 + 32];
        Encoding.ASCII.GetBytes(name).CopyTo(body, 0);
        return body;
    }

    private sealed record Subset(int Node, int Material, int FirstIndex, int IndexCount, int FirstVertex, int VertexCount);

    /// <summary>A mesh chunk: compressed positions inside a scaling box, 16-bit indices.</summary>
    private static byte[] Mesh(Vector3 min, Vector3 max, short[][] positions, ushort[] indices, params Subset[] subsets)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(0u); w.Write(5u);
        w.Write(positions.Length); w.Write(indices.Length); w.Write(subsets.Length); w.Write(0);
        w.Write(new byte[24]);
        w.Write(min.X); w.Write(min.Y); w.Write(min.Z);
        w.Write(max.X); w.Write(max.Y); w.Write(max.Z);
        w.Write(0u);
        w.Write(new byte[92]);
        foreach (var s in subsets)
        {
            w.Write((ushort)s.Material); w.Write((ushort)s.Node);
            w.Write(s.FirstIndex); w.Write(s.IndexCount); w.Write(s.FirstVertex); w.Write(0); w.Write(s.VertexCount);
            w.Write(new byte[24]);
        }

        void Pad() { while (ms.Position % 8 != 0) w.Write((byte)0); }

        w.Write(0xEECDC168u); w.Write(2);
        foreach (var i in indices) w.Write(i);
        Pad();

        w.Write(0x91329AE9u); w.Write(16);
        foreach (var p in positions)
        {
            w.Write(p[0]); w.Write(p[1]); w.Write(p[2]); w.Write((short)0);
            w.Write(0xFFFFFFFFu);                                  // colour
            w.Write((Half)0.25f); w.Write((Half)0.75f);            // uv
        }
        Pad();
        return ms.ToArray();
    }

    /// <summary>
    /// A hull and a hardpoint on it, the mesh in its own file as the game
    /// ships it (<c>.cga</c> beside <c>.cgam</c>), and the second subset's
    /// indices starting again from zero the way sixteen-bit indices do on a
    /// big hull - the Corsair's restart at vertex 61,363.
    /// </summary>
    private static (byte[] Nodes, byte[] Mesh) Hull()
    {
        var nodes = Ivo(
            (IvoModel.MaterialChunk, MaterialName("Objects/Spaceships/Ships/TEST/test_hull")),
            (IvoModel.NodeChunk, NodeTable(
                new Node("body", -1, Vector3.Zero, false),
                new Node("nose", 0, new Vector3(0, 10, 0), false),
                new Node("hardpoint_weapon_nose", 1, new Vector3(0, 2, -1), true))));
        var mesh = Ivo((IvoModel.MeshChunk, Mesh(
            new Vector3(-2, -2, 0), new Vector3(2, 2, 4),
            [[32767, 0, -32767], [0, 32767, 0], [0, 0, 32767], [-32767, 0, 0], [0, -32767, 0], [0, 0, -32767]],
            [0, 1, 2, 0, 1, 2],
            new Subset(0, 3, 0, 3, 0, 3),
            new Subset(1, 4, 3, 3, 3, 3))));
        return (nodes, mesh);
    }

    // --- models -----------------------------------------------------------------

    [Fact]
    public void A_model_reads_its_nodes_hardpoints_and_material_file()
    {
        var (nodes, mesh) = Hull();

        var model = IvoModel.Read(nodes, mesh)!;

        Assert.Equal(["body", "nose", "hardpoint_weapon_nose"], model.Nodes.Select(n => n.Name));
        Assert.Equal([-1, 0, 1], model.Nodes.Select(n => n.Parent));
        Assert.True(model.Nodes[2].Helper);
        Assert.Equal("Objects/Spaceships/Ships/TEST/test_hull", model.MaterialFile);

        // Transforms are local and compose up the parents: the hardpoint sits
        // two metres along the nose, which sits ten metres along the body.
        var world = model.World(2);
        Assert.Equal(new Vector3(0, 12, -1), world.Translation);
    }

    /// <summary>
    /// Compressed positions are signed-normalised within the mesh's scaling
    /// box: +1 is the box's far side, -1 its near side, 0 its centre.
    /// </summary>
    [Fact]
    public void Compressed_positions_are_scaled_out_of_the_mesh_box()
    {
        var (nodes, mesh) = Hull();

        var model = IvoModel.Read(nodes, mesh)!;

        Assert.Equal(6, model.Positions.Length / 3);
        Assert.Equal(2f, model.Positions[0], 3);        // x: +1 of a 2 m half-width
        Assert.Equal(0f, model.Positions[1], 3);        // y: the centre of a box from -2 to 2
        Assert.Equal(0f, model.Positions[2], 3);        // z: -1 of a box from 0 to 4
        Assert.Equal(4f, model.Positions[8], 3);        // vertex 2's z: +1
        Assert.Equal(0.25f, model.Uvs[0], 3);
    }

    [Fact]
    public void Indices_that_restart_from_zero_are_made_absolute()
    {
        var (nodes, mesh) = Hull();

        var model = IvoModel.Read(nodes, mesh)!;

        Assert.Equal([0u, 1u, 2u, 3u, 4u, 5u], model.Indices);
        Assert.Equal([0, 1], model.Subsets.Select(s => s.Node));
        Assert.Equal([3, 4], model.Subsets.Select(s => s.Material));
    }

    [Fact]
    public void Something_that_is_not_ivo_is_not_read()
    {
        Assert.Null(IvoModel.Read(Encoding.ASCII.GetBytes("CrChxxxxxxxxxxxxxxxx"), null));
    }

    /// <summary>
    /// The skeleton layout the install's FPS weapons use: a count, 68 bytes a
    /// bone, the names last. The bones are where attachments go, by the name
    /// the gun's port gives as its helper - the P4-AR's magazine port is
    /// <c>magazine_attach</c>, its bone <c>magAttach</c>.
    /// </summary>
    [Fact]
    public void A_character_skeleton_gives_its_bones_as_attachment_points()
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(2);
        void Bone(uint id, int parent, Vector3 at)
        {
            w.Write(id); w.Write(id); w.Write(parent);
            w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f); w.Write(0f); w.Write(0f); w.Write(0f);
            w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f); w.Write(at.X); w.Write(at.Y); w.Write(at.Z);
        }
        Bone(0x16F4F95B, -1, Vector3.Zero);
        Bone(0x12345678, 0, new Vector3(0, 0.173f, -0.011f));
        w.Write(Encoding.ASCII.GetBytes("root\0magAttach\0"));
        var chr = Ivo((IvoModel.BonesChunk, ms.ToArray()));

        var model = IvoModel.Read(chr, null)!;

        Assert.True(model.Skeleton);
        Assert.Equal(["root", "magAttach"], model.Nodes.Select(n => n.Name));
        Assert.Equal(0.173f, model.Nodes[1].Local.Translation.Y, 3);
        Assert.All(model.Nodes, n => Assert.True(n.Helper));
    }

    [Theory]
    [InlineData("body_damage", true)]
    [InlineData("nacelle_left_mesh_damage", true)]
    [InlineData("Physics_proxy", true)]
    [InlineData("hull_destroyed", true)]
    [InlineData("nose", false)]
    [InlineData("hardpoint_weapon_nose", false)]
    public void Damaged_and_physics_nodes_are_left_out_of_the_view(string node, bool hidden)
    {
        Assert.Equal(hidden, ModelMesh.Hidden(node));
    }

    // --- textures ---------------------------------------------------------------

    private static byte[] Dds(int size, int mips, string fourCc)
    {
        var levels = Enumerable.Range(0, mips).Select(i => ModelArchive.LevelBytes(size >> i, size >> i, 8)).ToArray();
        var dds = new byte[128 + levels.Sum()];
        Encoding.ASCII.GetBytes("DDS ").CopyTo(dds, 0);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(4), 124);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(12), size);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(16), size);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(28), mips);
        Encoding.ASCII.GetBytes(fourCc).CopyTo(dds, 84);
        var at = 128;
        for (var i = 0; i < mips; i++)
        {
            dds.AsSpan(at, (int)levels[i]).Fill((byte)(i + 1));
            at += (int)levels[i];
        }
        return dds;
    }

    /// <summary>
    /// A 4096 BC7 is 16 MB of video memory and a hull names a hundred
    /// textures, so the largest levels are dropped down to the asked size and
    /// the header is rewritten to match.
    /// </summary>
    [Fact]
    public void A_texture_is_trimmed_to_the_largest_level_asked_for()
    {
        var dds = Dds(16, 5, "DXT1");

        var trimmed = ModelArchive.Trim(dds, 4);

        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(trimmed.AsSpan(16)));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(trimmed.AsSpan(12)));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(trimmed.AsSpan(28)));
        Assert.Equal(3, trimmed[128]);                  // the third level is now the first
        Assert.Equal(128 + 8 + 8 + 8, trimmed.Length);
    }

    [Fact]
    public void A_texture_already_small_enough_is_left_alone()
    {
        var dds = Dds(8, 4, "DXT1");

        Assert.Same(dds, ModelArchive.Trim(dds, 2048));
    }

    [Theory]
    [InlineData("DXT1", 0, 8)]
    [InlineData("DXT5", 0, 16)]
    [InlineData("DX10", 80, 8)]     // BC4: the gloss maps
    [InlineData("DX10", 84, 16)]    // BC5 signed: the normal maps
    [InlineData("DX10", 98, 16)]    // BC7
    [InlineData("DX10", 28, 0)]     // plain RGBA: not cut here
    public void Block_sizes_follow_the_format(string fourCc, int dxgi, int bytes)
    {
        var dds = new byte[148];
        Encoding.ASCII.GetBytes("DDS ").CopyTo(dds, 0);
        Encoding.ASCII.GetBytes(fourCc).CopyTo(dds, 84);
        BinaryPrimitives.WriteInt32LittleEndian(dds.AsSpan(128), dxgi);

        Assert.Equal(bytes, ModelArchive.BlockBytes(dds));
    }

    [Theory]
    [InlineData("Objects/Spaceships/Ships/DRAK/Corsair/exterior/DRAK_Corsair.cga", @"Data\Objects\Spaceships\Ships\DRAK\Corsair\exterior\DRAK_Corsair.cga")]
    [InlineData(@"Data\Textures\x.dds", @"Data\Textures\x.dds")]
    [InlineData("/Materials/vehicles/a.mtl", @"Data\Materials\vehicles\a.mtl")]
    public void Paths_as_records_write_them_become_archive_paths(string written, string archive)
    {
        Assert.Equal(archive, ModelArchive.Normalise(written));
    }

    // --- materials --------------------------------------------------------------

    private const string HullMaterial = """
        <Material MtlFlags="256">
          <SubMaterials>
            <Material Name="Physics_proxy" Shader="NoDraw" Diffuse="1,0,0" />
            <Material Name="Ext_Panel_Paint_Secondary" Shader="HardSurface" StringGenMask="%WEAR_LAYER" Diffuse="1,1,1" Specular="1,1,1" Shininess="255">
              <Textures />
              <MatLayers>
                <Layer Name="Primary" Path="Materials/vehicles/layers/painted_panel.mtl" TintColor="1,1,1" GlossMult="0.8" UVTiling="2" PaletteTint="2" />
                <Layer Name="Wear" Path="Materials/vehicles/layers/missing.mtl" TintColor="1,1,1" PaletteTint="0" />
              </MatLayers>
            </Material>
            <Material Name="Ext_Glass_Canopy" Shader="GlassPBR" StringGenMask="%DIRT%USE_TINT_PALETTE" Diffuse="0.7,0.68,0.58" Opacity="1">
              <Textures>
                <Texture Map="TexSlot6" File="Textures/vehicles/tileables/glass/glass_canopy_gloss.tif" />
                <Texture Map="TexSlot9" File="$TintPaletteDecal" />
              </Textures>
            </Material>
            <Material Name="Ext_Decal_Diffuse_A" Shader="MeshDecal" Diffuse="1,1,1" AlphaTest="0.2">
              <Textures>
                <Texture Map="TexSlot1" File="Textures/decals/drak_decals_diff.tif" />
              </Textures>
            </Material>
          </SubMaterials>
        </Material>
        """;

    private const string PaintedPanel = """
        <Material Shader="Layer" Diffuse="0.9,0.9,0.9" Specular="0.04,0.04,0.04" Shininess="204">
          <Textures>
            <Texture Map="TexSlot1" File="Textures/vehicles/panels/ship_lf_panel_a_base_diff.tif" />
            <Texture Map="TexSlot2" File="Textures/vehicles/panels/ship_lf_panel_a_base_ddna.tif" />
          </Textures>
          <PublicParams LayerTiling="1.5" />
        </Material>
        """;

    private static List<ModelSubMaterial> Hullmaterials() =>
        ModelMaterials.Read(Encoding.UTF8.GetBytes(HullMaterial), path =>
            path.EndsWith("painted_panel.mtl", StringComparison.Ordinal) ? Encoding.UTF8.GetBytes(PaintedPanel) : null);

    [Fact]
    public void A_material_file_is_read_in_the_order_the_triangles_index_it()
    {
        var subs = Hullmaterials();

        Assert.Equal(["Physics_proxy", "Ext_Panel_Paint_Secondary", "Ext_Glass_Canopy", "Ext_Decal_Diffuse_A"], subs.Select(s => s.Name));
        Assert.True(subs[0].Hidden);
        Assert.False(subs[1].Hidden);
    }

    /// <summary>
    /// How a paint recolours a hull: the hull's layer names a palette entry -
    /// B here - and carries the layer file's tileable textures, which are
    /// resolved so the page loads them directly.
    /// </summary>
    [Fact]
    public void A_hull_layer_says_which_palette_colour_it_takes_and_brings_its_textures()
    {
        var layer = Hullmaterials()[1].Layers[0];

        Assert.Equal(2, layer.PaletteTint);
        Assert.Equal(0.8f, layer.GlossMult, 3);
        Assert.Equal(3f, layer.Tiling, 3);              // the sub-material's 2 times the layer's 1.5
        Assert.Equal("Textures/vehicles/panels/ship_lf_panel_a_base_diff.tif", layer.Diffuse);
        Assert.Equal("Textures/vehicles/panels/ship_lf_panel_a_base_ddna.tif", layer.Normal);
        Assert.Equal(0.8f, layer.Gloss, 2);             // 204 of 255
    }

    [Fact]
    public void A_layer_file_the_archive_lacks_still_leaves_the_layer()
    {
        var wear = Hullmaterials()[1].Layers[1];

        Assert.Equal("Wear", wear.Name);
        Assert.Null(wear.Diffuse);
        Assert.Equal(0, wear.PaletteTint);
    }

    [Fact]
    public void Glass_says_whether_the_palette_tints_it_and_engine_placeholders_are_not_files()
    {
        var glass = Hullmaterials()[2];

        Assert.True(glass.Glass);
        Assert.True(glass.TintsGlass);
        Assert.True(glass.Textures.ContainsKey("TexSlot6"));
        Assert.False(glass.Textures.ContainsKey("TexSlot9"));
    }

    [Fact]
    public void A_decal_is_known_for_one()
    {
        var decal = Hullmaterials()[3];

        Assert.True(decal.Decal);
        Assert.Equal(0.2f, decal.AlphaTest, 3);
        Assert.Equal("Textures/decals/drak_decals_diff.tif", decal.Textures["TexSlot1"]);
    }
}
