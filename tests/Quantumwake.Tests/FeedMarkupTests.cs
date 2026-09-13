using Quantumwake.Core.State;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The game's emphasis markup does not reach a feed this app draws.
/// </summary>
/// <remarks>
/// Contract notifications arrive wrapped in the game's own tags. The dashboard
/// had been stripping them in the browser ever since they first appeared, and
/// the in-game HUD had not - so the one surface a pilot cannot look away from
/// was the one printing <c>&lt;EM4&gt;</c> at them. Stripped once where the
/// feed is served, so no third page has to remember.
/// </remarks>
public class FeedMarkupTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 13, 3, 0, 0, TimeSpan.Zero);

    private static TimelineEntry Entry(string text, string? detail = null) =>
        LiveSessionService.Plain(new TimelineEntry(At, "contract", text, detail));

    /// <summary>Exactly as it arrived on this install.</summary>
    [Fact]
    public void Emphasis_tags_are_taken_off_a_contract_notification()
    {
        var entry = Entry(
            "Contract accepted",
            "New Defense Contract: Gabriel Lassort Elimination <EM4>[100 Rep] [BP]*</EM4>");

        Assert.Equal(
            "New Defense Contract: Gabriel Lassort Elimination [100 Rep] [BP]*",
            entry.Detail);
    }

    [Fact]
    public void The_tags_come_off_the_sentence_as_well_as_the_detail()
    {
        Assert.Equal("Verified Bounty: Toby Mumford [150 Rep]",
            Entry("Verified Bounty: Toby Mumford <EM4>[150 Rep]</EM4>").Text);
    }

    /// <summary>
    /// Any of them, in any case: the game numbers these tags and there is no
    /// reason to believe 4 is the only one it uses.
    /// </summary>
    [Theory]
    [InlineData("a <EM>b</EM> c")]
    [InlineData("a <em1>b</em1> c")]
    [InlineData("a <EM12>b</EM12> c")]
    public void Every_numbered_variant_is_recognised(string text)
    {
        Assert.Equal("a b c", Entry(text).Text);
    }

    /// <summary>
    /// Taking a tag out from between two words would otherwise leave the gap
    /// that held it.
    /// </summary>
    [Fact]
    public void No_double_space_is_left_where_a_tag_was()
    {
        Assert.Equal("Bounty [150 Rep] done", Entry("Bounty <EM4>[150 Rep]</EM4> done").Text);
    }

    [Fact]
    public void Text_with_no_markup_is_returned_untouched()
    {
        var plain = new TimelineEntry(At, "bought", "Bought Balor HCH Helmet Black", "31,836 aUEC");

        Assert.Same(plain, LiveSessionService.Plain(plain));
    }
}
