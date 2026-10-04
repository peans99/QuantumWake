using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One line of a scanned rock's composition.</summary>
/// <param name="Read">The mineral as the engine returned it.</param>
/// <param name="Mineral">The commodity table's name for it, when exactly one fits.</param>
/// <param name="Percent">Its share of the rock; 0 when the share did not read.</param>
/// <param name="Quality">The figure the panel prints beside it - the game's quality, 0 for inert material.</param>
public sealed record MiningScanPart(string Read, string? Mineral, double Percent, int? Quality);

/// <summary>What the scan-results panel said about a rock.</summary>
/// <param name="PrimaryRead">The mineral named under the title, as read.</param>
/// <param name="Primary">The commodity table's name for it, when exactly one fits.</param>
/// <param name="MassKg">The rock's mass. Kilograms, as the panel's figure is understood.</param>
/// <param name="ResistancePercent">The panel's resistance, in percent.</param>
/// <param name="Instability">The panel's instability, as printed - 21.89 on the first frame from this install, not a percentage.</param>
/// <param name="Scu">
/// The SCU the panel prints beside COMP.: what the rock would fill. A gem
/// cluster prints it in thousandths - "3.15m SCU" - and this is in SCU either way.
/// </param>
/// <param name="Difficulty">The word on the bar under the figures - EASY on every frame seen so far.</param>
public sealed record MiningScanReading(
    string? PrimaryRead,
    string? Primary,
    double? MassKg,
    double? ResistancePercent,
    double? Instability,
    double? Scu,
    string? Difficulty,
    IReadOnlyList<MiningScanPart> Parts)
{
    /// <summary>The shares added up, when every row has one.</summary>
    /// <remarks>
    /// <para>
    /// The panel's own checksum. Agreement between looks is not enough on its
    /// own: on the aphorite frame two sizes both read 23.74% as "3.74%", the
    /// leading 2 lost the same way twice, and the shares then came to 79.99.
    /// Every share is printed to two places, so a whole panel read right comes
    /// to 100 within a hundredth or two of rounding.
    /// </para>
    /// <para>
    /// Not used to correct anything. A sum that is twenty short says one share
    /// is misread and not which; the page says so beside the shares.
    /// </para>
    /// </remarks>
    public double? ShareTotal =>
        Parts.Count > 0 && Parts.All(p => p.Percent > 0) ? Math.Round(Parts.Sum(p => p.Percent), 2) : null;

    /// <summary>Whether <see cref="ShareTotal"/> is 100, give or take the rounding; null when a share did not read.</summary>
    public bool? SharesAddUp => ShareTotal is { } total ? Math.Abs(total - 100) <= 0.05 : null;
}

/// <summary>
/// The ship mining HUD's scan-results panel.
/// </summary>
/// <remarks>
/// <para>
/// First written from the Star Citizen Wiki's Alpha 4.7 crop, and rewritten from
/// the first frames this install took, on 2026-10-03 from a Golem at Daymar:
/// a silicon rock at 16:15:38 and an aphorite cluster at 16:02:48 and :49, all
/// 3440 × 1440. The live panel does not say what the wiki's said. Its title is
/// RESULTS, not SCAN RESULTS; its labels are MASS:, RES:, INST: and COMP., not
/// RESISTANCE, INSTABILITY and COMPOSITION; and each composition row is one
/// line - "8.56% SILICON (RAW)" - rather than a share column beside a name
/// column. The first reader asked for the wiki's words and so filed every real
/// scan as nothing. Both layouts read now, the wiki's kept as a fixture.
/// </para>
/// <para>
/// The labels are found by where they sit - in the title's column, below it -
/// as well as by what they say. The fracture HUD prints RESISTANCE and
/// INSTABILITY too, on the same frame, in another column, and a reader that
/// took the first such line anywhere would take the laser's figures for the
/// rock's. The mass label is found by position alone: the engine has read it
/// as "uss•.", "mss•.", "nss•.", "mgs:", "HAss:" and "RAss•." on six frames
/// and as MASS on none.
/// </para>
/// <para>
/// The panel's orange glow costs the whole-frame read most of its figures:
/// on the silicon frame it lost the mass, three shares' worth of names and
/// four of the five qualities. <see cref="MiningSecondLook"/> reads the panel
/// again at several sizes and keeps what two of them agree on.
/// </para>
/// </remarks>
public static partial class ScreenFrames
{
    /// <summary>
    /// What one look at the panel made of it, with the rows' heights kept so
    /// that several looks can be lined up row by row.
    /// </summary>
    internal sealed record ScanLook(MiningScanReading Reading, IReadOnlyList<double> RowTops, double TitleHeight);

