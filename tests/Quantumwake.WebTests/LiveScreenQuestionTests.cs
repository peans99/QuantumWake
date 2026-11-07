namespace Quantumwake.WebTests;

/// <summary>
/// Reading the screen live is asked at start, once, and is never on unless the
/// pilot said yes.
/// </summary>
public class LiveScreenQuestionTests
{
    private const string Unasked = """
        {"available":true,"unavailable":null,"asked":false,"enabled":false,"answeredAt":null,"ask":true,
         "status":{"state":"off","why":null,"lastFrameAt":null,"lastKind":null,"lastSummary":null,"frames":0,"kept":0,"lastReadMs":0}}
        """;

    private static string Answered(bool enabled) =>
        Unasked.Replace("\"asked\":false,\"enabled\":false", $"\"asked\":true,\"enabled\":{(enabled ? "true" : "false")}")
            .Replace("\"ask\":true", "\"ask\":false");

    private static Page Asked()
    {
        var page = new Page();
        page.Serve("/api/live", Unasked);
        page.Do("await maybeAskLive();");
        return page;
    }

    [Fact]
    public void An_unanswered_install_is_asked()
    {
        Assert.False(Asked().Truth("__dom.node('#live-ask').hidden"));
    }

    /// <summary>"Not now" is an answer: posted as off, so it is not asked again next start.</summary>
    [Fact]
    public void Not_now_is_kept_as_an_answer_of_off_and_closes_the_question()
    {
        var page = Asked();
        page.Serve("/api/live", Answered(false));
        page.Do("await __dom.node('#live-ask-no').onclick();");

        Assert.Contains("\"enabled\":false", page.BodyOf("/api/live"));
        Assert.True(page.Truth("__dom.node('#live-ask').hidden"));
    }

    [Fact]
    public void Turning_it_on_posts_a_yes()
    {
        var page = Asked();
        page.Serve("/api/live", Answered(true));
        page.Do("await __dom.node('#live-ask-yes').onclick();");

        Assert.Contains("\"enabled\":true", page.BodyOf("/api/live"));
        Assert.True(page.Truth("__dom.node('#live-ask').hidden"));
        Assert.True(page.Truth("__dom.node('#live-enabled').checked"));
    }

    [Fact]
    public void An_answered_install_is_not_asked_again()
    {
        var page = new Page();
        page.Serve("/api/live", Answered(false));
        page.Do("await maybeAskLive();");

        Assert.True(page.Truth("__dom.node('#live-ask').hidden"));
    }

    /// <summary>
    /// The question waits for first flight: the wizard says it showed, and the
    /// dashboard asks nothing over it.
    /// </summary>
    [Fact]
    public void Setup_says_when_it_showed_so_the_question_waits_for_it()
    {
        var page = new Page();
        page.Serve("/api/setup", """{"done":false}""");
        page.Serve("/api/overlay", """{"available":false,"visible":false}""");
        page.Serve("/api/uex/feeds", "[]");
        page.Serve("/api/wipe", """{"at":"2026-05-15T00:00:00+00:00","patch":"Alpha 4.8"}""");
        page.Do("globalThis.__shown = await maybeShowSetup();");
        Assert.True(page.Truth("__shown === true"));

        var done = new Page();
        done.Serve("/api/setup", """{"done":true}""");
        done.Do("globalThis.__shown = await maybeShowSetup();");
        Assert.True(done.Truth("__shown === false"));
    }

    /// <summary>The server on its own, or on Linux, has no switch to show.</summary>
    [Fact]
    public void The_switch_is_hidden_where_the_screen_cannot_be_read()
    {
        var page = new Page();
        page.Serve("/api/live", Unasked.Replace("\"available\":true", "\"available\":false").Replace("\"ask\":true", "\"ask\":false"));
        page.Do("await loadLiveScreen();");

        Assert.True(page.Truth("__dom.node('#live-block').hidden"));
    }

    [Fact]
    public void The_switch_shows_what_reading_is_waiting_for()
    {
        var page = new Page();
        page.Serve("/api/live", Answered(true).Replace("\"state\":\"off\",\"why\":null", "\"state\":\"waiting\",\"why\":\"Star Citizen is not the window in front\""));
        page.Do("await loadLiveScreen();");

        Assert.False(page.Truth("__dom.node('#live-block').hidden"));
        Assert.True(page.Truth("__dom.node('#live-enabled').checked"));
        Assert.Contains("Star Citizen is not the window in front", page.NodeText("#live-status"));
    }
}
