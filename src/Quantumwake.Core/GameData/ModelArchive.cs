using System.Buffers.Binary;

namespace Quantumwake.Core.GameData;

/// <summary>
/// The part of <c>Data.p4k</c> a 3D view reads from - ship, vehicle and FPS
/// weapon models, materials and textures - indexed once so a model's hundred
/// or so files are found without a directory walk each.
/// </summary>
/// <remarks>
/// <para>
/// The archive holds 1.36 million entries and one walk of its directory takes
/// 0.6-1.4 s on this machine. A Corsair with its turrets, guns, thrusters and
/// their materials names about 30 folders and 150 textures, so walking per
/// lookup is the difference between opening at once and opening in minutes.
/// </para>
/// <para>
/// Only the folders a view can need are kept: <c>Objects\Spaceships</c>
/// (296k entries), <c>Objects\Vehicles</c>, <c>Objects\fps_weapons</c>,
/// <c>Materials</c> and <c>Textures</c>, without the lower levels of detail
/// nobody asks for. Entries are keyed by a 64-bit hash of the folded path, not
/// the path itself, which keeps the index to a few megabytes.
/// </para>
/// </remarks>
public sealed class ModelArchive
{
    private readonly P4kArchive _archive;
    private readonly Dictionary<ulong, P4kEntry> _entries;

    private static readonly string[] Folders =
    [
        @"data\objects\spaceships\", @"data\objects\vehicles\", @"data\objects\fps_weapons\",
        @"data\materials\", @"data\textures\",
    ];

    private ModelArchive(P4kArchive archive, Dictionary<ulong, P4kEntry> entries)
    {
        _archive = archive;
        _entries = entries;
    }

    public int Count => _entries.Count;

    /// <summary>Walks the archive once and keeps what a model view can use.</summary>
    public static ModelArchive Open(string archivePath)
    {
        var archive = new P4kArchive(archivePath);
        var entries = new Dictionary<ulong, P4kEntry>();
        foreach (var entry in archive.Locate(Wanted))
            entries.TryAdd(Key(entry.Path), entry);
        return new ModelArchive(archive, entries);
    }

    private static bool Wanted(string name)
    {
        if (name.Length < 12) return false;
        var inFolder = false;
        foreach (var folder in Folders)
        {
            if (name.Length > folder.Length && Folded(name, folder))
            {
                inFolder = true;
                break;
            }
        }
        if (!inFolder) return false;

        // Lower levels of detail: _lod1 to _lod5 beside every model, a third of
        // the ship folder and nothing a close view draws.
        var lod = name.IndexOf("_lod", StringComparison.OrdinalIgnoreCase);
        return lod < 0 || lod + 5 >= name.Length || !char.IsAsciiDigit(name[lod + 4]);
    }

    private static bool Folded(string name, string folder)
    {
        for (var i = 0; i < folder.Length; i++)
        {
            var c = name[i] == '/' ? '\\' : char.ToLowerInvariant(name[i]);
            if (c != folder[i]) return false;
        }
        return true;
    }

