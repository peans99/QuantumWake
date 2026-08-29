using System.Text;

namespace Quantumwake.Core.GameData;

/// <summary>One record in the DataCore: a game object with a path and a type.</summary>
public sealed record DataRecord(string Name, string FileName, int StructIndex, Guid Hash, int VariantIndex);

/// <summary>
/// Reads Star Citizen's DataCore blob, <c>Data\Game2.dcb</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file is a schema and a set of records: struct definitions describing
/// types, property definitions describing their fields, typed value arrays, and
/// record instances that index into all of it. Everything is offsets into two
/// string tables, which is the detail that makes or breaks a reader — names come
/// from the <b>blob</b> table and file paths from the <b>text</b> table, and
/// mixing them up produces confident nonsense that lands mid-string.
/// </para>
/// <para>
/// The layout is undocumented by CIG. It is legible because the community worked
/// it out — scdatatools and the DataForge lineage in unp4k — and published what
/// they found. This is our own implementation of that understanding; nothing of
/// theirs is vendored. See CREDITS.md.
/// </para>
/// <para>
/// The header declares the value counts in a different order from the one the
/// sections appear in, which is a trap worth stating: booleans are declared
/// sixth and stored ninth. Getting that wrong shifts every offset after it.
/// </para>
/// </remarks>
public sealed class DataCore
{
    private readonly byte[] _data;
    private readonly long _textOffset;
    private readonly long _blobOffset;

    public int FileVersion { get; }
    public int StructDefinitionCount { get; }
    public int PropertyDefinitionCount { get; }
    public int EnumDefinitionCount { get; }
    public int RecordDefinitionCount { get; }

    private readonly long _structOffset;
    private readonly long _recordOffset;
    private readonly int _recordSize;
    private int _mappingCount;

    public DataCore(byte[] data)
    {
        _data = data;

        int I(long at) => BitConverter.ToInt32(_data, (int)at);
        uint U(long at) => BitConverter.ToUInt32(_data, (int)at);

        FileVersion = I(4);

        // Counts, in the order the header declares them.
        var at = 0x10L;
        int Next() { var v = I(at); at += 4; return v; }

        StructDefinitionCount = Next();
        PropertyDefinitionCount = Next();
        EnumDefinitionCount = Next();
        var dataMappingCount = Next();
        RecordDefinitionCount = Next();

        var boolean = Next();
        var int8 = Next(); var int16 = Next(); var int32 = Next(); var int64 = Next();
        var uint8 = Next(); var uint16 = Next(); var uint32 = Next(); var uint64 = Next();
        var single = Next(); var dbl = Next(); var guid = Next();
        var str = Next(); var locale = Next(); var @enum = Next();
        var strong = Next(); var weak = Next(); var reference = Next();
        var enumOption = Next();

        var textLength = U(at); at += 4;
        var blobLength = U(at);

        // Sections follow the header at 0x78, in this order. Note it is NOT the
        // order the counts are declared in.
        _structOffset = 0x78;
        var propertyOffset = _structOffset + StructDefinitionCount * 16L;
        var enumOffset = propertyOffset + PropertyDefinitionCount * 12L;
        _mappingOffset = enumOffset + EnumDefinitionCount * 8L;
        _mappingCount = dataMappingCount;
        _recordOffset = _mappingOffset + dataMappingCount * 8L;

        _recordSize = FileVersion < 8 ? 32 : 36;
        var cursor = _recordOffset + RecordDefinitionCount * (long)_recordSize;

        cursor += int8 * 1L; cursor += int16 * 2L; cursor += int32 * 4L; cursor += int64 * 8L;
        cursor += uint8 * 1L; cursor += uint16 * 2L; cursor += uint32 * 4L; cursor += uint64 * 8L;
        cursor += boolean * 1L;
        cursor += single * 4L; cursor += dbl * 8L;
        cursor += guid * 16L;
        cursor += str * 4L; cursor += locale * 4L; cursor += @enum * 4L;
        cursor += strong * 8L; cursor += weak * 8L;
        cursor += reference * 20L;
        cursor += enumOption * 4L;

        _textOffset = cursor;
        _blobOffset = _textOffset + textLength;
        _dataOffset = _blobOffset + blobLength;

        TextLength = textLength;
        BlobLength = blobLength;
    }

    public uint TextLength { get; }
    public uint BlobLength { get; }
    public long TextOffset => _textOffset;
    public long BlobOffset => _blobOffset;

    /// <summary>True when the computed offsets land on readable string tables.</summary>
    /// <remarks>
    /// Worth checking rather than assuming: every offset after a mis-sized
    /// section is wrong, and the failure looks like plausible fragments rather
    /// than an exception.
    /// </remarks>
    public bool LooksSane =>
        _textOffset > 0 && _blobOffset + BlobLength <= _data.LongLength
        && Readable(_textOffset) && Readable(_blobOffset);

