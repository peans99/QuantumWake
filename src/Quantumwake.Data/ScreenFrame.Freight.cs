using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>A pane's load as the Freight Manager prints it: "CAPACITY: 94/1120 SCU".</summary>
public sealed record FreightCapacity(int Used, int Total);

/// <summary>
/// One size of crate in a contract's order, as the confirmation's tile shows
/// it: "8 SCU", the commodity under the tile.
/// </summary>
/// <remarks>
/// The tile also prints how many crates of that size, "11/11", in small
/// green type - and it is not taken. Neither engine reads it: on the 4.10
/// video Windows never did, and ONNX read the 8 SCU tile's "11/11" as "1/1"
/// as often as right, two reads agreeing on the wrong answer. A count read
/// as 1 that was 11 is a manifest short by 80 SCU and looks entirely
/// plausible, so there is no count rather than a wrong one.
/// </remarks>
/// <param name="SizeScu">The crate's size; only the sizes crates come in are taken.</param>
/// <param name="CommodityRead">The commodity as read, verbatim.</param>
/// <param name="Commodity">The catalogue's name for it, when it read as exactly one.</param>
public sealed record FreightCrates(int SizeScu, string? CommodityRead, string? Commodity);

/// <summary>One contract's cargo on the Freight Manager.</summary>
/// <param name="Contractor">The issuer over the title on the list screen ("COVALEX INDEPENDENT CONTRACTORS"); the confirmation does not print it.</param>
/// <param name="Title">The contract's title as the terminal prints it.</param>
/// <param name="SecondsLeft">The confirmation's "Time Remaining", in seconds.</param>
/// <param name="Crates">Each crate size the confirmation lists; empty on the list screen, which draws crates as pictures.</param>
public sealed record FreightOrder(string? Contractor, string Title, int? SecondsLeft, IReadOnlyList<FreightCrates> Crates);

/// <summary>What a hangar's Freight Manager terminal said.</summary>
/// <param name="Stage">
/// <c>list</c> for the two panes - ELEVATOR and WAREHOUSE - with the cargo
/// grouped by contract; <c>confirm</c> for the TRANSACTION CONFIRMATION over
/// them, which is the one place crate sizes and commodities are written
/// rather than drawn.
/// </param>
public sealed record FreightReading(
    string Stage,
    FreightCapacity? Elevator,
    FreightCapacity? Warehouse,
    IReadOnlyList<FreightOrder> Orders);

public static partial class ScreenFrames
{
    // ---- the Freight Manager ----

    /// <summary>The sizes crates come in (GameCrates), so a misread digit cannot invent a sixth.</summary>
    private static readonly HashSet<int> CrateSizes = [1, 2, 4, 8, 16, 24, 32];

    /// <summary>
    /// The hangar's Freight Manager terminal, or null when this is not one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Written from a video, not from this install: zirHaze Gaming's "Hauling
    /// in Star Citizen 4.10 - What Has Changed?" (27 Aug 2026, 1440p), whose
    /// frames read by Windows' engine are the fixtures. Like the commodity
    /// kiosk's first reader, what it defends is the shape of the screen.
    /// </para>
    /// <para>
    /// The title is the anchor - Windows read FREIGHT MANAGER on all 35 frames
    /// of the terminal, with the logo's glyph in front of it on most. The
    /// confirmation is told by its own heading, and its contracts by their
    /// "Time Remaining" lines rather than by the CONTRACT ORDERS tab: the SELL
    /// ORDERS tab is printed beside it either way.
    /// </para>
    /// </remarks>
    private static FreightReading? ReadFreight(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodities)
    {
        if (!lines.Any(line => Fold(line.Text).Contains("FREIGHTMANAGER", StringComparison.Ordinal))) return null;

        var (elevator, warehouse) = Capacities(lines);

        if (lines.Any(line => Fold(line.Text).Contains("TRANSACTIONCONFIRMATION", StringComparison.Ordinal)))
            return new FreightReading("confirm", elevator, warehouse, ConfirmedOrders(lines, commodities));

        return new FreightReading("list", elevator, warehouse, ListedOrders(lines));
    }

