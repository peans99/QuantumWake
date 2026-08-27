using Quantumwake.Core.Parsing;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One tag the parser did not recognise, and how often.</summary>
/// <param name="Sample">
/// A line the parser could not read, or empty when examples were not asked
/// for. Scrubbed of the identifiers this install has read and of the shapes the
/// game is known to write names in - but see the remarks on
/// <see cref="Diagnostics"/> for what that cannot cover. Truncated by the
/// parser to 160 characters before it ever gets here.
/// </param>

public sealed record UnreadTag(string Tag, int Count, string Sample);

/// <summary>What the parser could not read, gathered while scanning.</summary>
public sealed record ParserHealth(int Unread, IReadOnlyList<UnreadTag> ByTag)
{
    public static readonly ParserHealth Empty = new(0, []);
}

/// <summary>
/// What to send with a bug report, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// "It works for you and not for me" is answered by what the parser could not
/// read, which the parser already records: a count per unrecognised tag and one
/// example line each. That is kilobytes, and it is the whole diagnosis for an
/// empty page - so a log never has to leave the machine that made it. A
/// gameplay log here runs to 8 MB and 29,000 lines, most public paste services
/// refuse it, and pasting one publishes the handles of everybody the pilot flew
/// with, not only their own.
/// </para>
/// <para>
/// The report is built by allow-list: every field here is one this code chose
/// to include, rather than a log with the bad parts stripped out. A deny-list
/// leaks the pattern nobody thought of, and the only thing worse than no report
/// is one that promises to be clean and is not. The install path is left out
/// for that reason - it reads
/// <c>C:\Users\&lt;name&gt;\...</c> on a great many machines, and a path is
/// never the answer to a parser question anyway.
/// </para>
/// <para>
/// Example lines are the exception, and they are off unless asked for. A
/// sample exists only because a known tag stopped parsing - which means the
/// format changed - and a changed format may write the pilot’s name in a shape
/// nothing here knows to look for. Scrubbing catches the identifiers this
/// install has read and the shapes the game has always used; it cannot catch
/// the shape that has just been invented. That was not a guess: a log written
/// with the login line reshaped to <c>Pilot{name}</c> came through with the
/// name intact, which is what moved samples behind their own consent.
/// </para>
/// <para>
/// So the report says how many lines of each tag went unread - enough to see
/// that something broke and where - and the lines themselves are a second,
/// separate yes, with the page saying plainly what it cannot promise about
/// them.
/// </para>

/// </remarks>
public static partial class Diagnostics
{
    /// <summary>
    /// Replaces this install's own identifiers wherever they appear.
    /// </summary>
    /// <param name="known">
    /// Handles and character ids read from the stored sessions. Short ones are
    /// ignored: a two-letter handle would match inside half the words in a log
    /// line and produce a sample nobody can read.
    /// </param>
    /// <remarks>
    /// Replaced rather than blanked, and with a name that says what was taken,
    /// because a report full of empty brackets cannot be reasoned about. Ids
    /// that look like account or session numbers go too, whether or not this
    /// install is the one that owns them.
    /// </remarks>
    public static string Scrub(string text, IEnumerable<string> known)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        foreach (var value in known
                     .Where(v => !string.IsNullOrWhiteSpace(v) && v.Length >= 3)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderByDescending(v => v.Length))
        {
            text = text.Replace(value, "<pilot>", StringComparison.OrdinalIgnoreCase);
        }

        // Then by shape, which is what covers the case that matters most: when
        // the line the parser lost is the login line itself, the handle never
        // reached the store, so there is no value to search for. The names are
        // where the game puts them either way.
        text = HandleRegex().Replace(text, "Handle[<pilot>]");
        text = NicknameRegex().Replace(text, "nickname=\"<entity>\"");
        text = NameRegex().Replace(text, "name <pilot>");

        text = AccountIdRegex().Replace(text, "accountId <id>");

        text = GeidRegex().Replace(text, "geid <id>");
        text = SessionIdRegex().Replace(text, "<session>");

        return text;
    }

    /// <summary>Turns raw parser counters into the report's shape.</summary>
    /// <param name="samples">
    /// Whether to carry the example lines. Off is the default because a sample
    /// is raw log text and the guarantee around it is weaker than the one around
    /// every other field in the report.
    /// </param>
    public static ParserHealth Health(
        int unread,
        IReadOnlyDictionary<string, (int Count, string Sample)> byTag,
        IEnumerable<string> known,
        bool samples = false)
    {
        var identifiers = known.ToList();

        return new ParserHealth(
            unread,
            [.. byTag
                .OrderByDescending(pair => pair.Value.Count)
                .Select(pair => new UnreadTag(
                    pair.Key,
                    pair.Value.Count,
                    samples ? Scrub(pair.Value.Sample, identifiers) : string.Empty))]);
    }

    /// <summary>Merges one file's parser into a running total.</summary>
    public static void Merge(
        this Dictionary<string, (int Count, string Sample)> into, LogEventParser parser)
    {
        foreach (var (tag, (count, sample)) in parser.UnmatchedByTag)
        {
            into[tag] = into.TryGetValue(tag, out var existing)
                ? (existing.Count + count, existing.Sample)
                : (count, sample);
        }
    }

    [GeneratedRegex(@"Handle\[[^\]]*\]", RegexOptions.Compiled)]
    private static partial Regex HandleRegex();

    [GeneratedRegex("nickname=\"[^\"]*\"", RegexOptions.Compiled)]
    private static partial Regex NicknameRegex();

    /// <summary>The character line writes the account name as "- name X -".</summary>
    [GeneratedRegex(@"\bname\s+[^\s-]+", RegexOptions.Compiled)]
    private static partial Regex NameRegex();

    [GeneratedRegex(@"accountId\s+\d+", RegexOptions.Compiled)]
    private static partial Regex AccountIdRegex();

    [GeneratedRegex(@"geid\s+\d+", RegexOptions.Compiled)]
    private static partial Regex GeidRegex();

    /// <summary>A GUID, which is how sessions and shards are named.</summary>
    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SessionIdRegex();
}
