using System.Text.RegularExpressions;

namespace Quantumwake.WebTests;

/// <summary>
/// The identity, trialled on three pages in 0.20.25 and on every page since
/// 0.20.30: the wake under each page header, drawn from the pilot's own
/// sessions, and gold on the pages that are the pilot's own history.
/// </summary>
public class IdentityTests
{
    private static readonly string Markup =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

    private static string Today(int daysAgo, int hour = 12) =>
        DateTime.Now.Date.AddDays(-daysAgo).AddHours(hour).ToString("yyyy-MM-ddTHH:mm:ss");

    /// <summary>Views the stub knows as <c>.view</c>, each given a header bar.</summary>
    private static readonly string[] Headed = ["fleet", "jobs", "mining", "itinerary"];

    private static Page WithSessions(string sessions)
    {
        var page = new Page();
        page.Do($"for (const v of {System.Text.Json.JsonSerializer.Serialize(Headed)}) {{ const bar = document.createElement('div'); bar.className = 'section-bar'; __dom.node('#view-' + v).append(bar); }}");
        page.Do($"allSessions = {sessions}; drawWakes();");
        return page;
    }

    private static string Bar(string view) => $"__dom.node('#view-{view}').querySelector('.section-bar')";

    [Fact]
    public void Every_page_header_carries_a_wake_from_the_logs()
    {
        var page = WithSessions($$"""
            [{"id":"a","startedAt":"{{Today(0)}}","inGame":3600},
             {"id":"b","startedAt":"{{Today(5)}}","inGame":1800}]
            """);

        foreach (var view in Headed)
        {
            Assert.Equal(1, page.Count($"{Bar(view)}.byClass('wake').length"));
            Assert.Contains("2 sessions", page.Text($"{Bar(view)}.byClass('wake-mark')[0].title"));
        }
    }

    /// <summary>
    /// The words go on the mark, not the header: a header-wide hover text
    /// surfaced over every search box and dropdown in it.
    /// </summary>
    [Fact]
    public void The_header_itself_keeps_no_hover_text()
    {
        var page = WithSessions($$"""[{"id":"a","startedAt":"{{Today(1)}}","inGame":600}]""");

        Assert.Equal(string.Empty, page.Text($"{Bar("fleet")}.title"));
    }

    /// <summary>
    /// A bar further down a page is not its header, and gets no wake: Help
    /// and About open on something else entirely.
    /// </summary>
    [Fact]
    public void A_bar_nested_inside_a_page_gets_no_wake()
    {
        var page = new Page();
        page.Do("const inner = document.createElement('div'); const bar = document.createElement('div'); bar.className = 'section-bar'; inner.append(bar); __dom.node('#view-help').append(inner);");
        page.Do($$"""allSessions = [{"id":"a","startedAt":"{{Today(0)}}","inGame":600}]; drawWakes();""");

        Assert.Equal(0, page.Count("__dom.node('#view-help').byClass('wake').length"));
    }

    /// <summary>The mark at today's end is filled only if you have flown today.</summary>
    [Fact]
    public void The_mark_at_today_is_filled_only_after_a_session_today()
    {
        var flown = WithSessions($$"""[{"id":"a","startedAt":"{{Today(0)}}","inGame":600}]""");
        var rested = WithSessions($$"""[{"id":"a","startedAt":"{{Today(3)}}","inGame":600}]""");

        var mark = $"{Bar("jobs")}.byClass('wake-mark')[0].className";
        Assert.Contains("today", flown.Text(mark));
        Assert.DoesNotContain("today", rested.Text(mark));
    }

    /// <summary>
    /// No session in the window draws no wake, rather than a flat line that
    /// would read as sixty days of nothing being logged.
    /// </summary>
    [Fact]
    public void No_recent_session_draws_no_wake()
    {
        var page = WithSessions($$"""[{"id":"a","startedAt":"{{Today(200)}}","inGame":600}]""");

        Assert.Equal(0, page.Count($"{Bar("fleet")}.byClass('wake').length"));
    }

    /// <summary>
    /// Gold on the kicker means the page is your own history. Reference pages
    /// are the game's files or the community's, and saying they were yours
    /// would be the one thing the colour exists to prevent.
    /// </summary>
    [Fact]
    public void Gold_kickers_are_on_history_pages_and_not_on_reference()
    {
        var own = Regex.Matches(Markup, "<section id=\"view-(?<view>[a-z]+)\" class=\"view\" data-voice=\"own\">")
            .Select(m => m.Groups["view"].Value).ToHashSet();

        foreach (var view in new[] { "sessions", "logbook", "fleet", "ledger", "crew" })
            Assert.Contains(view, own);
        foreach (var view in new[] { "ships", "components", "parts", "market", "settings", "events", "jobs" })
            Assert.DoesNotContain(view, own);
    }

    /// <summary>The trial's opt-in class is gone, so nothing can be left half in it.</summary>
    [Fact]
    public void No_page_keeps_the_trial_class()
    {
        Assert.DoesNotContain("qw-identity", Markup);
    }
}
