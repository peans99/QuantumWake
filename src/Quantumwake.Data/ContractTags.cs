using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// Reads the annotations a text mod leaves on a contract's own title.
/// </summary>
/// <remarks>
/// <para>
/// The game logs the contract's <em>displayed</em> title, and that title comes
/// from the localisation file. StarStrings replaces that file and appends what
/// it has worked out about a contract - <c>[150 Rep]</c> for the reputation it
/// pays, <c>[BP]</c> when it awards a blueprint - so with the mod installed the
/// log carries those facts in the only place the game was ever going to put
/// them. Nothing else in the logs mentions reputation at all: the value lives
/// on a server, and the client only ever opens a channel to ask about it.
/// </para>
/// <para>
/// So this is second-hand by nature. The numbers are one player's research
/// rather than the game reporting itself, they cover a fraction of the
/// contracts anyone actually flies, and a patch can move them. Everything built
/// on this says where it came from, and stays silent where there is no tag
/// rather than guessing a zero.
/// </para>
/// </remarks>
public static partial class ContractTags
{
    /// <summary><c>[150 Rep]</c>, <c>[+150 rep]</c>, <c>[16000 Rep]</c>.</summary>
    [GeneratedRegex(@"\[\s*\+?\s*(?<rep>-?\d{1,7})\s*rep\s*\]", RegexOptions.IgnoreCase)]
    private static partial Regex RepTag { get; }

    /// <summary><c>[BP]</c>, sometimes starred to mean "chance of".</summary>
    [GeneratedRegex(@"\[\s*BP\s*\]\*?", RegexOptions.IgnoreCase)]
    private static partial Regex BlueprintTag { get; }

    /// <summary>The mod wraps its additions in the game's own markup.</summary>
    [GeneratedRegex(@"</?EM\d*>", RegexOptions.IgnoreCase)]
    private static partial Regex Markup { get; }

    /// <summary>The reputation a title claims to pay, or null when it says nothing.</summary>
    public static int? RepFrom(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return null;

        var match = RepTag.Match(title);

        return match.Success && int.TryParse(match.Groups["rep"].Value, out var rep) ? rep : null;
    }

    /// <summary>Whether a title is tagged as awarding a blueprint.</summary>
    public static bool AwardsBlueprint(string? title) =>
        !string.IsNullOrWhiteSpace(title) && BlueprintTag.IsMatch(title);

    /// <summary>
    /// The title without the annotations, so a contract still reads as its own
    /// name once the numbers have been lifted off it.
    /// </summary>
    public static string Clean(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return string.Empty;

        var text = RepTag.Replace(title, "");
        text = BlueprintTag.Replace(text, "");
        text = Markup.Replace(text, "");

        // The game's own titles arrive with a trailing colon and the mod leaves
        // double spaces where a tag was.
        return Whitespace.Replace(text, " ").Trim().TrimEnd(':').Trim();
    }

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Whitespace { get; }

    /// <summary>
    /// Abbreviations the game itself uses for the same people.
    /// </summary>
    /// <remarks>
    /// Only where both forms appear in the logs of a single install, and only
    /// where the short form is the organisation's own initials. Guessing that
    /// two names mean one faction is how a page ends up quietly wrong about who
    /// the player has been working for.
    /// </remarks>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bhg"] = "Bounty Hunters Guild",
    };

    /// <summary>
    /// The key two spellings of one issuer share.
    /// </summary>
    /// <remarks>
    /// "Red Wind" and "Redwind" are the same people and the game writes both,
    /// so grouping ignores spacing and punctuation. Nothing else is assumed:
    /// two genuinely different names stay two rows.
    /// </remarks>
    public static string IssuerKey(string? issuer)
    {
        if (string.IsNullOrWhiteSpace(issuer))
            return string.Empty;

        var compact = new string([.. issuer.Where(char.IsLetterOrDigit)]).ToLowerInvariant();

        return Aliases.TryGetValue(compact, out var full)
            ? new string([.. full.Where(char.IsLetterOrDigit)]).ToLowerInvariant()
            : compact;
    }
}

