namespace Quantumwake.Core.GameData;

/// <summary>
/// The star map's own paragraph about a place, read from the install.
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
/// Most of the difference is what gets thrown away here. A great many objects
/// have a description that is only their own name repeated, or a key CIG have
/// not filled in, and passing those through would put "Downded Relay AC-652" in
/// a card that promised a paragraph about it.
/// </para>
/// </remarks>
public static class GameLore
{
    /// <summary>Reads every place description, given an open blob and its English table.</summary>
    public static Dictionary<string, string> Read(
        DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var lore = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var record in core.Records())
        {
            if (!record.Name.StartsWith("StarMapObject.", StringComparison.OrdinalIgnoreCase))
                continue;

            var at = core.InstanceAt(record, record.VariantIndex);

            var nameKey = core.StringAt(at, record.StructIndex, "name");
            var descriptionKey = core.StringAt(at, record.StructIndex, "description");

            if (nameKey is not { Length: > 0 } || descriptionKey is not { Length: > 0 }) continue;
            if (!text.TryGetValue(nameKey.TrimStart('@'), out var name)) continue;
            if (!text.TryGetValue(descriptionKey.TrimStart('@'), out var description)) continue;

            if (Worthless(name, description)) continue;

            lore.TryAdd(name, description);
        }

        return lore;
    }

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
