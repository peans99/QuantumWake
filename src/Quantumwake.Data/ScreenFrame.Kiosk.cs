using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>One commodity on a kiosk's list.</summary>
/// <param name="Read">The name as the engine returned it.</param>
/// <param name="Commodity">The catalogue's name for it, when exactly one fits.</param>
/// <param name="State">"MAX INVENTORY" and the like, as printed under the name.</param>
/// <param name="Price">The figure beside the commodity, with its suffix already applied.</param>
/// <param name="PriceUnit">
/// What the price is <i>per</i>, in the kiosk's own word - <c>SCU</c> or
/// <c>UNITS</c>. Kept and never converted. The two are not the same quantity,
/// and a hundredfold error that still looks like a plausible integer is
/// exactly how centi-SCU shipped twice.
/// </param>
public sealed record KioskRow(
    string Read,
    string? Commodity,
    string? State,
    double? Quantity,
    string? QuantityUnit,
    double? Price,
    string? PriceUnit);

/// <summary>What a commodity kiosk said.</summary>
/// <param name="Buying">
/// True on the buy side, false on the sell side, null when neither tab read.
/// The two sides mean opposite things about a price, so a reading that cannot
/// tell them apart says so rather than picking.
/// </param>
/// <param name="BalanceRead">
/// The balance as printed, verbatim. See <see cref="KioskReading.Balance"/>
/// for when there is a number beside it and when there is not.
/// </param>
public sealed record KioskReading(
    bool? Buying,
    string? ShipRead,
    string? Ship,
    double? CargoUsed,
    double? CargoCapacity,
    string? BalanceRead,
    IReadOnlyList<KioskRow> Rows)
{
    /// <summary>The balance as a number, only when the kiosk printed every digit of it.</summary>
    /// <remarks>
    /// Two kiosks have been photographed and they print the balance two ways.
    /// Somebody else's terminal abbreviated it - <c>¤1,583M AUEC</c> - which
    /// has dropped however many digits the suffix stands for, and a figure
    /// taken from that would give the wallet check a baseline wrong by
    /// whatever the rounding hid, with every later drift inheriting it. This
    /// pilot's own kiosk, on 10 Sep 2026, printed <c>¤2,092,773 AUEC</c> in
    /// full and in the regular face - the only place on any screen the figure
    /// has ever read, since the mobiGlas bar sets it in a face the engine does
    /// not. So every digit on the screen is a figure; a suffix is kept as
    /// printed and never becomes one.
    /// </remarks>
    public long? Balance => ScreenFrames.FullFigure(BalanceRead);
}

