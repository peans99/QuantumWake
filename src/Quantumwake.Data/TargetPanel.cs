using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>What the cockpit's target panel said about the ship locked.</summary>
/// <param name="Name">
/// The name the panel prints - "AEGIS IDRIS-P", "VANDUUL BLADE" - or null
/// while it prints UNKNOWN. The game names a target only once it has been
/// scanned; most fighters were UNKNOWN for their whole fight in the 2026-11-05
/// recording.
/// </param>
/// <param name="Unknown">The panel printed UNKNOWN in the name's place - not merely that no name was read.</param>
/// <param name="Faction">The faction line's value, or null when unread or blank.</param>
/// <param name="SpeedMs">The target's speed in m/s.</param>
/// <param name="RangeKm">How far away it is, in km.</param>
/// <param name="Em">Electromagnetic signature, in the panel's own units.</param>
/// <param name="Ir">Infrared signature.</param>
/// <param name="CrossSection">
/// Cross-section signature. The one of the three that belongs to the hull
/// rather than to what it is doing: it is the hull's largest cross-section
/// face times its armour, which is how an UNKNOWN can still be named.
/// </param>
/// <param name="Panel">Where the panel sat, so the next frame can read only that.</param>
public sealed record TargetReading(
    string? Name,
    bool Unknown,
    string? Faction,
    double? SpeedMs,
    double? RangeKm,
    double? Em,
    double? Ir,
    double? CrossSection,
    ScreenPatch Panel);

/// <summary>How the locked target is marked: the colour of its label in the world.</summary>
public enum TargetStance
{
    /// <summary>No label found, or a colour this cannot place.</summary>
    Unread,

    /// <summary>Red: the game's hostile.</summary>
    Hostile,

    /// <summary>Blue: party and friendly.</summary>
    Friendly,

    /// <summary>White or grey.</summary>
    Neutral,
}

/// <summary>
/// Reads the cockpit target panel - signatures, name, faction, velocity - out
/// of a frame's text lines.
/// </summary>
/// <remarks>
/// <para>
/// Measured on the 2026-11-05 recording (1716x720, read at 3440x1440), two
/// ship HUDs. Both put the same four rows in the same order - three signature
/// figures, the name, FACTION, VELOCITY - but one draws them as loose text and
/// the other in boxes with the label and value as separate lines, and the
/// panel's place on the screen differs between them. So nothing here is a
/// fixed position: the VELOCITY and FACTION lines are found by their words,
/// and everything else is measured from them.
/// </para>
/// <para>
/// The engine's misreadings that were actually seen are folded away and no
/// others: VELOCTY, VELOCY, FACTION UMKOWN, UNKNOWH, the letter O for a zero
/// speed, and the faction and velocity rows read as one line.
/// </para>
/// <para>
/// The hologram under the panel is not read for hostility, though it turns
/// red. It turns red with damage: the Idris-P the pilot was defending at 24%
/// ship health was drawn red, and Vanduul fighters went from white to red over
/// a fight. Hostility is on the target's label in the world instead; see
/// <see cref="Stance"/>.
/// </para>
/// </remarks>
public static partial class TargetPanel
{
    /// <summary>The frame height every distance here was measured at; the HUD scales with it.</summary>
    private const double MeasuredHeight = 1440;

    /// <summary>
    /// Where the panel sits around its VELOCITY line, in pixels at 1440 high.
    /// The figures sat 100-150 above it and the hologram 200 below; the patch
    /// stops short of the hologram, which only adds noise to a read.
    /// </summary>
    private const double Above = 190, Below = 60, LeftOf = 200, Width = 640;

    /// <summary>
    /// Finds the panel on a whole frame, or null when there is no target panel
    /// on it. Says nothing about what is in it: the patch is for a second,
    /// closer look.
    /// </summary>
    public static ScreenPatch? Locate(IReadOnlyList<ScreenTextLine> lines, int frameWidth, int frameHeight)
    {
        var u = frameHeight / MeasuredHeight;
        if (Anchor(lines, u) is not { } anchor)
            return null;

        var left = Math.Max(0, anchor.Left - LeftOf * u);
        var top = Math.Max(0, anchor.Top - Above * u);
        var width = Math.Min(Width * u, frameWidth - left);
        var height = Math.Min((Above + Below) * u, frameHeight - top);

        return width > 0 && height > 0 ? new ScreenPatch(left, top, width, height) : null;
    }