    /// <summary>
    /// The archive path for a path as the game writes it - with or without
    /// <c>Data/</c>, either slash, any case.
    /// </summary>
    public static string Normalise(string path)
    {
        var p = path.Replace('/', '\\').TrimStart('\\');
        return p.StartsWith(@"data\", StringComparison.OrdinalIgnoreCase) ? p : @"Data\" + p;
    }

    private static ulong Key(string path)
    {
        var p = Normalise(path);
        var hash = 14695981039346656037UL;
        foreach (var ch in p)
        {
            var c = ch == '/' ? '\\' : char.ToLowerInvariant(ch);
            hash ^= c;
            hash *= 1099511628211UL;
        }
        return hash;
    }

    public bool Exists(string path) => _entries.ContainsKey(Key(path));

    /// <summary>The decompressed bytes, or null when absent or unreadable.</summary>
    public byte[]? TryRead(string path) =>
        _entries.TryGetValue(Key(path), out var entry) ? _archive.TryRead(entry) : null;

    /// <summary>
    /// A texture as one DDS file the GPU can take whole, its largest levels
    /// trimmed to <paramref name="maxSize"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Materials name textures as <c>.tif</c>; the archive holds <c>.dds</c>.
    /// Larger textures are split: the <c>.dds</c> is the header and the
    /// smallest levels, and each larger level sits alone in <c>.dds.1</c>,
    /// <c>.dds.2</c> and so on up to the full size - on the Corsair's painted
    /// panel, a 484-byte <c>_ddna.dds</c> and seven parts up to 4 MB. Glued
    /// back largest first behind the header, they make an ordinary file.
    /// </para>
    /// <para>
    /// A normal map's gloss is a second texture beside it, <c>.dds.a</c>, whose
    /// parts are <c>.dds.1a</c> and up. Pass <paramref name="gloss"/> for that one.
    /// </para>
    /// <para>
    /// The pixels are left compressed - BC1, BC3, BC4, BC5 and BC7 all upload
    /// as they are to a desktop GPU - so nothing is decoded here at all.
    /// </para>
    /// </remarks>
    public byte[]? TryReadTexture(string path, int maxSize = 2048, bool gloss = false)
    {
        var stem = Normalise(path);
        var dot = stem.LastIndexOf('.');
        if (dot > stem.LastIndexOf('\\')) stem = stem[..dot];
        var basePath = stem + (gloss ? ".dds.a" : ".dds");

        var head = TryRead(basePath);
        // The gloss file is a DDS without its four-byte magic: it opens on the
        // header's own size field, 124. Put the magic back and it is ordinary.
        if (head is { Length: >= 124 } && head[0] == 124 && head[1] == 0 && head[2] == 0 && head[3] == 0)
            head = [(byte)'D', (byte)'D', (byte)'S', (byte)' ', .. head];
        if (head is null || head.Length < 128 || head[0] != 'D' || head[1] != 'D' || head[2] != 'S')
            return null;

        var headerLength = HeaderLength(head);
        if (head.Length < headerLength) return null;

        var parts = new List<byte[]>();
        for (var n = 1; n < 16; n++)
        {
            var part = TryRead(stem + (gloss ? $".dds.{n}a" : $".dds.{n}"));
            if (part is null) break;
            parts.Add(part);
        }

        var total = headerLength + parts.Sum(p => p.Length) + (head.Length - headerLength);
        var whole = new byte[total];
        head.AsSpan(0, headerLength).CopyTo(whole);
        var at = headerLength;
        for (var i = parts.Count - 1; i >= 0; i--)
        {
            parts[i].CopyTo(whole, at);
            at += parts[i].Length;
        }
        head.AsSpan(headerLength).CopyTo(whole.AsSpan(at));

        return Trim(whole, maxSize);
    }

    private static int HeaderLength(byte[] dds) =>
        dds.Length >= 148 && dds[84] == 'D' && dds[85] == 'X' && dds[86] == '1' && dds[87] == '0' ? 148 : 128;

    /// <summary>Bytes a 4x4 block takes, or 0 for a format this does not cut.</summary>
    public static int BlockBytes(byte[] dds)
    {
        var fourCc = System.Text.Encoding.ASCII.GetString(dds, 84, 4);
        if (fourCc == "DX10")
        {
            var dxgi = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(128));
            return dxgi switch
            {
                70 or 71 or 72 or 79 or 80 or 81 => 8,                        // BC1, BC4
                73 or 74 or 75 or 76 or 77 or 78 or 82 or 83 or 84
                    or 94 or 95 or 96 or 97 or 98 or 99 => 16,                // BC2, BC3, BC5, BC6H, BC7
                _ => 0,
            };
        }
        return fourCc switch
        {
            "DXT1" or "ATI1" or "BC4U" or "BC4S" => 8,
            "DXT3" or "DXT5" or "ATI2" or "BC5U" or "BC5S" => 16,
            _ => 0,
        };
    }

    /// <summary>
    /// Drops the levels wider than <paramref name="maxSize"/>. A 4096 BC7 is
    /// 16 MB of video memory; a ship names a hundred textures.
    /// </summary>
    internal static byte[] Trim(byte[] dds, int maxSize)
    {
        var width = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(16));
        var height = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(12));
        var mips = Math.Max(1, BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(28)));
        var block = BlockBytes(dds);
        if (block == 0 || maxSize <= 0) return dds;

        var drop = 0;
        var skip = 0L;
        while (drop < mips - 1 && Math.Max(width >> drop, height >> drop) > maxSize)
        {
            skip += LevelBytes(width >> drop, height >> drop, block);
            drop++;
        }
        if (drop == 0) return dds;

        var header = HeaderLength(dds);
        if (header + skip > dds.Length) return dds;

        var trimmed = new byte[dds.Length - skip];
        dds.AsSpan(0, header).CopyTo(trimmed);
        dds.AsSpan(header + (int)skip).CopyTo(trimmed.AsSpan(header));
        var w = Math.Max(1, width >> drop);
        var h = Math.Max(1, height >> drop);
        BinaryPrimitives.WriteInt32LittleEndian(trimmed.AsSpan(12), h);
        BinaryPrimitives.WriteInt32LittleEndian(trimmed.AsSpan(16), w);
        BinaryPrimitives.WriteInt32LittleEndian(trimmed.AsSpan(20), (int)LevelBytes(w, h, block));
        BinaryPrimitives.WriteInt32LittleEndian(trimmed.AsSpan(28), mips - drop);
        return trimmed;
    }

    internal static long LevelBytes(int width, int height, int block) =>
        (long)Math.Max(1, (Math.Max(1, width) + 3) / 4) * Math.Max(1, (Math.Max(1, height) + 3) / 4) * block;
}
