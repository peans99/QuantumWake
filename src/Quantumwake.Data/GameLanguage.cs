using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// Which text table the game reads, from <c>g_language</c> in the install's
/// <c>user.cfg</c>.
/// </summary>
/// <remarks>
/// <para>
/// The game reads <c>Data\Localization\{g_language}\global.ini</c>, and English
/// when the line is absent - which is why the item labels and StarStrings both
/// write the English file. A game set to French never reads it, so a page that
/// says "installed" would be telling a French player something true of a file
/// and false of their game.
/// </para>
/// <para>
/// StarStrings ships a <c>user.cfg</c> of its own, and the one in this install
/// after installing it reads exactly <c>g_language = english</c>: installing it
/// switches a game in another language back to English. That is the mod's
/// choice, and the page says so before it is made.
/// </para>
/// </remarks>
public static partial class GameLanguage
{
    public const string English = "english";

    /// <summary>The language the game at this root reads, or English when nothing says otherwise.</summary>
    public static string Read(string? gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot))
            return English;

        var path = Path.Combine(gameRoot, "user.cfg");

        try
        {
            return File.Exists(path) ? Parse(File.ReadAllText(path)) : English;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return English;
        }
    }

    /// <summary>
    /// The value of the last <c>g_language</c> line, as the game would take it;
    /// a commented line does not count.
    /// </summary>
    public static string Parse(string? cfg)
    {
        var found = English;

        foreach (var line in (cfg ?? "").Split('\n'))
        {
            if (Setting().Match(line) is { Success: true } match)
                found = match.Groups[1].Value.ToLowerInvariant();
        }

        return found;
    }

    /// <summary>The line the item labels add when <c>user.cfg</c> names no language.</summary>
    public const string EnglishLine = "g_language = english";

    /// <summary>
    /// <c>user.cfg</c> as it must read for the game to load a loose English
    /// table, or null when it already does or names another language.
    /// </summary>
    /// <remarks>
    /// <para>
    /// English being the default is true of the table inside <c>Data.p4k</c>,
    /// not of a loose <c>Data\Localization\english\global.ini</c>: the game
    /// reads that only when <c>user.cfg</c> sets <c>g_language</c>. StarStrings
    /// ships the line in a <c>user.cfg</c> of its own, so while it was
    /// installed the labels written over its table showed. Once it was gone,
    /// so was the line, and the labels' file sat in the game folder unread -
    /// the item marks and StarStrings' rep markers on contract titles both
    /// disappeared on this install, with the file still there.
    /// </para>
    /// <para>
    /// A file that names another language is left as the player set it: the
    /// page already says the labels are written for English.
    /// </para>
    /// </remarks>
    /// <param name="cfg">The file as it is, or null when there is none.</param>
    public static string? WithEnglish(string? cfg)
    {
        if (cfg is not null && cfg.Split('\n').Any(l => Setting().IsMatch(l))) return null;
        if (string.IsNullOrEmpty(cfg)) return EnglishLine + "\r\n";
        var newline = cfg.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var joined = cfg.EndsWith('\n') ? cfg : cfg + newline;
        return joined + EnglishLine + newline;
    }

    /// <summary>
    /// <c>user.cfg</c> without the line <see cref="WithEnglish"/> added, keeping
    /// anything the player wrote since; empty when that line was all there was.
    /// </summary>
    public static string WithoutAddedLine(string cfg)
    {
        var lines = cfg.Split('\n').ToList();
        var at = lines.FindLastIndex(l => l.TrimEnd('\r').Trim() == EnglishLine);
        if (at >= 0) lines.RemoveAt(at);
        var rest = string.Join('\n', lines);
        return rest.Trim().Length == 0 ? string.Empty : rest;
    }

    [GeneratedRegex(@"^\s*g_language\s*=\s*([A-Za-z_()]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Setting();
}
