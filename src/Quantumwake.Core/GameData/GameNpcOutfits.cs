using System.Xml.Linq;

namespace Quantumwake.Core.GameData;

/// <summary>One kind of NPC that can be wearing or carrying an item.</summary>
/// <param name="Npc">The actor's entity class - <c>PU_Human-Hurston-Guard-Male-Heavy_01</c>.</param>
/// <param name="Share">
/// The share of that NPC's spawns with the item on them, if every outfit,
/// kit and alternative is picked evenly. The files list the choices but not
/// how a spawner makes them, so this is an estimate and labelled one.
/// </param>
public sealed record NpcWear(string Npc, double Share);

/// <summary>
/// What NPCs on foot wear and carry, from their loadouts.
/// </summary>
/// <remarks>
/// <para>
/// An actor's default loadout is a tree of choices. An outfit loadout has a
/// base everyone gets and alternative outfits picked by tag; a random one
/// names assortments (<c>SLoadoutAssortment</c>, 321 records), each grouping
/// alternatives by slot - Body, Outfit, PrimaryWeapon. The leaves are almost
/// all loadout files under <c>Data\Scripts\Loadouts</c>: 73,767 references
/// against 9,260 items written into the DataCore, and 1,864 of 2,312 actor
/// classes name nothing else. Those files are stored encrypted; they are read
/// through <see cref="P4kArchive.TryReadDecrypting"/>.
/// </para>
/// <para>
/// Every choice is taken as even: a guard whose assortment lists three
/// armour sets wears each a third of the time. Groups with the same slot name
/// and different requirements - male and female bodies - are alternatives to
/// one another; groups with different slot names all apply.
/// </para>
/// </remarks>
public static class GameNpcOutfits
{
    private const string Folder = @"Data\Scripts\Loadouts\";

    /// <summary>
    /// The kinds of item a body yields that are worth listing. Bodies, heads,
    /// hair and the mobiGlas are the character, not loot.
    /// </summary>
    public static bool Kept(string? type) =>
        type is not null
        && (type.StartsWith("Char_Armor", StringComparison.OrdinalIgnoreCase)
            || type.StartsWith("Char_Clothing", StringComparison.OrdinalIgnoreCase)
            || type is "WeaponPersonal" or "WeaponAttachment" or "FPS_Consumable" or "Gadget"
                or "Grenade" or "Magazine" or "Food" or "Drink");

    /// <summary>Every kept item NPCs wear, by item class, each with the NPC kinds that wear it.</summary>
    /// <param name="typeOf">An item class's type, or null when unknown.</param>
    public static Dictionary<string, List<NpcWear>> Read(P4kArchive p4k, DataCore core, Func<string, string?> typeOf)
    {
        var files = new Dictionary<string, P4kEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in p4k.Locate(Folder)) files.TryAdd(entry.Path, entry);

        var parsed = new Dictionary<string, List<string>?>(StringComparer.OrdinalIgnoreCase);

        List<string>? ItemsIn(string path)
        {
            var full = @"Data\" + path.Replace('/', '\\').TrimStart('\\');
            if (parsed.TryGetValue(full, out var known)) return known;

            List<string>? items = null;
            if (files.TryGetValue(full, out var entry) && p4k.TryReadDecrypting(entry) is { } bytes)
                items = ItemsOf(bytes);

            parsed[full] = items;
            return items;
        }

        var byHash = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byHash.TryAdd(record.Hash, record);

