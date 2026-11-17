using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// Whether a name seen in a stash is the thing a shopping line asks for.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.2.45 any stash name merely containing the line counted, which
/// against this install's 40 stash names and the 2,565-name shopping catalogue
/// made 7 catalogue names "found" that were not exact: "Oza" inside
/// "MedPen (Hemozal)", "Seanut" inside "Fried Seanut", "Odyssey" inside
/// "Odyssey II Undersuit Alpha". A line ticked by the wrong item is worse than
/// one left open, because nobody goes and buys it.
/// </para>
/// <para>
/// What still matches beyond the exact name: the same name with a bracketed
/// qualifier dropped ("Hadanite" for "Hadanite (Raw)", "MedPen" for
/// "MedPen (Hemozal)"), and a variant that only adds words after a name of
/// two words or more ("ORC-mkV Arms" for "ORC-mkV Arms Red"). One word is too
/// little to call the rest a variant: "Odyssey II" is another suit.
/// </para>
/// </remarks>
public static partial class HeldNames
{
    public static bool Matches(string held, string wanted)
    {
        if (string.IsNullOrWhiteSpace(held) || string.IsNullOrWhiteSpace(wanted))
            return false;

        var h = held.Trim();
        var w = wanted.Trim();
        if (string.Equals(h, w, StringComparison.OrdinalIgnoreCase))
            return true;

        var bare = Brackets().Replace(h, " ").Trim();
        bare = Spaces().Replace(bare, " ");
        if (string.Equals(bare, w, StringComparison.OrdinalIgnoreCase))
            return true;

        return w.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2
            && h.Length > w.Length
            && h.StartsWith(w, StringComparison.OrdinalIgnoreCase)
            && h[w.Length] == ' ';
    }

    /// <summary>
    /// Whether a stash name is probably the line's thing in another colour or
    /// version (1.2.60): never counted as in hand, only said beside the line.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The name less its last word - the colour, "ORC-mkV Core" of "ORC-mkV
    /// Core Grey" - has to open the stash name, and be two words or more; and
    /// both names have to be in the reference data as the same type of item
    /// from the same maker. The names alone would call a "Tailwind Flight
    /// Helmet" a version of the "Tailwind Flight Suit"; the type tells a
    /// helmet from a suit. A name the reference data does not hold gets no
    /// hint, since there is then nothing to tell the two apart by.
    /// </para>
    /// <para>
    /// On 2026-11-16 a list for a group's uniforms wanted the ORC-mkV Core,
    /// Arms and Legs in Grey, and the stash at Terminus LEO held all three in
    /// Red; the list said nothing, since Red is not Grey.
    /// </para>
    /// </remarks>
    public static bool Alike(string held, string wanted, Func<string, ItemInfo?> reference)
    {
        if (string.IsNullOrWhiteSpace(held) || string.IsNullOrWhiteSpace(wanted) || Matches(held, wanted))
            return false;

        var words = wanted.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3)
            return false;

        var stem = string.Join(' ', words[..^1]);
        var h = held.Trim();
        if (!h.StartsWith(stem, StringComparison.OrdinalIgnoreCase) || (h.Length > stem.Length && h[stem.Length] != ' '))
            return false;

        return reference(h) is { } a && reference(wanted.Trim()) is { } b
            && a.Type is { Length: > 0 } && string.Equals(a.Type, b.Type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.SubType ?? "", b.SubType ?? "", StringComparison.OrdinalIgnoreCase)
            && string.Equals(a.Manufacturer ?? "", b.Manufacturer ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
