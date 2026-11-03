using System.Security.Cryptography;
using System.Text;
using ZstdSharp;

namespace Quantumwake.Core.GameData;

/// <summary>One entry's place in the archive, as <see cref="P4kArchive.Locate"/> found it.</summary>
public readonly record struct P4kEntry(string Path, long Size, long LocalOffset, long Compressed, ushort Method);

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
/// Some entries are encrypted. By default those are reported rather than
/// read. The one exception is opt-in and narrow, see
/// <see cref="TryReadDecrypting"/>.
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

    /// <summary>The file name of an entry, or of a path the DataCore records for one.</summary>
    /// <remarks>
    /// Not <see cref="Path.GetFileName(string)"/>. Entry names are Windows paths
    /// whatever machine reads them, and <see cref="Path"/> only splits on '\'
    /// where '\' is the platform's separator: on Linux it hands back
    /// <c>Data\UI\...\logo.dds</c> whole, which cost the default liveries and
    /// the attachment icons there. The DataCore writes some of its paths with
    /// '/', so both count.
    /// </remarks>
    public static string EntryFileName(string entry) =>
        entry[(entry.LastIndexOfAny(['\\', '/']) + 1)..];

    /// <summary>The file name of an entry without its extension; see <see cref="EntryFileName"/>.</summary>
    public static string EntryStem(string entry)
    {
        var name = EntryFileName(entry);
        var dot = name.LastIndexOf('.');
        return dot < 0 ? name : name[..dot];
    }

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
        return Decode(payload, entry.Value.Method);
    }

    /// <summary>
    /// Extracts one entry already found by <see cref="Locate"/>, without
    /// walking the directory again.
    /// </summary>
    /// <remarks>
    /// Each call opens its own handle, so many can run at once. That is the
    /// point: the directory holds 1.36 million entries, and walking it once for
    /// each of the 9,668 map files was most of what made the first places scan
    /// take 45 minutes.
    /// </remarks>
    public byte[]? TryRead(P4kEntry entry)
    {
        using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        return Decode(ReadPayload(stream, reader, entry.LocalOffset, entry.Compressed), entry.Method);
    }

    /// <summary>
    /// Extracts an entry the archive stores encrypted, or null when it is
    /// neither readable plainly nor with the community's key.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Encrypted entries are AES-128 in CBC mode with a zero IV over an
    /// ordinary ZStd frame, padded to the block size. The key is the one
    /// published in unp4k (https://github.com/dolkensp/unp4k) and used by
    /// scdatatools; see docs/credits.md. Checked on this install: the decrypted
    /// bytes begin with the ZStd magic and inflate to exactly the size the
    /// directory records.
    /// </para>
    /// <para>
    /// Opt-in, and used for one thing: the NPC loadout files under
    /// <c>Data\Scripts\Loadouts</c>, which say what NPCs wear. Nicolas
    /// approved that on 2026-10-31. Everything else keeps
    /// <see cref="TryRead(P4kEntry)"/>, which reports encrypted entries as
    /// unreadable. The padding is why the frame is streamed to the recorded
    /// size rather than unwrapped whole: the bytes after the frame are not
    /// ZStd, and a one-shot unwrap refuses them.
    /// </para>
    /// </remarks>
    public byte[]? TryReadDecrypting(P4kEntry entry)
    {
        using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);
        var payload = ReadPayload(stream, reader, entry.LocalOffset, entry.Compressed);

        if (Decode(payload, entry.Method) is { } plain) return plain;
        if (entry.Method == 0 || payload.Length == 0 || payload.Length % 16 != 0) return null;

        using var aes = Aes.Create();
        aes.Key = CommunityKey;
        var decrypted = aes.DecryptCbc(payload, new byte[16], PaddingMode.None);
        if (!IsZstd(decrypted)) return null;

        var output = new byte[entry.Size];
        try
        {
            using var frame = new DecompressionStream(new MemoryStream(decrypted));
            frame.ReadExactly(output);
        }
        catch (Exception e) when (e is ZstdException or EndOfStreamException)
        {
            return null;
        }

        return output;
    }

    private static readonly byte[] CommunityKey =
        [0x5E, 0x7A, 0x20, 0x02, 0x30, 0x2E, 0xEB, 0x1A, 0x3B, 0xB6, 0x17, 0xC3, 0x0F, 0xDE, 0x1E, 0x47];

    private static bool IsZstd(byte[] bytes) =>
        bytes.Length >= 4 && bytes[0] == 0x28 && bytes[1] == 0xB5 && bytes[2] == 0x2F && bytes[3] == 0xFD;

    /// <summary>Every entry under a prefix, located in one walk of the directory.</summary>
    public IReadOnlyList<P4kEntry> Locate(string prefix)
    {
        var normalised = prefix.Replace('/', '\\');
        return Locate(name => name.Replace('/', '\\').StartsWith(normalised, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Every entry whose name passes <paramref name="keep"/>, in one walk of the
    /// directory - for gathering several folders without a walk each.
    /// </summary>
    public IReadOnlyList<P4kEntry> Locate(Func<string, bool> keep)
    {
        using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);

        var (directoryOffset, entryCount) = FindCentralDirectory(stream, reader);
        stream.Seek(directoryOffset, SeekOrigin.Begin);

        var found = new List<P4kEntry>();

        for (long i = 0; i < entryCount; i++)
        {
            if (reader.ReadUInt32() != CentralHeaderSignature)
                break;

            stream.Seek(4, SeekOrigin.Current);
            _ = reader.ReadUInt16();
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

            if (!keep(name))
                continue;

            ApplyZip64Extra(extra, ref uncompressed, ref compressed, ref localOffset);
            found.Add(new P4kEntry(name, uncompressed, localOffset, compressed, method));
        }

        return found;
    }

    /// <summary>
    /// A read-only window onto a stored entry, or null for a compressed one.
    /// </summary>
    /// <remarks>
    /// The map files are zips held uncompressed in the archive - all 9,668 of
    /// them, 4.8 GB - and the parts worth reading are a few kilobytes of each.
    /// Handing <see cref="System.IO.Compression.ZipArchive"/> a window instead
    /// of the bytes lets it seek to those and leave the geometry on disk.
    /// The caller disposes the stream, which closes its handle.
    /// </remarks>
    public Stream? OpenStored(P4kEntry entry)
    {
        if (entry.Method != 0) return null;

        var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        try
        {
            using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
            stream.Seek(entry.LocalOffset, SeekOrigin.Begin);

            if (reader.ReadUInt32() != LocalHeaderSignature)
                throw new InvalidDataException("Data.p4k local header is malformed.");

            stream.Seek(22, SeekOrigin.Current);
            var nameLength = reader.ReadUInt16();
            var extraLength = reader.ReadUInt16();

            return new Window(stream, stream.Position + nameLength + extraLength, entry.Compressed);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }

    /// <summary>A seekable slice of the archive file that owns its handle.</summary>
    private sealed class Window(FileStream file, long start, long length) : Stream
    {
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => length;

        public override long Position
        {
            get => _position;
            set => _position = Math.Clamp(value, 0, length);
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            var wanted = (int)Math.Min(buffer.Length, length - _position);
            if (wanted <= 0) return 0;

            file.Seek(start + _position, SeekOrigin.Begin);
            var read = file.Read(buffer[..wanted]);
            _position += read;
            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            Position = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => _position + offset,
                _ => length + offset,
            };

        public override void Flush() { }
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) file.Dispose();
            base.Dispose(disposing);
        }
    }

    private static byte[]? Decode(byte[] payload, ushort method)
    {
        // Stored entries need no decoding.
        if (method == 0)
            return payload;

        // A ZStd frame starts 28 B5 2F FD. Anything else here is encrypted.
        if (!IsZstd(payload))
            return null;

        using var decompressor = new Decompressor();
        return decompressor.Unwrap(payload).ToArray();
    }

    /// <summary>
    /// Every entry path under a prefix, with its stored size. The same walk
    /// <see cref="TryRead"/> makes to find one entry, kept for finding out
    /// what is there at all - a texture folder, say - without guessing names.
    /// </summary>
    public IReadOnlyList<(string Path, long Size)> List(string prefix)
    {
        using var stream = File.Open(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new BinaryReader(stream);

        var (directoryOffset, entryCount) = FindCentralDirectory(stream, reader);
        stream.Seek(directoryOffset, SeekOrigin.Begin);

        var normalised = prefix.Replace('/', '\\');
        var found = new List<(string, long)>();

        for (long i = 0; i < entryCount; i++)
        {
            if (reader.ReadUInt32() != CentralHeaderSignature)
                break;

            stream.Seek(4, SeekOrigin.Current);
            _ = reader.ReadUInt16();
            _ = reader.ReadUInt16();
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

            if (!name.Replace('/', '\\').StartsWith(normalised, StringComparison.OrdinalIgnoreCase))
                continue;

            ApplyZip64Extra(extra, ref uncompressed, ref compressed, ref localOffset);
            found.Add((name, uncompressed));
        }

        return found;
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
