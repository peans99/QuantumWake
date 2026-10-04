using Quantumwake.Core.Events;
using Quantumwake.Core.Logging;
using Quantumwake.Core.Parsing;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Refinery orders: the terminal's screenshots joined to the game's word that
/// each one finished.
/// </summary>
/// <remarks>
/// The times are the real ones, in UTC: the quote at 03:10:04, the running
/// order at 03:10:19 with 6 m 26 s left, and the log's completion at 03:16:54
/// - nine seconds after the countdown, which is what the mixed notification
/// mode's minute of grace is sized against.
/// </remarks>
public class RefineryOrdersTests
{
    private static readonly DateTimeOffset Quoted = new(2026, 10, 4, 3, 10, 4, TimeSpan.Zero);
    private static readonly DateTimeOffset Running = new(2026, 10, 4, 3, 10, 19, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed = new(2026, 10, 4, 3, 16, 54, TimeSpan.Zero);

    private const string Mic = "MIC-L5 Modern Icarus Station";

    private static ScreenSighting Sighting(string shot, DateTimeOffset at, RefineryReading reading, bool dismissed = false) =>
        new(shot, at, ScreenKind.Refinery, "", [], null, null, null, null, [], 0, Dismissed: dismissed, Refinery: reading);

    private static RefineryReading Quote(int seconds = 395, string station = Mic) =>
        new(station, "setup", "Pyrometric Chromalysis", "Pyrometric Chromalvsis", "HIGH YIELD // LOW COST // SLOWEST",
            682, 182,
            [
                new("SILICON (RAW)", "Raw Silicon", 310, 383, null),
                new("SILICON (RAW)", "Raw Silicon", 510, 142, 64),
                new("ASLARITE (RAW)", "Aslarite (Raw)", 575, 6, 2),
                new("AGRICIUM (ORE)", "Agricium (Ore)", 588, 33, null),
            ],
            121.00m, seconds, 5435, "Drake Golem", 682, 151);

    private static RefineryReading Processing(int seconds = 386) =>
        new(Mic, "processing", null, null, null, null, null,
            [
                new("SILICON", "Silicon", null, null, 64, 63, 1),
                new("ASLARITE", "Aslarite", 575, null, 2, 3),
                new("AGRICIUM", "Agricium", 588, null, 15, 15),
            ],
            null, seconds, 5435, null, null, null);

    [Fact]
    public void The_quote_and_the_running_screen_are_one_order_and_the_log_closes_it()
    {
        var picture = RefineryOrders.Build(
            [Sighting("quote.jpg", Quoted, Quote()), Sighting("running.jpg", Running, Processing())],
            [new RefineryCompletion(Completed, Mic)]);

        var order = Assert.Single(picture.Orders);
        Assert.Equal("running", order.Basis);
        Assert.Equal(Quoted, order.SeenAt);
        Assert.Equal("running.jpg", order.Shot);

        // 03:10:19 + 6 m 26 s.
        Assert.Equal(new DateTimeOffset(2026, 10, 4, 3, 16, 45, TimeSpan.Zero), order.DueAt);
        Assert.Equal("Pyrometric Chromalysis", order.Method);
        Assert.Equal(121.00m, order.Cost);
        Assert.Equal(182, order.InCscu);

        // The quote read 66 cSCU back; the running screen read all three lots, 81.
        Assert.Equal(81, order.OutCscu);
        Assert.Equal(Completed, order.CompletedAt);
        Assert.Empty(picture.Unmatched);
    }

    /// <summary>A quote alone is an order only if the pilot confirmed it, and says so.</summary>
    [Fact]
    public void A_quote_alone_counts_from_the_shot_as_a_quote()
    {
        var order = Assert.Single(RefineryOrders.Build([Sighting("quote.jpg", Quoted, Quote())], []).Orders);

        Assert.Equal("quote", order.Basis);
        Assert.Equal(Quoted.AddSeconds(395), order.DueAt);
        Assert.Equal(66, order.OutCscu);
        Assert.Null(order.CompletedAt);
    }

    [Fact]
    public void A_completion_with_no_screenshot_is_kept_on_its_own()
    {
        var picture = RefineryOrders.Build(
            [Sighting("quote.jpg", Quoted, Quote(station: "ARC-L1 Wide Forest Station"))],
            [new RefineryCompletion(Completed, Mic)]);

        Assert.Null(Assert.Single(picture.Orders).CompletedAt);
        Assert.Equal(Mic, Assert.Single(picture.Unmatched).Station);
    }

    /// <summary>A completion cannot belong to an order first seen after it.</summary>
    [Fact]
    public void A_completion_before_the_order_was_seen_is_not_its()
    {
        var picture = RefineryOrders.Build(
            [Sighting("quote.jpg", Quoted, Quote())],
            [new RefineryCompletion(Quoted.AddMinutes(-30), Mic)]);

        Assert.Null(Assert.Single(picture.Orders).CompletedAt);
        Assert.Single(picture.Unmatched);
    }

    [Fact]
    public void A_dismissed_reading_makes_no_order()
    {
        Assert.Empty(RefineryOrders.Build([Sighting("quote.jpg", Quoted, Quote(), dismissed: true)], []).Orders);
    }

    /// <summary>The quote's lots with both figures read: the yields nobody publishes.</summary>
    [Fact]
    public void The_quote_measures_each_lot_it_read_both_ends_of()
    {
        var measured = RefineryOrders.Build(
            [Sighting("quote.jpg", Quoted, Quote()), Sighting("again.jpg", Quoted.AddSeconds(5), Quote())], []).Measured;

        // Two frames of one quote are one measurement each lot, not two.
        Assert.Equal(2, measured.Count);
        Assert.Contains(measured, y => y.Mineral == "Raw Silicon" && y.Quality == 510 && y.InCscu == 142 && y.OutCscu == 64);
        Assert.Contains(measured, y => y.Mineral == "Aslarite (Raw)" && y.InCscu == 6 && y.OutCscu == 2);
        Assert.All(measured, y => Assert.Equal("Pyrometric Chromalysis", y.Method));
    }

    [Fact]
    public void The_log_line_is_read_as_a_completion_and_reaches_the_timeline()
    {
        const string raw =
            "<2026-10-04T03:16:54.738Z> [Notice] <SHUDEvent_OnNotification> Added notification "
            + "\"A Refinery Work Order has been Completed at MIC-L5 Modern Icarus Station: \" [42] to queue. "
            + "New queue size: 1, MissionId: [00000000-0000-0000-0000-000000000000], ObjectiveId: [] "
            + "[Team_CoreGameplayFeatures][Missions][Comms]";

        Assert.True(LogEnvelope.TryParse(raw, out var line));
        var toast = Assert.IsType<NotificationEvent>(new LogEventParser().Parse(line));
        Assert.Equal(Mic, toast.RefineryStation);

        var builder = new SessionBuilder("Game.log");
        builder.Add(toast);

        // The game repeats a toast as it fades; one order is one completion.
        builder.Add(toast with { });
        var session = builder.Build();

        var done = Assert.Single(session.RefineryCompletions);
        Assert.Equal(Completed.AddMilliseconds(738), done.At);
        Assert.Equal(Mic, done.Station);
        Assert.Contains(session.Timeline, t => t.Kind == "refinery" && t.Detail == Mic);
    }

    [Theory]
    [InlineData("Received Blueprint: Refinery Work Order")]
    [InlineData("A Refinery Work Order has been Completed at")]
    [InlineData("Contract Complete: Refinery Work Order: ")]
    public void Other_toasts_are_not_completions(string text)
    {
        Assert.Null(new NotificationEvent(Completed, text, "1", null).RefineryStation);
    }
}
