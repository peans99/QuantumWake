using System.Text;
using ZstdSharp;

namespace SCCompanion.Core.GameData;

/// <summary>
/// Reads single files out of Star Citizen's <c>Data.p4k</c>.
/// </summary>
/// <remarks>
/// <para>
/// The archive is a ZIP64 container - roughly 150 GB and 1.36 million entries on
/// a current install - whose central directory is entirely standard. What breaks
/// ordinary ZIP readers is the payload codec: entries use ZStd (method 100),
/// which <c>System.IO.Compression</c> refuses, so the directory is walked by hand
/// and the compressed bytes handed to a managed ZStd decoder.
/// </para>
/// <para>
/// The ZStd-under-method-100 detail is community knowledge, established by the
/// reverse-engineering behind scdatatools (https://github.com/ventorvar/scdatatools)
/// and the unp4ck tools before it; the reader below is our own. See
/// docs/credits.md.
/// </para>
/// <para>
/// No third-party extraction tool is involved, and nothing is unpacked to disk:
/// one entry is located and decompressed in memory. The file is opened read-only
/// and shared, so it is safe to do while the game is running.
/// </para>
/// <para>
/// Some entries are encrypted. Those are reported rather than guessed at - see
/// <see cref="TryRead"/>.
/// </para>
/// </remarks>
public sealed class P4kArchive
{
    private const uint LocalHeaderSignature = 0x04034B50;
    private const uint CentralHeaderSignature = 0x02014B50;
    private const uint EndOfCentralDirectory = 0x06054B50;
    private const uint Zip64Locator = 0x07064B50;
    private const uint Zip64EndOfCentralDirectory = 0x06064B50;

    private readonly string _path;

    public P4kArchive(string path) => _path = path;

    /// <summary>Standard location of the archive within a channel install.</summary>
    public static string PathFor(string installRoot) => Path.Combine(installRoot, "Data.p4k");

    public static bool Exists(string installRoot) => File.Exists(PathFor(installRoot));

    /// <summary>
    /// Extracts one entry by full path, e.g.
    /// <c>Data\Localization\english\global.ini</c>.
    /// </summary>
    /// <returns>The decompressed bytes, or null if absent or encrypted.</returns>
    public byte[]? TryRead(string entryPath)
    {
        using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);

        var (directoryOffset, entryCount) = FindCentralDirectory(stream, reader);
        var entry = FindEntry(stream, reader, directoryOffset, entryCount, entryPath);

        if (entry is null)
            return null;

        var payload = ReadPayload(stream, reader, entry.Value.LocalOffset, entry.Value.Compressed);

        // Stored entries need no decoding.
        if (entry.Value.Method == 0)
            return payload;

        // A ZStd frame starts 28 B5 2F FD. Anything else here is encrypted.
        if (payload.Length < 4 || payload[0] != 0x28 || payload[1] != 0xB5
            || payload[2] != 0x2F || payload[3] != 0xFD)
        {
            return null;
        }