    /// <summary>Whether the panel says NO TARGET, which is the panel with nothing locked.</summary>
    public static bool SaysNoTarget(IReadOnlyList<ScreenTextLine> lines) =>
        lines.Any(line => Letters(line.Text) is "NOTARGET" or "NOTARGEI" or "NOTARCET");

    /// <summary>
    /// What the panel says, read from lines in frame coordinates - a whole
    /// frame, or a patch read again and mapped back.
    /// </summary>
    public static TargetReading? Read(IReadOnlyList<ScreenTextLine> lines, int frameHeight, ScreenPatch? panel = null)
    {
        var all = lines
            .Select(line => line with { Text = line.Text.Trim() })
            .Where(line => line.Text.Length > 0)
            .ToList();

        var u = frameHeight / MeasuredHeight;
        if (Anchor(all, u) is not { } velocity)
            return null;

        var row = Math.Max(velocity.Height, 18 * u);

        // The boxed HUD prints the value as its own line to the right of the
        // label, on the same row; the loose one prints them as one line.
        var speedText = velocity.Text + " " + string.Join(" ", RowRightOf(all, velocity, row).Select(l => l.Text));
        var (speed, range) = Velocity(speedText);

        var factionLine = all.FirstOrDefault(line => IsFaction(line.Text) && Math.Abs(line.Top - velocity.Top) < 80 * u);
        string? faction = null;
        if (factionLine is not null)
        {
            // The value is on the label's own line in the loose HUD and is the
            // next box along in the boxed one; only the boxed case looks
            // further, or a tall merged line drags the name in beside it.
            var own = AfterFaction(factionLine.Text);
            faction = Faction(own) ?? (IsUnknown(own) ? null
                : RowRightOf(all, factionLine, Math.Min(row, factionLine.Height)).Select(l => Faction(l.Text)).FirstOrDefault(f => f is not null));
        }

        // The name is the line just above the faction row - or above the
        // velocity row, on frames where the two were read as one line.
        var under = factionLine is not null && factionLine.Top < velocity.Top ? factionLine : velocity;
        var nameLine = all
            .Where(line => line.Top < under.Top - 4 * u && line.Top > under.Top - 70 * u)
            .Where(line => LooksLikeName(line.Text))
            .Where(line => line.Left < velocity.Left + 360 * u && line.Left + line.Text.Length * 8 * u > velocity.Left - 220 * u)
            .OrderByDescending(line => line.Top)
            .FirstOrDefault();

        var unknown = nameLine is not null && IsUnknown(nameLine.Text);
        var name = nameLine is null || unknown ? null : Tidy(nameLine.Text);

        var (em, ir, cs) = Signatures(all, nameLine?.Top ?? under.Top, velocity, u);

        var at = panel ?? new ScreenPatch(
            Math.Max(0, velocity.Left - LeftOf * u), Math.Max(0, velocity.Top - Above * u),
            Width * u, (Above + Below) * u);

        return new TargetReading(name, unknown, faction, speed, range, em, ir, cs, at);
    }

    /// <summary>
    /// The locked target's label in the world: its callsign over a line like
    /// "1.2km (26m/s)". Only the locked target carries the speed in brackets;
    /// every other contact shows a distance alone.
    /// </summary>
    public static ScreenTextLine? LockLabel(IReadOnlyList<ScreenTextLine> lines) =>
        lines.FirstOrDefault(line => LockLabelText().IsMatch(line.Text));

