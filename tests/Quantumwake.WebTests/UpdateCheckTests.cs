namespace Quantumwake.WebTests;

/// <summary>
/// Asking whether this copy may look for a newer one, and what it does with
/// the answer. The promise being kept here is that nothing reaches the internet
/// until somebody says it may.
/// </summary>
public class UpdateCheckTests
{
    private static Page Started(string state, string? check = null)
    {
        var page = new Page();
        page.Serve("/api/updates", state);
        page.Serve("/api/updates/answer?automatic=true", "{}");
        page.Serve("/api/updates/answer?automatic=false", "{}");

        if (check is not null)
            page.Serve("/api/updates/check", check);

        page.Do("await checkForUpdate();");
        return page;
    }

    private const string Unasked = """{"asked":false,"automatic":false,"lastCheckedAt":null}""";
    private const string Automatic = """{"asked":true,"automatic":true,"lastCheckedAt":"2026-08-21T09:00:00Z"}""";
    private const string Declined = """{"asked":true,"automatic":false,"lastCheckedAt":null}""";

    private const string NewerOut = """
        {"newer":true,"current":"0.5.1","latest":"0.6.0",
         "url":"https://github.com/peans99/QuantumWake/releases/tag/v0.6.0",
         "notes":"Cargo map.","publishedAt":"2026-09-01T12:00:00Z"}
        """;

    private const string UpToDate = """
        {"newer":false,"current":"0.6.0","latest":"0.6.0","url":"x","notes":null,"publishedAt":null}
        """;

    private static bool Shown(Page page) => !page.Truth("__dom.node('#update').hidden");

    [Fact]
    public void The_first_start_asks_rather_than_checking()
    {
        var page = Started(Unasked);

        Assert.True(Shown(page));
        Assert.Contains("Look for a newer version", page.NodeText("#update-title"));
        Assert.DoesNotContain("POST /api/updates/check", page.Fetched());
    }

    [Fact]
    public void The_question_offers_every_start_once_or_never()
    {
        var page = Started(Unasked);

        var buttons = page.Text("__dom.node('#update-actions').children.map(b => b.textContent).join('|')");

        Assert.Equal("Yes, every start|Just this once|No thanks", buttons);
    }

    [Fact]
    public void Saying_yes_turns_it_on_and_looks_now()
    {
        var page = Started(Unasked, NewerOut);

        page.Do("__dom.node('#update-actions').children[0].fire('click');");

        Assert.Contains("POST /api/updates/answer?automatic=true", page.Fetched());
        Assert.Contains("POST /api/updates/check", page.Fetched());
    }

    [Fact]
    public void Just_this_once_looks_without_turning_it_on()
    {
        var page = Started(Unasked, NewerOut);

        page.Do("__dom.node('#update-actions').children[1].fire('click');");

        Assert.Contains("POST /api/updates/answer?automatic=false", page.Fetched());
        Assert.Contains("POST /api/updates/check", page.Fetched());
    }

    /// <summary>"No" is an answer. It must not reach the internet, then or later.</summary>
    [Fact]
    public void Saying_no_never_looks()
    {
        var page = Started(Unasked, NewerOut);

        page.Do("__dom.node('#update-actions').children[2].fire('click');");

        Assert.Contains("POST /api/updates/answer?automatic=false", page.Fetched());
        Assert.DoesNotContain("POST /api/updates/check", page.Fetched());
        Assert.True(page.Truth("__dom.node('#update').hidden"));
    }

    [Fact]
    public void A_refusal_is_not_asked_about_again()
    {
        var page = Started(Declined, NewerOut);

        Assert.False(Shown(page));
        Assert.DoesNotContain("POST /api/updates/check", page.Fetched());
    }

    [Fact]
    public void Agreeing_to_every_start_checks_without_asking()
    {
        var page = Started(Automatic, NewerOut);

        Assert.Contains("POST /api/updates/check", page.Fetched());
        Assert.True(Shown(page));
        Assert.Contains("0.6.0 is out", page.NodeText("#update-title"));
        Assert.Contains("You are running 0.5.1", page.NodeText("#update-detail"));
    }

    /// <summary>
    /// A startup check nobody asked a question of should say nothing when there
    /// is nothing to say.
    /// </summary>
    [Fact]
    public void Being_current_is_silent_at_startup()
    {
        var page = Started(Automatic, UpToDate);

        Assert.False(Shown(page));
    }

    /// <summary>A click is a question, and deserves an answer either way.</summary>
    [Fact]
    public void Checking_by_hand_says_so_even_when_current()
    {
        var page = Started(Declined, UpToDate);

        page.Do("__dom.node('#update-check').fire('click', { currentTarget: __dom.node('#update-check') });");

        Assert.Contains("up to date", page.NodeText("#update-status"));
    }

    [Fact]
    public void The_download_is_a_link_not_an_install()
    {
        var page = Started(Automatic, NewerOut);

        var buttons = page.Text("__dom.node('#update-actions').children.map(b => b.textContent).join('|')");

        Assert.Equal("Open the release page|Later", buttons);
    }

    [Fact]
    public void The_settings_toggle_records_the_choice()
    {
        var page = Started(Declined);

        page.Do("""
            __dom.node('#update-auto').checked = true;
            __dom.node('#update-auto').fire('change');
            """);

        Assert.Contains("POST /api/updates/answer?automatic=true", page.Fetched());
    }

    [Fact]
    public void A_server_that_cannot_answer_asks_nothing()
    {
        var page = new Page();
        page.Do("await checkForUpdate();");

        Assert.False(Shown(page));
    }
}
