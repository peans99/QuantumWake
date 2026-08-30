namespace Quantumwake.WebTests;

/// <summary>
/// The trading-rate card on the Now page.
/// </summary>
/// <remarks>
/// Commodity sales are the only income the logs carry, so this is a floor on
/// earnings rather than a measure of them. The card has to say so — a number
/// labelled "credits per hour" that silently omits every contract payout would
/// be worse than showing nothing.
/// </remarks>
public class EarningRateTests
{
    private const string Trading = """
        {"window":{"earned":4200000,"inGame":"12:30:00","perHour":336000,"days":30},
         "lifetime":{"earned":9000000,"inGame":"40:00:00","perHour":225000,"days":0},
         "goal":null,"hoursToGoal":null,"basis":"recent"}
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/earnings", body);
        page.Do("await loadEarnings();");
        return page;
    }

    [Fact]
    public void The_rate_is_shown_per_in_game_hour()
    {
        var page = Loaded(Trading);

        Assert.Contains("336,000 aUEC/h", page.NodeText("#now-earning-rate"));
        Assert.Contains("in game", page.NodeText("#now-earning-sub"));
    }

    /// <summary>
    /// Calling it a trading rate rather than an earning rate is the whole
    /// honesty of this card.
    /// </summary>
    [Fact]
    public void It_says_what_it_cannot_see()
    {
        var note = Loaded(Trading).NodeText("#now-earning-note");

        Assert.Contains("contracts", note);
        Assert.Contains("floor", note);
    }

    /// <summary>
    /// Nothing sold is not a rate of nought an hour. The card stays away.
    /// </summary>
    [Fact]
    public void A_player_who_has_never_traded_sees_no_card()
    {
        var page = Loaded("""
            {"window":{"earned":0,"inGame":"04:00:00","perHour":0,"days":30},
             "lifetime":{"earned":0,"inGame":"04:00:00","perHour":0,"days":0},
             "goal":null,"hoursToGoal":null,"basis":"lifetime"}
            """);

        Assert.True(page.Truth("__dom.node('#now-earning-card').hidden"));
    }

    /// <summary>
    /// The distance to a goal is hours of trading, never a date: the app has no
    /// idea how often somebody plays, and a date would be inventing that.
    /// </summary>
    [Fact]
    public void A_goal_is_measured_in_hours_of_trading()
    {
        var page = Loaded("""
            {"window":{"earned":4200000,"inGame":"12:30:00","perHour":336000,"days":30},
             "lifetime":{"earned":9000000,"inGame":"40:00:00","perHour":225000,"days":0},
             "goal":{"name":"Drake Corsair","target":3360000,"setAt":"2026-08-30T00:00:00Z"},
             "hoursToGoal":10,"basis":"recent"}
            """);

        Assert.Contains("Drake Corsair", page.NodeText("#now-goal-name"));
        Assert.Contains("10h of trading", page.NodeText("#now-goal-eta"));
        Assert.False(page.Truth("__dom.node('#now-goal').hidden"));
    }
}
