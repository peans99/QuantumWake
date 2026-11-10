using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Kiosk prices beside UEX's (1.2.22), on this install's own kiosk as the
/// reader takes it and the figures UEX listed for Pyro's counters on
/// 9 Nov 2026.
/// </summary>
public class KioskPricesTests
{
    private static readonly DateTimeOffset Shot = new(2026, 9, 11, 0, 53, 54, TimeSpan.Zero);

    private static ScreenSighting Kiosk(KioskReading kiosk, string shot = "ScreenShot-2026-09-10_20-53-54-CF5.jpg") =>
        new(shot, Shot, ScreenKind.Kiosk, "a kiosk", [], null, null, null, null, [], 0, Kiosk: kiosk);

    private static readonly KioskReading Own = ScreenFrames.Read(
        ScreenKioskFixtures.OwnBuySide, [], [], ["Scrap", "Methane", "Ship Ammunition"]).Kiosk!;

    private static IReadOnlyList<UexMarketRow> Market(string commodity) => commodity switch
    {
        "Ship Ammunition - Size 2" => [new(436, "Checkmate", 7126, 0, 12000, 0, 1793698904)],
        "Ship Ammunition - Size 3" => [new(436, "Checkmate", 7384, 0, 12000, 0, 1793698904)],
        _ => [],
    };

    private static KioskPriceReport For(string commodity, params ScreenSighting[] readings) =>
        KioskPrices.For(commodity, readings, _ => "Checkmate Station", place => place.StartsWith("Checkmate") ? "Checkmate" : null, Market);

    [Fact]
    public void A_price_that_agrees_with_uex_at_the_same_counter_matches()
    {
        var seen = Assert.Single(For("Ship Ammunition - Size 2", Kiosk(Own)).Seen);

        Assert.True(seen.Buying);
        Assert.Equal(7126, seen.Price);
        Assert.Equal("Checkmate", seen.Terminal);
        Assert.Equal(7126, seen.Uex);
        Assert.Equal("matches", seen.Verdict);
        Assert.Equal(12000, seen.Stock);
    }

    [Fact]
    public void A_figure_that_lost_its_first_group_is_called_a_misread()
    {
        // 384 on the frame, 7,384 at the counter.
        Assert.Equal("misread", Assert.Single(For("Ship Ammunition - Size 3", Kiosk(Own)).Seen).Verdict);
    }

    [Fact]
    public void A_place_no_counter_is_named_for_is_set_beside_every_counter_and_the_matching_ones_named()
    {
        // The log called this install's kiosk Nyx Castra Jump Point Rest Stop;
        // UEX has no counter by any such name, and three quoting Scrap at 3,265.
        IReadOnlyList<UexMarketRow> scrap(string _) =>
        [
            new(1, "ARC-L4", 3265, 0, 900), new(2, "Stanton Gateway (Nyx)", 3265, 0, 1612, 0, 1794222977),
            new(3, "MIC-L5", 3265, 0, 500), new(4, "HUR-L5", 2749, 0, 700), new(5, "Checkmate", 0, 3900),
        ];
        var report = KioskPrices.For("Scrap", [Kiosk(Own)], _ => "Nyx Castra Jump Point Rest Stop", _ => null, scrap);

        // The frame's first look read ¤3,265 as 43,265 - the currency sign as
        // a 4, which nothing on the frame gives away - and the nearest figure
        // anywhere, thirteen times smaller, calls it what it is.
        var seen = Assert.Single(report.Seen);
        Assert.Equal(43265, seen.Price);
        Assert.Equal("misread", seen.Verdict);
        Assert.Equal(3265, seen.Uex);
        Assert.False(seen.AtThisCounter);
        Assert.Equal(["ARC-L4", "MIC-L5", "Stanton Gateway (Nyx)"], seen.UexCounters);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1794222977), seen.UexAt);
    }

    [Fact]
    public void Nothing_to_set_it_beside_is_unchecked_not_matched()
    {
        Assert.Equal("unchecked", Assert.Single(For("Methane", Kiosk(Own)).Seen).Verdict);
    }

    [Theory]
    [InlineData(100, 100, "matches")]
    [InlineData(101.5, 100, "matches")]
    [InlineData(130, 100, "differs")]
    [InlineData(60, 100, "differs")]
    [InlineData(250, 100, "misread")]
    [InlineData(3.312, 3312, "misread")]
    public void Verdicts(double price, double uex, string verdict) =>
        Assert.Equal(verdict, KioskPrices.Verdict(price, uex));

    [Fact]
    public void A_reading_from_the_earlier_reader_is_counted_and_left_out()
    {
        var old = Own with { Reader = 0, Buying = false };
        var report = For("Methane", Kiosk(old));

        Assert.Empty(report.Seen);
        Assert.Equal(1, report.OlderReader);
    }

    [Fact]
    public void A_price_per_unit_is_never_set_beside_a_price_per_scu()
    {
        var units = Own with { Rows = [new KioskRow("METHANE", "Methane", null, 10, "SCU", 3312, "UNITS")] };
        Assert.Empty(For("Methane", Kiosk(units)).Seen);
    }
}
