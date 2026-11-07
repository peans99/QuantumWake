namespace Quantumwake.WebTests;

/// <summary>
/// Settings → Callouts (1.1.14): every sound with its own switch and a button
/// to hear it, and the Log page's third group switch, Missions.
/// </summary>
public class CalloutSettingsTests
{
    private const string Live = """
        {"available":true,"unavailable":null,"asked":true,"enabled":true,"answeredAt":null,"ask":false,
         "voice":true,"canSpeak":true,"voiceCombat":true,"voiceMining":false,"voiceMissions":true,
         "status":{"state":"reading","frames":40,"kept":0,"lastReadMs":120},
         "callouts":[
           {"kind":"hostile","group":"combat","name":"Hostile","sample":"Hostile target identified: Vanduul Blade","on":true,"voice":"af_bella"},
           {"kind":"missile","group":"combat","name":"Missile inbound","sample":"Missile inbound","on":false,"voice":"af_bella"},
           {"kind":"contract-done","group":"missions","name":"Contract complete","sample":"Contract complete","on":true,"voice":"af_bella"},
           {"kind":"ping","group":"mining","name":"Radar ping","sample":"three Riccite","on":true,"voice":"windows"}]}
        """;

    private const string List = "__dom.node('#settings-callouts')";

    private static Page Loaded(string json)
    {
        var page = new Page();
        page.Serve("/api/live", json);
        page.Do("await loadLiveScreen();");
        return page;
    }

    private static string Box(string kind) =>
        $"{List}.descendants().find(n => n.tagName === 'input' && n.dataset.calloutKind === '{kind}')";

    [Fact]
    public void Every_sound_is_listed_under_its_group_with_its_own_switch()
    {
        var page = Loaded(Live);

        Assert.Equal(3, page.Number($"{List}.byClass('callout-group').length"));
        Assert.Equal(4, page.Number($"{List}.byClass('callout-row').length"));
        Assert.True(page.Truth($"{Box("hostile")}.checked"));
        Assert.False(page.Truth($"{Box("missile")}.checked"));
        Assert.Contains("Missile inbound", page.Text($"{List}.byClass('callout-row')[1].textContent"));
    }

    [Fact]
    public void A_group_switched_off_on_the_log_page_greys_its_sounds_here()
    {
        var page = Loaded(Live);

        Assert.True(page.Truth($"{Box("ping")}.disabled"));
        Assert.False(page.Truth($"{Box("hostile")}.disabled"));
        Assert.True(page.Truth($"{List}.byClass('callout-group')[2].classList.contains('callout-group-off')"));
    }

    [Fact]
    public void A_sound_the_recorded_voice_lacks_says_which_voice_will_say_it()
    {
        var page = Loaded(Live);
        Assert.Contains("Windows voice", page.Text($"{List}.byClass('callout-row')[3].textContent"));
        Assert.DoesNotContain("Windows voice", page.Text($"{List}.byClass('callout-row')[0].textContent"));
    }

    [Fact]
    public void Switching_one_sound_posts_that_kind_alone()
    {
        var page = Loaded(Live);
        page.Serve("/api/live/callout", Live.Replace("\"on\":false", "\"on\":true"));
        page.Do($$"""
            const box = {{Box("missile")}};
            box.checked = true;
            {{List}}.fire('change', { target: box });
            await new Promise(r => setTimeout(r, 0));
            """);

        var body = page.BodyOf("/api/live/callout");
        Assert.Contains("\"kind\":\"missile\"", body);
        Assert.Contains("\"on\":true", body);
    }

    [Fact]
    public void A_group_switch_here_posts_the_same_as_the_log_pages()
    {
        var page = Loaded(Live);
        page.Serve("/api/live/voice", Live);
        page.Do($$"""
            const box = {{List}}.descendants().find(n => n.tagName === 'input' && n.dataset.calloutGroup === 'missions');
            box.checked = false;
            {{List}}.fire('change', { target: box });
            await new Promise(r => setTimeout(r, 0));
            """);

        var body = page.BodyOf("/api/live/voice");
        Assert.Contains("\"enabled\":true", body);
        Assert.Contains("\"missions\":false", body);
    }

    [Fact]
    public void Hear_it_asks_for_that_sounds_sample()
    {
        var page = Loaded(Live);
        page.Serve("/api/live/voice/test?kind=missile", "{}");
        page.Do($$"""
            const button = {{List}}.descendants().find(n => n.dataset.calloutHear === 'missile');
            {{List}}.fire('click', { target: button });
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Contains("POST /api/live/voice/test?kind=missile", page.Fetched());
    }

    [Fact]
    public void The_line_above_says_why_nothing_is_heard()
    {
        Assert.Contains("Speak callouts is off", Loaded(Live.Replace("\"voice\":true", "\"voice\":false")).Text("__dom.node('#settings-callouts-state').textContent"));
        Assert.Contains("Reading the game is off", Loaded(Live.Replace("\"enabled\":true", "\"enabled\":false")).Text("__dom.node('#settings-callouts-state').textContent"));
        Assert.Contains("still listed", Loaded(Live).Text("__dom.node('#settings-callouts-state').textContent"));
    }

    [Fact]
    public void The_ships_sound_comes_first_with_no_group_switch()
    {
        var page = Loaded(Live.Replace("\"callouts\":[",
            "\"callouts\":[{\"kind\":\"online\",\"group\":\"ship\",\"name\":\"Ship HUD online\",\"sample\":\"Quantum Wake system online\",\"on\":true,\"voice\":\"af_bella\"},"));

        Assert.Contains("Quantum Wake system online", page.Text($"{List}.byClass('callout-group')[0].textContent"));
        Assert.Equal(0, page.Number($"{List}.byClass('callout-group')[0].descendants().filter(n => n.tagName === 'input' && n.dataset.calloutGroup).length"));
        Assert.True(page.Truth($"{Box("online")}.checked"));
        Assert.False(page.Truth($"{Box("online")}.disabled"));
    }

    [Fact]
    public void The_log_page_has_a_missions_switch()
    {
        var page = Loaded(Live.Replace("\"voiceMissions\":true", "\"voiceMissions\":false"));
        Assert.False(page.Truth("__dom.node('#live-voice-missions').checked"));

        page.Serve("/api/live/voice", Live);
        page.Do("""
            const box = __dom.node('#live-voice-missions');
            box.checked = true;
            box.fire('change');
            await new Promise(r => setTimeout(r, 0));
            """);
        Assert.Contains("\"missions\":true", page.BodyOf("/api/live/voice"));
    }
}
