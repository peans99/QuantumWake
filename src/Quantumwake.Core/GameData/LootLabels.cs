using System.Text.RegularExpressions;

namespace Quantumwake.Core.GameData;

/// <summary>
/// Readable names for the records loot comes from, which have none of their own.
/// </summary>
/// <remarks>
/// A slot preset is named for the kind of place that uses it -
/// <c>V3SlotPreset_DistributionCentres_HighSecurtiy</c>,
/// <c>LootGeneration_MultiSlotPreset_DerelictOutposts</c> - and until the
/// object containers are read that is the best answer to "where", so it is
/// shown as a kind of place and never as a place. The words are the record's
/// own with the machinery stripped; CIG's one misspelling is mended because a
/// label is not evidence and "Securtiy" on a page reads as ours.
/// </remarks>
public static partial class LootLabels
{
    private static readonly string[] PresetPrefixes =
    [
        "LootGeneration_MultiSlotPreset_", "LootGeneration_SlotPreset_", "V3SlotPreset_",
        "V3SubHarvestableConfig_", "SubHarvestableConfig_", "LootGeneration_", "LootArchetype_",
    ];

    /// <summary>"Distribution Centres · High Security" from the preset record name.</summary>
    public static string Preset(string preset)
    {
        var name = preset;
        foreach (var prefix in PresetPrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        return string.Join(" · ", name.Split('_', StringSplitOptions.RemoveEmptyEntries).Select(Words))
            .Replace("Securtiy", "Security", StringComparison.Ordinal);
    }

    /// <summary>
    /// "Prisoner" from <c>NPC_Archetypes_Male_Human_Civilians_Prisoner_13</c>,
    /// "Shattered Blade · soldier" from <c>Corpse_ShatteredBlade_soldier</c>.
    /// </summary>
    /// <remarks>
    /// The fourteen prisoner classes differ only by a number and a body, so
    /// the number and the archetype path are dropped and they read as one.
    /// </remarks>
    public static string Body(string entityClass)
    {
        var parts = entityClass.Split('_', StringSplitOptions.RemoveEmptyEntries).ToList();

        if (parts.Count > 0 && parts[0].Equals("NPC", StringComparison.OrdinalIgnoreCase))
            parts = parts.SkipWhile(p => p is "NPC" or "Archetypes" or "Male" or "Female" or "Human" or "Civilians" or "Combatants").ToList();
        else if (parts.Count > 0 && parts[0].Equals("Corpse", StringComparison.OrdinalIgnoreCase))
            parts.RemoveAt(0);

        parts = [.. parts.Where(p => !p.All(char.IsDigit))];
        return parts.Count == 0 ? entityClass : string.Join(" · ", parts.Select(Words));
    }

    private static string Words(string part) => WordBoundary().Replace(part, " ");

    [GeneratedRegex("(?<=[a-z])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])")]
    private static partial Regex WordBoundary();
}
