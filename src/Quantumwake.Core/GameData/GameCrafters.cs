using System.IO.Compression;
using System.Text;

namespace Quantumwake.Core.GameData;

/// <summary>A crafter that makes some blueprints faster, and the ships it is fitted to.</summary>
/// <param name="Class">The crafter's entity class, <c>BaseBuilding_Interactables_ItemFabricator_Vehicle_ANVL_Carrack</c>.</param>
/// <param name="TimeMultiplier">What the craft time is multiplied by: 0.7 is 30% faster.</param>
/// <param name="Categories">The blueprint categories it applies to, bare: <c>FPSArmours</c>. Empty when not limited by category.</param>
/// <param name="Blueprints">The blueprint records it applies to, bare. Empty when not limited by blueprint.</param>
/// <param name="Ships">The player ships that carry it, as the catalogue names them.</param>
public sealed record GameCraftSpeedup(
    string Class, double TimeMultiplier, IReadOnlyList<string> Categories, IReadOnlyList<string> Blueprints,
    IReadOnlyList<string> Ships);

/// <summary>
/// The crafters 5.0 gives their own speed, and where they are.
/// </summary>
/// <remarks>
/// <para>
/// 5.0 adds <c>baseCraftingTime</c> and <c>modifiers</c> to every
/// <c>CrafterComponentParams</c>. On the PTU (5.0.202.46035) there are five
/// crafters, all with a base of 1, and only the two new ship fabricators carry
/// a modifier: <c>CraftingModifierType_Time</c> 0.7 when the blueprint's
/// category is <c>FPSArmours</c>. Their category lists are the standard
/// fabricator's, so a ship's fabricator makes everything the standard one does
/// and armour in seven tenths of the time.
/// </para>
/// <para>
/// Which ships carry one is in two places. The Carrack's fabricator is a
/// default-loadout entry on the Carrack and the Carrack Expedition. The Drake
/// one is placed in the ships' interiors - the object containers under
/// <c>Data\ObjectContainers\Ships</c>, <c>DRAK\Clipper</c> and
/// <c>DRAK\ironclad_assault</c> - and nothing in the DataCore names it. A ship
/// is matched to its interior by the folder: <c>Ships\DRAK\Clipper</c> is every
/// <c>DRAK_Clipper</c> hull.
/// </para>
/// <para>
/// A <c>CraftingModifierCondition_ProcessTypes</c> condition is not read: no
/// crafter on the PTU uses one, and a blueprint's process is not yet kept.
/// </para>
/// </remarks>
public static class GameCrafters
{
    private const string ShipFolder = @"Data\ObjectContainers\Ships\";

    /// <summary>AI, mission and template hulls share their interiors with the player's, and are not ships a pilot owns.</summary>
    private static readonly string[] NotOwned = ["_AI_", "_PU_", "Unmanned", "Template", "Boarded", "_Wreck", "Derelict"];

    public static List<GameCraftSpeedup> Read(DataCore core, P4kArchive? p4k, IReadOnlyDictionary<string, GameVehicle> vehicles)
    {
        var crafterStruct = core.StructIndexOf("CrafterComponentParams");
        if (crafterStruct < 0) return [];

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        var faster = new List<(DataRecord Record, double Multiplier, List<string> Categories, List<string> Blueprints)>();

        foreach (var record in byId.Values)
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;

            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex != crafterStruct) continue;
                var at = core.InstanceAt(component);
                var basis = core.SingleAt(at, crafterStruct, "baseCraftingTime") ?? 1;

