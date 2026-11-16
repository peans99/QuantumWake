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

    [GeneratedRegex(@"\s*[\(\[][^\)\]]*[\)\]]")]
    private static partial Regex Brackets();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex Spaces();
}