        var loadoutComponent = core.StructIndexOf("SEntityComponentDefaultLoadoutParams");
        var found = new Dictionary<string, Dictionary<string, double>>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("EntityClassDefinition.", StringComparison.Ordinal)) continue;
            if (!record.FileName.Contains("/actor/actors/", StringComparison.OrdinalIgnoreCase)) continue;

            var npc = record.Name["EntityClassDefinition.".Length..];
            if (IsTestRig(npc)) continue;
            var shares = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            var walker = new Walker(core, byHash, ItemsIn, shares);

            foreach (var component in core.PointerArray(record, "Components"))
            {
                if (component.StructIndex != loadoutComponent) continue;
                if (core.PointerAt(core.InstanceAt(component), component.StructIndex, "loadout") is { } loadout)
                    walker.Walk(loadout, 1, 0);
            }

            foreach (var (item, share) in shares)
            {
                if (!Kept(typeOf(item))) continue;
                if (!found.TryGetValue(item, out var npcs)) found[item] = npcs = new(StringComparer.Ordinal);
                npcs[npc] = Math.Min(1, share);
            }
        }

        return found.ToDictionary(
            f => f.Key,
            f => f.Value.Select(n => new NpcWear(n.Key, n.Value))
                .OrderByDescending(n => n.Share).ThenBy(n => n.Npc, StringComparer.Ordinal).ToList(),
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Actor classes that are test rigs rather than anybody met in the game:
    /// <c>DummyPlayer</c>, <c>FeatureTest_Human_Male_Rifle</c>,
    /// <c>PU_Human-Test-Guild-Criminal</c>, the "Behavior Zoo Testmap"
    /// vendors. Judged on whole parts of the name, so Contestedzones - which
    /// has "test" inside it - is kept.
    /// </summary>
    public static bool IsTestRig(string actorClass) =>
        actorClass.Split(['_', '-'], StringSplitOptions.RemoveEmptyEntries).Any(part =>
            part is "Test" or "DummyPlayer"
            || part.StartsWith("FeatureTest", StringComparison.Ordinal)
            || part.EndsWith("Test", StringComparison.Ordinal)
            || part.EndsWith("Testmap", StringComparison.Ordinal));

    /// <summary>Every item a loadout file names, at any depth.</summary>
    internal static List<string>? ItemsOf(byte[] bytes)
    {
        try
        {
            var doc = CryXml.IsBinary(bytes)
                ? CryXml.Parse(bytes)
                : XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes).TrimStart('﻿'));

            return [.. doc.Descendants("Item")
                .Select(i => i.Attribute("itemName")?.Value)
                .OfType<string>()
                .Where(n => n.Length > 0)];
        }
        catch (Exception e) when (e is System.Xml.XmlException or InvalidDataException or ArgumentException or IndexOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Walks one actor's loadout tree, sharing each branch's weight among its alternatives.</summary>
    private sealed class Walker(
        DataCore core,
        Dictionary<Guid, DataRecord> byHash,
        Func<string, List<string>?> itemsIn,
        Dictionary<string, double> shares)
    {
        private void Add(string item, double weight) =>
            shares[item] = shares.GetValueOrDefault(item) + weight;

        public void Walk(DataCore.Pointer loadout, double weight, int depth)
        {
            // Nested loadouts go body, undersuit, armour, attachment; the bound
            // is against a loadout that reaches itself.
            if (depth > 10 || weight <= 0) return;

            var at = core.InstanceAt(loadout);
            switch (core.StructName(loadout.StructIndex))
            {
                case "SItemPortLoadoutManualParams":
                    foreach (var entry in core.ClassArrayAt(at, loadout.StructIndex, "entries"))
                    {
                        var e = core.InstanceAt(entry);
                        var item = core.StringAt(e, entry.StructIndex, "entityClassName");
                        if (item is not { Length: > 0 }
                            && core.ReferenceAt(e, entry.StructIndex, "entityClassReference") is { } id
                            && byHash.TryGetValue(id, out var referenced))
                            item = referenced.Name[(referenced.Name.IndexOf('.') + 1)..];

                        if (item is { Length: > 0 }) Add(item, weight);
                        if (core.PointerAt(e, entry.StructIndex, "loadout") is { } nested)
                            Walk(nested, weight, depth + 1);
                    }
                    Inventory(at, loadout.StructIndex, weight);
                    break;

                case "SItemPortLoadoutXMLParams":
                    if (core.StringAt(at, loadout.StructIndex, "loadoutPath") is { Length: > 0 } path
                        && itemsIn(path) is { } items)
                        foreach (var item in items) Add(item, weight);
                    Inventory(at, loadout.StructIndex, weight);
                    break;

                case "SItemPortLoadoutRecordParams":
                    if (core.ReferenceAt(at, loadout.StructIndex, "loadoutRecord") is { } recordId
                        && byHash.TryGetValue(recordId, out var record)
                        && core.PointerAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "loadout") is { } inner)
                        Walk(inner, weight, depth + 1);
                    break;

                case "SItemPortLoadoutOutfitParams":
                {
                    if (core.PointerAt(at, loadout.StructIndex, "BaseLoadout") is { } baseLoadout)
                        Walk(baseLoadout, weight, depth + 1);

                    // An outfit is picked by tag, and nothing here says which
                    // tags a spawner passes: each counts the same.
                    var outfits = core.ClassArrayAt(at, loadout.StructIndex, "Outfits");
                    foreach (var outfit in outfits)
                    {
                        var choices = core.PointerArrayAt(core.InstanceAt(outfit), outfit.StructIndex, "Loadouts");
                        foreach (var choice in choices)
                            Walk(choice, weight / outfits.Count / choices.Count, depth + 1);
                    }
                    Inventory(at, loadout.StructIndex, weight);
                    break;
                }

                case "SItemPortLoadoutRandomGeneratedParams":
                {
                    var assortments = core.ReferenceArrayAt(at, loadout.StructIndex, "LoadoutAssortments")
                        .Where(byHash.ContainsKey).ToList();
                    foreach (var id in assortments)
                        Assortment(byHash[id], weight / assortments.Count, depth + 1);
                    Inventory(at, loadout.StructIndex, weight);
                    break;
                }
            }
        }

        /// <summary>
        /// One assortment: every slot applies, and within a slot the groups
        /// that share its name are alternatives, as are each group's loadouts.
        /// </summary>
        private void Assortment(DataRecord record, double weight, int depth)
        {
            var at = core.InstanceAt(record, record.VariantIndex);
            var groups = core.ClassArrayAt(at, record.StructIndex, "GroupedLoadouts")
                .Select(g => (Pointer: g, Slot: core.EnumAt(core.InstanceAt(g), g.StructIndex, "Group") ?? ""))
                .GroupBy(g => g.Slot);

            foreach (var slot in groups)
            {
                var alternatives = slot.ToList();
                foreach (var (group, _) in alternatives)
                {
                    var choices = core.PointerArrayAt(core.InstanceAt(group), group.StructIndex, "Loadouts");
                    foreach (var choice in choices)
                        Walk(choice, weight / alternatives.Count / choices.Count, depth + 1);
                }
            }
        }

        /// <summary>Items put in the NPC's inventory rather than on a port: spare magazines, medpens.</summary>
        private void Inventory(long at, int structIndex, double weight)
        {
            foreach (var item in core.ClassArrayAt(at, structIndex, "InventoryItems"))
            {
                var i = core.InstanceAt(item);
                var name = core.StringAt(i, item.StructIndex, "entityClassName");
                if (name is not { Length: > 0 }
                    && core.ReferenceAt(i, item.StructIndex, "entityClassReference") is { } id
                    && byHash.TryGetValue(id, out var referenced))
                    name = referenced.Name[(referenced.Name.IndexOf('.') + 1)..];

                if (name is { Length: > 0 }) Add(name, weight);
            }
        }
    }
}
