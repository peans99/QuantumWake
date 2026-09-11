using System.Buffers.Binary;
using System.IO.Compression;

namespace Quantumwake.Core.GameData;

/// <summary>A vehicle's silhouette, as a PNG the page can draw, and its extent.</summary>
/// <param name="Png">White on transparent, cropped to the silhouette.</param>
/// <param name="Width">Pixels across the cropped image - nose to tail, since the icons face right.</param>
/// <param name="Height">Pixels top to bottom - the beam.</param>
public sealed record VehicleIcon(byte[] Png, int Width, int Height);

/// <summary>
/// Turns the game's vehicle icons into something a browser can show.
/// </summary>
/// <remarks>
/// <para>
/// The icons are 1024-square DDS files compressed as BC3 (DXT5) - the header
/// says <c>DXT5</c> and the size is one byte a pixel, which is what BC3 comes
/// to. BC3 is a small, fixed decoder: sixteen bytes a block, eight for the
/// alpha ramp and eight for two RGB565 colours and their mixes. There is no
/// library for it here because it is sixty lines and the dependency would be
/// larger than the format.
/// </para>
/// <para>
/// The silhouette is cropped to its opaque bounds before encoding. Every icon
/// sits differently inside its square - the Corsair spans 595 of 1024 pixels,
/// a Pisces far fewer - so the square is no use for drawing to scale; the
/// cropped width is the ship's length and the page can size it in metres.
/// </para>
/// <para>
/// PNG is written by hand too: one IHDR, one zlib-deflated IDAT with filter
/// type zero on every row, one IEND. <see cref="ZLibStream"/> does the
/// deflate; the CRC table is the one every PNG writer carries.
/// </para>
/// </remarks>
public static class VehicleIcons
{
    private const int DdsHeader = 128;

    /// <summary>Decodes and crops one icon, or null when the bytes are not the BC3 square expected.</summary>
    public static VehicleIcon? Convert(byte[] dds)
    {
        if (dds.Length < DdsHeader || dds[0] != (byte)'D' || dds[1] != (byte)'D' || dds[2] != (byte)'S')
            return null;

        var height = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(12));
        var width = BinaryPrimitives.ReadInt32LittleEndian(dds.AsSpan(16));
        var fourCc = System.Text.Encoding.ASCII.GetString(dds, 84, 4);

        if (fourCc != "DXT5" || width <= 0 || height <= 0 || width % 4 != 0 || height % 4 != 0)
            return null;
        if (dds.Length < DdsHeader + width * height)
            return null;

        var rgba = DecodeBc3(dds, DdsHeader, width, height);

