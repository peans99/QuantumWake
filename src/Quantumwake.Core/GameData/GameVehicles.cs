namespace Quantumwake.Core.GameData;

/// <summary>What the install says a vehicle is, for drawing it.</summary>
/// <param name="Class">The entity class - <c>DRAK_Corsair</c> - which is also the log's word for it.</param>
/// <param name="Name">The localised name, when the text table has one.</param>
/// <param name="Beam">Width across, metres; the bounding box's x.</param>
/// <param name="Length">Nose to tail, metres; the box's y.</param>
/// <param name="Height">The box's z, metres.</param>
/// <param name="Icon">
/// The archive path of the game's own top-down silhouette, or null when the
/// entity names none. One icon serves a base model and every variant of it.
/// </param>
public sealed record GameVehicle(
    string Class,
    string Name,
    double Beam,
    double Length,
    double Height,
    string? Icon,
    string Kind = "");

/// <summary>
/// The vehicles the install describes: name, size and silhouette.
/// </summary>
/// <remarks>
/// <para>
/// A vehicle entity carries one <c>VehicleComponentParams</c>, and that is where
/// its <c>maxBoundingBoxSize</c> lives - the Fortune's reads 16.5 × 26.5 × 8,
/// which is the beam, length and height the wiki publishes. Its icon path is on
/// the entity's <c>EntityUIDisplayParams</c>, under <c>StaticEntityClassData</c>,
/// as a <c>.tif</c> name that the archive holds as <c>.dds</c>: 98 of them, one
/// per base model, 1024-square BC3 silhouettes.
/// </para>
/// <para>
/// Every ship, not only the ones flown here: the fleet page draws what the logs
/// say was flown, and which ships that will be is not known when this is read.
/// </para>
/// </remarks>
public static class GameVehicles
{
    private const string IconFolder = @"Data\UI\Textures\EA\VehicleIcons\";

    public static Dictionary<string, GameVehicle> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var vehicles = new Dictionary<string, GameVehicle>(StringComparer.OrdinalIgnoreCase);

        var vehicleParams = core.StructIndexOf("VehicleComponentParams");
        var display = core.StructIndexOf("EntityUIDisplayParams");
        var attach = core.StructIndexOf("SAttachableComponentParams");
        if (vehicleParams < 0) return vehicles;

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.OrdinalIgnoreCase))
                continue;

            DataCore.Pointer? found = null;
            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex == vehicleParams) { found = component; break; }
            }

            if (found is not { } vehicle) continue;

            var at = core.InstanceAt(vehicle);
            var (box, boxField) = core.FieldAt(at, vehicleParams, "maxBoundingBoxSize");
            if (box < 0 || boxField is null) continue;

            // Rounded to the decimetre: the file stores singles, and 27.4 arrives
            // as 27.399999618, which is not a fact about the ship.
            var beam = Math.Round(core.SingleAt(box, boxField.StructIndex, "x") ?? 0, 1);
            var length = Math.Round(core.SingleAt(box, boxField.StructIndex, "y") ?? 0, 1);
            var height = Math.Round(core.SingleAt(box, boxField.StructIndex, "z") ?? 0, 1);

            // A box of nothing is a placeholder entity, not a ship.
            if (length <= 0 || beam <= 0) continue;

            var bare = record.Name[(record.Name.LastIndexOf('.') + 1)..];

            var nameKey = core.StringAt(at, vehicleParams, "vehicleName");
            var attachKey = AttachNameKey(core, record, attach);
            var name = Localised(text, NameKey(text, nameKey, attachKey))
                ?? Localised(text, "vehicle_Name" + bare) ?? bare.Replace('_', ' ');

            string? icon = null;
            if (display >= 0)
            {
                foreach (var ui in core.PointerArray(record, "StaticEntityClassData"))
                {
                    if (ui.StructIndex != display) continue;
                    var path = core.StringAt(core.InstanceAt(ui), display, "displayIcon");
                    if (path is { Length: > 0 } && path.Contains("VehicleIcon_", StringComparison.OrdinalIgnoreCase))
                        icon = IconFolder + P4kArchive.EntryStem(path) + ".dds";
                    break;
                }
            }

            // The game's own word for what moves it - Spaceship, Ground, Gravlev -
            // so a hangar can shelve ships and vehicles apart without guessing.
            vehicles[bare] = new GameVehicle(bare, name, beam, length, height, icon,
                core.EnumAt(at, vehicleParams, "movementClass") ?? "");
        }

        return vehicles;
    }

    /// <summary>
    /// Which of a vehicle's two name keys to show: <c>vehicleName</c>, or the
    /// attach definition's <c>Localization.Name</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Neither is always the specific one. The 5.0 Wikelo specials point
    /// <c>vehicleName</c> at the base hull (<c>vehicle_NameDRAK_Pitbull</c>) and
    /// put "Wikelo Sneak Special" only on the attach name; the PYAM Exec ships run
    /// the other way, a specific <c>vehicleName</c> over a generic attach name.
    /// Preferring either field outright renames one family to its base hull.
    /// </para>
    /// <para>
    /// What both families share is that the specific key extends the generic
    /// one - <c>X_Collector_Stealth</c> over <c>X</c> - so the longer key wins
    /// when it does and it has text. Anything else keeps <c>vehicleName</c>: the
    /// derelicts and hijacked hulls whose two keys are unrelated read today as
    /// the game shows them, and are not this rule's business.
    /// </para>
    /// </remarks>
    public static string? NameKey(IReadOnlyDictionary<string, string> text, string? vehicleKey, string? attachKey)
    {
        if (attachKey is not { Length: > 0 }) return vehicleKey;
        if (vehicleKey is not { Length: > 0 }) return attachKey;

        var (shorterKey, longerKey) = vehicleKey.TrimStart('@').Length <= attachKey.TrimStart('@').Length
            ? (vehicleKey, attachKey) : (attachKey, vehicleKey);
        var shorter = shorterKey.TrimStart('@');
        var longer = longerKey.TrimStart('@');

        // Case-blind because the text table is: a key that only differs in case
        // from the one it extends still names the same line.
        var extends = longer.Length > shorter.Length
            && longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase)
            && longer[shorter.Length] == '_';

        return extends && Localised(text, longer) is not null ? longerKey : vehicleKey;
    }

    private static string? AttachNameKey(DataCore core, DataRecord record, int attach)
    {
        if (attach < 0) return null;
        foreach (var component in core.PointerArray(record, "Components"))
        {
            if (component.StructIndex != attach) continue;
            var (def, field) = core.FieldAt(core.InstanceAt(component), attach, "AttachDef");
            if (def < 0 || field is null) return null;
            var (loc, locField) = core.FieldAt(def, field.StructIndex, "Localization");
            return loc < 0 || locField is null ? null : core.StringAt(loc, locField.StructIndex, "Name");
        }
        return null;
    }

    private static string? Localised(IReadOnlyDictionary<string, string> text, string? key)
    {
        if (key is not { Length: > 0 }) return null;
        var bare = key.TrimStart('@');

        if (text.TryGetValue(bare, out var english) && english.Length > 0) return english;
        if (text.TryGetValue(bare + ",P", out var variant) && variant.Length > 0) return variant;
        return null;
    }
}
