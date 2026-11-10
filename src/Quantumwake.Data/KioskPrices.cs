namespace Quantumwake.Data;

/// <summary>One price read off a kiosk, set beside UEX's for the same counter.</summary>
/// <param name="At">When the frame was taken.</param>
/// <param name="Shot">The screenshot, or the live reading's name.</param>
/// <param name="Commodity">What the price is of, as the kiosk names it.</param>
/// <param name="Buying">True when it is what the kiosk charges, false when it is what it pays.</param>
/// <param name="Price">aUEC per SCU, as read.</param>
/// <param name="Stock">The shop's stock, SCU, when read.</param>
/// <param name="Place">Where the logs put the pilot when the frame was taken; null when no session covers it.</param>
/// <param name="Terminal">The UEX counter that place matched; null when none did.</param>
/// <param name="Uex">
/// UEX's figure for the same side: at <paramref name="Terminal"/> when that
/// matched, otherwise the nearest of every counter's. Null when UEX lists none.
/// </param>
/// <param name="UexAt">When UEX's figure was last reported.</param>
/// <param name="UexCounters">
/// The counters <paramref name="Uex"/> came from - the one at the place, or
/// when the place matched none, every counter quoting the same figure.
/// </param>
/// <param name="AtThisCounter">Whether <paramref name="Uex"/> is the place's own counter rather than the nearest anywhere.</param>
/// <param name="Frames">Frames that read the same price at the same place within half an hour, folded into this one.</param>
/// <param name="Verdict">
/// <c>matches</c> within 2%, <c>differs</c> within a factor of two,
/// <c>misread</c> beyond it - a price no counter would ask is the reading's
/// fault before it is the market's - or <c>unchecked</c> with nothing to set it beside.
/// </param>
public sealed record KioskPriceSeen(
    DateTimeOffset At,
    string Shot,
    string Commodity,
    bool Buying,
    double Price,
    double? Stock,
    string? Place,
    string? Terminal,
    double? Uex,
    DateTimeOffset? UexAt,
    string Verdict,
    IReadOnlyList<string>? UexCounters = null,
    bool AtThisCounter = false,
    int Frames = 1);

/// <summary>What <c>/api/kiosk-prices</c> says for one commodity.</summary>
/// <param name="Seen">Every price read for it, newest first.</param>
/// <param name="OlderReader">Kiosk readings kept by the reader before 1.2.22, left out; Read again brings them in.</param>
public sealed record KioskPriceReport(IReadOnlyList<KioskPriceSeen> Seen, int OlderReader);

/// <summary>
/// Prices the pilot saw at a kiosk - the only first-hand prices the app has -
/// each set beside what UEX lists for the same counter, with both dates.
/// </summary>
/// <remarks>
/// <para>
/// The check is the point, not a garnish. The first kiosk read on this install
/// (10 Sep 2026) came back with Methane at 3.312 aUEC and two ammunition
/// sizes at 384 and 641, and the reading looked fine: plausible integers. UEX,
/// which the reader never looks at, lists 3,312-class figures for the gases at
/// Pyro's counters and 7,384 and 7,641 for those two sizes - the thousands
/// separator had been read as a decimal point and two leading "7,"s lost. The
/// reader was fixed for what the frame shows; what it cannot see - a currency
/// sign read as a 4, a lost group with no separator left behind - is what this
/// catches. A figure more than a factor of two from UEX's for the same side at
/// the same counter is called a misread and not offered as a price.
/// </para>
/// <para>
/// The place is the log's, and the log's names do not always meet UEX's: this
/// install's kiosk stood at what the log calls Nyx Castra Jump Point Rest Stop
/// (RR_JP_NyxCastra), and no UEX counter is called anything like it. So a
/// place that matches no counter is set beside every counter trading the
/// commodity on the same side, and the ones quoting the same figure are named:
/// on that frame UEX (9 Nov 2026) had Scrap at 3,265 at three counters and
/// Methane at 3,312 at five, and the one counter on both lists is Stanton
/// Gateway (Nyx). Which counter it was is left to the pilot to read off that
/// list rather than asserted: the place to counter join is a name match.
/// </para>
/// <para>
/// Only prices per SCU are set beside UEX: UEX quotes per SCU, and a price per
/// UNITS is a different quantity (centi-SCU shipped wrong twice that way).
/// </para>
/// </remarks>
public static class KioskPrices
{
    /// <summary>Within this, the two agree.</summary>
    public const double Agrees = 0.02;

