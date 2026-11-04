using System.Collections.Concurrent;
using System.IO.Compression;
using System.Xml.Linq;

namespace Quantumwake.Core.GameData;

/// <summary>
/// One place a loot preset is set down: a star-map location, or a spot on a
/// body that the map does not mark.
/// </summary>
/// <param name="Name">The star map's name for it, or null for an unmarked spot - most of them caves.</param>
/// <param name="Body">The planet or moon it is on, as the map names it, when known.</param>
/// <param name="Spots">
/// How many placed crate sets use the preset there, counting each building
/// module once however many times the place repeats it. A floor.
/// </param>
/// <param name="Id">
/// The star map record behind it - <c>Stanton1_UGF_HDRSO-Olliff</c> - or, for
/// an unmarked spot, the body's: the same ids the logs name destinations by.
/// </param>
public sealed record LootPlace(string? Name, string? Body, int Spots, string? Id = null);

/// <summary>
/// Where in the universe each loot preset is placed, read from the map files.
/// </summary>
/// <remarks>
/// <para>
/// The DataCore says what a preset rolls but not where it is used. Placement
/// lives in the object containers - the <c>.socpak</c> zips under
/// <c>Data\ObjectContainers</c>, 9,668 of them on this install. An entity that
/// carries loot has its preset set per instance, in that zip's
/// <c>entdata/*.entxml</c>, as <c>HarvestableComponent</c> &#8594;
/// <c>looseSubConfigBase</c> &#8594; a <c>subConfigRef</c> or
/// <c>multiConfigRef</c>. Only three entity classes carry a default preset
/// (Fleet Week shipping containers), so instance data is the whole picture.
/// </para>
/// <para>
/// Each zip's own XML lists the containers it places. A planet's lists
/// thousands, grouped under location entities, and the ones the star map shows
/// carry a <c>starMapRecord</c>: that is how "final_s1_dc_dpre_imf" becomes a
/// named distribution centre. A module is walked upward until it meets one;
/// one that reaches its planet without meeting one is an unmarked spot on that
/// planet - caves, on this install, nearly all of them.
/// </para>
/// <para>
/// The first probe of this read every byte of every zip and took 45 minutes.
/// The zips are stored uncompressed inside the archive, so here each is opened
/// as a window onto the archive file and only its XML and instance data are
/// read: a few seconds, once per game build, then cached.
/// </para>
/// </remarks>
public static class GameLootPlaces
{
    private const string Folder = @"Data\ObjectContainers\";

    /// <summary>One container placed inside another, and the map marker it sits under there.</summary>
    private readonly record struct Edge(string Parent, Guid? Marker);

    /// <summary>Who a place is, before it has a name: a map marker, or a body with no marker.</summary>
    internal readonly record struct PlaceKey(Guid? Marker, string? BodyFile);

    /// <summary>What one container holds: where it is placed, and the presets set down in it.</summary>
    private sealed record Scan(string Path, List<(string Child, Guid? Marker)> Children, Dictionary<Guid, int> Presets);

    /// <summary>
    /// Every preset's places, keyed by the preset's record name without its
    /// type prefix - the same key <see cref="LootSource.Preset"/> carries.
    /// </summary>
    public static Dictionary<string, List<LootPlace>> Read(
        P4kArchive p4k, DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var scans = ScanAll(p4k);
        return Resolve(scans, core, text);
    }

    private static List<Scan> ScanAll(P4kArchive p4k)
    {
        var entries = p4k.Locate(Folder)
            .Where(e => e.Path.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var scans = new ConcurrentBag<Scan>();

        Parallel.ForEach(
            entries,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount, 1, 8) },
            entry =>
            {
                try
                {
                    if (ScanOne(p4k, entry) is { } scan) scans.Add(scan);
                }
                catch (Exception e) when (e is InvalidDataException or IOException or System.Xml.XmlException)
                {
                    // One unreadable zip costs the places under it, not the read.
                }
            });