public static partial class ScreenFrames
{
    /// <summary>A commodity kiosk, or null when this is not one.</summary>
    /// <remarks>
    /// <para>
    /// Written from one frame, and that frame is not from this install - it is
    /// a photograph of somebody else's terminal - so what it defends is the
    /// shape of the screen rather than any number in this app. Marked as such
    /// in <c>docs/screen-insight.md</c>, because every other measurement in
    /// this feature came from the logs and screenshots on this machine.
    /// </para>
    /// <para>
    /// Each row is anchored on its own <c>SHOP QUANTITY</c> heading, which is
    /// the one label the kiosk repeats once per commodity. The name sits to
    /// its left on the same line, the stock directly beneath it, and the price
    /// beneath that.
    /// </para>
    /// </remarks>
    private static KioskReading? ReadKiosk(
        IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodities, IReadOnlyList<string> shipNames)
    {
        if (!lines.Any(line => Is(line.Text, "COMMODITIES"))) return null;

        var anchors = lines.Where(line => Opens(line.Text, "SHOP QUANTITY")).OrderBy(line => line.Top).ToList();

        var shop = lines.Any(line => Is(line.Text, "SHOP INVENTORY"));
        var mine = lines.Any(line => Is(line.Text, "YOUR INVENTORIES"));

        // The heading needs one corroborating word: "COMMODITIES" on its own
        // could be a sign on a wall.
        if (!shop && !mine && anchors.Count == 0 && !lines.Any(line => Opens(line.Text, "CARGO CAPACITY")))
            return null;

        // Which way the counter faces. The buy side prints the shop's stock in
        // its own panel; the sell side lists what is in the pilot's hold and
        // has no such panel, so its absence is the tell.
        bool? buying = shop ? true : mine ? false : null;

        // The shop's own panel. A kiosk has two, and the pilot's inventory on
        // the left prints a ship and a cargo grid on the very rows the shop
        // prints its commodities: reading across both named a cargo grid as a
        // commodity, at a price, which is the kind of wrong answer that gets
        // believed.
        // Where the shop's panel begins. Its own heading when that read; the
        // headings of its rows, set back by the width of a commodity name,
        // when it did not. Zero when there are no rows at all, which is the
        // sell side, and the loop below then does not run.
        var panel = lines.FirstOrDefault(line => Is(line.Text, "SHOP INVENTORY"));

        var panelLeft = panel?.Left
            ?? (anchors.Count > 0 ? anchors.Min(a => a.Left) - anchors[0].Height * 25 : 0);

        // Only the buy side yields rows today. The sell side has been seen -
        // it lists the hold rather than the shop, with no "SHOP QUANTITY" to
        // anchor on - but only in a photograph of a screen too small to read,
        // so nothing is written for it. The frame is filed and kept whole,
        // which is how every reader here got written.
        var rows = new List<KioskRow>();

        foreach (var anchor in anchors)
        {
            // The name: inside the shop's panel, left of the heading, and the
            // thing most on the heading's own row. Nearest rather than
            // leftmost, because the ship rendered behind the glass leaves
            // words on these rows - a stray "Ton" seventeen pixels off took
            // the Titanium row, while the Titanium itself was five pixels off.
            var named = lines
                .Where(line => line.Left >= panelLeft - anchor.Height)
                .Where(line => line.Left < anchor.Left - anchor.Height)
                .Where(line => Math.Abs(line.Top - anchor.Top) <= anchor.Height * 1.2)
                .Select(line => (Line: line, Text: LeadingGlyph().Replace(line.Text, "").Trim()))
                .Where(found => found.Text.Length >= 3)
                .OrderBy(found => Math.Abs(found.Line.Top - anchor.Top))
                .ThenBy(found => found.Line.Left)
                .FirstOrDefault();

            if (named.Line is not { } name) continue;

            var text = named.Text;

            // Everything printed under this row's heading, down to the next.
            var next = anchors.FirstOrDefault(a => a.Top > anchor.Top + anchor.Height);
            var bottom = next?.Top ?? double.MaxValue;

            bool Below(ScreenTextLine line) => line.Top > anchor.Top && line.Top < bottom;

            var stock = lines
                .Where(Below)
                .Where(line => Math.Abs(line.Left - anchor.Left) <= anchor.Height * 3)
                .Select(line => QuantityRegex().Match(line.Text))
                .FirstOrDefault(m => m.Success);

            var price = lines
                .Where(Below)
                .Select(line => PriceRegex().Match(line.Text))
                .FirstOrDefault(m => m.Success);

            var state = lines
                .Where(Below)
                .Where(line => Math.Abs(line.Left - name.Left) <= name.Height * 3)
                .Select(line => line.Text)
                .FirstOrDefault(t => Fold(t).EndsWith("INVENTORY", StringComparison.Ordinal));

            rows.Add(new KioskRow(
                text,
                NameCommodity(text, commodities),
                state,
                stock is null ? null : Number(stock.Groups["n"].Value),
                stock?.Groups["unit"].Value is { Length: > 0 } qu ? qu.ToUpperInvariant() : null,
                price is null ? null : Money(price.Groups["n"].Value, price.Groups["suffix"].Value),
                price?.Groups["unit"].Value is { Length: > 0 } pu ? Unit(pu) : null));
        }

        // The left panel: the ship the cargo would go in, and how full it is.
        var capacity = lines.FirstOrDefault(line => Opens(line.Text, "CARGO CAPACITY"));
        Match? fill = null;

        if (capacity is not null)
        {
            fill = lines
                .Where(line => Math.Abs(line.Top - capacity.Top) <= capacity.Height * 1.2)
                .Select(line => FillRegex().Match(line.Text))
                .FirstOrDefault(m => m.Success);
        }

        var inventories = lines.FirstOrDefault(line => Is(line.Text, "YOUR INVENTORIES"));

        var shipRead = inventories is null ? null : lines
            .Where(line => line.Top > inventories.Top && line.Top < inventories.Top + inventories.Height * 4)
            .Where(line => Math.Abs(line.Left - inventories.Left) <= inventories.Height * 2)
            .OrderBy(line => line.Top)
            .FirstOrDefault()?.Text;

        var (ship, _) = shipRead is null ? (null, []) : NameShip(shipRead, shipNames);

        // The figure sits under its label, not beside it, on both kiosks
        // photographed - and on this install's the close button's "x" sat
        // beside the label instead, so asking for what is beside it first
        // returned that and the balance was never looked for. The unit is
        // the tell: the balance is the highest line that carries it.
        var balance = lines
            .Where(line => Fold(line.Text).EndsWith("AUEC", StringComparison.Ordinal))
            .OrderBy(line => line.Top)
            .Select(line => line.Text)
            .FirstOrDefault()
            ?? Beside(lines, "CURRENT BALANCE");

        return new KioskReading(
            buying, shipRead, ship,
            fill is null ? null : Number(fill.Groups["used"].Value),
            fill is null ? null : Number(fill.Groups["of"].Value),
            balance,
            rows);
    }