    /// <summary>
    /// The two panes' loads: the elevator's on the left, the warehouse's on
    /// the right. Windows read the warehouse's "0/5000" as "O/SOOO" and once
    /// as "n Isooo", so the figure is folded before it is taken, and refused
    /// when it still is not two numbers.
    /// </summary>
    private static (FreightCapacity? Elevator, FreightCapacity? Warehouse) Capacities(IReadOnlyList<ScreenTextLine> lines)
    {
        var read = lines
            .Where(line => Fold(line.Text).StartsWith("CAPACITY", StringComparison.Ordinal))
            .OrderBy(line => line.Left)
            .Select(line => (line.Left, Capacity: FreightFill(line.Text)))
            .ToList();

        if (read.Count == 0) return (null, null);
        if (read.Count >= 2) return (read[0].Capacity, read[^1].Capacity);

        // One label read: which pane it is by which side of the terminal's
        // text it sits on - the panes split the screen down the middle.
        var middle = (lines.Min(l => l.Left) + lines.Max(l => l.Left)) / 2;
        return read[0].Left < middle ? (read[0].Capacity, null) : (null, read[0].Capacity);
    }

    private static FreightCapacity? FreightFill(string text)
    {
        var at = text.IndexOf(':');
        if (at < 0) return null;
        var figure = new string([.. text[(at + 1)..].ToUpperInvariant().Select(c => c switch
        {
            'O' or 'D' or 'Q' => '0',
            'I' or 'L' or '|' => '1',
            'S' => '5',
            'B' => '8',
            'Z' => '2',
            _ => c,
        })]);
        var m = FreightFillRegex().Match(figure);
        return m.Success && int.TryParse(m.Groups["used"].Value, out var used) && int.TryParse(m.Groups["total"].Value, out var total)
            && total > 0 && used <= total
            ? new FreightCapacity(used, total)
            : null;
    }

    /// <summary>
    /// The list screen's contract groups: the issuer in small type, the title
    /// under it. The pilot's own belongings are grouped the same way under
    /// PERSONAL INVENTORY, and are not a contract.
    /// </summary>
    private static List<FreightOrder> ListedOrders(IReadOnlyList<ScreenTextLine> lines)
    {
        var orders = new List<FreightOrder>();
        foreach (var issuer in lines.OrderBy(l => l.Top))
        {
            var folded = Fold(issuer.Text);
            if (!folded.EndsWith("CONTRACTORS", StringComparison.Ordinal) && !folded.EndsWith("CONTRACTOR", StringComparison.Ordinal))
                continue;

            var title = lines
                .Where(l => l.Top > issuer.Top && l.Top - issuer.Top <= issuer.Height * 2.5)
                .Where(l => Math.Abs(l.Left - issuer.Left) <= issuer.Height * 3)
                .OrderBy(l => l.Top)
                .FirstOrDefault();

            if (title is not null && FreightTitle(title.Text) is { } named)
                orders.Add(new FreightOrder(issuer.Text.Trim(), named, null, []));
        }

        return orders;
    }

    /// <summary>
    /// The confirmation's contracts: each a title, its "Time Remaining", and
    /// a row of crate tiles under them, down to the next contract's title.
    /// </summary>
    private static List<FreightOrder> ConfirmedOrders(IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodities)
    {
        var clocks = lines
            .Where(l => Fold(l.Text).StartsWith("TIMEREMAIN", StringComparison.Ordinal))
            .OrderBy(l => l.Top)
            .ToList();

        var sizes = lines
            .Select(l => (Line: l, Size: CrateSize(l.Text)))
            .Where(s => s.Size is not null)
            .ToList();

        // Every block's title first: a block runs from its clock down to the
        // next block's title, and a tile's commodity must not be looked for
        // past it. On this install's 24 Oct frame the next title sat 94 px
        // under a tile, and the close read named it the tile's commodity.
        var blocks = clocks
            .Select(clock => (Clock: clock, Title: lines
                .Where(l => l.Top < clock.Top && clock.Top - l.Top <= clock.Height * 3)
                .Where(l => Math.Abs(l.Left - clock.Left) <= clock.Height * 2)
                .OrderByDescending(l => l.Top)
                .FirstOrDefault()))
            .ToList();

        var orders = new List<FreightOrder>();

        for (var i = 0; i < blocks.Count; i++)
        {
            var (clock, titleLine) = blocks[i];
            if (titleLine is null || FreightTitle(titleLine.Text) is not { } title) continue;

            var next = i + 1 < blocks.Count ? blocks[i + 1] : default;
            var bottom = next.Title?.Top ?? (next.Clock is { } c ? c.Top - c.Height * 3 : double.MaxValue);

            var crates = sizes
                .Where(s => s.Line.Top > clock.Top && s.Line.Top < bottom)
                .OrderBy(s => s.Line.Left)
                .Select(s => Crate(s.Line, s.Size!.Value, bottom, lines, commodities))
                .ToList();

            orders.Add(new FreightOrder(null, title, Clock(clock.Text), crates));
        }

        return orders;
    }

