using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// UEX serves history one counter at a time, so drawing a commodity's trend
/// means choosing which counters to ask about. That choice is the feature: a
/// sample taken from the wrong end describes a market nobody trades in.
/// </summary>
public class UexHistorySamplingTests
{
    private static UexMarketRow Sells(int id, decimal price, decimal demand) =>
        new(id, $"T{id}", Buy: 0, Sell: price, BuyScu: 0, SellScu: demand);

    private static UexMarketRow Stocks(int id, decimal price, decimal stock) =>
        new(id, $"T{id}", Buy: price, Sell: 0, BuyScu: stock, SellScu: 0);

    /// <summary>
    /// Both ends of the trade get sampled: where a hold can be emptied and
    /// where it can be filled. Taking only one leaves half the chart blank.
    /// </summary>
    [Fact]
    public void Samples_the_buying_and_the_selling_side()
    {
        var rows = new[]
        {
            Sells(1, 3800, 9000),
            Sells(2, 3700, 8000),
            Stocks(3, 1200, 7000),
            Stocks(4, 1100, 6000),
        };

        var sample = UexData.SampleTerminals(rows, perSide: 2);

        Assert.Equal(4, sample.Count);
        Assert.Equal([1, 2, 3, 4], sample.Select(r => r.TerminalId).Order());
    }

    /// <summary>
    /// Ranked by volume, not by price. The best price is often a counter that
    /// wants nine SCU, and a trend drawn from those is not the market.
    /// </summary>
    [Fact]
    public void Volume_decides_the_sample_not_price()
    {
        var rows = new[]
        {
            Sells(1, 9999, 12),      // Superb price, no room.
            Sells(2, 3800, 9000),    // Where a full hold actually goes.
            Sells(3, 3700, 8000),
        };

        var sample = UexData.SampleTerminals(rows, perSide: 2);

        Assert.Equal([2, 3], sample.Select(r => r.TerminalId).Order());
        Assert.DoesNotContain(sample, r => r.TerminalId == 1);
    }

    /// <summary>
    /// A counter that both stocks and buys leads both lists, and must not be
    /// fetched twice - the whole point of the sample is bounding the requests.
    /// </summary>
    [Fact]
    public void A_counter_on_both_sides_is_asked_about_once()
    {
        var both = new UexMarketRow(7, "TDD Area 18", Buy: 1200, Sell: 3800, BuyScu: 9000, SellScu: 9000);

        var sample = UexData.SampleTerminals([both, Sells(2, 3700, 10)], perSide: 4);

        Assert.Single(sample, r => r.TerminalId == 7);
    }

    /// <summary>
    /// The request count is the reason this method exists, so the cap holds per
    /// side however many counters trade the thing.
    /// </summary>
    [Fact]
    public void The_sample_stays_within_two_per_side()
    {
        var rows = Enumerable.Range(1, 30).Select(i => Sells(i, 3800, i * 100))
            .Concat(Enumerable.Range(100, 30).Select(i => Stocks(i, 1200, i)))
            .ToList();

        var sample = UexData.SampleTerminals(rows, perSide: 3);

        Assert.Equal(6, sample.Count);

        // The busiest, not the first seen.
        Assert.Contains(sample, r => r.TerminalId == 30);
        Assert.DoesNotContain(sample, r => r.TerminalId == 1);
    }

    /// <summary>
    /// A counter that neither buys nor sells the commodity carries no price to
    /// chart, and asking about it spends a request on nothing.
    /// </summary>
    [Fact]
    public void Counters_that_do_not_trade_it_are_not_asked_about()
    {
        var rows = new[]
        {
            new UexMarketRow(1, "T1", Buy: 0, Sell: 0, BuyScu: 0, SellScu: 0),
            Sells(2, 3800, 500),
        };

        var sample = UexData.SampleTerminals(rows, perSide: 4);

        Assert.Single(sample);
        Assert.Equal(2, sample[0].TerminalId);
    }

    [Fact]
    public void A_commodity_nobody_trades_samples_nothing()
    {
        Assert.Empty(UexData.SampleTerminals([], perSide: 4));
    }
}
