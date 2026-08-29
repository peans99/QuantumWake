using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Where a commodity changes hands, taken from UEX rather than the community
/// download.
/// </summary>
/// <remarks>
/// A counter that charges you has a buy price and sells to you; one that pays
/// you has a sell price and buys from you. The two are separate lists because a
/// terminal commonly does one and not the other, and merging them would put a
/// refinery on the list of places to go shopping.
/// </remarks>
public class UexTradeLocationsTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-trade-{Guid.NewGuid():N}");

    public UexTradeLocationsTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private UexData Seeded(string rows)
    {
        File.WriteAllText(Path.Combine(_directory, "prices.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "commodity-ids.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "terminals.json"), "[]");
        File.WriteAllText(Path.Combine(_directory, "matrix.json"), $$"""{"Agricium":[{{rows}}]}""");

        return new UexData(_directory);
    }

    [Fact]
    public void A_counter_that_charges_you_sells_and_one_that_pays_you_buys()
    {
        var uex = Seeded("""
            {"TerminalId":1,"Terminal":"Area18 TDD","Buy":2800,"Sell":0},
            {"TerminalId":2,"Terminal":"HDMS-Anderson","Buy":0,"Sell":2650}
            """);

        var (sells, buys) = uex.TradeLocations("Agricium");

        Assert.Equal(["Area18 TDD"], sells);
        Assert.Equal(["HDMS-Anderson"], buys);
    }

    /// <summary>
    /// UEX reports a terminal once per observation, so the same counter can
    /// appear more than once and would otherwise be counted twice.
    /// </summary>
    [Fact]
    public void The_same_counter_is_listed_once()
    {
        var uex = Seeded("""
            {"TerminalId":1,"Terminal":"Area18 TDD","Buy":2800,"Sell":2700},
            {"TerminalId":1,"Terminal":"area18 tdd","Buy":2810,"Sell":2710}
            """);

        var (sells, buys) = uex.TradeLocations("Agricium");

        Assert.Single(sells);
        Assert.Single(buys);
    }

    /// <summary>
    /// A commodity UEX has never priced returns nothing rather than throwing,
    /// because that is most of them on a fresh install.
    /// </summary>
    [Fact]
    public void An_unpriced_commodity_is_empty_rather_than_an_error()
    {
        var (sells, buys) = Seeded("").TradeLocations("Quantanium");

        Assert.Empty(sells);
        Assert.Empty(buys);
    }
}
