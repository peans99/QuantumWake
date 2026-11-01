namespace Quantumwake.WebTests;

/// <summary>
/// The Now page with the game closed. Game.log keeps the last session until
/// the next launch, and before 0.20.31 the page showed it as live: the
/// session clock counted up from its start for as long as the app ran.
/// </summary>
public class NowClosedTests
{
    private const string LastSession =
        "sessionStarted:'2026-10-29T19:00:00Z', lastSeen:'2026-10-29T21:15:00Z', "
        + "location:'Seraphim Station', ship:'Drake Cutter'";

    private static Page Now(string extra)
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[], {extra} }});");
        return page;
    }

    [Fact]
    public void A_closed_game_says_so_and_when_the_session_ended()
    {
        var page = Now($"gameRunning:false, {LastSession}");

        Assert.False(page.Truth("__dom.node('#now-closed').hidden"));
        Assert.Contains("Your last session ended", page.NodeText("#now-closed-title"));

        var detail = page.NodeText("#now-closed-detail");
        Assert.Contains("2h 15m long", detail);
        Assert.Contains("Seraphim Station", detail);
        Assert.Contains("Drake Cutter", detail);
    }

    /// <summary>The clock stops at the log's last line instead of counting on from the start.</summary>
    [Fact]
    public void The_session_clock_stops_where_the_log_ends()
    {
        var page = Now($"gameRunning:false, {LastSession}");

        Assert.Equal("02:15:00", page.NodeText("#now-clock"));
        Assert.Equal("Last session", page.NodeText("#now-session-label"));
        Assert.Equal("Where you left off", page.NodeText("#now-location-label"));
        Assert.Equal("Feed from the last session", page.NodeText("#now-feed-label"));
    }

    /// <summary>A log that ended mid-jump is not a pilot in quantum now.</summary>
    [Fact]
    public void A_jump_the_log_ended_in_is_not_shown_as_under_way()
    {
        var page = Now($"gameRunning:false, travelling:true, travellingTo:'Hurston', {LastSession}");

        Assert.True(page.Truth("__dom.node('#now-travel').hidden"));
    }

    [Fact]
    public void A_running_game_keeps_the_live_page()
    {
        var page = Now($"gameRunning:true, {LastSession}");

        Assert.True(page.Truth("__dom.node('#now-closed').hidden"));
        Assert.Equal("Session", page.NodeText("#now-session-label"));
        Assert.Equal("Location", page.NodeText("#now-location-label"));
    }

    /// <summary>
    /// An older server, or one with no Game.log, sends no flag. Saying nothing
    /// is not saying the game is closed.
    /// </summary>
    [Fact]
    public void No_flag_is_not_closed()
    {
        var page = Now(LastSession);

        Assert.True(page.Truth("__dom.node('#now-closed').hidden"));
    }
}