    private bool Readable(long at)
    {
        if (at < 0 || at + 256 > _data.LongLength) return false;

        var printable = 0;
        for (var i = at; i < at + 256; i++)
        {
            var b = _data[i];
            if (b == 0) continue;
            if (b < 32 || b >= 127) return false;
            printable++;
        }

        return printable > 32;
    }

    private string StringAt(long table, uint offset)
    {
        var at = table + offset;
        if (at < 0 || at >= _data.LongLength) return string.Empty;

        var end = at;
        while (end < _data.LongLength && _data[end] != 0) end++;

        return Encoding.UTF8.GetString(_data, (int)at, (int)(end - at));
    }

    /// <summary>
    /// The 16 bytes a record stores as its id, as the GUID the game uses.
    /// </summary>
    /// <remarks>
    /// Stored as the big-endian GUID with each 8-byte half reversed, which is
    /// neither of the two layouts anybody tries first. Reading it as a plain
    /// .NET GUID produces the same nibbles regrouped - Aluminum's
    /// 48c7080a-bbef-43d2-901a-698321ed4340 comes back as
    /// bbef43d2-080a-48c7-4043-ed2183691a90 - which is convincing enough to be
    /// mistaken for a different id space entirely. It is not: it is the id the
    /// logs carry, and this is the whole GUID-to-name table.
    /// </remarks>
    private static Guid ReadHash(ReadOnlySpan<byte> raw)
    {
        Span<byte> guid = stackalloc byte[16];

        for (var i = 0; i < 8; i++) guid[i] = raw[7 - i];
        for (var i = 0; i < 8; i++) guid[8 + i] = raw[15 - i];

        // That yields big-endian; .NET wants the first three fields swapped.
        guid[..4].Reverse();
        guid[4..6].Reverse();
        guid[6..8].Reverse();

        return new Guid(guid);
    }

    /// <summary>A type name, which lives in the blob table.</summary>
    public string Blob(uint offset) => StringAt(_blobOffset, offset);

    /// <summary>A record path, which lives in the text table.</summary>
    public string Text(uint offset) => StringAt(_textOffset, offset);

    /// <summary>The name of struct definition <paramref name="index"/>.</summary>
    public string StructName(int index)
    {
        if (index < 0 || index >= StructDefinitionCount) return string.Empty;
        return Blob(BitConverter.ToUInt32(_data, (int)(_structOffset + index * 16L)));
    }

    private long _mappingOffset;
    private long _dataOffset;
    private Dictionary<int, long>? _structData;

    /// <summary>The instance size of a struct, in bytes.</summary>
    public int StructSize(int index) =>
        index < 0 || index >= StructDefinitionCount
            ? 0
            : (int)BitConverter.ToUInt32(_data, (int)(_structOffset + index * 16L + 12));

    /// <summary>
    /// Where each struct's instances begin, relative to the data section.
    /// </summary>
    /// <remarks>
    /// Instances are laid out one struct at a time, in data-mapping order, each
    /// block being count x the struct's own instance size. There is one mapping
    /// per struct - which is why the header declares the same number of both -
    /// so the mapping's index is the struct's.
    ///
    /// The total is the reader's best self-check: laid out correctly it lands
    /// exactly on the end of the file, so a single wrong section size anywhere
    /// upstream shows up here rather than as quiet nonsense downstream.
    /// </remarks>
    public long DataTotal { get; private set; }

    public long DataOffset => _dataOffset;

    /// <summary>True when the instance layout ends exactly at the end of file.</summary>
    public bool LayoutAddsUp => _dataOffset + DataTotal == _data.LongLength;

    private void MapInstances()
    {
        if (_structData is not null) return;

        _structData = [];
        var running = 0L;

        for (var i = 0; i < _mappingCount; i++)
        {
            var at = _mappingOffset + i * 8L;
            var count = BitConverter.ToUInt32(_data, (int)at);
            var structIndex = (int)BitConverter.ToUInt32(_data, (int)(at + 4));

            _structData.TryAdd(structIndex, running);
            running += count * (long)StructSize(i);
        }

        DataTotal = running;
    }

    /// <summary>Where one record's instance data begins, or -1.</summary>
    public long InstanceAt(DataRecord record, int variantIndex)
    {
        MapInstances();

        if (_structData is null || !_structData.TryGetValue(record.StructIndex, out var block))
            return -1;

        return _dataOffset + block + variantIndex * (long)StructSize(record.StructIndex);
    }

    /// <summary>One field on a struct.</summary>
    /// <param name="DataType">The DataForge type code, e.g. 0x000A for a string.</param>
    /// <param name="ConversionType">0 is a plain attribute; 1-3 are array forms.</param>
    public sealed record Property(string Name, ushort DataType, ushort ConversionType);