        return [.. scans];
    }

    private static Scan? ScanOne(P4kArchive p4k, P4kEntry entry)
    {
        using var window = p4k.OpenStored(entry);
        if (window is null) return null;

        using var zip = new ZipArchive(window, ZipArchiveMode.Read);

        var children = new List<(string, Guid?)>();
        var presets = new Dictionary<Guid, int>();

        foreach (var file in zip.Entries)
        {
            var name = file.FullName;

            if (!name.Contains('/')
                && name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                && !name.EndsWith("_editor.xml", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = file.Open();
                children.AddRange(Children(XDocument.Load(stream)));
            }
            else if (name.EndsWith(".entxml", StringComparison.OrdinalIgnoreCase)
                && name.Contains("/entdata/", StringComparison.OrdinalIgnoreCase))
            {
                using var stream = file.Open();
                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);

                foreach (var id in PresetsSet(buffer.ToArray()))
                    presets[id] = presets.GetValueOrDefault(id) + 1;
            }
        }

        return new Scan(Normalise(entry.Path), children, presets);
    }

    /// <summary>
    /// The containers an object container places, each with the nearest map
    /// marker it sits under in this file.
    /// </summary>
    internal static IEnumerable<(string Child, Guid? Marker)> Children(XDocument doc)
    {
        foreach (var child in doc.Descendants("Child"))
        {
            if (child.Attribute("name")?.Value is not { Length: > 0 } path) continue;

            Guid? marker = null;
            foreach (var at in child.AncestorsAndSelf("Child"))
            {
                if (Guid.TryParse(at.Attribute("starMapRecord")?.Value, out var id) && id != Guid.Empty)
                {
                    marker = id;
                    break;
                }
            }

            yield return (Normalise(path), marker);
        }
    }

    /// <summary>The loot presets one entity's instance data sets, if any.</summary>
    internal static IEnumerable<Guid> PresetsSet(byte[] entxml)
    {
        XDocument doc;
        try
        {
            doc = CryXml.IsBinary(entxml)
                ? CryXml.Parse(entxml)
                : XDocument.Parse(System.Text.Encoding.UTF8.GetString(entxml));
        }
        catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException or ArgumentException or IndexOutOfRangeException)
        {
            yield break;
        }

        foreach (var harvestable in doc.Descendants("HarvestableComponent"))
            foreach (var element in harvestable.DescendantsAndSelf())
                foreach (var name in (string[])["subConfigRef", "multiConfigRef"])
                    if (Guid.TryParse(element.Attribute(name)?.Value, out var id) && id != Guid.Empty)
                        yield return id;
    }

    /// <summary>Paths as the child lists spell them and as the archive does, made comparable.</summary>
    internal static string Normalise(string path)
    {
        var p = path.Replace('/', '\\').ToLowerInvariant();
        return p.StartsWith(@"data\", StringComparison.Ordinal) ? p : @"data\" + p;
    }

    private static Dictionary<string, List<LootPlace>> Resolve(
        List<Scan> scans, DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var byHash = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byHash.TryAdd(record.Hash, record);

        // Star map objects by id, and the bodies by the file stem their
        // containers are named for: stanton1.socpak is StarMapObject.Stanton1.
        var markers = new Dictionary<Guid, (string? Name, Guid? Parent, string Id)>();
        var bodies = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("StarMapObject.", StringComparison.Ordinal)) continue;

            var at = core.InstanceAt(record, record.VariantIndex);
            var key = core.StringAt(at, record.StructIndex, "name");
            var name = key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var shown)
                && shown.Length > 0 && !shown.Contains("UNINITIALIZED", StringComparison.OrdinalIgnoreCase)
                    ? shown.Trim()
                    : null;

            var id = record.Name["StarMapObject.".Length..];
            markers.TryAdd(record.Hash, (name, core.ReferenceAt(at, record.StructIndex, "parent"), id));
            bodies.TryAdd(id, record.Hash);
        }

        var PlacesOf = Walker(
            scans.Select(s => (s.Path, (IEnumerable<(string, Guid?)>)s.Children)),
            markers.ContainsKey,
            path => bodies.ContainsKey(P4kArchive.EntryStem(path)) ? P4kArchive.EntryStem(path) : null);

        var spots = new Dictionary<(Guid Preset, PlaceKey Place), int>();
        foreach (var scan in scans)
        {
            if (scan.Presets.Count == 0) continue;
            var places = PlacesOf(scan.Path);

            foreach (var (preset, count) in scan.Presets)
                foreach (var place in places)
                    spots[(preset, place)] = spots.GetValueOrDefault((preset, place)) + count;
        }

        // A multi-slot preset can lend a slot group to another record by
        // reference, and that record's own rows on the page are the same
        // crates in the same places.
        foreach (var (multi, singles) in Lends(core, byHash))
        {
            var lent = spots.Where(s => s.Key.Preset == multi).ToList();
            foreach (var single in singles)
                foreach (var (key, count) in lent)
                    spots[(single, key.Place)] = spots.GetValueOrDefault((single, key.Place)) + count;
        }

        string? NameOf(Guid id) => markers.TryGetValue(id, out var m) ? m.Name : null;

        // An unnamed marker counts as an unmarked spot on its body, so it
        // takes the body's id as well as its name.
        string? IdOf(PlaceKey place) =>
            place.Marker is { } m && NameOf(m) is not null ? markers[m].Id
            : place.BodyFile is { } file ? markers[bodies[file]].Id
            : place.Marker is { } u && markers[u].Parent is { } parent && markers.TryGetValue(parent, out var p) ? p.Id
            : null;

        string? BodyName(PlaceKey place)
        {
            if (place.BodyFile is { } file) return NameOf(bodies[file]);

            // A marker's parent is its body for nearly everything; a station
            // or a Lagrange point names its own parent, which is still the
            // most useful thing to put beside it.
            return place.Marker is { } id && markers[id].Parent is { } parent ? NameOf(parent) : null;
        }

        var result = new Dictionary<string, List<LootPlace>>(StringComparer.Ordinal);
        foreach (var group in spots.GroupBy(s => s.Key.Preset))
        {
            if (!byHash.TryGetValue(group.Key, out var record)) continue;

            var preset = record.Name[(record.Name.IndexOf('.') + 1)..];

            var places = Merge(group
                .Select(s => new LootPlace(
                    s.Key.Place.Marker is { } m ? NameOf(m) : null,
                    BodyName(s.Key.Place),
                    s.Value,
                    IdOf(s.Key.Place))));

            if (places.Count > 0) result[preset] = places;
        }

        return result;
    }

    /// <summary>
    /// One row per place a pilot would name: the spots of every marker that
    /// shares a name and a body, summed.
    /// </summary>
    /// <remarks>
    /// A merged row still needs one star-map id, the one the logs name a
    /// destination by. It is the ordinally first of the group's, not the first
    /// met: the order places are met in is the archive's, and the LIVE delta
    /// patch of 2026-11-04 rebuilt that order without changing a byte of game
    /// data, which renamed two derelict outposts' example (Monox, 39 spots
    /// either way) and made a patch look like a change.
    /// </remarks>
    internal static List<LootPlace> Merge(IEnumerable<LootPlace> places)
    {
        return places
            // A marker the map leaves unnamed is still a spot on a body.
            .GroupBy(p => (p.Name, p.Body))
            .Select(g => new LootPlace(g.Key.Name, g.Key.Body, g.Sum(p => p.Spots),
                g.Select(p => p.Id).Where(id => id is not null).Order(StringComparer.Ordinal).FirstOrDefault()))
            .Where(p => p.Name is not null || p.Body is not null)
            .OrderBy(p => p.Name is null)
            .ThenBy(p => p.Body, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Every place a container ends up in: up through the containers that
    /// place it until a map marker, or until a body's own container, which
    /// makes it an unmarked spot on that body.
    /// </summary>
    /// <remarks>
    /// Memoised, because cave modules are reused across dozens of planets and
    /// moons. A container reached again while it is being walked - a loop,
    /// which no file on this install has - contributes nothing rather than
    /// recursing for ever.
    /// </remarks>
    internal static Func<string, HashSet<PlaceKey>> Walker(
        IEnumerable<(string Path, IEnumerable<(string Child, Guid? Marker)> Children)> containers,
        Func<Guid, bool> isMarker,
        Func<string, string?> bodyOf)
    {
        var parents = new Dictionary<string, List<Edge>>(StringComparer.Ordinal);
        foreach (var (path, children) in containers)
            foreach (var (child, marker) in children)
            {
                if (!parents.TryGetValue(child, out var list)) parents[child] = list = [];
                list.Add(new Edge(path, marker));
            }

        var memo = new Dictionary<string, HashSet<PlaceKey>>(StringComparer.Ordinal);
        var walking = new HashSet<string>(StringComparer.Ordinal);

        HashSet<PlaceKey> PlacesOf(string path)
        {
            if (memo.TryGetValue(path, out var known)) return known;

            var found = new HashSet<PlaceKey>();
            if (!walking.Add(path)) return found;

            foreach (var edge in parents.GetValueOrDefault(path) ?? [])
            {
                if (edge.Marker is { } marker && isMarker(marker))
                    found.Add(new PlaceKey(marker, null));
                else if (bodyOf(edge.Parent) is { } body)
                    found.Add(new PlaceKey(null, body));
                else
                    found.UnionWith(PlacesOf(edge.Parent));
            }

            walking.Remove(path);
            memo[path] = found;
            return found;
        }

        return PlacesOf;
    }

    /// <summary>Each multi-slot preset's referenced single presets.</summary>
    private static IEnumerable<(Guid Multi, List<Guid> Singles)> Lends(
        DataCore core, Dictionary<Guid, DataRecord> byHash)
    {
        foreach (var record in byHash.Values)
        {
            if (core.StructName(record.StructIndex) != "SubHarvestableMultiConfigRecord") continue;

            var (multi, field) = core.FieldAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "multiConfig");
            if (multi < 0 || field is null) continue;

            var singles = new List<Guid>();
            foreach (var tagged in core.ClassArrayAt(multi, field.StructIndex, "taggedConfigs"))
            {
                if (core.PointerAt(core.InstanceAt(tagged), tagged.StructIndex, "subConfig") is not { } sub) continue;
                if (core.StructName(sub.StructIndex) == "SubHarvestableConfigSingleManual") continue;
                if (core.ReferenceAt(core.InstanceAt(sub), sub.StructIndex, "subConfigRef") is { } id && !singles.Contains(id))
                    singles.Add(id);
            }

            if (singles.Count > 0) yield return (record.Hash, singles);
        }
    }
}