        // Opaque bounds. A threshold rather than zero: BC3's alpha ramp leaves
        // faint haloes around the shape that would pad every crop by a pixel.
        int minX = width, minY = height, maxX = -1, maxY = -1;
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                if (rgba[(y * width + x) * 4 + 3] < 32) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }

        if (maxX < 0) return null;

        var cropW = maxX - minX + 1;
        var cropH = maxY - minY + 1;
        var cropped = new byte[cropW * cropH * 4];

        for (var y = 0; y < cropH; y++)
            Buffer.BlockCopy(rgba, ((minY + y) * width + minX) * 4, cropped, y * cropW * 4, cropW * 4);

        return new VehicleIcon(EncodePng(cropped, cropW, cropH), cropW, cropH);
    }

    /// <summary>BC3: a 4x4 block is an 8-byte alpha ramp then an 8-byte BC1 colour block.</summary>
    internal static byte[] DecodeBc3(byte[] data, int at, int width, int height)
    {
        var rgba = new byte[width * height * 4];
        Span<byte> alpha = stackalloc byte[8];
        Span<(byte R, byte G, byte B)> colour = stackalloc (byte, byte, byte)[4];

        for (var by = 0; by < height / 4; by++)
            for (var bx = 0; bx < width / 4; bx++)
            {
                var a0 = data[at];
                var a1 = data[at + 1];
                ulong alphaBits = 0;
                for (var i = 0; i < 6; i++) alphaBits |= (ulong)data[at + 2 + i] << (8 * i);

                alpha[0] = a0;
                alpha[1] = a1;
                if (a0 > a1)
                {
                    for (var i = 1; i <= 6; i++) alpha[i + 1] = (byte)(((7 - i) * a0 + i * a1) / 7);
                }
                else
                {
                    for (var i = 1; i <= 4; i++) alpha[i + 1] = (byte)(((5 - i) * a0 + i * a1) / 5);
                    alpha[6] = 0;
                    alpha[7] = 255;
                }

                var c0 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at + 8));
                var c1 = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(at + 10));
                var colourBits = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(at + 12));

                colour[0] = Rgb565(c0);
                colour[1] = Rgb565(c1);
                colour[2] = ((byte)((2 * colour[0].R + colour[1].R) / 3), (byte)((2 * colour[0].G + colour[1].G) / 3), (byte)((2 * colour[0].B + colour[1].B) / 3));
                colour[3] = ((byte)((colour[0].R + 2 * colour[1].R) / 3), (byte)((colour[0].G + 2 * colour[1].G) / 3), (byte)((colour[0].B + 2 * colour[1].B) / 3));

                for (var py = 0; py < 4; py++)
                    for (var px = 0; px < 4; px++)
                    {
                        var i = py * 4 + px;
                        var (r, g, b) = colour[(int)((colourBits >> (2 * i)) & 3)];
                        var o = ((by * 4 + py) * width + bx * 4 + px) * 4;
                        rgba[o] = r;
                        rgba[o + 1] = g;
                        rgba[o + 2] = b;
                        rgba[o + 3] = alpha[(int)((alphaBits >> (3 * i)) & 7)];
                    }

                at += 16;
            }

        return rgba;
    }

    private static (byte, byte, byte) Rgb565(ushort c) =>
        ((byte)(((c >> 11) & 31) * 255 / 31), (byte)(((c >> 5) & 63) * 255 / 63), (byte)((c & 31) * 255 / 31));

    /// <summary>An 8-bit RGBA PNG, filter 0 on every row.</summary>
    internal static byte[] EncodePng(byte[] rgba, int width, int height)
    {
        var raw = new byte[height * (width * 4 + 1)];
        for (var y = 0; y < height; y++)
        {
            raw[y * (width * 4 + 1)] = 0;
            Buffer.BlockCopy(rgba, y * width * 4, raw, y * (width * 4 + 1) + 1, width * 4);
        }

        byte[] deflated;
        using (var zipped = new MemoryStream())
        {
            using (var zlib = new ZLibStream(zipped, CompressionLevel.Optimal, leaveOpen: true))
                zlib.Write(raw);
            deflated = zipped.ToArray();
        }

        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

        Span<byte> ihdr = stackalloc byte[13];
        BinaryPrimitives.WriteInt32BigEndian(ihdr, width);
        BinaryPrimitives.WriteInt32BigEndian(ihdr[4..], height);
        ihdr[8] = 8;   // bit depth
        ihdr[9] = 6;   // colour type: RGBA
        ihdr[10] = 0;  // compression
        ihdr[11] = 0;  // filter
        ihdr[12] = 0;  // interlace
        Chunk(png, "IHDR", ihdr);
        Chunk(png, "IDAT", deflated);
        Chunk(png, "IEND", []);

        return png.ToArray();
    }

    private static void Chunk(Stream png, string type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        png.Write(length);

        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        png.Write(typeBytes);
        png.Write(data);

        var crc = Crc32(typeBytes, 0xFFFFFFFF);
        crc = Crc32(data, crc);
        Span<byte> crcBytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(crcBytes, crc ^ 0xFFFFFFFF);
        png.Write(crcBytes);
    }

    private static readonly uint[] CrcTable = BuildCrcTable();

    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint n = 0; n < 256; n++)
        {
            var c = n;
            for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320 ^ (c >> 1) : c >> 1;
            table[n] = c;
        }
        return table;
    }

    private static uint Crc32(ReadOnlySpan<byte> data, uint crc)
    {
        foreach (var b in data) crc = CrcTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
        return crc;
    }
}
