using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One row of a refinery work order.</summary>
/// <param name="Read">The material as the engine returned it.</param>
/// <param name="Mineral">The commodity table's name for it, when exactly one fits.</param>
/// <param name="Quality">The game's quality figure for this lot.</param>
/// <param name="Quantity">What is in the hold, in cSCU - on the setup screen.</param>
/// <param name="Yield">What the refinery says it will give back, in cSCU. Unread or "--" when the lot is not being refined.</param>
/// <param name="ToDo">cSCU still to refine - on the processing screen.</param>
/// <param name="Done">cSCU refined so far - on the processing screen.</param>
public sealed record RefineryLot(
    string Read,
    string? Mineral,
    int? Quality,
    int? Quantity,
    int? Yield,
    int? ToDo = null,
    int? Done = null);

/// <summary>What a refinery terminal said.</summary>
/// <param name="Station">The station in the title, as read and with its words' case tidied.</param>
/// <param name="Stage">
/// <c>profile</c> before a work order is opened, <c>setup</c> while one is
/// being priced, <c>processing</c> once it is running.
/// </param>
/// <param name="Method">The refining method, named from the nine the game has when the read is within two letters of one.</param>
/// <param name="MethodRead">The method as the engine returned it.</param>
/// <param name="Ratings">The method's own line - "HIGH YIELD // LOW COST // SLOWEST" - when a method is chosen.</param>
/// <param name="InManifest">cSCU of refinable ore in the chosen hold.</param>
/// <param name="ToRefine">cSCU going into this order.</param>
/// <param name="Cost">The quote, in aUEC.</param>
/// <param name="Seconds">
/// On a setup screen the processing time quoted; on a processing screen the
/// time remaining. Either way, from the moment the shot was taken.
/// </param>
/// <param name="CapacityPercent">
/// The station's own load - 5,339% on the first frame from this install, with
/// the terminal warning of "a large surcharge".
/// </param>
/// <param name="Source">Where the ore is taken from, when a ship is picked - "Drake Golem".</param>
public sealed record RefineryReading(
    string? Station,
    string Stage,
    string? Method,
    string? MethodRead,
    string? Ratings,
    int? InManifest,
    int? ToRefine,
    IReadOnlyList<RefineryLot> Lots,
    decimal? Cost,
    int? Seconds,
    double? CapacityPercent,
    string? Source,
    int? Refinable,
    int? Inert);

/// <summary>
/// A station's refinery terminal.
/// </summary>
/// <remarks>
/// <para>
/// Written from four frames taken at MIC-L5 on 2026-10-03 between 23:08 and
/// 23:10, 3440 × 1440, one of each state: the station profile, a work order
/// being set up before and after a method was picked, and the order running.
/// The terminal reads far better than the mining HUD - white on dark, upright
/// - and the whole-frame read returned every label and nearly every figure.
/// What it missed: one yield of three on the quote (15 cSCU of agricium), the
/// silicon lot's quality on the processing screen, and the station's yield
/// bonuses, which are small green type and never read at all; the UEX feed
/// already carries those.
/// </para>
/// <para>
/// This is the screen that answers what <c>docs/mining.md</c> said nobody
/// publishes. The game's files name nine refining processes and give none of
/// them a yield, a cost or a time; the terminal prints all three for the ore in
/// front of it. On the quote measured, Pyrometric Chromalysis at MIC-L5 gave
/// 64 of 142 cSCU of silicon and 15 of 33 of agricium - 45% both - for 121
/// aUEC and 6 m 35 s, and the pilot's balance on the next frame was 121 lower.
/// </para>
/// <para>
/// Columns are placed by the header row rather than by order, because a row
/// whose yield did not read has two figures where its neighbour has three. On
/// the processing screen the engine ran QUALITY and YIELD together into one
/// header; the four columns there are evenly spaced - 61, 59 and 59 px apart
/// on the frame measured - so the yield column is put one spacing left of TO DO.
/// </para>
/// </remarks>
public static partial class ScreenFrames
{
    /// <summary>
    /// The game's nine refining methods, as UEX lists them (fetched 2026-10-03)
    /// and as the terminal printed the one chosen that evening.
    /// </summary>
    /// <remarks>
    /// The game's own <c>RefiningProcess</c> records carry the names too; they
    /// are not read here because nothing else in the app needs that table, and
    /// a method renamed in a patch shows up as read, unnamed, rather than wrong.
    /// </remarks>
    public static readonly IReadOnlyList<string> RefiningMethods =
    [
        "Cormack", "Dinyx Solventation", "Electrostarolysis", "Ferron Exchange", "Gaskin Process",
        "Kazen Winnowing", "Pyrometric Chromalysis", "Thermonatic Deposition", "XCR Reaction",
    ];

