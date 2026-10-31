namespace Quantumwake.Core.GameData;

/// <summary>One hull's cargo grids as the install places them.</summary>
/// <param name="Spaceship">False for a ground vehicle, which Cargo fit's list of hulls leaves out.</param>
public sealed record InstallCargoHull(bool Spaceship, List<CargoGrid> Grids)
{
    /// <summary>The capacity: whole cells, the way the game counts SCU.</summary>
    public double Scu => Grids.Sum(g => g.Scu);
}

/// <summary>
/// Every hull's cargo grids, as its own default loadout places them.
/// </summary>
/// <remarks>
/// <para>
/// docs/datacore.md once recorded the count of each grid a hull carries as
/// out of reach - "a grid record is a type, and a ship may place it more than
/// once", with the count assumed to live in the encrypted vehicle
/// definitions. It does not. The ship's <c>SEntityComponentDefaultLoadoutParams</c>
/// names a port per grid: the Freelancer's <c>hardpoint_cargogrid_mid_left</c>
/// and <c>_mid_right</c> both hold <c>MISC_Freelancer_CargoGrid_Mid</c>, which
/// is the 6 SCU the type-sum missed, and the Spirit C1 places its one grid on
/// a left and a right port. So the install answers alone, without the
/// community dump Cargo fit used to need.
/// </para>
/// <para>
/// A grid is an item whose <c>AttachDef.Type</c> is <c>CargoGrid</c> - not one
/// carrying <c>SCItemCargoGridParams</c>, which no grid on this install has. Its
/// <c>SCItemInventoryContainerComponentParams.containerParams</c> names an
/// <c>InventoryContainer</c> whose <c>interiorDimensions</c> are the inside in
/// metres - 1.25 m cells - and whose open container type gives the smallest
/// and largest box it accepts and whether it hangs outside the hull.
/// </para>
/// </remarks>
public static class GameCargoGrids
{
    private const double Cell = 1.25;