        using var decompressor = new Decompressor();
        return decompressor.Unwrap(payload).ToArray();
    }

    /// <summary>Locates the central directory, following the ZIP64 records.</summary>
    private static (long Offset, long Count) FindCentralDirectory(FileStream stream, BinaryReader reader)
    {
        var tail = (int)Math.Min(70_000, stream.Length);
        stream.Seek(-tail, SeekOrigin.End);

        var buffer = reader.ReadBytes(tail);
        var eocd = -1;

        for (var i = buffer.Length - 22; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(buffer, i) == EndOfCentralDirectory)
            {
                eocd = i;
                break;
            }
        }

        if (eocd < 0)
            throw new InvalidDataException("Data.p4k has no end-of-central-directory record.");

        long count = BitConverter.ToUInt16(buffer, eocd + 10);
        long offset = BitConverter.ToUInt32(buffer, eocd + 16);

        // Sentinel values mean the real ones are in the ZIP64 record, which is
        // always the case at this archive's size.
        if (offset != 0xFFFFFFFF && count != 0xFFFF)
            return (offset, count);

        for (var i = eocd - 20; i >= 0; i--)
        {
            if (BitConverter.ToUInt32(buffer, i) != Zip64Locator)
                continue;

            stream.Seek(BitConverter.ToInt64(buffer, i + 8), SeekOrigin.Begin);

            if (reader.ReadUInt32() != Zip64EndOfCentralDirectory)
                throw new InvalidDataException("Data.p4k ZIP64 record is malformed.");

            stream.Seek(28, SeekOrigin.Current);
            count = reader.ReadInt64();
            stream.Seek(8, SeekOrigin.Current);
            offset = reader.ReadInt64();

            return (offset, count);
        }

        throw new InvalidDataException("Data.p4k ZIP64 locator not found.");
    }

    private static (long LocalOffset, long Compressed, ushort Method)?
        FindEntry(FileStream stream, BinaryReader reader, long directoryOffset, long entryCount, string wanted)
    {
        stream.Seek(directoryOffset, SeekOrigin.Begin);

        var normalised = wanted.Replace('/', '\\');

        for (long i = 0; i < entryCount; i++)
        {
            if (reader.ReadUInt32() != CentralHeaderSignature)
                break;

            stream.Seek(4, SeekOrigin.Current);
            _ = reader.ReadUInt16();                       // flags
            var method = reader.ReadUInt16();
            stream.Seek(8, SeekOrigin.Current);
            long compressed = reader.ReadUInt32();
            long uncompressed = reader.ReadUInt32();
            var nameLength = reader.ReadUInt16();
            var extraLength = reader.ReadUInt16();
            var commentLength = reader.ReadUInt16();
            stream.Seek(8, SeekOrigin.Current);
            long localOffset = reader.ReadUInt32();

            var name = Encoding.UTF8.GetString(reader.ReadBytes(nameLength));
            var extra = reader.ReadBytes(extraLength);
            stream.Seek(commentLength, SeekOrigin.Current);

            if (!name.Replace('/', '\\').Equals(normalised, StringComparison.OrdinalIgnoreCase))
                continue;

            ApplyZip64Extra(extra, ref uncompressed, ref compressed, ref localOffset);
            return (localOffset, compressed, method);
        }

        return null;
    }

    /// <summary>Replaces 32-bit sentinels with the real values from the extra field.</summary>
    private static void ApplyZip64Extra(byte[] extra, ref long uncompressed, ref long compressed, ref long localOffset)
    {
        var cursor = 0;

        while (cursor + 4 <= extra.Length)
        {
            var tag = BitConverter.ToUInt16(extra, cursor);
            var size = BitConverter.ToUInt16(extra, cursor + 2);
            var body = cursor + 4;

            if (tag == 0x0001)
            {
                var at = body;

                if (uncompressed == 0xFFFFFFFF && at + 8 <= extra.Length)
                {
                    uncompressed = BitConverter.ToInt64(extra, at);
                    at += 8;
                }

                if (compressed == 0xFFFFFFFF && at + 8 <= extra.Length)
                {
                    compressed = BitConverter.ToInt64(extra, at);
                    at += 8;
                }

                if (localOffset == 0xFFFFFFFF && at + 8 <= extra.Length)
                    localOffset = BitConverter.ToInt64(extra, at);
            }

            cursor = body + size;
        }
    }

    private static byte[] ReadPayload(FileStream stream, BinaryReader reader, long localOffset, long compressed)
    {
        stream.Seek(localOffset, SeekOrigin.Begin);

        if (reader.ReadUInt32() != LocalHeaderSignature)
            throw new InvalidDataException("Data.p4k local header is malformed.");

        stream.Seek(22, SeekOrigin.Current);
        var nameLength = reader.ReadUInt16();
        var extraLength = reader.ReadUInt16();
        stream.Seek(nameLength + extraLength, SeekOrigin.Current);

        return reader.ReadBytes((int)compressed);
    }
}