                foreach (var modifier in core.ClassArrayAt(at, crafterStruct, "modifiers"))
                {
                    var ma = core.InstanceAt(modifier);
                    if (core.PointerAt(ma, modifier.StructIndex, "modifierType") is not { } type
                        || core.StructName(type.StructIndex) != "CraftingModifierType_Time"
                        || core.SingleAt(core.InstanceAt(type), type.StructIndex, "timeMultiplier") is not { } multiplier)
                        continue;

                    List<string> categories = [], blueprints = [];
                    if (core.PointerAt(ma, modifier.StructIndex, "modifierCondition") is { } condition)
                    {
                        var ca = core.InstanceAt(condition);
                        switch (core.StructName(condition.StructIndex))
                        {
                            case "CraftingModifierCondition_BlueprintCategory":
                                categories = Names(byId, core.ReferenceArrayAt(ca, condition.StructIndex, "categories"));
                                break;
                            case "CraftingModifierCondition_BlueprintWhitelist":
                                blueprints = Names(byId, core.ReferenceArrayAt(ca, condition.StructIndex, "blueprintWhitelist"));
                                break;
                            default:
                                // A condition this does not read would make the
                                // speed-up look universal; leave it out instead.
                                continue;
                        }
                    }

                    faster.Add((record, basis * multiplier, categories, blueprints));
                }
            }
        }

        if (faster.Count == 0) return [];

        var carriers = Carriers(core, p4k, byId, faster.Select(f => f.Record).ToList(), vehicles);
        return [.. faster.Select(f => new GameCraftSpeedup(
            Bare(f.Record.Name), Math.Round(f.Multiplier, 3), f.Categories, f.Blueprints,
            carriers.GetValueOrDefault(f.Record.Hash) ?? []))];
    }

    /// <summary>Every owned ship that carries each crafter, by loadout or by interior.</summary>
    private static Dictionary<Guid, List<string>> Carriers(
        DataCore core, P4kArchive? p4k, Dictionary<Guid, DataRecord> byId, List<DataRecord> crafters,
        IReadOnlyDictionary<string, GameVehicle> vehicles)
    {
        var wanted = crafters.Select(c => c.Hash).ToHashSet();
        var classes = new Dictionary<Guid, HashSet<string>>();
        void Add(Guid crafter, string vehicle)
        {
            if (!classes.TryGetValue(crafter, out var set)) classes[crafter] = set = new(StringComparer.OrdinalIgnoreCase);
            set.Add(vehicle);
        }

        // Default loadouts: the Carrack's fabricator is an entry there.
        var entityByName = byId.Values
            .Where(r => r.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal))
            .GroupBy(r => Bare(r.Name), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        foreach (var vehicle in vehicles.Keys)
        {
            if (!entityByName.TryGetValue(vehicle, out var record)) continue;
            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (Loadout(core, core.InstanceAt(component), component.StructIndex) is { } loadout)
                    foreach (var id in LoadoutReferences(core, loadout.At, loadout.Struct, 0))
                        if (wanted.Contains(id)) Add(id, vehicle);
            }
        }

        // Interiors: an object container that places the crafter, matched to
        // the hulls named for its folder.
        if (p4k is not null)
        {
            var needles = crafters.Select(c => (c.Hash, Name: Bare(c.Name), Id: c.Hash.ToString())).ToList();
            foreach (var entry in p4k.Locate(ShipFolder).Where(e => e.Path.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase)))
            {
                List<Guid> found;
                try { found = Placed(p4k, entry, needles); }
                catch (Exception e) when (e is InvalidDataException or IOException) { continue; }
                if (found.Count == 0) continue;

                var parts = entry.Path[ShipFolder.Length..].Split('\\');
                if (parts.Length < 3) continue;
                var prefix = parts[0] + "_" + parts[1];
                foreach (var vehicle in vehicles.Keys.Where(v =>
                    v.Equals(prefix, StringComparison.OrdinalIgnoreCase)
                    || v.StartsWith(prefix + "_", StringComparison.OrdinalIgnoreCase)))
                    foreach (var id in found) Add(id, vehicle);
            }
        }

        return classes.ToDictionary(
            c => c.Key,
            c => c.Value
                .Where(v => !NotOwned.Any(n => v.Contains(n, StringComparison.OrdinalIgnoreCase)))
                .Select(v => vehicles.TryGetValue(v, out var known) ? known.Name : v)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private static IEnumerable<Guid> LoadoutReferences(DataCore core, long at, int structIndex, int depth)
    {
        if (depth > 6) yield break;
        foreach (var entry in core.ClassArrayAt(at, structIndex, "entries"))
        {
            var ea = core.InstanceAt(entry);
            if (core.ReferenceAt(ea, entry.StructIndex, "entityClassReference") is { } id) yield return id;
            if (Loadout(core, ea, entry.StructIndex) is { } nested)
                foreach (var inner in LoadoutReferences(core, nested.At, nested.Struct, depth + 1)) yield return inner;
        }
    }

    /// <summary>A <c>loadout</c> field, written inline on some structs and as a pointer to a manual loadout on others.</summary>
    private static (long At, int Struct)? Loadout(DataCore core, long at, int structIndex)
    {
        if (core.PointerAt(at, structIndex, "loadout") is { } pointer) return (core.InstanceAt(pointer), pointer.StructIndex);
        var (inline, field) = core.FieldAt(at, structIndex, "loadout");
        return inline >= 0 && field is not null && field.DataType == 0x0010 ? (inline, field.StructIndex) : null;
    }

    /// <summary>The crafters one object container places, by class name or record id anywhere in its XML.</summary>
    private static List<Guid> Placed(P4kArchive p4k, P4kEntry entry, List<(Guid Hash, string Name, string Id)> needles)
    {
        using var window = p4k.OpenStored(entry);
        if (window is null) return [];
        using var zip = new ZipArchive(window, ZipArchiveMode.Read);

        var found = new List<Guid>();
        foreach (var file in zip.Entries)
        {
            if (!file.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && !file.FullName.EndsWith(".entxml", StringComparison.OrdinalIgnoreCase)) continue;

            using var stream = file.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            var text = Encoding.UTF8.GetString(buffer.ToArray());

            foreach (var (hash, name, id) in needles)
                if (!found.Contains(hash)
                    && (text.Contains(name, StringComparison.OrdinalIgnoreCase) || text.Contains(id, StringComparison.OrdinalIgnoreCase)))
                    found.Add(hash);
        }
        return found;
    }

    private static List<string> Names(Dictionary<Guid, DataRecord> byId, IEnumerable<Guid> ids) =>
        [.. ids.Select(id => byId.TryGetValue(id, out var r) ? Bare(r.Name) : null).OfType<string>()];

    private static string Bare(string name) => name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
}
