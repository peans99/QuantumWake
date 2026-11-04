namespace Quantumwake.WebTests;

/// <summary>
/// The 3D viewer's own module: the formats it unpacks and the rule it sorts
/// a hull's inside from its outside by.
/// </summary>
/// <remarks>
/// Each buffer here is built the way the server writes it (<c>ModelMesh.cs</c>
/// for the mesh, a DDS header as Microsoft lays it out for the textures), so
/// a change on either side that the other does not follow shows up as a
/// number, not as a ship drawn blank.
/// </remarks>
public sealed class ViewerModuleTests
{
    private readonly Viewer _viewer = new();

    /// <summary>
    /// Builders for the two byte formats, kept in the page so a test reads as
    /// a spec: so many vertices, this fourCC, these mips.
    /// </summary>
    private const string Builders = """
        /* QWM1: magic, 32-bit header length, JSON header padded to four bytes,
           then positions (3 floats), normals (4 bytes), uvs (2 floats), 32-bit
           indices - the layout ModelMesh.cs documents. */
        globalThis.qwm = (header, positions, uvs, indices) => {
          const json = JSON.stringify(header);
          const bytes = Array.from(json, c => c.charCodeAt(0));
          const padded = bytes.length + (4 - bytes.length % 4) % 4;
          const n = header.vertices;
          const buf = new ArrayBuffer(8 + padded + n * 12 + n * 4 + n * 8 + indices.length * 4);
          const view = new DataView(buf);
          [0x51, 0x57, 0x4d, 0x31].forEach((b, i) => view.setUint8(i, b));
          view.setUint32(4, padded, true);
          bytes.forEach((b, i) => view.setUint8(8 + i, b));
          let at = 8 + padded;
          positions.forEach((v, i) => view.setFloat32(at + i * 4, v, true)); at += n * 12;
          at += n * 4;
          uvs.forEach((v, i) => view.setFloat32(at + i * 4, v, true)); at += n * 8;
          indices.forEach((v, i) => view.setUint32(at + i * 4, v, true));
          return buf;
        };

        /* A DDS: 'DDS ' magic, height at 12, width at 16, mip count at 28, the
           pixel format's fourCC at 84; a DX10 header adds the DXGI format at
           128 and the data starts at 148. The payload is zeros of the size
           the block format says. */
        globalThis.dds = ({ width, height, mips = 1, fourCC, dxgi = null, block = 8, bytes = null }) => {
          const headerLength = dxgi === null ? 128 : 148;
          let size = 0, w = width, h = height;
          for (let i = 0; i < mips; i++) {
            size += Math.max(1, (w + 3) >> 2) * Math.max(1, (h + 3) >> 2) * block;
            w = Math.max(1, w >> 1); h = Math.max(1, h >> 1);
          }
          const buf = new ArrayBuffer(headerLength + (bytes === null ? size : bytes));
          const view = new DataView(buf);
          view.setUint32(0, 0x20534444, true);
          view.setUint32(12, height, true);
          view.setUint32(16, width, true);
          view.setUint32(28, mips, true);
          Array.from(fourCC, (c, i) => view.setUint8(84 + i, c.charCodeAt(0)));
          if (dxgi !== null) view.setUint32(128, dxgi, true);
          return buf;
        };
        """;

    public ViewerModuleTests() => _viewer.Do(Builders);

    [Fact]
    public void The_module_announces_itself_the_way_the_dashboard_waits_for()
    {
        // app.js adds the script tag and resolves on qw3d-ready, then calls
        // window.QW3D.openModel: both halves have to be there.
        Assert.Equal("function", _viewer.Text("typeof window.QW3D.openModel"));
        Assert.Equal("function", _viewer.Text("typeof window.QW3D.closeModel"));
        Assert.Contains("qw3d-ready", (object[])_viewer.Eval("__events")!);
    }

    [Fact]
    public void A_packed_mesh_unpacks_to_its_header_and_four_buffers()
    {
        _viewer.Do("""
            const header = { vertices: 2, indices: 3, materials: ['objects/a.mtl'], nodes: [{ n: 'root', p: -1, m: [], h: false }], subsets: [] };
            globalThis.mesh = __viewer.parseMesh(qwm(header, [1, 2, 3, 4, 5, 6], [0.5, 0.25, 0.75, 1], [0, 1, 0]));
            """);

        Assert.Equal(2, _viewer.Number("mesh.header.vertices"));
        Assert.Equal("objects/a.mtl", _viewer.Text("mesh.header.materials[0]"));
        Assert.Equal("root", _viewer.Text("mesh.header.nodes[0].n"));

        // The second vertex's x, the second uv's u, and the index run.
        Assert.Equal(4, _viewer.Number("mesh.positions[3]"));
        Assert.Equal(6, _viewer.Number("mesh.positions.length"));
        Assert.Equal(8, _viewer.Number("mesh.normals.length"));
        Assert.Equal(0.75, _viewer.Number("mesh.uvs[2]"));
        Assert.Equal(3, _viewer.Number("mesh.indices.length"));
        Assert.Equal(1, _viewer.Number("mesh.indices[1]"));
    }

    [Fact]
    public void A_buffer_that_is_not_a_packed_mesh_is_refused_not_misread()
    {
        _viewer.Do("""
            const wrong = qwm({ vertices: 1, indices: 0, materials: [], nodes: [], subsets: [] }, [0, 0, 0], [0, 0], []);
            new DataView(wrong).setUint8(3, 0x32); // QWM2
            globalThis.refused = __viewer.parseMesh(wrong);
            """);

        Assert.True(_viewer.Truth("refused === null"));
    }

