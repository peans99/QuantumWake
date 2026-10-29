namespace Quantumwake.Core.GameData;

/// <summary>
/// The makers' marks the game itself carries, by maker code.
/// </summary>
/// <remarks>
/// <para>
/// Every <c>SCItemManufacturer</c> record has a <c>Logo</c>; for a real maker
/// it names <c>UI/SharedAssets/ManufacturerLogos/&lt;Maker&gt;_256.tif</c>, which
/// the archive holds as a 256-square BC3 <c>.dds</c> - the same shape as the
/// paint renders and the vehicle icons, so <see cref="VehicleIcons.Convert"/>
/// already turns one into a PNG. 127 of the 211 non-paint maker records
/// point at a texture that exists (4.10); the 84 that do not are shops and
/// stations wearing the same record type (GrimHex, NewDeal, TeachsRentals),
/// plus KAP, whose <c>KAP_256_diff.tif</c> is not in the archive.
/// </para>
/// <para>
/// Keyed twice: by the record's own suffix (<c>SCItemManufacturer.BEHR</c>)
/// and by its <c>Code</c> field (<c>BEH</c>), because the dataset spells a
/// part's maker the first way and the game's own item records the second.
/// </para>
/// </remarks>
public static class GameMakerLogos
{
    public const string Folder = @"Data\UI\SharedAssets\ManufacturerLogos\";

    public static Dictionary<string, string> Read(DataCore core, IEnumerable<string> archiveEntries)
    {
        var present = new HashSet<string>(archiveEntries.Select(P4kArchive.EntryFileName), StringComparer.OrdinalIgnoreCase);
        var logos = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("SCItemManufacturer.", StringComparison.OrdinalIgnoreCase)
                || record.Name.Contains("Paint_", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);
            var logo = core.StringAt(at, record.StructIndex, "Logo");
            if (logo is not { Length: > 0 } || !logo.Contains("ManufacturerLogos", StringComparison.OrdinalIgnoreCase))
                continue;

            // The record says .tif; the archive ships the .dds it was cooked to.
            var file = P4kArchive.EntryStem(logo) + ".dds";
            if (!present.Contains(file)) continue;

            var entry = Folder + file;
            logos.TryAdd(record.Name["SCItemManufacturer.".Length..], entry);

            if (core.StringAt(at, record.StructIndex, "Code") is { Length: > 0 } code)
                logos.TryAdd(code, entry);
        }

        return logos;
    }
}
