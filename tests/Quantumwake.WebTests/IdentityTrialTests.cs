namespace Quantumwake.WebTests;

/// <summary>
/// The 0.20.25 identity trial on Sessions, Fleet and Market: the wake under
/// each header, drawn from the pilot's own sessions, and the source colours
/// on Market's columns.
/// </summary>
public class IdentityTrialTests
{
    private static string Today(int daysAgo, int hour = 12) =>
        DateTime.Now.Date.AddDays(-daysAgo).AddHours(hour).ToString("yyyy-MM-ddTHH:mm:ss");

    private static Page WithSessions(string sessions)
    {
        var page = new Page();
        page.Do("for (const v of ['sessions', 'fleet', 'market']) { const bar = document.createElement('div'); bar.className = 'section-bar'; __dom.node('#view-' + v).append(bar); }");
        page.Do($"allSessions = {sessions}; drawWakes();");
        return page;
    }

    [Fact]
    public void Each_trial_page_header_carries_a_wake_from_the_logs()
    {
        var page = WithSessions($$"""
            [{"id":"a","startedAt":"{{Today(0)}}","inGame":3600},
             {"id":"b","startedAt":"{{Today(5)}}","inGame":1800}]
            """);

        foreach (var view in new[] { "sessions", "fleet", "market" })
        {
            var bar = $"__dom.node('#view-{view}').querySelector('.section-bar')";
            Assert.Equal(1, page.Count($"{bar}.byClass('wake').length"));
            Assert.Contains("2 sessions", page.Text($"{bar}.title"));
        }
    }

    /// <summary>The mark at today's end is filled only if you have flown today.</summary>
    [Fact]
    public void The_mark_at_today_is_filled_only_after_a_session_today()
    {
        var flown = WithSessions($$"""[{"id":"a","startedAt":"{{Today(0)}}","inGame":600}]""");
        var rested = WithSessions($$"""[{"id":"a","startedAt":"{{Today(3)}}","inGame":600}]""");

        const string Mark = "__dom.node('#view-sessions').querySelector('.section-bar').byClass('wake-mark')[0].className";
        Assert.Contains("today", flown.Text(Mark));
        Assert.DoesNotContain("today", rested.Text(Mark));
    }

    /// <summary>
    /// No session in the window draws no wake, rather than a flat line that
    /// would read as sixty days of nothing being logged.
    /// </summary>
    [Fact]
    public void No_recent_session_draws_no_wake()
    {
        var page = WithSessions($$"""[{"id":"a","startedAt":"{{Today(200)}}","inGame":600}]""");

        Assert.Equal(0, page.Count("__dom.node('#view-sessions').querySelector('.section-bar').byClass('wake').length"));
    }
}