    /// <summary>Beyond this ratio either way, the reading is taken to be wrong rather than the market.</summary>
    public const double Implausible = 2.0;

    public static KioskPriceReport For(
        string commodity,
        IEnumerable<ScreenSighting> readings,
        Func<DateTimeOffset, string?> placeAt,
        Func<string, string?> terminalFor,
        Func<string, IReadOnlyList<UexMarketRow>> market)
    {
        var seen = new List<KioskPriceSeen>();
        var older = 0;

        foreach (var reading in readings)
        {
            if (reading.Kind != ScreenKind.Kiosk || reading.Kiosk is not { } kiosk) continue;
            if (kiosk.Reader < ScreenFrames.KioskReader)
            {
                if (kiosk.Rows.Any(r => Same(r.Commodity, commodity) || Same(r.Read, commodity))) older++;
                continue;
            }
            if (kiosk.Buying is not { } buying) continue;

            foreach (var row in kiosk.Rows)
            {
                if (!Same(row.Commodity, commodity) || row.Price is not { } price || row.PriceUnit != "SCU") continue;

                var place = placeAt(reading.ShotAt);
                var terminal = place is null ? null : terminalFor(place);
                var rows = market(row.Commodity!)
                    .Select(m => (Row: m, Price: (double)(buying ? m.Buy : m.Sell)))
                    .Where(m => m.Price > 0)
                    .ToList();

                var here = terminal is null ? default
                    : rows.FirstOrDefault(m => string.Equals(m.Row.Terminal, terminal, StringComparison.OrdinalIgnoreCase));

                double? uex;
                UexMarketRow? source;
                IReadOnlyList<string> counters;
                if (here.Row is not null)
                {
                    (uex, source, counters) = (here.Price, here.Row, [here.Row.Terminal]);
                }
                else if (rows.Count > 0)
                {
                    // The nearest figure anywhere, by ratio, and every counter quoting it.
                    var nearest = rows.MinBy(m => Math.Abs(Math.Log(price / m.Price)));
                    var same = rows.Where(m => Math.Abs(m.Price - nearest.Price) < 0.5).ToList();
                    (uex, source, counters) = (nearest.Price, same.MaxBy(m => m.Row.Seen).Row, [.. same.Select(m => m.Row.Terminal).Order()]);
                }
                else
                {
                    (uex, source, counters) = (null, null, []);
                }

                seen.Add(new KioskPriceSeen(
                    reading.ShotAt, reading.Shot, row.Commodity!, buying, price, row.Stock(),
                    place, terminal, uex,
                    source is { Seen: > 0 } ? DateTimeOffset.FromUnixTimeSeconds(source.Seen) : null,
                    Verdict(price, uex), counters, here.Row is not null));
            }
        }

        // The same price at the same place within half an hour is one sighting:
        // this install's three kiosk shots were taken five seconds apart, and a
        // live reading is renewed every few seconds while the kiosk stays up.
        var folded = new List<KioskPriceSeen>();
        foreach (var s in seen.OrderByDescending(s => s.At))
        {
            var last = folded.Count > 0 ? folded[^1] : null;
            if (last is not null && last.Place == s.Place && last.Buying == s.Buying
                && Math.Abs(last.Price - s.Price) < 0.5 && last.At - s.At <= TimeSpan.FromMinutes(30))
                folded[^1] = last with { Frames = last.Frames + 1 };
            else
                folded.Add(s);
        }

        return new KioskPriceReport(folded, older);
    }

    /// <summary>How a read price stands against UEX's.</summary>
    public static string Verdict(double price, double? uex)
    {
        if (uex is not { } other || other <= 0 || price <= 0) return "unchecked";
        var ratio = price / other;
        if (Math.Abs(ratio - 1) <= Agrees) return "matches";
        return ratio > Implausible || ratio < 1 / Implausible ? "misread" : "differs";
    }

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static double? Stock(this KioskRow row) => row.QuantityUnit == "SCU" ? row.Quantity : null;
}