    /// <summary>
    /// One tile: the commodity printed under it, above <paramref name="bottom"/>.
    /// The count between the two is not taken; see <see cref="FreightCrates"/>.
    /// </summary>
    private static FreightCrates Crate(ScreenTextLine size, int scu, double bottom, IReadOnlyList<ScreenTextLine> lines, IReadOnlyList<string> commodities)
    {
        var h = size.Height;

        // Both engines' words for the commodity may be here: the whole read's
        // and the second look's. Whichever names a commodity wins; the count's
        // misreadings ("M", "/m", "nnt") are too short or not words.
        var words = lines
            .Where(l => l.Top < bottom)
            .Where(l => l.Top > size.Top + h * 2.5 && l.Top < size.Top + h * 6.5 && Math.Abs(l.Left - size.Left) <= h * 3)
            .Select(l => l.Text.Trim())
            .Where(t => t.Length >= 4 && t.All(c => char.IsLetter(c) || c == ' ') && CrateSize(t) is null)
            .ToList();

        var named = words.Select(w => (Read: w, Name: NameCommodity(w, commodities))).FirstOrDefault(w => w.Name is not null);

        return new FreightCrates(scu, named.Read ?? words.FirstOrDefault(), named.Name);
    }

    /// <summary>
    /// A tile's size, or null. Windows read every 8 as "B" and once a 2 as
    /// "z", so those two letters are folded - only in front of SCU, and only
    /// to a size crates are made in.
    /// </summary>
    internal static int? CrateSize(string text)
    {
        var m = CrateSizeRegex().Match(text.Trim());
        if (!m.Success) return null;
        var digits = m.Groups["n"].Value.ToUpperInvariant().Replace('B', '8').Replace('Z', '2');
        return int.TryParse(digits, out var n) && CrateSizes.Contains(n) ? n : null;
    }

    /// <summary>
    /// "Time Remaining: 07:17" in seconds, or null when the figure misread
    /// ("01:sa") - or read as 00:00. Both Red Wind hauls on this install's
    /// 24 Oct frames printed 00:00 with the contract open and accepted: a
    /// haul with no timer, not one whose time is up.
    /// </summary>
    private static int? Clock(string text)
    {
        var m = FreightClockRegex().Match(text);
        if (!m.Success) return null;
        var parts = new[] { m.Groups["a"].Value, m.Groups["b"].Value, m.Groups["c"].Value }
            .Where(p => p.Length > 0).Select(int.Parse).ToArray();
        var seconds = parts.Length == 3 ? parts[0] * 3600 + parts[1] * 60 + parts[2] : parts[0] * 60 + parts[1];
        return seconds > 0 ? seconds : null;
    }

    /// <summary>
    /// A contract title as printed, tidied: the bullet the engine reads for
    /// the terminal's separator ("ROOKIE RANK • DIRECT…") back to the dash,
    /// the rep ladder a Red Wind title carries cut off - in brackets when it
    /// read, as "tsan00/2001250…" when it did not, or only its last "R" - and
    /// the trailing mark after the title dropped. The "|" read as "1" is left:
    /// <see cref="SameContract"/> already folds it when a title is matched.
    /// </summary>
    private static string? FreightTitle(string text)
    {
        var title = TitleBracketRegex().Replace(text.Replace('•', '-').Replace('·', '-'), "");
        var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(w => !w.Contains('/') && !w.Contains('[') && !w.Contains('('))
            .ToList();
        if (words.Count > 2 && words[^1].Length == 1 && char.IsLetter(words[^1][0])) words.RemoveAt(words.Count - 1);
        title = string.Join(' ', words).Trim().TrimEnd('-', '•', '·', ' ', '.', ',');
        return title.Length >= 6 && !Fold(title).StartsWith("PERSONALINVENTORY", StringComparison.Ordinal) ? title : null;
    }

    [GeneratedRegex(@"(?<used>\d[\d,]*)\s*/\s*(?<total>\d[\d,]*)")]
    private static partial Regex FreightFillRegex();

    [GeneratedRegex(@"^(?<n>[0-9BbZz]{1,2})\s*S\s*C\s*[UuVv]\b", RegexOptions.IgnoreCase)]
    private static partial Regex CrateSizeRegex();

    [GeneratedRegex(@":\s*(?<a>\d{1,2}):(?<b>\d{2})(?::(?<c>\d{2}))?\b")]
    private static partial Regex FreightClockRegex();
}
