using System.Globalization;

namespace Quantumwake.OrgShared;

/// <summary>
/// Length caps for text that gets stored and drawn.
/// </summary>
/// <remarks>
/// Shared rather than copied per store, because the import path faces text
/// somebody else wrote and the authoring path does not. Two copies of a cap
/// drift, and the one that drifts is never the one being read carefully.
/// </remarks>
public static class Sanitise
{
    /// <summary>A title, a name, a line of a list.</summary>
    public const int Title = 240;

    /// <summary>A note, a URL, anything that can reasonably run on.</summary>
    public const int Note = 1000;

    /// <summary>Text with a fallback for when there is none.</summary>
    public static string Clean(string? value, string fallback, int max = Title) =>
        string.IsNullOrWhiteSpace(value) ? fallback : Clip(value.Trim(), max);

    /// <summary>Text that is allowed to be absent.</summary>
    public static string? CleanOptional(string? value, int max = Note) =>
        string.IsNullOrWhiteSpace(value) ? null : Clip(value.Trim(), max);

    /// <summary>
    /// The first <paramref name="max"/> characters, cut where a character ends.
    /// </summary>
    /// <remarks>
    /// A plain slice counts UTF-16 code units, so a cut landing between the two
    /// halves of a surrogate pair leaves a lone surrogate — half an emoji, which
    /// is not text any more and which a JSON writer has to either mangle or
    /// refuse. Unreachable from the app's own forms, since nothing offers a
    /// 240-character title box, but an imported file chooses its own lengths.
    /// </remarks>
    public static string Clip(string value, int max)
    {
        if (value.Length <= max)
            return value;

        var cut = max;

        // Stepping back off a low surrogate keeps its pair whole. Combining
        // marks can still be separated from what they modify, which is ugly but
        // is still text; a lone surrogate is not.
        if (char.IsLowSurrogate(value[cut]))
            cut--;

        return value[..cut];
    }

    /// <summary>
    /// Text with the control characters taken out.
    /// </summary>
    /// <remarks>
    /// A title of a carriage return and two hundred spaces draws as a blank row
    /// that cannot be clicked, which is a cheap way to make somebody's page look
    /// broken with data they accepted from a friend. Newlines and tabs survive
    /// in the fields that are allowed to have them.
    /// </remarks>
    public static string Printable(string value, bool allowBreaks = false)
    {
        if (!value.Any(c => char.GetUnicodeCategory(c) == UnicodeCategory.Control))
            return value;

        return string.Concat(value.Where(c =>
            char.GetUnicodeCategory(c) != UnicodeCategory.Control
            || (allowBreaks && c is '\n' or '\t')));
    }
}
