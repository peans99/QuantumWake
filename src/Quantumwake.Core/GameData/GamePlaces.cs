namespace Quantumwake.Core.GameData;

/// <summary>What the star map knows about one place.</summary>
/// <param name="Parent">The body it belongs to, or null for a system.</param>
/// <param name="Kind">The map's own icon for it: Moon, Station, Planet.</param>
/// <param name="Description">The paragraph the map shows, or null.</param>
/// <param name="Amenities">Services the map lists there.</param>
public sealed record GamePlace(
    string Name,
    string? Parent,
    string? Kind,
    string? Description,
    IReadOnlyList<string> Amenities);

/// <summary>
/// The star map's own account of a place, read from the install.
/// </summary>
/// <remarks>
/// <para>
/// Every <c>StarMapObject</c> carries a name and a description as localisation
/// keys, and the English behind them is the same text the map shows in game.
/// Measured against the community download on this install: it describes 1,361
/// places, this describes 1,344, and of the 1,294 both have 1,251 are word for
/// word.
/// </para>
/// <para>
/// Beside that it names a parent and lists amenities, neither of which the
/// download carried. 1,977 objects name a parent, which is the body tree the
/// game itself uses, and 257 list services — 22 distinct kinds, from Hospital
/// and Refinery to Landing Pad XL.
/// </para>
/// <para>
/// Most of the difference in description count is what gets thrown away here.
/// A great many objects have a description that is only their own name
/// repeated, or a key CIG have not filled in, and passing those through would
/// put "Downded Relay AC-652" in a card that promised a paragraph about it.
/// </para>
/// </remarks>
public static class GamePlaces
{
    /// <summary>Reads every mapped place, given an open blob and its English table.</summary>
    public static Dictionary<string, GamePlace> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var places = new Dictionary<string, GamePlace>(StringComparer.OrdinalIgnoreCase);

        var byId = new Dictionary<Guid, DataRecord>();
        foreach (var record in core.Records()) byId.TryAdd(record.Hash, record);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("StarMapObject.", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);

            if (Named(core, text, at, record.StructIndex) is not { } name) continue;

            var descriptionKey = core.StringAt(at, record.StructIndex, "description");

            var description =
                descriptionKey is { Length: > 0 }
                && text.TryGetValue(descriptionKey.TrimStart('@'), out var about)
                && !Worthless(name, about)
                    ? about
                    : null;

            string? parent = null;
            if (core.ReferenceAt(at, record.StructIndex, "parent") is { } parentId
                && byId.TryGetValue(parentId, out var parentRecord))
            {
                var parentAt = core.InstanceAt(parentRecord, parentRecord.VariantIndex);
                parent = Named(core, text, parentAt, parentRecord.StructIndex);
            }

            var amenities = new List<string>();
            foreach (var id in core.ReferenceArrayAt(at, record.StructIndex, "amenities"))
            {
                if (!byId.TryGetValue(id, out var amenity)) continue;

                var amenityAt = core.InstanceAt(amenity, amenity.VariantIndex);
                var key = core.StringAt(amenityAt, amenity.StructIndex, "displayName");

                var label = key is { Length: > 0 } && text.TryGetValue(key.TrimStart('@'), out var shown)
                    ? shown
                    : core.StringAt(amenityAt, amenity.StructIndex, "name");

                if (label is { Length: > 0 } && !amenities.Contains(label)) amenities.Add(label);
            }

            var place = new GamePlace(
                name, parent, core.EnumAt(at, record.StructIndex, "navIcon"), description, amenities);

            // The same name can appear more than once; the entry that actually
            // says something is the one worth keeping.
            if (!places.TryGetValue(name, out var existing) || Thinner(existing, place))
                places[name] = place;
        }

        return places;
    }

    /// <summary>The English name of a map object, or null when it has none worth using.</summary>
    private static string? Named(
        DataCore core, IReadOnlyDictionary<string, string> text, long instance, int structIndex)
    {
        var key = core.StringAt(instance, structIndex, "name");
        if (key is not { Length: > 0 }) return null;

        if (!text.TryGetValue(key.TrimStart('@'), out var name) || name.Length == 0) return null;

        return name.Contains("UNINITIALIZED", StringComparison.OrdinalIgnoreCase)
            || name.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
            ? null
            : name;
    }

    /// <summary>Whether the one already kept says less than the one just read.</summary>
    private static bool Thinner(GamePlace kept, GamePlace found) =>
        (kept.Description is null && found.Description is not null)
        || (kept.Amenities.Count == 0 && found.Amenities.Count > 0);

    /// <summary>
    /// Whether a description says anything the name did not.
    /// </summary>
    /// <remarks>
    /// Twenty characters is the shortest thing here that reads as a sentence.
    /// Below that they are labels, and a card that opens to show a label is
    /// worse than one that says it has nothing. Public because it is the whole
    /// judgement in this class, and the blob it would otherwise be tested
    /// through is 316 MB of proprietary data.
    /// </remarks>
    public static bool Worthless(string name, string description) =>
        description.Length < 20
        || description.Equals(name, StringComparison.OrdinalIgnoreCase)
        || description.Contains("UNINITIALIZED", StringComparison.OrdinalIgnoreCase)
        || description.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
}