    /// <summary>How far below the title the labels and rows reach, in title heights.</summary>
    /// <remarks>
    /// The last row sat 20.6 heights below RESULTS on the silicon frame and
    /// 17.6 on the aphorite; the wiki's COMPOSITION 10. Twenty-four leaves room
    /// for a rock of six minerals without reaching CARGO, which sat 25 below.
    /// </remarks>
    private const double PanelDepth = 24;

    /// <summary>How far a label may stand off the title's left edge, in title heights.</summary>
    /// <remarks>
    /// The labels step right as they go down - the panel is drawn in
    /// perspective - by 20 px over the five of them on a 17 px title. Three
    /// heights is more than twice that and a fraction of the gap to the
    /// fracture HUD's column.
    /// </remarks>
    private const double ColumnSlack = 3;

    private static readonly Regex PercentFigure = new(@"^(\d{1,3}(?:[.,]\d{1,2})?)\s*%$", RegexOptions.Compiled);
    private static readonly Regex ScuFigure = new(@"^(\d+(?:[.,]\d+)?)\s*(m)?\s*SCU$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WholeFigure = new(@"^\d{1,6}(?:[.,]\d{1,3})?$", RegexOptions.Compiled);
    private static readonly Regex LeadingShare = new(@"^\s*(?<share>\S+%)\s+(?<name>.+)$", RegexOptions.Compiled);

    private static MiningScanReading? ReadMiningScan(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodityNames) =>
        LookAtScan(lines, commodityNames)?.Reading;

    internal static ScanLook? LookAtScan(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodityNames)
    {
        var title = lines.FirstOrDefault(line => Is(line.Text, "SCAN RESULTS") || Is(line.Text, "RESULTS"));
        if (title is null) return null;

        var h = title.Height;
        var column = lines
            .Where(line => line != title && line.Top > title.Top && line.Top < title.Top + h * PanelDepth)
            .Where(line => Math.Abs(line.Left - title.Left) <= h * ColumnSlack)
            .OrderBy(line => line.Top)
            .ToList();

        var resistance = column.FirstOrDefault(line => Opens(line.Text, "RES"));
        var instability = column.FirstOrDefault(line => Opens(line.Text, "INST"));
        var composition = column.FirstOrDefault(line => Opens(line.Text, "COMP") || Opens(line.Text, "CONP"));

        // The title alone is not enough - other panels say "results" - and the
        // three labels together are what make it this one. The panel left
        // behind after a rock breaks keeps its title and loses INST, which is
        // the right answer for it: there is no rock to read.
        if (resistance is null || instability is null || composition is null) return null;

        var mass = column
            .Where(line => line.Top < resistance.Top && line.Top >= resistance.Top - h * 2.5)
            .OrderByDescending(line => line.Top)
            .FirstOrDefault();

        var massKg = mass is null ? null : FigureOn(lines, mass, allowPercent: false);
        var resistancePercent = FigureOn(lines, resistance, allowPercent: true);
        var instabilityFigure = FigureOn(lines, instability, allowPercent: false);

        var primaryLine = column
            .Where(line => line.Top < (mass ?? resistance).Top && line != mass)
            .FirstOrDefault(line => line.Text.Count(char.IsLetter) >= 3 && !WholeFigure.IsMatch(Digits(line.Text)));
        var primaryRead = primaryLine?.Text;
        var primary = primaryRead is null ? null : NameMineral(primaryRead, commodityNames);

        // On the composition row, to its right. Anywhere on the frame would
        // take "CARGO 0.00 / 32.00 SCU" under the panel, which the first
        // version of this did whenever the engine listed it first.
        var scu = lines
            .Where(line => line.Left > composition.Left && Math.Abs(line.Top - composition.Top) <= h * 1.2)
            .Select(line => ScuFigure.Match(Digits(line.Text).Trim()))
            .Where(match => match.Success)
            .Select(match => ParseFigure(match.Groups[1].Value) is { } value
                ? match.Groups[2].Success ? value / 1000 : value
                : (double?)null)
            .FirstOrDefault(value => value is not null);

        var near = lines.Where(line => line.Left >= title.Left - h * ColumnSlack && line.Left <= title.Left + h * 20).ToList();

        var difficulty = near
            .Where(line => line.Top > instability.Top && line.Top < composition.Top)
            .Select(line => line.Text.Trim())
            .FirstOrDefault(text => text.Length is >= 3 and <= 12 && text.All(c => char.IsLetter(c) || c == ' '));

        // The rows run from under COMP. to the LOCK / TARG / AUTO row, or
        // twelve heights when that did not read.
        var stop = near
            .Where(line => line.Top > composition.Top)
            .Where(line => ScreenInsight.Fold(line.Text) is var f && (f.Contains("IOCK") || f.Contains("TARG") || f.StartsWith("CARGO")))
            .Select(line => (double?)line.Top)
            .Min() ?? composition.Top + h * 12;

        var below = near
            .Where(line => line.Top > composition.Top + composition.Height * 0.5 && line.Top < stop - h * 0.3)
            .ToList();

        var parts = new List<MiningScanPart>();
        var tops = new List<double>();

        foreach (var name in below
            .Where(line => line.Text.Count(char.IsLetter) >= 4 && !ScuFigure.IsMatch(line.Text.Trim()))
            .OrderBy(line => line.Top))
        {
            var row = below.Where(line => line != name && Math.Abs(line.Top - name.Top) <= name.Height * 0.8).ToList();

            // The live panel prints the share on the name's own line; the
            // wiki's crop had it in a column to the left.
            string nameText = name.Text.Trim();
            double? share;

            if (LeadingShare.Match(nameText) is { Success: true } split)
            {
                nameText = split.Groups["name"].Value.Trim();
                share = Share(split.Groups["share"].Value);
            }
            else
            {
                share = row.Where(line => line.Left < name.Left).Select(line => Share(line.Text)).FirstOrDefault(v => v is not null);
            }

            var quality = row
                .Where(line => line.Left > name.Left)
                .OrderBy(line => Math.Abs(line.Top - name.Top))
                .Select(line => Quality(line.Text))
                .FirstOrDefault(q => q is not null);

            var inert = ScreenInsight.Fold(nameText) is var folded
                && (folded.Contains("INERT", StringComparison.Ordinal) || folded.Contains("MATERIAI", StringComparison.Ordinal));

            parts.Add(new MiningScanPart(nameText, inert ? "Inert materials" : NameMineral(nameText, commodityNames), share ?? 0, quality));
            tops.Add(name.Top);
        }

        return new ScanLook(
            new MiningScanReading(primaryRead, primary, massKg, resistancePercent, instabilityFigure, scu, difficulty, parts),
            tops,
            h);
    }

    /// <summary>
    /// Where a second look at the scan panel should point, or null when the
    /// frame is not a scan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only asked when the whole frame already read as a scan, so a second look
    /// never turns some other screen into one.
    /// </para>
    /// <para>
    /// The patch is generous above because the whole-frame read and the patch
    /// read disagree about where things are. On the silicon frame the whole
    /// read put RESULTS at y=512 and every patch read put it at 545; a plain
    /// crop of the file shows 542, so it is the whole read that sits 33 px
    /// high - and by the same 33 px on CARGO, 423 px further down, which makes
    /// it an offset rather than a scale. The patch reaches far enough either
    /// way, and the second look reads its rows from its own lines only, so the
    /// two are never mixed.
    /// </para>
    /// </remarks>
    public static ScreenPatch? ScanPanel(IReadOnlyList<ScreenTextLine> lines)
    {
        var all = lines
            .Select(line => line with { Text = line.Text.Trim() })
            .Where(line => line.Text.Length > 0)
            .ToList();

        if (LookAtScan(all, []) is null) return null;

        var title = all.First(line => Is(line.Text, "SCAN RESULTS") || Is(line.Text, "RESULTS"));
        var h = title.Height;

        return new ScreenPatch(
            Left: Math.Max(0, title.Left - h * 2),
            Top: Math.Max(0, title.Top - h),
            Width: h * 26,
            Height: h * 30);
    }

    /// <summary>
    /// One reading out of several looks at the same panel: a figure counts when
    /// two looks returned it, and a name when it is the one most looks gave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rule is <see cref="WalletSecondLook"/>'s. A single look at one size
    /// misreads in ways that look like figures - 572 as 72, 348 as 34B, 42.13%
    /// as 42A3% - and no two sizes made the same misreading on any frame
    /// measured. A figure no two looks agree on is left empty rather than
    /// chosen between.
    /// </para>
    /// <para>
    /// Names are matched against the commodity table before they get here, so
    /// one look naming a mineral is already a checked reading; the vote only
    /// settles looks that named different ones.
    /// </para>
    /// <para>
    /// Rows are lined up by height, because a look can drop a row - the x1 read
    /// of the silicon frame returned "8.56%" with no name - and lining them up
    /// by order would then pair each row with the one below it. A row only one
    /// look found is dropped: it is one read, with nothing to check it against.
    /// </para>
    /// </remarks>
    internal static MiningScanReading? Settle(IReadOnlyList<ScanLook> looks)
    {
        if (looks.Count == 0) return null;

        var readings = looks.Select(look => look.Reading).ToList();

        var primary = Most(readings.Select(r => r.Primary));
        var primaryRead = readings.FirstOrDefault(r => primary is not null && r.Primary == primary)?.PrimaryRead
            ?? readings.Select(r => r.PrimaryRead).FirstOrDefault(r => r is not null);

        var slack = looks.Average(look => look.TitleHeight) * 0.6;
        var rows = new List<List<MiningScanPart>>();
        var centres = new List<double>();

        foreach (var (look, i) in looks.SelectMany(look => look.RowTops.Select((top, i) => (look, i))).OrderBy(x => x.look.RowTops[x.i]))
        {
            var top = look.RowTops[i];
            var at = centres.FindIndex(centre => Math.Abs(centre - top) <= slack);

            if (at < 0)
            {
                centres.Add(top);
                rows.Add([look.Reading.Parts[i]]);
            }
            else
            {
                rows[at].Add(look.Reading.Parts[i]);
            }
        }

        var parts = rows
            .Where(row => row.Count >= WalletSecondLook.Agreement)
            .Select(row =>
            {
                var mineral = Most(row.Select(p => p.Mineral));
                var read = row.FirstOrDefault(p => mineral is not null && p.Mineral == mineral)?.Read ?? row[0].Read;
                var share = Agreed(row.Select(p => p.Percent > 0 ? p.Percent : (double?)null));
                var quality = Agreed(row.Select(p => (double?)p.Quality));

                return new MiningScanPart(read, mineral, share ?? 0, quality is { } q ? (int)q : null);
            })
            .ToList();

        return new MiningScanReading(
            primaryRead,
            primary,
            Agreed(readings.Select(r => r.MassKg)),
            Agreed(readings.Select(r => r.ResistancePercent)),
            Agreed(readings.Select(r => r.Instability)),
            Agreed(readings.Select(r => r.Scu)),
            Most(readings.Select(r => r.Difficulty)),
            parts);
    }

    /// <summary>The value at least two looks returned, the commonest when several did.</summary>
    private static double? Agreed(IEnumerable<double?> values) =>
        values
            .Where(v => v is not null)
            .GroupBy(v => v!.Value)
            .Where(g => g.Count() >= WalletSecondLook.Agreement)
            .OrderByDescending(g => g.Count())
            .Select(g => (double?)g.Key)
            .FirstOrDefault();

    private static string? Most(IEnumerable<string?> values) =>
        values
            .Where(v => v is not null)
            .GroupBy(v => v!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

    /// <summary>The figure on a label's row: after the colon in the label itself, or in the line beside it.</summary>
    /// <remarks>
    /// The whole line has to read as the figure. "21 .eø" is how the whole
    /// frame read an instability of 21.89, and taking the first run of digits
    /// out of it gave 21 - a figure, and wrong.
    /// </remarks>
    private static double? FigureOn(IReadOnlyList<ScreenTextLine> lines, ScreenTextLine label, bool allowPercent)
    {
        var colon = label.Text.IndexOf(':');
        var tail = colon >= 0 ? Digits(label.Text[(colon + 1)..]).Trim() : "";

        if (Figure(tail, allowPercent) is { } inline)
            return inline;

        return lines
            .Where(line => line != label && line.Left > label.Left)
            .Where(line => Math.Abs(line.Top - label.Top) <= label.Height * 1.2)
            .OrderBy(line => line.Left)
            .Select(line => Figure(Digits(line.Text).Trim(), allowPercent))
            .FirstOrDefault(value => value is not null);
    }

    private static double? Figure(string text, bool allowPercent)
    {
        if (allowPercent && text.EndsWith('%')) text = text[..^1].TrimEnd();
        return WholeFigure.IsMatch(text) ? ParseFigure(text) : null;
    }

    private static double? Share(string text) =>
        PercentFigure.Match(Digits(text).Replace(" ", "")) is { Success: true } m
        && ParseFigure(m.Groups[1].Value) is { } value && value <= 100
            ? value
            : null;

    /// <summary>
    /// The HUD draws zero with a slash through it, and the engine returns the
    /// glyph it looks like. Nothing else is changed: a letter in a figure is a
    /// misreading, and only <see cref="Quality"/> - a column that holds nothing
    /// but whole numbers - takes letters for the digits they resemble.
    /// </summary>
    private static string Digits(string text) => text.Replace('ø', '0').Replace('Ø', '0');

    /// <summary>
    /// A quality or quantity column's figure, letters read as the digits they
    /// were drawn as: "SIO" is 510, "58B" is 588, "34B" is 348.
    /// </summary>
    /// <remarks>
    /// Each of those three is a measured misreading with its right answer on
    /// the same screen. Only short tokens in columns of whole numbers are
    /// treated this way; a name or a label never is.
    /// </remarks>
    internal static int? Quality(string text)
    {
        var token = text.Trim();
        if (token.Length is 0 or > 4) return null;

        var digits = new string([.. token.Select(c => c switch
        {
            'O' or 'o' or 'ø' or 'Ø' or 'D' => '0',
            'I' or 'l' or '|' => '1',
            'S' or 's' => '5',
            'B' => '8',
            'Z' => '2',
            _ => c,
        })]);

        return digits.All(char.IsAsciiDigit) && int.TryParse(digits, out var value) ? value : null;
    }

    private static double? ParseFigure(string text) =>
        double.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>The commodity table's name for a mineral as read, when exactly one fits.</summary>
    /// <remarks>
    /// <para>
    /// The HUD and the table do not name minerals alike. The HUD prints
    /// "SILICON (RAW)" and "HEPH (RAW)"; the table this install builds has "Raw
    /// Silicon", "Silicon" and "Raw Hephaestanite". So after an exact match
    /// the raw or ore marker is taken off both sides and the bare names
    /// compared, the marker then choosing between "Raw Silicon" and the refined
    /// "Silicon"; and a bare name of four letters or more that begins exactly
    /// one of the table's - HEPH - is that one.
    /// </para>
    /// <para>
    /// Two letters of slack take a dropped or doubled stroke when only one
    /// name is that close; "ÄLUMNUX (ORE)" is three off and stays as read,
    /// named by nobody.
    /// </para>
    /// </remarks>
    internal static string? NameMineral(string read, IReadOnlyList<string> commodityNames)
    {
        var folded = ScreenInsight.Fold(read);
        var exact = commodityNames.Where(name => ScreenInsight.Fold(name) == folded).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (exact.Count == 1) return exact[0];

        var (readBase, readKind) = Bare(read);
        if (readBase.Length < 4) return null;

        var table = commodityNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => (Name: name, Bare: Bare(name)))
            .ToList();

        var close = table
            .Where(t => t.Bare.Name == readBase
                || (Math.Abs(t.Bare.Name.Length - readBase.Length) <= 1 && Within(t.Bare.Name, readBase, 2)))
            .ToList();

        if (close.Count == 0)
            close = [.. table.Where(t => t.Bare.Name.Length > readBase.Length && t.Bare.Name.StartsWith(readBase, StringComparison.Ordinal))];

        // One mineral, possibly under two names: the raw and refined of it.
        if (close.Select(t => t.Bare.Name).Distinct().Count() != 1) return null;

        var sameKind = close.Where(t => t.Bare.Kind == readKind).ToList();
        return sameKind.Count == 1 ? sameKind[0].Name : close.Count == 1 ? close[0].Name : null;
    }

    /// <summary>A name without its raw or ore marker, folded, and which marker it had.</summary>
    private static (string Name, string? Kind) Bare(string name)
    {
        var text = name.Trim();
        string? kind = null;

        if (MarkerAfter().Match(text) is { Success: true } after)
        {
            kind = after.Groups["kind"].Value.ToUpperInvariant();
            text = text[..after.Index];
        }
        else if (MarkerBefore().Match(text) is { Success: true } before)
        {
            kind = before.Groups["kind"].Value.ToUpperInvariant();
            text = text[before.Length..];
        }

        return (ScreenInsight.Fold(text), kind);
    }

    // "(RAW)", "(ORE)", and the engine's "CRAW)" for an opening bracket it
    // took for a C - which it did on three of the five silicon rows read.
    [GeneratedRegex(@"(?:\s*[\(\[]|\s+C|\s+)(?<kind>RAW|ORE)\s*\)?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex MarkerAfter();

    [GeneratedRegex(@"^(?<kind>RAW|ORE)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex MarkerBefore();

    /// <summary>Whether two strings are within a given edit distance - the cheap kind, substitutions and one length step.</summary>
    private static bool Within(string a, string b, int slack)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        var mismatches = 0;
        for (int i = 0, j = 0; i < a.Length && j < b.Length; i++, j++)
        {
            if (a[i] == b[j]) continue;
            mismatches++;
            if (mismatches > slack) return false;
            if (a.Length > b.Length) j--;
            else if (b.Length > a.Length) i--;
        }
        return mismatches + Math.Abs(a.Length - b.Length) <= slack;
    }
}
