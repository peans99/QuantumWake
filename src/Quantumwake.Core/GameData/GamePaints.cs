namespace Quantumwake.Core.GameData;

/// <summary>A paint scheme the game sells or awards for a ship, and its picture.</summary>
/// <param name="Item">The paint item's class - <c>Paint_Corsair_Black_Black_Gold_Camo</c>.</param>
/// <param name="Name">The localised name.</param>
/// <param name="Tag">The paint port tag it fits - <c>Paint_Corsair</c> - which is how the game ties it to a hull.</param>
/// <param name="Render">The archive path of the game's own picture of the ship in this paint.</param>
public sealed record GamePaint(string Item, string Name, string Tag, string Render);

/// <summary>
/// The paints the install describes, with the one kind of painted ship
/// picture the game files hold.
/// </summary>
/// <remarks>
/// <para>
/// There is no stock-finish render of any ship in <c>Data.p4k</c> - the game
/// draws ships live. What there is: a paint item per purchasable scheme, whose
/// manufacturer record's <c>Logo</c> is a 256-square three-quarter render of
/// that ship in that paint, under <c>UI\SharedAssets\PaintColorLogos</c>.
/// 1,042 of 1,087 paint items carry one; the rest are templates whose logo is
/// the maker's mark, which this reader leaves out rather than show as a ship.
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
    private const string RenderFolder = @"Data\UI\SharedAssets\PaintColorLogos\";

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
    /// The paints the game has for a hull, by the name rule: a paint item is
    /// <c>Paint_&lt;model&gt;_...</c> and the hull's model is its class without
    /// the maker. Tried longest first - <c>Hornet_F7CM_Mk2</c>, then
    /// <c>Hornet_F7CM</c>, then <c>Hornet</c> - and then each token on its own,
    /// so a <c>C8X_Pisces_Expedition</c> still finds the Pisces paints.
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
            var prefix = "Paint_" + candidate + "_";
            var found = paints.Where(p => p.Item.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            if (found.Count > 0) return found.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
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