    /// <summary>
    /// Hostile, friendly or neutral from the label's pixels: red is hostile,
    /// blue is party and friendly, as the game draws them. Only the lit
    /// pixels count - the label is thin text over whatever is behind it.
    /// </summary>
    public static TargetStance Stance(ColourPatch? pixels, ScreenTextLine label)
    {
        if (pixels is null)
            return TargetStance.Unread;

        int lit = 0, red = 0, blue = 0, white = 0;
        var right = label.Left + Math.Max(label.Height * 0.6 * label.Text.Length, label.Height);

        for (var y = (int)label.Top; y < label.Top + label.Height; y++)
        {
            for (var x = (int)label.Left; x < right; x++)
            {
                var (r, g, b) = pixels.At(x, y);
                var max = Math.Max(r, Math.Max(g, b));
                if (max < 110) continue;

                lit++;
                if (r > g * 1.6 && r > b * 1.6) red++;
                else if (b > r * 1.25 && b >= g) blue++;
                else if (Math.Max(Math.Abs(r - g), Math.Abs(g - b)) < 40) white++;
            }
        }

        // Thin text: a few dozen lit pixels is a real label at 1440 high.
        if (lit < 12)
            return TargetStance.Unread;

        if (red >= lit * 0.35) return TargetStance.Hostile;
        if (blue >= lit * 0.35) return TargetStance.Friendly;
        if (white >= lit * 0.5) return TargetStance.Neutral;
        return TargetStance.Unread;
    }

    // ---- the rows ----

    /// <summary>
    /// The VELOCITY row. Windows' engine never read the boxed HUD's labels at
    /// all - VELOCITY and FACTION were missing from every boxed frame - and
    /// read only the value, "1B3M?0.28KM)". The value alone is still the
    /// panel's: its range is upper-case KM in a bracket, where every label in
    /// the world writes lower-case km. Found that way, the row is moved left
    /// by the label's width, about 100 at 1440 high in both HUDs, so the
    /// columns above it are measured from the same place either way.
    /// </summary>
    private static ScreenTextLine? Anchor(IReadOnlyList<ScreenTextLine> lines, double u)
    {
        var labelled = lines
            .Where(line => IsVelocity(line.Text))
            .OrderByDescending(line => Velocity(line.Text).Speed is not null)
            .FirstOrDefault();
        if (labelled is not null)
            return labelled;

        var value = lines.FirstOrDefault(line => PanelRange().IsMatch(line.Text) && !IsFaction(line.Text));
        return value is null ? null
            : value with { Left = value.Left - LabelWidth * u, Text = "VELOCITY " + value.Text };
    }

    /// <summary>The VELOCITY label's width, in pixels at 1440 high.</summary>
    private const double LabelWidth = 100;

    /// <summary>
    /// VELOCITY, as read. ONNX gave VELOCTY, VELOCY and VELOCITY43M/S;
    /// Windows' engine VELCCITY, VELOCtTY, VELOC?Y and l_VECCtry. So the word
    /// is compared rather than listed: up to two stray characters before a V,
    /// then within three letters of VELOCITY. Not the flight HUD's VEL LIM.
    /// </summary>
    private static bool IsVelocity(string text)
    {
        var flat = new string([.. text.ToUpperInvariant().Where(c => char.IsLetter(c) || c == '?' || c == '�')]);
        var v = flat.IndexOf('V');
        if (v is < 0 or > 2) return false;

        var word = flat[v..];
        if (word.StartsWith("VELLIM", StringComparison.Ordinal)) return false;

        var head = word[..Math.Min(8, word.Length)];
        return head.Length >= 5 && Distance(head, "VELOCITY"[..Math.Min(8, Math.Max(head.Length, 6))]) <= 3;
    }

    /// <summary>
    /// FACTION as read. The boxed HUD draws the label small and dim, and the
    /// recording read it FAETION, PACTION, FASTION, FACTON, AGTON, CTION,
    /// STION and FAPION - each of which, missed, was taken for the ship's
    /// name and announced.
    /// </summary>
    private static bool IsFaction(string text)
    {
        var letters = Letters(text);
        if (letters.Contains("FACTION", StringComparison.Ordinal) || letters.Contains("FACTI0N", StringComparison.Ordinal))
            return true;

        var first = Letters(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "");
        return first.Length is >= 4 and <= 8
            && (Distance(first, "FACTION") <= 3 || first.EndsWith("TION", StringComparison.Ordinal)
                || first.EndsWith("CTON", StringComparison.Ordinal));
    }

    private static string AfterFaction(string text)
    {
        var at = text.IndexOf("TION", StringComparison.OrdinalIgnoreCase);
        if (at >= 0) return text[(at + 4)..];

        // A misread label is its first word; the value is whatever follows.
        var space = text.Trim().IndexOf(' ');
        return space < 0 ? "" : text.Trim()[(space + 1)..];
    }

