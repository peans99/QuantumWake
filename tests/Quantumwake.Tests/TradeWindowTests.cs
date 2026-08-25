using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// "Trades from the last week" means the trades, not the sessions holding them.
/// </summary>
/// <remarks>
/// <see cref="LogLibrary.Trades(int)"/> windows on <c>StartedAt</c>, which is
/// right for a list of sessions and wrong for a list of trades: an evening that
/// began just outside the window and ran past midnight takes every trade made
/// inside it out of the answer. Export leans on this, and a sharing feature that
/// quietly drops a night's receipts is worse than one that refuses to run.
/// </remarks>
public class TradeWindowTests : IDisposable
{
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;

    public TradeWindowTests() => _library = new LogLibrary(_store);

    private void Save(string id, DateTimeOffset started, params DateTimeOffset[] tradedAt) =>
        _store.Save(
            new SessionSummary
            {
                Id = id,
                SourceFile = $"{id}.log",
                StartedAt = started,
                EndedAt = started.AddHours(8),
                Handle = "nekron",
                Trades = [.. tradedAt.Select(at =>
                    new CommodityTrade(at, "TDD Area 18", 288_000m, 96, true, "Cargo", $"guid-{id}"))],
            },
            $"fingerprint:{id}");

    [Fact]
    public void A_trade_inside_the_window_survives_a_session_that_started_outside_it()
    {
        var now = DateTimeOffset.UtcNow;

        // The marathon: began eight days ago, still trading six days ago.
        Save("marathon", now.AddDays(-8), now.AddDays(-6));

        // What the session-shaped window does, and why it cannot be used here.
        Assert.Empty(_library.Trades(7));

        var kept = Assert.Single(_library.TradesWithin(7));
        Assert.Equal("guid-marathon", kept.ResourceId);
    }

    [Fact]
    public void A_trade_outside_the_window_is_dropped_even_from_a_recent_session()
    {
        var now = DateTimeOffset.UtcNow;

        Save("old", now.AddDays(-9), now.AddDays(-9).AddHours(1));
        Save("recent", now.AddDays(-2), now.AddDays(-2).AddHours(1));

        var kept = Assert.Single(_library.TradesWithin(7));
        Assert.Equal("guid-recent", kept.ResourceId);
    }

    /// <summary>The widened fetch must not widen the answer.</summary>
    [Fact]
    public void The_two_day_overfetch_does_not_leak_into_the_result()
    {
        var now = DateTimeOffset.UtcNow;

        // Inside the fetch (7 + 2 days) but outside the window the caller asked for.
        Save("overfetched", now.AddDays(-8), now.AddDays(-8).AddMinutes(30));

        Assert.Empty(_library.TradesWithin(7));
    }

    [Fact]
    public void No_window_is_every_trade()
    {
        var now = DateTimeOffset.UtcNow;

        Save("ancient", now.AddDays(-400), now.AddDays(-400));
        Save("today", now.AddHours(-2), now.AddHours(-1));

        Assert.Equal(2, _library.TradesWithin(0).Count);
    }

    /// <summary>
    /// The name comes from a dataset that may be off; the id is what the game
    /// wrote. A reader with a different catalogue needs the second one.
    /// </summary>
    [Fact]
    public void A_trade_carries_the_id_the_log_gave_it_even_with_no_catalogue()
    {
        Save("one", DateTimeOffset.UtcNow.AddHours(-3), DateTimeOffset.UtcNow.AddHours(-2));

        var trade = Assert.Single(_library.TradesWithin(1));
        Assert.Equal("guid-one", trade.ResourceId);
        Assert.Null(trade.Commodity);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _store.Dispose();
    }
}
