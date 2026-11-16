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

    /// <summary>Every choice is off unless ticked (1.2.47): saving with nothing ticked is a no.</summary>
    [Fact]
    public void Saving_with_nothing_ticked_posts_a_no()
    {
        var page = Asked();
        page.Serve("/api/live", Answered(false));
        page.Do("await __dom.node('#live-ask-yes').onclick();");

        Assert.Equal("""{"enabled":false,"targets":false}""", page.BodyOf("/api/live"));
        Assert.True(page.Truth("__dom.node('#live-ask').hidden"));
    }

    [Fact]
    public void Ticking_reading_posts_a_yes()
    {
        var page = Asked();
        page.Serve("/api/live", Answered(true));
        page.Do("""
            __dom.node('#live-ask-read').checked = true;
            __dom.node('#live-ask-read').onchange();
            await __dom.node('#live-ask-yes').onclick();
            """);

        Assert.Equal("""{"enabled":true,"targets":false}""", page.BodyOf("/api/live"));
        Assert.True(page.Truth("__dom.node('#live-ask').hidden"));
        Assert.True(page.Truth("__dom.node('#live-enabled').checked"));
    }

    /// <summary>Identifying targets is its own yes, and only under a yes to reading.</summary>
    [Fact]
    public void Targets_are_a_choice_of_their_own_that_needs_reading()
    {
        var page = Asked();
        Assert.True(page.Truth("__dom.node('#live-ask-targets').disabled"));

        page.Serve("/api/live", Answered(true));
        page.Do("""
            __dom.node('#live-ask-read').checked = true;
            __dom.node('#live-ask-read').onchange();
            __dom.node('#live-ask-targets').checked = true;
            await __dom.node('#live-ask-yes').onclick();
            """);

        Assert.Equal("""{"enabled":true,"targets":true}""", page.BodyOf("/api/live"));
    }

    /// <summary>
    /// A pilot who said yes to reading before targets were a question sees it
    /// ticked: the question asks about targets, it does not take reading back.
    /// </summary>
    [Fact]
    public void An_earlier_yes_to_reading_is_shown_ticked()
    {
        var page = new Page();
        page.Serve("/api/live", Unasked.Replace("\"asked\":false,\"enabled\":false", "\"asked\":true,\"enabled\":true"));
        page.Do("await maybeAskLive();");

        Assert.False(page.Truth("__dom.node('#live-ask').hidden"));
        Assert.True(page.Truth("__dom.node('#live-ask-read').checked"));
        Assert.False(page.Truth("__dom.node('#live-ask-targets').disabled"));
    }

    /// <summary>Listening is offered where the copy can listen and nobody has answered.</summary>
    [Fact]
    public void Listening_is_offered_and_its_answer_posted_on_its_own()
    {
        var page = new Page();
        page.Serve("/api/live", Answered(false));
        page.Serve("/api/voice", """{"available":true,"enabled":false,"ask":true}""");
        page.Do("await maybeAskLive();");

        Assert.False(page.Truth("__dom.node('#live-ask').hidden"));
        Assert.True(page.Truth("__dom.node('#live-ask-reading').hidden"));
        Assert.False(page.Truth("__dom.node('#live-ask-voice-row').hidden"));

        page.Do("""
            __dom.node('#live-ask-voice').checked = true;
            await __dom.node('#live-ask-yes').onclick();
            """);

        Assert.Equal("""{"enabled":true}""", page.BodyOf("/api/voice"));
        Assert.DoesNotContain("POST /api/live", page.Fetched());
    }

    [Fact]
    public void Leaving_them_all_off_answers_listening_no_as_well()
    {
        var page = Asked();
        page.Serve("/api/voice", """{"available":true,"enabled":false,"ask":true}""");
        page.Do("await maybeAskLive();");
        page.Serve("/api/live", Answered(false));

        page.Do("""
            __dom.node('#live-ask-voice').checked = true;
            await __dom.node('#live-ask-no').onclick();
            """);

        Assert.Equal("""{"enabled":false}""", page.BodyOf("/api/voice"));
        Assert.Contains("\"enabled\":false", page.BodyOf("/api/live"));
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