    /// <summary>
    /// Factions seen in the recording, so a misread one is put right: the
    /// engine read Vanduul as Yanduul, Vandull and Vanduley within one fight.
    /// Anything else is shown as read.
    /// </summary>
    private static readonly string[] KnownFactions = ["Vanduul", "Outlaws", "Shattered Blade"];

    /// <summary>
    /// Factions that were shooting at the pilot in the recording: Vanduul
    /// fighters, Outlaws in Paladins and Perseuses, a Shattered Blade turret.
    /// The list grows as fights show more, rather than guessing at the game's.
    /// </summary>
    public static bool IsHostileFaction(string faction) =>
        KnownFactions.Contains(faction, StringComparer.OrdinalIgnoreCase);

    /// <summary>The faction's words, or null for a blank or UNKNOWN faction.</summary>
    private static string? Faction(string rest)
    {
        // A merged line carries the velocity after the faction or before it.
        var words = Regex.Replace(rest, @"VEL\S*.*$", "", RegexOptions.IgnoreCase);
        words = Regex.Replace(words, @"[^A-Za-z' \-]", " ");
        words = Regex.Replace(words, @"\s+", " ").Trim();
        if (words.Length < 3 || IsUnknown(words)) return null;

        var flat = Letters(words);
        foreach (var known in KnownFactions)
        {
            var k = Letters(known);
            if (flat.Length >= 5 && Distance(flat, k) <= Math.Max(1, k.Length / 3))
                return known;
        }

        return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(words.ToLowerInvariant());
    }

    private static IEnumerable<ScreenTextLine> RowRightOf(IReadOnlyList<ScreenTextLine> lines, ScreenTextLine of, double row) =>
        lines
            .Where(line => line != of && line.Left > of.Left && line.Left - of.Left < row * 18)
            .Where(line => Math.Abs((line.Top + line.Height / 2) - (of.Top + of.Height / 2)) < row * 0.6)
            .OrderBy(line => line.Left);

    /// <summary>"43M/S (2.55KM)", "OM/S (5.28KM)", "18M [1.28KM]": speed and range.</summary>
    public static (double? Speed, double? Range) Velocity(string text)
    {
        var flat = text.ToUpperInvariant().Replace(',', '.');
        double? speed = null, range = null;

        if (SpeedText().Match(flat) is { Success: true } s)
        {
            var digits = Digits(s.Groups["n"].Value);
            if (double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v < 5000)
                speed = v;
        }

        if (RangeText().Match(flat) is { Success: true } r
            && double.TryParse(r.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var km)
            && km < 1000)
            range = km;

        return (speed, range);
    }

    /// <summary>
    /// The three figures over the name, left to right EM, IR, CS. Each column
    /// has a second, smaller figure under it; the top one is the signature.
    /// </summary>
    private static (double? Em, double? Ir, double? Cs) Signatures(
        IReadOnlyList<ScreenTextLine> lines, double nameTop, ScreenTextLine velocity, double u)
    {
        var figures = lines
            .Where(line => line.Top < nameTop - 2 * u && line.Top > nameTop - 150 * u)
            .Where(line => line.Left > velocity.Left - 260 * u && line.Left < velocity.Left + 420 * u)
            .SelectMany(line => Figures(line))
            .OrderBy(f => f.Left)
            .ToList();

        // Columns are 100-130 apart at 1440 high; a gap of 60 splits them.
        var columns = new List<List<(double Left, double Top, double Value)>>();
        foreach (var figure in figures)
        {
            if (columns.Count == 0 || figure.Left - columns[^1][^1].Left > 60 * u)
                columns.Add([]);
            columns[^1].Add(figure);
        }

        var tops = columns.Select(c => c.OrderBy(f => f.Top).First()).ToList();
        if (tops.Count >= 3)
            return (tops[0].Value, tops[1].Value, tops[2].Value);

        // A column lost - EM, the dimmest, most often - leaves two figures,
        // and which two is a matter of where they sit. CS started 218 and 287
        // right of the VELOCITY line in the two HUDs measured, and IR 98 and
        // 161: 200 splits them in both. Counting alone put the Idris-P's IR
        // in the CS column, and named it a Polaris.
        double? em = null, ir = null, cs = null;
        foreach (var figure in tops)
        {
            var offset = (figure.Left - velocity.Left) / u;
            if (offset >= 200) cs = figure.Value;
            else if (offset >= 60 || (tops.Count == 2 && figure == tops[1])) ir = figure.Value;
            else em = figure.Value;
        }

        return (em, ir, cs);
    }