    /// <summary>A whole number of aUEC, or null when a suffix or anything else stands in for digits.</summary>
    internal static long? FullFigure(string? read)
    {
        if (read is null) return null;

        var match = BalanceRegex().Match(read);
        if (!match.Success) return null;

        var digits = new string([.. match.Groups["n"].Value.Where(char.IsAsciiDigit)]);
        return long.TryParse(digits, out var value) ? value : null;
    }

    // The currency glyph, which the engine renders as anything; then the
    // digits, in groups of three with a separator the engine may have padded
    // - "2,092, 773" is how this install's kiosk read; then the unit. A
    // letter between the digits and the unit is a suffix, and fails it.
    [GeneratedRegex(@"^\D{0,3}(?<n>\d{1,3}(?:[,.]\s?\d{3})+|\d{1,3})\s*AUEC\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex BalanceRegex();

    /// <summary>Fold, but without the confusions - a plain uppercase key.</summary>
    private static string Fold(string text) => ScreenInsight.Plain(text).ToUpperInvariant();

    /// <summary>The catalogue's name for a read commodity, or null.</summary>
    /// <remarks>
    /// Exact and confusable only, and a tie names nothing - the same three
    /// rules the parts use. Commodity names are short and there are hundreds
    /// of them, so a looser match here would be the most dangerous one in the
    /// feature: it decides what a price is a price <i>of</i>.
    /// </remarks>
    private static string? NameCommodity(string read, IReadOnlyList<string> commodities)
    {
        var plain = ScreenInsight.Plain(read);
        if (plain.Length < 3) return null;

        var folded = ScreenInsight.Fold(read);

        var hits = commodities
            .Where(name => string.Equals(ScreenInsight.Plain(name), plain, StringComparison.OrdinalIgnoreCase)
                || ScreenInsight.Fold(name) == folded)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return hits.Count == 1 ? hits[0] : null;
    }

    /// <summary>The unit a price is per, in the kiosk's own word.</summary>
    private static string Unit(string read)
    {
        var folded = Fold(read);
        return folded.StartsWith("UNIT", StringComparison.Ordinal) ? "UNITS" : "SCU";
    }

    /// <summary>
    /// A price, with the thousands suffix the kiosk sometimes uses applied.
    /// </summary>
    /// <remarks>
    /// The kiosk printed <c>2.19700002K/UNITS</c> on the frame measured - the
    /// game's own float noise for 2,197 - so the suffix is multiplied and the
    /// noise left where it is rather than rounded away. What the reading is
    /// good for is the order of magnitude and the unit; the last decimal of a
    /// number the game itself printed wrong is not something to defend.
    /// </remarks>
    private static double? Money(string figure, string suffix)
    {
        if (Number(figure) is not { } value) return null;

        return suffix.ToUpperInvariant() switch
        {
            "K" => value * 1_000,
            "M" => value * 1_000_000,
            _ => value,
        };
    }

    private static double? Number(string text)
    {
        var cleaned = text.Replace(",", "").Replace(" ", "");

        return double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
    }

    // "5,000 SCU". The unit is kept because a kiosk stocks by SCU and prices
    // by SCU or by unit, and the two are not always the same word.
    [GeneratedRegex(@"^(?<n>[\d,]+(?:\.\d+)?)\s*(?<unit>SCU|UNITS?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityRegex();

    // "¤296/SCU" and "¤2.19700002K/UNITS". The currency glyph reads as Ä, n,
    // ¤ or nothing at all, so anything that is not a digit opens the line.
    [GeneratedRegex(@"^\D{0,3}(?<n>\d[\d,]*(?:\.\d+)?)\s*(?<suffix>[KkMm]?)\s*/\s*(?<unit>UNITS?|SCU)\b", RegexOptions.IgnoreCase)]
    private static partial Regex PriceRegex();

    // "0 / 64 SCU".
    [GeneratedRegex(@"(?<used>[\d,]+(?:\.\d+)?)\s*/\s*(?<of>[\d,]+(?:\.\d+)?)\s*SCU", RegexOptions.IgnoreCase)]
    private static partial Regex FillRegex();

    // The icon beside a commodity name, when the engine made a letter of it.
    [GeneratedRegex(@"^[A-Za-z0-9]\s+(?=[A-Za-z]{3})")]
    private static partial Regex LeadingGlyph();
}