    /// <summary>Grids by hull class, for every hull whose loadout places one.</summary>
    public static Dictionary<string, InstallCargoHull> Read(DataCore core)
    {
        var byHash = new Dictionary<Guid, DataRecord>();
        var byClass = new Dictionary<string, DataRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in core.Records())
        {
            byHash.TryAdd(record.Hash, record);
            if (record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal))
                byClass.TryAdd(record.Name["EntityClassDefinition.".Length..], record);
        }

        var loadoutComponent = core.StructIndexOf("SEntityComponentDefaultLoadoutParams");
        var attachComponent = core.StructIndexOf("SAttachableComponentParams");
        var containerComponent = core.StructIndexOf("SCItemInventoryContainerComponentParams");
        if (loadoutComponent < 0 || attachComponent < 0 || containerComponent < 0) return [];

        var grids = new Dictionary<string, CargoGrid?>(StringComparer.OrdinalIgnoreCase);

        CargoGrid? Grid(string cls)
        {
            if (grids.TryGetValue(cls, out var known)) return known;
            CargoGrid? grid = null;
            if (byClass.TryGetValue(cls, out var record)
                && !cls.Contains("Template", StringComparison.OrdinalIgnoreCase))
                grid = ReadGrid(core, record, attachComponent, containerComponent, byHash);
            grids[cls] = grid;
            return grid;
        }

        var result = new Dictionary<string, InstallCargoHull>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in byClass.Values)
        {
            if (!record.FileName.Contains("/entities/spaceships/", StringComparison.OrdinalIgnoreCase)
                && !record.FileName.Contains("/entities/groundvehicles/", StringComparison.OrdinalIgnoreCase)) continue;

            var hull = record.Name["EntityClassDefinition.".Length..];
            // A template is a base the real hulls inherit from, not a ship.
            if (hull.EndsWith("_Template", StringComparison.OrdinalIgnoreCase)) continue;

            var placed = new List<CargoGrid>();
            Place(record, placed, [], 0);
            if (placed.Count > 0)
                result[hull] = new InstallCargoHull(
                    record.FileName.Contains("/entities/spaceships/", StringComparison.OrdinalIgnoreCase), placed);
        }

        return result;

        // A part carries its own default loadout, and that is where a lift or
        // a rack keeps its grid: the 400i's cargo lift places ORIG_400i_CargoGrid
        // on its own Cargo_Grid port, and the hull's loadout only names the lift.
        void Place(DataRecord entity, List<CargoGrid> into, HashSet<string> path, int depth)
        {
            if (depth > 6) return;
            foreach (var component in core.PointerArray(entity, "Components"))
            {
                if (component.StructIndex != loadoutComponent) continue;
                if (core.PointerAt(core.InstanceAt(component), component.StructIndex, "loadout") is not { } loadout) continue;

                Walk(core, byHash, loadout, cls =>
                {
                    if (Grid(cls) is { } g) { into.Add(g); return; }
                    // Into the part's own loadout, once per path: a part that
                    // names itself would otherwise never end.
                    if (byClass.TryGetValue(cls, out var part) && path.Add(cls))
                    {
                        Place(part, into, path, depth + 1);
                        path.Remove(cls);
                    }
                }, 0);
            }
        }
    }

    /// <summary>The cargo capacity of a set of grids: whole cells, the way the game counts SCU.</summary>
    public static double Scu(IEnumerable<CargoGrid> grids) => grids.Sum(g => g.Scu);

    private static CargoGrid? ReadGrid(
        DataCore core, DataRecord record, int attachComponent, int containerComponent, Dictionary<Guid, DataRecord> byHash)
    {
        var isGrid = false;
        DataRecord? container = null;

        foreach (var component in core.PointerArray(record, "Components"))
        {
            if (component.StructIndex == attachComponent)
            {
                var (def, field) = core.FieldAt(core.InstanceAt(component), attachComponent, "AttachDef");
                isGrid = def >= 0 && field is not null && core.EnumAt(def, field.StructIndex, "Type") == "CargoGrid";
            }
            else if (component.StructIndex == containerComponent
                && core.ReferenceAt(core.InstanceAt(component), containerComponent, "containerParams") is { } id
                && byHash.TryGetValue(id, out var found))
                container = found;
        }

        if (!isGrid || container is null) return null;

        var at = core.InstanceAt(container, container.VariantIndex);
        var inside = Vec(core, at, container.StructIndex, "interiorDimensions");
        if (inside.X < Cell || inside.Y < Cell || inside.Z < Cell) return null;

        Vec3 min = new(0, 0, 0), max = new(0, 0, 0);
        var external = false;
        if (core.PointerAt(at, container.StructIndex, "inventoryType") is { } type
            && core.StructName(type.StructIndex) == "InventoryOpenContainerType")
        {
            var t = core.InstanceAt(type);
            min = Vec(core, t, type.StructIndex, "minPermittedItemSize");
            max = Vec(core, t, type.StructIndex, "maxPermittedItemSize");
            external = core.BoolAt(t, type.StructIndex, "isExternalContainer") == true;
        }

        var cls = record.Name["EntityClassDefinition.".Length..];
        var scu = Cells(inside.X) * Cells(inside.Y) * Cells(inside.Z);

        return new CargoGrid(cls, record.Hash.ToString(), inside.X, inside.Y, inside.Z, scu, min, max, external);
    }

    // Whole cells, forgiving the float a 3.75 is stored as.
    private static int Cells(double metres) => (int)Math.Floor(metres / Cell + 1e-3);

    private static Vec3 Vec(DataCore core, long at, int structIndex, string name)
    {
        var (field, info) = core.FieldAt(at, structIndex, name);
        if (field < 0 || info is null) return new Vec3(0, 0, 0);
        return new Vec3(
            Math.Round(core.SingleAt(field, info.StructIndex, "x") ?? 0, 3),
            Math.Round(core.SingleAt(field, info.StructIndex, "y") ?? 0, 3),
            Math.Round(core.SingleAt(field, info.StructIndex, "z") ?? 0, 3));
    }

    /// <summary>Every class a manual loadout fits, through nested ports - a grid inside a module.</summary>
    private static void Walk(DataCore core, Dictionary<Guid, DataRecord> byHash, DataCore.Pointer loadout, Action<string> visit, int depth)
    {
        if (depth > 8) return;
        var at = core.InstanceAt(loadout);
        if (core.StructName(loadout.StructIndex) != "SItemPortLoadoutManualParams") return;

        foreach (var entry in core.ClassArrayAt(at, loadout.StructIndex, "entries"))
        {
            var e = core.InstanceAt(entry);
            var item = core.StringAt(e, entry.StructIndex, "entityClassName");
            if (item is not { Length: > 0 }
                && core.ReferenceAt(e, entry.StructIndex, "entityClassReference") is { } id
                && byHash.TryGetValue(id, out var referenced))
                item = referenced.Name[(referenced.Name.IndexOf('.') + 1)..];

            if (item is { Length: > 0 }) visit(item);
            if (core.PointerAt(e, entry.StructIndex, "loadout") is { } nested)
                Walk(core, byHash, nested, visit, depth + 1);
        }
    }
}