    /// <summary>"29.6K", "17.0K 285.6", "GM 35.1K": every K figure on a line, with where it sat.</summary>
    private static IEnumerable<(double Left, double Top, double Value)> Figures(ScreenTextLine line)
    {
        var step = line.Text.Length == 0 ? 0 : line.Height * 0.55;
        foreach (Match m in KFigure().Matches(line.Text.ToUpperInvariant().Replace(',', '.')))
        {
            // A figure needs a real digit in it: OK is not 0K.
            if (m.Groups["n"].Value.Any(char.IsDigit)
                && double.TryParse(Digits(m.Groups["n"].Value), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                yield return (line.Left + m.Index * step, line.Top, v * 1000);
        }
    }

    private static bool LooksLikeName(string text)
    {
        var letters = text.Count(char.IsLetter);
        if (letters < 3 || letters < text.Length * 0.6) return false;
        var flat = Letters(text);
        return !IsFaction(text) && !IsVelocity(text) && flat != "PARTY"
            // "X UNKNOWN" is a faction row whose label went unread, not a name.
            && !(flat.Length > 8 && flat.EndsWith("UNKNOWN", StringComparison.Ordinal));
    }

    /// <summary>UNKNOWN as read: UNKNOWH, UMKOWN, UNKNOW.</summary>
    public static bool IsUnknown(string text)
    {
        var flat = Letters(text);
        if (flat.Length is < 5 or > 9) return false;
        return Distance(flat, "UNKNOWN") <= 2;
    }

    private static string Tidy(string text) =>
        Regex.Replace(text.Trim(), @"\s+", " ").ToUpperInvariant();

    private static string Letters(string text) =>
        new([.. text.ToUpperInvariant().Where(char.IsLetterOrDigit)]);

    internal static int Distance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
            for (var j = 1; j <= b.Length; j++)
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
        return d[a.Length, b.Length];
    }

    /// <summary>
    /// The letters both engines read for digits on this HUD's face - IO.4K
    /// for 10.4K, 1B3M for 183M, OM/S for 0M/S - turned back into digits.
    /// Only ever applied to a token already matched as a figure.
    /// </summary>
    private static string Digits(string token) =>
        token.Replace('O', '0').Replace('D', '0').Replace('I', '1').Replace('L', '1').Replace('B', '8').Replace('A', '4');

    // "18M [1.28KM]" lost its /S and kept the bracket, and "1B3M?0.28KM)" lost
    // both; either still says metres a second when a range follows.
    [GeneratedRegex(@"(?<n>\d[0-9ODIBL]{0,3}|(?<![A-Z])[ODIBL][0-9ODIBL]{0,3})\s*M(?=\s*/|\s*[\(\[\{]|.{0,2}\d+\.\d{1,2}\s*KM)", RegexOptions.CultureInvariant)]
    private static partial Regex SpeedText();

    [GeneratedRegex(@"(?<n>\d{1,3}\.\d{1,2})\s*KM", RegexOptions.CultureInvariant)]
    private static partial Regex RangeText();

    /// <summary>The panel's own range: upper-case KM and a closing bracket. The world's labels write km.</summary>
    [GeneratedRegex(@"\d[.,]\d{1,2}\s*KM\s*[\)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex PanelRange();

    // A for the 4 after the point: the Blade's 10.4K read 10.AK. Only there -
    // before the point it would turn words into figures.
    [GeneratedRegex(@"(?<![\dA-Z.])(?<n>[0-9IO]{1,3}(\.[0-9IOA])?)K\b", RegexOptions.CultureInvariant)]
    private static partial Regex KFigure();

    // The closing speed, so often negative, and the unit read every way:
    // "25km [-8m/s]", "2.4km f-125m/c]", "3.0{m[75m/s]", "8.4km [98m/]".
    [GeneratedRegex(@"[\(\[\{f]\s*-?\s*\d{1,4}\s*m\s*/", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LockLabelText();
}