    [Fact]
    public void A_legacy_DXT1_texture_reads_its_mip_chain()
    {
        // 8 × 8 with two mips: a 2 × 2 block level of 32 bytes, then one block of 8.
        _viewer.Do("globalThis.tex = __viewer.parseDds(dds({ width: 8, height: 8, mips: 2, fourCC: 'DXT1', block: 8 }), { s3tc: true });");

        Assert.Equal("dxt1", _viewer.Text("tex.kind"));
        Assert.Equal(33777, _viewer.Number("tex.format"));
        Assert.False(_viewer.Truth("tex.srgb"));
        Assert.Equal(2, _viewer.Number("tex.mipmaps.length"));
        Assert.Equal(32, _viewer.Number("tex.mipmaps[0].data.length"));
        Assert.Equal(8, _viewer.Number("tex.mipmaps[0].width"));
        Assert.Equal(4, _viewer.Number("tex.mipmaps[1].width"));
        Assert.Equal(8, _viewer.Number("tex.mipmaps[1].data.length"));
    }

    [Fact]
    public void A_DX10_BC7_texture_reads_as_sRGB_from_the_DXGI_format()
    {
        _viewer.Do("globalThis.bc7 = __viewer.parseDds(dds({ width: 4, height: 4, fourCC: 'DX10', dxgi: 99, block: 16 }), { bptc: true });");

        Assert.Equal("bc7", _viewer.Text("bc7.kind"));
        Assert.Equal(36492, _viewer.Number("bc7.format"));
        Assert.True(_viewer.Truth("bc7.srgb"));
        Assert.Equal(16, _viewer.Number("bc7.mipmaps[0].data.length"));
    }

    [Fact]
    public void A_signed_BC5_normal_map_is_told_from_the_unsigned_one()
    {
        _viewer.Do("""
            globalThis.signed = __viewer.parseDds(dds({ width: 4, height: 4, fourCC: 'DX10', dxgi: 84, block: 16 }), { rgtc: true });
            globalThis.unsigned = __viewer.parseDds(dds({ width: 4, height: 4, fourCC: 'ATI2', block: 16 }), { rgtc: true });
            """);

        Assert.Equal("bc5", _viewer.Text("signed.kind"));
        Assert.True(_viewer.Truth("signed.signed"));
        Assert.Equal(36286, _viewer.Number("signed.format"));
        Assert.Equal("bc5", _viewer.Text("unsigned.kind"));
        Assert.False(_viewer.Truth("unsigned.signed"));
        Assert.Equal(36285, _viewer.Number("unsigned.format"));
    }

    [Fact]
    public void A_texture_the_GPU_cannot_take_is_null_rather_than_uploaded()
    {
        _viewer.Do("""
            globalThis.noCaps = __viewer.parseDds(dds({ width: 4, height: 4, fourCC: 'DXT1', block: 8 }), { s3tc: false });
            globalThis.unknownDxgi = __viewer.parseDds(dds({ width: 4, height: 4, fourCC: 'DX10', dxgi: 28, block: 16 }), { s3tc: true, rgtc: true, bptc: true });
            globalThis.notDds = __viewer.parseDds(new ArrayBuffer(256), { s3tc: true });
            """);

        Assert.True(_viewer.Truth("noCaps === null"));
        Assert.True(_viewer.Truth("unknownDxgi === null"));
        Assert.True(_viewer.Truth("notDds === null"));
    }

    [Fact]
    public void A_truncated_texture_keeps_the_mips_it_has()
    {
        // Two mips declared, bytes for one: the chain stops rather than reading past the end.
        _viewer.Do("globalThis.cut = __viewer.parseDds(dds({ width: 8, height: 8, mips: 2, fourCC: 'DXT5', block: 16, bytes: 64 }), { s3tc: true });");

        Assert.Equal("dxt5", _viewer.Text("cut.kind"));
        Assert.Equal(1, _viewer.Number("cut.mipmaps.length"));
    }

    [Theory]
    [InlineData("int_metal_c", true)]
    [InlineData("Int_Decal_POM_A", true)]
    [InlineData("Paint_DarkGrey_INT", true)]
    [InlineData("Glass_INT", true)]
    [InlineData("internal_frame_a", true)]
    [InlineData("Damage_Internals_Piping", true)]
    [InlineData("Hull_Interior_Trim", true)]
    [InlineData("Paint_Hull_A", false)]
    [InlineData("Glass_Ext", false)]
    [InlineData("Paint_Integral", false)]
    [InlineData("Printed_Metal", false)]
    [InlineData("", false)]
    public void The_inside_of_a_hull_is_known_by_each_studios_naming(string subMaterial, bool inside)
    {
        // The Interior box switches these; matching Int_ alone left it with
        // nothing to switch on an Avenger or a Cutlass.
        Assert.Equal(inside, _viewer.Truth($"__viewer.interiorSub({System.Text.Json.JsonSerializer.Serialize(subMaterial)})"));
    }

    [Fact]
    public void A_missing_sub_material_name_is_outside()
    {
        Assert.False(_viewer.Truth("__viewer.interiorSub(null)"));
        Assert.False(_viewer.Truth("__viewer.interiorSub(undefined)"));
    }
}