    private static RefineryReading? ReadRefinery(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodityNames)
    {
        var system = lines.FirstOrDefault(line => Opens(line.Text, "REFINERY SYSTEM") || Opens(Unslashed(line.Text), "REFINERY SYSTEM"));

        // Both, because each alone is a phrase other screens could carry. On
        // the four frames measured both read every time.
        if (system is null || !lines.Any(line => Is(line.Text, "REFINEMENT CENTER"))) return null;

        var h = system.Height;

        var stationLine = lines
            .Where(line => line != system && line.Top > system.Top && line.Top <= system.Top + h * 4)
            .Where(line => Math.Abs(line.Left - system.Left) <= h * 3)
            .OrderByDescending(line => line.Height)
            .FirstOrDefault();

        var station = stationLine is null ? null : Tidy(stationLine.Text);

        var workOrder = lines.FirstOrDefault(line => Opens(line.Text, "WORK ORDER"));
        var remaining = lines.FirstOrDefault(line => Opens(line.Text, "TIME REMAINING"));
        var totalCost = lines.FirstOrDefault(line => Opens(line.Text, "TOTAL COST"));
        var processingTime = lines.FirstOrDefault(line => Opens(line.Text, "PROCESSING TIME"));

        var stage = workOrder is null ? "profile"
            : remaining is not null ? "processing"
            : "setup";

        // ---- the method ----
        var selection = lines.FirstOrDefault(line => ScreenInsight.Fold(line.Text).Contains("PROCESSINGSEIECTION", StringComparison.Ordinal));
        string? methodRead = null;

        if (selection is not null)
        {
            methodRead = lines
                .Where(line => line.Top > selection.Top && line.Top <= selection.Top + selection.Height * 3)
                .Where(line => Math.Abs(line.Left - selection.Left) <= selection.Height * 3)
                .OrderBy(line => line.Top)
                .Select(line => line.Text.Trim())
                .FirstOrDefault(text => text.Count(char.IsLetter) >= 4);
        }

        // "Select an option" is the selector before a method is picked.
        if (methodRead is not null && Opens(methodRead, "SELECT")) methodRead = null;
        var method = methodRead is null ? null : NameMethod(methodRead);

        // The terminal prints a ratings line before any method is chosen too,
        // and it describes nothing, so it is only kept beside a method.
        var ratings = method is null && methodRead is null ? null : lines
            .Select(line => line.Text.Trim())
            .FirstOrDefault(text => text.Contains("//") && text.Contains("YIELD", StringComparison.OrdinalIgnoreCase)
                && text.Contains("COST", StringComparison.OrdinalIgnoreCase));

        // ---- the manifest ----
        var inManifest = FigureUnder(lines, "IN MANIFEST");
        var toRefine = FigureUnder(lines, "TO REFINE");
        var refinable = lines.Select(line => CscuLine().Match(line.Text.Trim())).FirstOrDefault(m => m.Success && m.Groups["what"].Value.StartsWith("REF", StringComparison.OrdinalIgnoreCase));
        var inert = lines.Select(line => CscuLine().Match(line.Text.Trim())).FirstOrDefault(m => m.Success && m.Groups["what"].Value.StartsWith("INE", StringComparison.OrdinalIgnoreCase));

        var sourceLabel = lines.FirstOrDefault(line => ScreenInsight.Fold(line.Text).Contains("MATERIAISEIECTION", StringComparison.Ordinal));
        var source = sourceLabel is null ? null : lines
            .Where(line => line.Top > sourceLabel.Top && line.Top <= sourceLabel.Top + sourceLabel.Height * 3)
            .Where(line => Math.Abs(line.Left - sourceLabel.Left) <= sourceLabel.Height * 3)
            .OrderBy(line => line.Top)
            .Select(line => line.Text.Trim())
            .FirstOrDefault(text => text.Count(char.IsLetter) >= 3);

        if (source is not null && Opens(source, "SELECT")) source = null;

        var cost = totalCost is null ? null : OnRow(lines, totalCost).Select(Money).FirstOrDefault(v => v is not null);
        var timeLabel = remaining ?? processingTime;
        var seconds = timeLabel is null ? null : OnRow(lines, timeLabel).Select(Duration).FirstOrDefault(v => v is not null);

        var capacityLabel = lines.FirstOrDefault(line => Opens(line.Text, "CURRENT CAPACITY"));
        var capacity = capacityLabel is null ? null : OnRow(lines, capacityLabel)
            .Select(text => Figure(Digits(text), allowPercent: true) is { } v && text.TrimEnd().EndsWith('%') ? v : (double?)null)
            .FirstOrDefault(v => v is not null);

        return new RefineryReading(
            station,
            stage,
            method,
            methodRead,
            ratings,
            inManifest,
            toRefine,
            ReadLots(lines, commodityNames),
            cost,
            seconds,
            capacity,
            source,
            refinable is { Success: true } r ? int.Parse(r.Groups["n"].Value, CultureInfo.InvariantCulture) : null,
            inert is { Success: true } i ? int.Parse(i.Groups["n"].Value, CultureInfo.InvariantCulture) : null);
    }

