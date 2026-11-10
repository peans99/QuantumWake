using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>How the frames of a week went: kinds counted, and the unplaced ones by shape.</summary>
/// <param name="From">The first frame counted.</param>
/// <param name="To">The last.</param>
/// <param name="Frames">Frames read in all.</param>
/// <param name="Kinds">Frames per kind the reader placed, Unknown included.</param>
/// <param name="Shapes">The Unknown frames by shape, for those logged since 1.2.24 - older lines carry no shape.</param>
public sealed record FrameTally(
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Frames,
    IReadOnlyDictionary<string, int> Kinds,
    IReadOnlyDictionary<string, int> Shapes);

/// <summary>
/// What an unplaced frame looked like, in this app's words and never the
/// screen's (1.2.24).
/// </summary>
/// <remarks>
/// <para>
/// 5,018 of the 7,796 frames the live reader took on this install between 6
/// and 9 November 2026 were filed Unknown, and the privacy page promises that
/// a screen the app cannot read is not kept - so there was nothing to learn
/// from about which reader to write next. A shape is that something: a word
/// from a short list, written into the frame log's kind column beside
/// "Unknown", which already held only the app's own words.
/// </para>
/// <para>
/// The shapes come from the 42 screenshots of this install filed Unknown, whose
/// text the reading log does keep, and sort them: 17 no text at all (every one
/// the same 136,061-byte black picture, a screenshot saved before the scene
/// drew), 4 the looting view, 4 the flight HUD, 3 chat, 14 other - and 8 of
/// those 14 are Freight Manager and refinery frames kept from before those
/// readers existed. Re-read one by one, at least 4 are placed now (three
/// refinery frames, and the 12 Sep Freight Manager frame through the Windows
/// engine); hence Read again for all of them at once. Order matters: chat sits over
/// every other screen, so it is the shape only when it is a third of the text.
/// </para>
/// </remarks>
public static partial class UnplacedFrames
{
    /// <summary>The shapes, in the order they are tried, and how the page names them.</summary>
    public static readonly IReadOnlyList<(string Shape, string Means)> Shapes =
    [
        ("no-text", "no text at all - a black or loading screen, or open space"),
        ("inventory", "an inventory or looting view, which carries no item names"),
        ("flight-hud", "the flight HUD with nothing locked or fractured"),
        ("chat", "chat and little else"),
        ("few-words", "a handful of words"),
        ("other", "something else"),
    ];

    /// <summary>The shape of a frame no reader placed.</summary>
    public static string Shape(IReadOnlyList<ScreenTextLine> lines)
    {
        // A line with no run of three letters is a glyph, a gauge or a number.
        var worded = lines.Where(l => Wordy().IsMatch(l.Text)).ToList();
        if (worded.Count == 0) return "no-text";

        // Plain drops the spaces: LOOTING VIEW is LOOTINGVIEW here.
        if (worded.Any(l => Fold(l.Text) is var t && (t.Contains("LOOTINGVIEW") || t.StartsWith("MOVEALL"))))
            return "inventory";

        if (HudText.FlightHud(lines)) return "flight-hud";

        var chat = worded.Count(l => Chat().IsMatch(l.Text));
        // Chat over a screen still leaves that screen's few words - INTERACT,
        // LOBBY - and a long message wraps onto lines with no [GLOBAL] of their
        // own: 5 tagged lines of 13 on a chat-only screenshot of this install.
        // So a third tagged is chat.
        if (chat >= 3 && chat * 3 >= worded.Count) return "chat";

        return worded.Count <= 5 ? "few-words" : "other";
    }

    /// <summary>The frame log's kind for an unplaced frame: Unknown and its shape.</summary>
    public static string Kind(IReadOnlyList<ScreenTextLine> lines) => $"Unknown/{Shape(lines)}";

    /// <summary>Counts the frame logs in a folder: every read frame by kind, the unplaced by shape.</summary>
    public static FrameTally Tally(string folder)
    {
        var kinds = new Dictionary<string, int>(StringComparer.Ordinal);
        var shapes = new Dictionary<string, int>(StringComparer.Ordinal);
        DateTimeOffset? from = null, to = null;
        var frames = 0;

        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.EnumerateFiles(folder, "frames-*.csv").Order())
            {
                IEnumerable<string> lines;
                try { lines = File.ReadAllLines(file).Skip(1); }
                catch (IOException) { continue; }

                foreach (var line in lines)
                {
                    // at,state,kind,... - the kind never holds a comma; the
                    // waiting reason after it may, and is not needed here.
                    var cells = line.Split(',', 4);
                    if (cells.Length < 3 || cells[1] != "read") continue;
                    if (!DateTimeOffset.TryParse(cells[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)) continue;

                    frames++;
                    from = from is null || at < from ? at : from;
                    to = to is null || at > to ? at : to;

                    var kind = cells[2];
                    var slash = kind.IndexOf('/');
                    var main = slash < 0 ? kind : kind[..slash];
                    if (main.Length == 0) main = "Unknown";
                    kinds[main] = kinds.GetValueOrDefault(main) + 1;
                    if (slash >= 0) shapes[kind[(slash + 1)..]] = shapes.GetValueOrDefault(kind[(slash + 1)..]) + 1;
                }
            }
        }

        return new FrameTally(from, to, frames, kinds, shapes);
    }

    private static string Fold(string text) => ScreenInsight.Plain(text).ToUpperInvariant();

    [GeneratedRegex(@"[A-Za-z]{3}")]
    private static partial Regex Wordy();

    // [GLOBAL] Name: ... as the engine reads it: IGLOBALI, tGLOBALJ, (GLOBAL ...
    [GeneratedRegex(@"^\W?\s*[\[\(\{tI1l]?\s*(GLOBAL|GLOBAU|GLOßAU|LOCAL|PARTY|ORG)", RegexOptions.IgnoreCase)]
    private static partial Regex Chat();
}
