using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The commodity kiosk: what a shop stocks, and at what price per what.
/// </summary>
/// <remarks>
/// The frame behind these is not from this install - see
/// <see cref="ScreenKioskFixtures"/> - so what is defended is the reading and
/// the refusals, not any number of this pilot's. The refusals are the point:
/// a price carries the unit it was printed in and is never converted, and the
/// kiosk's abbreviated balance never becomes a figure.
/// </remarks>
public class ScreenKioskTests
{
    private static readonly string[] Commodities =
        ["Hephaestanite", "Corundum", "Titanium", "Carbon", "Dymantium", "Aluminum", "Quartz"];

    private static readonly string[] Ships = ["MISC Hull A", "Drake Caterpillar", "Drake Corsair"];

    private static ScreenFrame Read(ScreenTextLine[] lines) =>
        ScreenFrames.Read(lines, [], Ships, "nekron", Commodities);

    private sealed class Beliefs : IScreenBeliefs
    {
        public (string Id, string Name, string? System)? WhereAt(DateTimeOffset at) => null;
        public (string Id, string Name, string? System)? PlaceNamed(string read) => null;
        public IReadOnlyList<string>? OpenContractsAt(DateTimeOffset at) => null;
        public decimal? LedgerRunningAt(DateTimeOffset at) => null;
        public IReadOnlyList<string> StockParts(string ship) => [];
        public IReadOnlyList<string> FlownShips() => [];
    }

    [Fact]
    public void A_kiosk_is_recognised_with_its_side_its_ship_and_its_hold()
    {
        var frame = Read(ScreenKioskFixtures.BuySide);

        Assert.Equal(ScreenKind.Kiosk, frame.Kind);
        var kiosk = frame.Kiosk!;

        Assert.True(kiosk.Buying);
        Assert.Equal(0, kiosk.CargoUsed);
        Assert.Equal(64, kiosk.CargoCapacity);

        // "usc HULL A" for the MISC Hull A: the name did not read, and is kept
        // as read rather than reached for.
        Assert.Null(kiosk.Ship);
        Assert.Contains("HULL A", kiosk.ShipRead);
    }

    [Fact]
    public void Every_anchored_row_gives_its_commodity_stock_and_state()
    {
        var kiosk = Read(ScreenKioskFixtures.BuySide).Kiosk!;

        Assert.Equal(3, kiosk.Rows.Count);
        Assert.Equal(["Hephaestanite", "Corundum", "Titanium"], kiosk.Rows.Select(r => r.Commodity));
        Assert.All(kiosk.Rows, r => Assert.Equal("SCU", r.QuantityUnit));
        Assert.Equal([5000, 6000, 6000], kiosk.Rows.Select(r => r.Quantity));
        Assert.All(kiosk.Rows, r => Assert.Contains("INVENTORY", r.State));
    }

    /// <summary>
    /// The whole reason this screen is worth reading, and the whole danger of
    /// it: two commodities on one screen priced in different quantities.
    /// </summary>
    [Fact]
    public void A_price_carries_the_unit_it_was_printed_in_and_nothing_converts_it()
    {
        var kiosk = Read(ScreenKioskFixtures.BuySide).Kiosk!;

        var hephaestanite = kiosk.Rows.Single(r => r.Commodity == "Hephaestanite");
        Assert.Equal("UNITS", hephaestanite.PriceUnit);

        // 2.19700002K, the game's own float noise, multiplied by its suffix
        // and not rounded away.
        Assert.Equal(2197, hephaestanite.Price!.Value, precision: 2);

        var corundum = kiosk.Rows.Single(r => r.Commodity == "Corundum");
        Assert.Equal("SCU", corundum.PriceUnit);
        Assert.Equal(296, corundum.Price);
    }

    [Fact]
    public void A_price_the_engine_did_not_return_is_absent_rather_than_borrowed_from_the_row_above()
    {
        var titanium = Read(ScreenKioskFixtures.BuySide).Kiosk!.Rows.Single(r => r.Commodity == "Titanium");

        Assert.Null(titanium.Price);
        Assert.Null(titanium.PriceUnit);
        Assert.Equal(6000, titanium.Quantity);
    }

    /// <summary>
    /// The kiosk rounds the balance and the mobiGlas bar does not. Taking a
    /// figure from the rounded one would give the wallet check a baseline
    /// wrong by whatever the abbreviation hid, and every later drift would
    /// inherit it.
    /// </summary>
    [Fact]
    public void The_kiosk_balance_is_kept_as_printed_and_never_becomes_a_number()
    {
        var kiosk = Read(ScreenKioskFixtures.BuySide).Kiosk!;

        Assert.Contains("AUEC", kiosk.BalanceRead);
        Assert.Null(kiosk.Balance);

        // Nor does it reach the wallet check by the other door.
        var frame = Read(ScreenKioskFixtures.BuySide);
        Assert.DoesNotContain(ScreenChecks.Check(frame, DateTimeOffset.UtcNow, new Beliefs(), null),
            c => c.Subject == "Wallet" && c.Verdict == "new");
    }

    [Fact]
    public void The_check_reports_a_kiosk_as_new_and_says_the_two_units_are_kept_apart()
    {
        var frame = Read(ScreenKioskFixtures.BuySide);

        var check = Assert.Single(ScreenChecks.Check(frame, DateTimeOffset.UtcNow, new Beliefs(), null),
            c => c.Subject == "Kiosk");

        Assert.Equal("new", check.Verdict);
        Assert.Contains("never carry a shop's price", check.Belief);
        Assert.Contains("never converted", check.Note);
        Assert.Contains("2,197 per UNITS", check.Claim);
    }

    /// <summary>
    /// The pilot's own panel prints a ship and a cargo grid on the very rows
    /// the shop prints its commodities. Reading across both named the cargo
    /// grid as a commodity, at a price.
    /// </summary>
    [Fact]
    public void Nothing_from_the_pilots_own_panel_is_read_as_a_commodity()
    {
        var kiosk = Read(ScreenKioskFixtures.BuySide).Kiosk!;

        Assert.DoesNotContain(kiosk.Rows, r => r.Read.Contains("CARGO", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(kiosk.Rows, r => r.Read.Contains("HULL", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_sell_side_kiosk_is_filed_as_one_and_claims_no_rows_it_cannot_read()
    {
        var frame = Read(ScreenKioskFixtures.SellSide);

        Assert.Equal(ScreenKind.Kiosk, frame.Kind);
        Assert.False(frame.Kiosk!.Buying);
        Assert.Empty(frame.Kiosk.Rows);

        // Filed and kept whole, which is what a reader gets written from.
        Assert.Contains("DYMANTIUM", frame.Lines);
        Assert.Equal(576, frame.Kiosk.CargoCapacity);
    }

    [Fact]
    public void A_screen_merely_saying_commodities_is_not_a_kiosk()
    {
        var frame = Read([new("COMMODITIES", 100, 40, 54), new("WELCOME TO PORT OLISAR", 100, 200, 30)]);

        Assert.NotEqual(ScreenKind.Kiosk, frame.Kind);
    }
}