    /// <summary>The balance under // FUNDS in the terminal's corner.</summary>
    /// <remarks>
    /// Read as "867.190 al-JEC" on the quote and "867.069" on the next frame,
    /// 121 apart - the quote's cost. A figure that lost digits ("867" on the
    /// setup frame before it) is no balance and reads as none.
    /// </remarks>
    private static long? RefineryFunds(IReadOnlyList<ScreenTextLine> lines)
    {
        var label = lines.FirstOrDefault(line => Is(line.Text, "FUNDS") || Is(Unslashed(line.Text), "FUNDS"));
        if (label is null) return null;

        return lines
            .Where(line => line.Top > label.Top && line.Top <= label.Top + label.Height * 3)
            .Where(line => Math.Abs(line.Left - label.Left) <= label.Height * 2)
            .Select(line => LeadingBalance().Match(line.Text.Trim()))
            .Where(m => m.Success)
            .Select(m => long.TryParse(m.Groups["n"].Value.Replace(",", "").Replace(".", "").Replace(" ", ""), out var v) ? v : (long?)null)
            .FirstOrDefault(v => v is not null);
    }

    private static List<RefineryLot> ReadLots(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodityNames)
    {
        var header = lines.FirstOrDefault(line =>
            Opens(line.Text, "MATERIALS SELECTED") || Opens(line.Text, "MATERIALS YIELDED"));
        if (header is null) return [];

        var h = header.Height;
        var headerRow = lines.Where(line => line != header && Math.Abs(line.Top - header.Top) <= h && line.Left > header.Left).ToList();

        double? Anchor(string word) => headerRow.FirstOrDefault(line => Opens(line.Text, word))?.Left;

        var quality = Anchor("QUALITY");
        var todo = Anchor("TO DO");
        var done = Anchor("DONE");
        var qty = Anchor("QTY");
        var yield = todo is { } t && done is { } d ? t - (d - t) : Anchor("YIELD");

        var anchors = new List<(string Column, double At)>();
        if (quality is { } q) anchors.Add(("quality", q));
        if (qty is { } n) anchors.Add(("qty", n));
        if (yield is { } y) anchors.Add(("yield", y));
        if (todo is { } td) anchors.Add(("todo", td));
        if (done is { } dn) anchors.Add(("done", dn));

        if (anchors.Count == 0) return [];

        var stop = lines
            .Where(line => line.Top > header.Top + h)
            .Where(line => Opens(line.Text, "TOTAL COST") || Is(line.Text, "YIELD") || Opens(line.Text, "TIME REMAINING")
                || Opens(line.Text, "PROCESSING TIME"))
            .Select(line => (double?)line.Top)
            .Min() ?? header.Top + h * 30;

        var names = lines
            .Where(line => line.Top > header.Top + h * 0.8 && line.Top < stop)
            .Where(line => line.Left >= header.Left - h && line.Left <= header.Left + h * 6)
            .Where(line => line.Text.Count(char.IsLetter) >= 4)
            .OrderBy(line => line.Top)
            .ToList();

        var lots = new List<RefineryLot>();

        foreach (var name in names)
        {
            var figures = new Dictionary<string, (int Value, double Off)>();

            foreach (var cell in lines.Where(line => line != name && line.Left > name.Left + h * 4
                && Math.Abs(line.Top - name.Top) <= name.Height * 0.9))
            {
                if (Quality(cell.Text) is not { } value) continue;

                var (column, at) = anchors.MinBy(a => Math.Abs(a.At - cell.Left));
                var off = Math.Abs(at - cell.Left);

                if (!figures.TryGetValue(column, out var held) || off < held.Off)
                    figures[column] = (value, off);
            }

            int? Get(string column) => figures.TryGetValue(column, out var f) ? f.Value : null;

            // "e SILICON (RAW)": the row's icon, read as a letter.
            var read = LeadingIcon().Replace(name.Text.Trim(), "");

            lots.Add(new RefineryLot(
                read,
                NameMineral(read, commodityNames),
                Get("quality"),
                Get("qty"),
                Get("yield"),
                Get("todo"),
                Get("done")));
        }

        return lots;
    }

