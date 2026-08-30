namespace Quantumwake.Core.GameData;

/// <summary>What the game says an item is.</summary>
/// <param name="Name">The name the game displays, or the class name behind it.</param>
/// <param name="Type">Weapon, Armor, Cooler and so on.</param>
/// <param name="SubType">The narrower kind: Medium armour, a gun turret.</param>
/// <param name="Size">Component size, 0 where the item has none.</param>
/// <param name="Grade">Component grade as the game stores it, an ordinal.</param>
/// <param name="Manufacturer">The maker's full name, or its code when unnamed.</param>
/// <param name="Description">What the game says the thing is, or empty.</param>
/// <param name="Tags">
/// The game's own labels, space separated - <c>gimbalMount flightReady</c>.
/// </param>
/// <param name="MicroScu">
/// The room it takes up, in millionths of an SCU. Every item has one.
/// </param>
public sealed record GameItem(
    string Name, string Type, string SubType, int Size, int Grade, string Manufacturer,
    string Description = "", string Tags = "", long MicroScu = 0);

/// <summary>
/// The item catalogue, read from the install instead of downloaded.
/// </summary>
/// <remarks>
/// <para>
/// Every item the game can attach to something carries an
/// <c>SAttachableComponentParams</c> component, and its <c>AttachDef</c> is a
/// plain <c>SItemDefinition</c>: type, sub-type, size, grade and a reference to
/// the manufacturer. That is the whole of what the reference pages show, and it
/// is sitting in the install.
/// </para>
/// <para>
/// Checked against the community dataset on this install rather than assumed:
/// all 10,843 of its items are here by class name, and type, sub-type, size and
/// grade agree on every one of them. The install describes 26,028, so the
/// download was the smaller catalogue.
/// </para>
/// <para>
/// Manufacturers are the one place the two differ in form. The install stores a
/// code and a localisation key - SHIN and manufacturer_NameSHIN - where the
/// download stores "Shubin Interstellar", so the key is resolved and the code
/// kept only when English has nothing behind it.
/// </para>
/// </remarks>
public static class GameItems
{
    /// <summary>
    /// Reads every attachable item, given an open blob and its English table.
    /// </summary>
    /// <remarks>
    /// Takes the reader rather than opening its own, because the blob is 316 MB
    /// decompressed and one pass has to answer everything.
    /// </remarks>
    public static Dictionary<string, GameItem> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var items = new Dictionary<string, GameItem>(StringComparer.OrdinalIgnoreCase);

        var attach = core.StructIndexOf("SAttachableComponentParams");
        if (attach < 0) return items;

        var makers = Manufacturers(core, text);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex != attach) continue;

                var (at, field) = core.FieldAt(core.InstanceAt(component), attach, "AttachDef");
                if (at < 0 || field is null) continue;

                var bare = record.Name[(record.Name.LastIndexOf('.') + 1)..];
                var maker = core.ReferenceAt(at, field.StructIndex, "Manufacturer");

                items[bare] = new GameItem(
                    Localised(core, text, at, field.StructIndex) ?? bare,
                    Known(core.EnumAt(at, field.StructIndex, "Type")),
                    Known(core.EnumAt(at, field.StructIndex, "SubType")),
                    core.Int32At(at, field.StructIndex, "Size") ?? 0,
                    core.Int32At(at, field.StructIndex, "Grade") ?? 0,
                    maker is not null ? makers.GetValueOrDefault(maker.Value, string.Empty) : string.Empty,
                    Localised(core, text, at, field.StructIndex, "Description") ?? string.Empty,
                    core.StringAt(at, field.StructIndex, "Tags") ?? string.Empty,
                    Volume(core, at, field.StructIndex));

                break;
            }
        }

        return items;
    }

    /// <summary>
    /// An enum value, unless the game's way of saying there is not one.
    /// </summary>
    /// <remarks>
    /// Most items have no sub-type and the enum says so as the literal
    /// <c>UNDEFINED</c>. Passing that through puts a shouted word in a column
    /// where every other empty cell is a dash, and it reads as a failure to look
    /// something up rather than as an item that simply has no sub-type.
    /// </remarks>
    private static string Known(string? value) =>
        value is null || value.Equals("UNDEFINED", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : value;

    /// <summary>
    /// How much room an item takes up, in millionths of an SCU.
    /// </summary>
    /// <remarks>
    /// Every one of the 26,028 items carries this, which is what makes it worth
    /// reading: a list of gear becomes a volume, and a volume can be held
    /// against a hold.
    /// </remarks>
    private static long Volume(DataCore core, long instance, int structIndex)
    {
        var volume = core.PointerAt(instance, structIndex, "inventoryOccupancyVolume");
        if (volume is null) return 0;

        var at = core.InstanceAt(volume.Value);
        var s = volume.Value.StructIndex;

        var micro = core.Int32At(at, s, "microSCU") ?? (int?)core.SingleAt(at, s, "microSCU") ?? 0;

        // A single millionth of an SCU is the game's way of saying an entity has
        // no volume worth speaking of, and 14,492 of them say it - ports, seat
        // access, cargo grids. Reported as such it would put "1 microSCU" beside
        // a rifle's 16,862 and look like a measurement.
        return micro > 1 ? micro : 0;
    }

    /// <summary>Maker id to the name a player would recognise.</summary>
    private static Dictionary<Guid, string> Manufacturers(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var makers = new Dictionary<Guid, string>();

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("SCItemManufacturer.", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);
            var name = Localised(core, text, at, record.StructIndex)
                ?? core.StringAt(at, record.StructIndex, "Code");

            if (name is { Length: > 0 }) makers[record.Hash] = name;
        }

        return makers;
    }

    /// <summary>
    /// The English behind an instance's own localisation key, or null.
    /// </summary>
    /// <remarks>
    /// Public because blueprints need the same answer for the resources and
    /// items a recipe consumes, and they reach those as records rather than
    /// through this class. The same block holds the item's description, so
    /// <paramref name="which"/> picks between them.
    /// </remarks>
    /// <remarks>
    /// Items and manufacturers both carry an inline <c>SCItemLocalization</c>
    /// holding a key, so one reader serves both. A key CIG have not filled in
    /// yields null rather than the raw key, which would read as a bug on the
    /// page.
    /// </remarks>
    public static string? Localised(
        DataCore core, IReadOnlyDictionary<string, string> text, long instance, int structIndex,
        string which = "Name")
    {
        var (at, field) = core.FieldAt(instance, structIndex, "Localization");
        if (at < 0 || field is null) return null;

        var key = core.StringAt(at, field.StructIndex, which);
        if (key is not { Length: > 0 }) return null;

        return text.TryGetValue(key.TrimStart('@'), out var english) && english.Length > 0
            ? english
            : null;
    }
}
