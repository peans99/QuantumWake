namespace Quantumwake.Core.GameData;

/// <summary>A paint scheme the game sells or awards for a ship, and its picture.</summary>
/// <param name="Item">The paint item's class - <c>Paint_Corsair_Black_Black_Gold_Camo</c>.</param>
/// <param name="Name">The localised name.</param>
/// <param name="Tag">The paint port tag it fits - <c>Paint_Corsair</c> - which is how the game ties it to a hull.</param>
/// <param name="Render">The archive path of the game's own picture of the ship in this paint.</param>
/// <param name="Stock">
/// True for the default livery: a render the archive holds under a
/// <c>_default</c> or <c>_base</c> name that no paint item claims. Not every
/// hull has one - 26 do.
/// </param>
public sealed record GamePaint(string Item, string Name, string Tag, string Render, bool Stock = false);

/// <summary>
/// The paints the install describes, with the one kind of painted ship
/// picture the game files hold.
/// </summary>
/// <remarks>
/// <para>
/// The game draws ships live, so <c>Data.p4k</c> has no render of most hulls
/// in their stock finish. What there is: a paint item per purchasable scheme,
/// whose manufacturer record's <c>Logo</c> is a 256-square three-quarter
/// render of that ship in that paint, under <c>UI\SharedAssets\PaintColorLogos</c>.
/// 1,042 of 1,087 paint items carry one; the rest are templates whose logo is
/// the maker's mark, which this reader leaves out rather than show as a ship.
/// </para>
/// <para>
/// The same folder holds 26 renders that no paint item references -
/// <c>paint_clipper_default.dds</c>, <c>Paint_RSI_Hermes_Default.dds</c>,
/// <c>Paint_Paladin_Default_Icon.dds</c> - which are the default liveries of
/// the hulls that have one. <see cref="Stock"/> lifts them from the archive
/// listing, so a hull with one shows it before any paint; a hull without one
/// has no stock picture in the files at all, and the app says so rather than
/// borrowing a paint.
/// </para>
/// <para>
/// Which paint a pilot flies is not in the logs, so the app never picks one:
/// the Fleet page offers the paints the game has for the hull and the pilot
/// chooses. The join from hull to paint is by name - a paint item is
/// <c>Paint_&lt;model&gt;_&lt;scheme&gt;</c> and a hull is <c>&lt;MAKER&gt;_&lt;model&gt;</c> -
/// because the port tag that ties them in the game sits in the vehicle XML,
/// which this reader does not open.
/// </para>
/// </remarks>
public static class GamePaints
{
    public const string RenderFolder = @"Data\UI\SharedAssets\PaintColorLogos\";

    public static List<GamePaint> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var paints = new List<GamePaint>();

        var attach = core.StructIndexOf("SAttachableComponentParams");
        if (attach < 0) return paints;

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.Paint_", StringComparison.OrdinalIgnoreCase))
                continue;

            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex != attach) continue;

                var (at, field) = core.FieldAt(core.InstanceAt(component), attach, "AttachDef");
                if (at < 0 || field is null) break;
                if (core.EnumAt(at, field.StructIndex, "Type") != "Paints") break;

                // The render is the maker record's logo, and only when it is a
                // picture of the ship: a template's logo is the maker's mark.
                if (core.ReferenceAt(at, field.StructIndex, "Manufacturer") is not { } makerId
                    || !byId.TryGetValue(makerId, out var maker))
                    break;

                var logo = core.StringAt(core.InstanceAt(maker, maker.VariantIndex), maker.StructIndex, "Logo");
                if (logo is not { Length: > 0 } || !logo.Contains("PaintColorLogos", StringComparison.OrdinalIgnoreCase))
                    break;

                var tag = (core.StringAt(at, field.StructIndex, "RequiredTags") ?? "").Split(' ')[0];
                var item = record.Name["EntityClassDefinition.".Length..];
                var name = Localised(core, text, at, field.StructIndex) ?? item.Replace('_', ' ');

                paints.Add(new GamePaint(item, name, tag, RenderFolder + Path.GetFileNameWithoutExtension(logo) + ".dds"));
                break;
            }
        }

        return paints;
    }

    /// <summary>
    /// The default liveries: renders in the paint folder that no paint item
    /// references and whose name says <c>default</c> or <c>base</c>. The item
    /// is the file's stem, which is what the hull join and the render route
    /// go by; the name is the same for every one, because the file has none.
    /// </summary>
    /// <param name="entries">The archive's entries, or the paint folder's; anything else is skipped.</param>
    public static List<GamePaint> Stock(IEnumerable<string> entries, IReadOnlyList<GamePaint> paints)
    {
        var claimed = new HashSet<string>(paints.Select(p => p.Render), StringComparer.OrdinalIgnoreCase);
        var stock = new List<GamePaint>();

        foreach (var entry in entries)
        {
            if (!entry.StartsWith(RenderFolder, StringComparison.OrdinalIgnoreCase)) continue;
            // Split mip chains - Paint_Meteor_Default_Icon.dds.4 - are not pictures on their own.
            if (!entry.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)) continue;
            if (claimed.Contains(entry)) continue;

            var stem = Path.GetFileNameWithoutExtension(entry);
            var lower = stem.ToLowerInvariant();
            if (!lower.StartsWith("paint_") || !(lower.Contains("_default") || lower.Contains("_base"))) continue;

            stock.Add(new GamePaint(stem, "Default livery", "", entry, Stock: true));
        }

        return stock;
    }

    /// <summary>
    /// The paints the game has for a hull, by the name rule: a paint item is
    /// <c>Paint_&lt;model&gt;_...</c> and the hull's model is its class without
    /// the maker. Tried longest first - <c>Hornet_F7CM_Mk2</c>, then
    /// <c>Hornet_F7CM</c>, then <c>Hornet</c> - and then each token on its own,
    /// so a <c>C8X_Pisces_Expedition</c> still finds the Pisces paints. A
    /// stock render may carry the maker too - <c>Paint_RSI_Hermes_Default</c> -
    /// so each candidate is also tried with it. The default livery, when the
    /// hull has one, comes first.
    /// </summary>
    public static IReadOnlyList<GamePaint> ForHull(IReadOnlyList<GamePaint> paints, string vehicleClass)
    {
        var tokens = vehicleClass.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2) return [];

        var model = tokens[1..];
        var candidates = new List<string>();
        for (var n = model.Length; n >= 1; n--) candidates.Add(string.Join('_', model[..n]));
        foreach (var token in model)
            if (token.Length > 2 && !token.StartsWith("Mk", StringComparison.OrdinalIgnoreCase)) candidates.Add(token);

        foreach (var candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var prefixes = new[] { "Paint_" + candidate + "_", "Paint_" + tokens[0] + "_" + candidate + "_" };
            var found = paints
                .Where(p => prefixes.Any(prefix => p.Item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToList();
            if (found.Count > 0)
                return found
                    .OrderByDescending(p => p.Stock)
                    .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        return [];
    }

    private static string? Localised(DataCore core, IReadOnlyDictionary<string, string> text, long at, int structIndex)
    {
        var (loc, locField) = core.FieldAt(at, structIndex, "Localization");
        if (loc < 0 || locField is null) return null;

        var key = core.StringAt(loc, locField.StructIndex, "Name");
        if (key is not { Length: > 0 }) return null;

        var bare = key.TrimStart('@');
        if (text.TryGetValue(bare, out var english) && english.Length > 0) return english;
        if (text.TryGetValue(bare + ",P", out var variant) && variant.Length > 0) return variant;
        return null;
    }
}
