using Quantumwake.Core.Events;
using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// A timeline sentence keeps the class it is about, so the feed can name it.
/// </summary>
/// <remarks>
/// The sentence is written while the log is parsed and nothing at that moment
/// can name an item: the catalogues that can are read out of the install by
/// the app, not by the parser. Keeping the class beside the sentence is what
/// lets the name be put in when the feed is served, and it is why
/// <c>PayloadVersion</c> moved - a session summarised before this has no class
/// to resolve, and nothing later would ever give it one.
/// </remarks>
public class TimelineSubjectTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 3, 1, 28, TimeSpan.Zero);

    private static SessionSummary Bought(string itemClass)
    {
        var builder = new SessionBuilder("test.log");

        builder.Add(new ShopRequestEvent(
            At, "SCShop_MiningStall", "shop-1", "kiosk-1", itemClass, 2700m, 1));

        builder.Add(new ShopFlowResponseEvent(
            At.AddSeconds(1), "SCShop_MiningStall", "kiosk-1", "Idle", "Success", "Buying"));

        return builder.Build();
    }

    [Fact]
    public void A_kiosk_purchase_carries_the_class_it_was_for()
    {
        var entry = Assert.Single(
            Bought("grin_multitool_01_tractorbeam").Timeline, t => t.Kind == "bought");

        Assert.Equal("grin_multitool_01_tractorbeam", entry.Subject);

        // The sentence still reads as it always did. Naming happens where the
        // catalogues are, and this half must keep working when they are absent.
        Assert.Equal("Bought grin_multitool_01_tractorbeam", entry.Text);
    }

    /// <summary>
    /// Everything else on the timeline is about a moment rather than a thing,
    /// and must not claim a subject it cannot resolve.
    /// </summary>
    [Fact]
    public void Entries_that_are_not_about_an_item_carry_no_subject()
    {
        var timeline = Bought("grin_multitool_01_tractorbeam").Timeline;

        Assert.All(
            timeline.Where(t => t.Kind != "bought" && t.Kind != "sold"),
            t => Assert.Null(t.Subject));
    }
}
