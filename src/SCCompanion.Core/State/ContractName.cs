using System.Text.RegularExpressions;

namespace SCCompanion.Core.State;

/// <summary>A contract identifier decomposed into facets.</summary>
/// <param name="Raw">The original contract string.</param>
/// <param name="Issuer">Company or organisation, e.g. <c>Covalex</c>.</param>
/// <param name="System">Star system or scope, e.g. <c>Stanton</c>, <c>Interstellar</c>.</param>
/// <param name="Difficulty">Normalised difficulty, e.g. <c>Very Hard</c>.</param>
/// <param name="Type">Activity, e.g. <c>Recover Cargo</c>.</param>
public sealed record ContractName(
    string Raw,
    string Issuer,
    string? System,
    string? Difficulty,
    string? Type)
{
    /// <summary>Readable summary for display.</summary>
    public string DisplayName =>
        string.Join(" · ", new[] { Issuer, Type, Difficulty }.Where(p => !string.IsNullOrEmpty(p)));
}

/// <summary>
/// Decomposes contract identifiers into facets for filtering and charting.
/// </summary>
/// <remarks>
/// Contract strings are underscore-delimited and mostly follow
/// <c>Issuer_System_Difficulty_Type</c>, but not reliably:
/// <code>
/// Covalex_Stanton_VeryHard_RecoverCargo
/// Ling_Stanton_VeryEasy_RecoverCargo
/// FTL_Courier_Stanton_AmmoCrate_Rank0_2
/// GillysPilotSchool_Mission06_2
/// HaulCargo_AToB_Interstellar_Bulk_DistSp_Dia_FresFoo_Gol_Aphor
/// </code>
/// So rather than assuming positions, known difficulty and system tokens are
/// recognised wherever they appear and the remainder is treated as the type.
/// 112 distinct contract strings were observed in the backfill.
/// </remarks>
public static partial class ContractNameParser
{
    private static readonly Dictionary<string, string> Difficulties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["VeryEasy"] = "Very Easy",
        ["Easy"] = "Easy",
        ["Medium"] = "Medium",
        ["Normal"] = "Medium",
        ["Hard"] = "Hard",
        ["VeryHard"] = "Very Hard",
        ["Extreme"] = "Extreme"
    };

    private static readonly HashSet<string> Systems = new(StringComparer.OrdinalIgnoreCase)
    {
        "Stanton", "Pyro", "Terra", "Interstellar"
    };

    /// <summary>Tokens that carry no meaning for display.</summary>
    private static readonly HashSet<string> Noise = new(StringComparer.OrdinalIgnoreCase)
    {
        "BP", "Def", "Mission"
    };

    public static ContractName Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new ContractName(raw ?? string.Empty, "Unknown", null, null, null);

        var parts = raw.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return new ContractName(raw, raw, null, null, null);

        string? system = null;
        string? difficulty = null;
        var remainder = new List<string>();

        // Index 0 is the issuer by convention and is never reinterpreted.
        for (var i = 1; i < parts.Length; i++)
        {
            var part = parts[i];

            if (system is null && Systems.Contains(part))
            {
                system = Title(part);
                continue;
            }

            if (difficulty is null && Difficulties.TryGetValue(part, out var mapped))
            {
                difficulty = mapped;
                continue;
            }

            // Trailing "Rank0", "_2" style variant markers add nothing.
            if (RankRegex.IsMatch(part) || VariantRegex.IsMatch(part) || Noise.Contains(part))
                continue;

            remainder.Add(part);
        }

        var type = remainder.Count > 0 ? Spaced(string.Join(' ', remainder)) : null;

        return new ContractName(raw, Spaced(parts[0]), system, difficulty, type);
    }

    private static string Title(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>
    /// Inserts spaces at camel-case boundaries: <c>RecoverCargo</c> becomes
    /// <c>Recover Cargo</c>, while acronyms such as <c>FTL</c> stay intact.
    /// </summary>
    private static string Spaced(string value) =>
        CamelBoundaryRegex.Replace(value, " ").Replace("  ", " ").Trim();

    [GeneratedRegex(@"^Rank\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RankRegex { get; }

    [GeneratedRegex(@"^\d+$", RegexOptions.Compiled)]
    private static partial Regex VariantRegex { get; }

    // Zero-width split points: lower/digit followed by upper, or the last capital
    // of an acronym that runs into a new word (HTTPServer -> HTTP Server).
    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled)]
    private static partial Regex CamelBoundaryRegex { get; }
}