    private static string? NameMethod(string read)
    {
        var folded = ScreenInsight.Fold(read);
        var close = RefiningMethods
            .Where(m => ScreenInsight.Fold(m) == folded
                || (Math.Abs(ScreenInsight.Fold(m).Length - folded.Length) <= 1 && Within(ScreenInsight.Fold(m), folded, 2)))
            .ToList();

        return close.Count == 1 ? close[0] : null;
    }

    /// <summary>The figure printed under a small label - IN MANIFEST's 682.</summary>
    private static int? FigureUnder(IReadOnlyList<ScreenTextLine> lines, string label)
    {
        var at = lines.FirstOrDefault(line => Opens(line.Text, label) || Opens(Unslashed(line.Text), label));
        if (at is null) return null;

        var h = at.Height;

        return lines
            .Where(line => line.Top > at.Top && line.Top <= at.Top + h * 3)
            .Where(line => line.Left >= at.Left - h && line.Left <= at.Left + h * 12)
            .OrderBy(line => line.Top)
            .Select(line => Quality(line.Text) is { } v && line.Text.Trim().Length >= 1 && line.Text.Any(char.IsAsciiDigit) ? v : (int?)null)
            .FirstOrDefault(v => v is not null);
    }

    /// <summary>The text of every line on a label's row, to its right, left to right.</summary>
    private static IEnumerable<string> OnRow(IReadOnlyList<ScreenTextLine> lines, ScreenTextLine label) =>
        lines
            .Where(line => line != label && line.Left > label.Left)
            .Where(line => Math.Abs(line.Top - label.Top) <= Math.Max(line.Height, label.Height) * 1.2)
            .OrderBy(line => line.Left)
            .Select(line => line.Text.Trim());

    private static decimal? Money(string text) =>
        MoneyFigure().Match(text.Replace(" ", "")) is { Success: true } m
        && decimal.TryParse(m.Groups["whole"].Value.Replace(",", "") + (m.Groups["cents"].Success ? "." + m.Groups["cents"].Value : ""),
            NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;

    private static int? Duration(string text)
    {
        var m = DurationFigure().Match(text.Trim());
        if (!m.Success || m.Value.Trim().Length == 0) return null;

        int Part(string group) => m.Groups[group].Success ? int.Parse(m.Groups[group].Value, CultureInfo.InvariantCulture) : 0;

        var total = Part("d") * 86400 + Part("h") * 3600 + Part("m") * 60 + Part("s");
        return total > 0 ? total : null;
    }

    /// <summary>"MIC-L5 MODERN ICARUS STATION" as "MIC-L5 Modern Icarus Station" - the log's own spelling.</summary>
    private static string Tidy(string text) =>
        string.Join(' ', text.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Any(char.IsDigit) || word.Length <= 1
                ? word
                : char.ToUpperInvariant(word[0]) + word[1..].ToLowerInvariant()));

    /// <summary>The terminal's "// " section marks, and the engine's renderings of them.</summary>
    private static string Unslashed(string text) => SectionMark().Replace(text.Trim(), "");

    [GeneratedRegex(@"^\s*\S{1,2}\s+(?=\S{3})")]
    private static partial Regex LeadingIcon();

    // "//", and the "k/", "i/", "4/" the engine made of it on the frames measured.
    [GeneratedRegex(@"^\s*\S?/{1,2}\s*")]
    private static partial Regex SectionMark();

    [GeneratedRegex(@"^(?<n>\d+)\s*CSCU\s+(?<what>[A-Z ]+)$", RegexOptions.IgnoreCase)]
    private static partial Regex CscuLine();

    [GeneratedRegex(@"^(?<n>\d{1,3}(?:[.,]\d{3})+)")]
    private static partial Regex LeadingBalance();

    [GeneratedRegex(@"^(?<whole>\d{1,3}(?:,\d{3})*|\d+)(?:\.(?<cents>\d{2}))?$")]
    private static partial Regex MoneyFigure();

    [GeneratedRegex(@"^(?:(?<d>\d+)\s*d)?\s*(?:(?<h>\d+)\s*h)?\s*(?:(?<m>\d+)\s*m)?\s*(?:(?<s>\d+)\s*s)?$", RegexOptions.IgnoreCase)]
    private static partial Regex DurationFigure();
}