    private long PropertyOffset => _structOffset + StructDefinitionCount * 16L;

    /// <summary>
    /// Every property on a struct, its inherited ones first.
    /// </summary>
    /// <remarks>
    /// A struct declares only its own fields and points at its parent, so the
    /// full layout is the parent chain walked from the root down - inherited
    /// fields are laid out before the struct's own, and reading them in the
    /// wrong order misaligns every value after the first.
    /// </remarks>
    public IReadOnlyList<Property> StructProperties(int index)
    {
        var chain = new List<int>();

        for (var i = index; i >= 0 && i < StructDefinitionCount && chain.Count < 32;)
        {
            chain.Insert(0, i);
            var parent = (int)BitConverter.ToUInt32(_data, (int)(_structOffset + i * 16L + 4));
            if (parent == i || parent < 0 || parent >= StructDefinitionCount) break;
            i = parent;
        }

        var all = new List<Property>();

        foreach (var s in chain)
        {
            var at = _structOffset + s * 16L;
            var count = BitConverter.ToUInt16(_data, (int)(at + 8));
            var first = BitConverter.ToUInt16(_data, (int)(at + 10));

            for (var p = 0; p < count; p++)
            {
                var pat = PropertyOffset + (first + p) * 12L;
                if (pat + 12 > _data.LongLength) break;

                all.Add(new Property(
                    Blob(BitConverter.ToUInt32(_data, (int)pat)),
                    BitConverter.ToUInt16(_data, (int)(pat + 6)),
                    (ushort)(BitConverter.ToUInt16(_data, (int)(pat + 8)) & 0xFF)));
            }
        }

        return all;
    }

    /// <summary>
    /// How many bytes a property of this type occupies inside an instance.
    /// </summary>
    /// <remarks>
    /// An array of anything is a count and an index into the value arrays, so it
    /// is eight bytes whatever it holds. Everything else is stored inline, and
    /// the strings are four-byte offsets rather than the text itself.
    /// </remarks>
    private int Width(Property p) => p.ConversionType != 0
        ? 8
        : p.DataType switch
        {
            0x0001 or 0x0002 or 0x0006 => 1,          // bool, int8, uint8
            0x0003 or 0x0007 => 2,                     // int16, uint16
            0x0004 or 0x0008 or 0x000B => 4,           // int32, uint32, single
            0x0005 or 0x0009 or 0x000C => 8,           // int64, uint64, double
            0x000A or 0x000D or 0x000F => 4,           // string, locale, enum - all offsets
            0x000E => 16,                              // guid
            0x0110 or 0x0210 => 8,                     // strong and weak pointers
            0x0310 => 20,                              // reference: an index and a guid
            _ => 0,                                    // varClass is inline; handled by the caller
        };

    /// <summary>
    /// A named string, locale or enum value on a record, or null.
    /// </summary>
    /// <remarks>
    /// Walks the struct's fields in layout order, adding each one's width, and
    /// stops at the wanted name. Inline classes are not walked into - the first
    /// one ends the walk rather than being skipped by a guessed width, because a
    /// wrong width here reads a neighbouring field and returns something that
    /// looks like an answer.
    /// </remarks>
    public string? TextProperty(DataRecord record, string name)
    {
        var at = InstanceAt(record, record.VariantIndex);
        if (at < 0) return null;

        foreach (var p in StructProperties(record.StructIndex))
        {
            var width = Width(p);

            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && p.ConversionType == 0)
            {
                if (at + 4 > _data.LongLength) return null;
                var offset = BitConverter.ToUInt32(_data, (int)at);

                return p.DataType switch
                {
                    0x000A => Text(offset),                    // string
                    0x000D => Text(offset),                    // locale reference
                    0x000F => Text(offset),                    // enum name
                    _ => null,
                };
            }

            if (width == 0) return null;                        // inline class: cannot walk past it
            at += width;
        }

        return null;
    }

    /// <summary>Every record, with its path and type.</summary>
    public IEnumerable<DataRecord> Records()
    {
        for (var i = 0; i < RecordDefinitionCount; i++)
        {
            var at = _recordOffset + i * (long)_recordSize;

            // V8: name, filename, devteam, struct index, then the 16-byte hash.
            // That hash is the entity id the game writes into Game.log, which is
            // what makes this file a GUID-to-name table without a download.
            var name = Blob(BitConverter.ToUInt32(_data, (int)at));
            var fileName = Text(BitConverter.ToUInt32(_data, (int)(at + 4)));
            var structIndex = BitConverter.ToInt32(_data, (int)(at + 12));
            var hash = ReadHash(_data.AsSpan((int)(at + 16), 16));

            var variant = BitConverter.ToUInt16(_data, (int)(at + 32));

            yield return new DataRecord(name, fileName, structIndex, hash, variant);
        }
    }
}
