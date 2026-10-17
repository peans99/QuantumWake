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

    [GeneratedRegex(@"^\s*g_language\s*=\s*([A-Za-z_()]+)", RegexOptions.IgnoreCase)]
    private static partial Regex Setting();
}
