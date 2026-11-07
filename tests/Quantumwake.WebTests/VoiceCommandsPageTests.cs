namespace Quantumwake.WebTests;

/// <summary>
/// Settings → Voice & callouts → Voice commands (1.2.0): the copilot's switch,
/// how it is woken, and each command with the key it would press.
/// </summary>
public class VoiceCommandsPageTests
{
    private const string Voice = """
        {"available":true,"recogniser":true,"canPress":true,"canSpeak":true,"trouble":null,"state":"asleep","learning":false,
         "enabled":true,"trigger":"phrase","button":null,"key":null,"keyShown":null,"buttonDevice":null,
         "buttonDeviceName":null,"buttonDevicePresent":false,"minConfidence":0.7,"replies":true,
         "wake":"quantum wake copilot on","sleep":"quantum wake copilot off",
         "commands":[
           {"id":"mining","phrase":"mining mode","also":[],"confirm":false,"on":true,"action":"Toggle Mining Operator Mode","key":"M","canPress":true,"why":null,"fromProfile":false},
           {"id":"atc","phrase":"request landing","also":["request departure"],"confirm":false,"on":true,"action":"Request Landing","key":"RCtrl + Numpad 5","canPress":true,"why":null,"fromProfile":true},
           {"id":"guns","phrase":"gun mode","also":["guns mode"],"confirm":false,"on":true,"action":"Toggle Guns Operator Mode","key":"","canPress":false,"why":"It has no keyboard key, and voice can only press keys. You have it on js2 button10.","fromProfile":false},
           {"id":"selfdestruct","phrase":"self destruct","also":[],"confirm":true,"on":false,"action":"Self Destruct","key":"Backspace","canPress":true,"why":null,"fromProfile":false}],
         "heard":[{"at":"2026-11-07T20:00:03Z","text":"mining mode","confidence":0.91,"outcome":"Pressed M.","command":"mining"}]}
        """;

    private const string Root = "__dom.node('#voice-commands')";

    private static Page Loaded(string json)
    {
        var page = new Page();
        page.Serve("/api/voice", json);
        page.Do("await loadVoice();");
        return page;
    }

    private static string Text(Page page) => page.Text($"{Root}.textContent");

    private static string Row(string id) =>
        $"{Root}.descendants().find(n => n.dataset && n.dataset.voiceRow === '{id}')";

    [Fact]
    public void Each_command_shows_the_key_it_presses_or_why_it_cannot()
    {
        var page = Loaded(Voice);

        Assert.Equal(4, page.Number($"{Root}.byClass('voice-command').length"));
        Assert.Contains("“mining mode”", page.Text($"{Row("mining")}.textContent"));
        Assert.Contains("M", page.Text($"{Row("mining")}.textContent"));
        // The pilot's own binding is told apart from the game's default.
        Assert.Contains("RCtrl + Numpad 5 (your binding)", page.Text($"{Row("atc")}.textContent"));
        Assert.Contains("“request departure”", page.Text($"{Row("atc")}.textContent"));
        // A command on a stick only says so, and is marked.
        Assert.Contains("js2 button10", page.Text($"{Row("guns")}.textContent"));
        Assert.True(page.Truth($"{Row("guns")}.classList.contains('voice-command-unbound')"));
        Assert.Contains("asks to confirm", page.Text($"{Row("selfdestruct")}.textContent"));
    }

    [Fact]
    public void The_state_line_and_what_was_heard_say_what_the_copilot_is_doing()
    {
        var page = Loaded(Voice);
        Assert.Contains("Asleep: say “Quantum Wake copilot on” to wake it.", Text(page));
        Assert.Contains("“mining mode”", page.Text($"{Root}.byClass('voice-heard')[0].textContent"));
        Assert.Contains("Pressed M.", page.Text($"{Root}.byClass('voice-heard')[0].textContent"));

        Assert.Contains("Awake", Text(Loaded(Voice.Replace("\"state\":\"asleep\"", "\"state\":\"awake\""))));
        Assert.Contains("Not listening: no microphone", Text(Loaded(Voice.Replace("\"trouble\":null", "\"trouble\":\"no microphone could be opened\""))));
    }

    [Fact]
    public void A_copy_that_cannot_listen_says_why_and_shows_no_switches()
    {
        var bare = Loaded(Voice.Replace("\"canPress\":true,\"canSpeak\"", "\"canPress\":false,\"canSpeak\""));
        Assert.Contains("Only the Windows app listens", Text(bare));
        Assert.True(bare.Truth($"{Root}.byClass('voice-commands-controls')[0].hidden"));

        var deaf = Loaded(Voice.Replace("\"recogniser\":true", "\"recogniser\":false"));
        Assert.Contains("no English speech recogniser", Text(deaf));
    }

    [Fact]
    public void Switching_a_command_off_posts_that_command_alone()
    {
        var page = Loaded(Voice);
        page.Do($$"""
            const box = {{Root}}.descendants().find(n => n.tagName === 'input' && n.dataset.voiceCommand === 'mining');
            box.checked = false;
            {{Root}}.fire('change', { target: box });
            await new Promise(r => setTimeout(r, 0));
            """);

        var body = page.BodyOf("/api/voice");
        Assert.Contains("\"command\":\"mining\"", body);
        Assert.Contains("\"on\":false", body);
    }

    [Fact]
    public void Push_to_talk_shows_its_button_and_can_be_learned_from_a_stick()
    {
        var phraseOnly = Loaded(Voice);
        Assert.DoesNotContain("Use a stick button", Text(phraseOnly));

        var page = Loaded(Voice.Replace("\"trigger\":\"phrase\"", "\"trigger\":\"button\"")
            .Replace("\"button\":null", "\"button\":7")
            .Replace("\"buttonDeviceName\":null,\"buttonDevicePresent\":false", "\"buttonDeviceName\":\"VKB Gladiator\",\"buttonDevicePresent\":true"));
        Assert.Contains("Push-to-talk: button 7 on VKB Gladiator.", Text(page));
        Assert.Contains("Ready: hold push-to-talk", Text(page));

        page.Serve("/api/voice/learn", Voice.Replace("\"learning\":false", "\"learning\":true").Replace("\"trigger\":\"phrase\"", "\"trigger\":\"button\""));
        page.Do($$"""
            const button = {{Root}}.descendants().find(n => n.dataset && n.dataset.voiceAct === 'learn');
            {{Root}}.fire('click', { target: button });
            await new Promise(r => setTimeout(r, 0));
            await new Promise(r => setTimeout(r, 0));
            """);
        Assert.Contains("POST /api/voice/learn", page.Fetched());
        Assert.Contains("Press the stick button now", Text(page));
    }

    [Fact]
    public void Hear_it_asks_for_that_commands_answer()
    {
        var page = Loaded(Voice);
        page.Serve("/api/voice/hear?id=selfdestruct", "{}");
        page.Do($$"""
            const button = {{Root}}.descendants().find(n => n.dataset && n.dataset.voiceHear === 'selfdestruct');
            {{Root}}.fire('click', { target: button });
            await new Promise(r => setTimeout(r, 0));
            """);
        Assert.Contains("POST /api/voice/hear?id=selfdestruct", page.Fetched());

        // A copy with no voice offers nothing to hear.
        var mute = Loaded(Voice.Replace("\"canSpeak\":true", "\"canSpeak\":false"));
        Assert.Equal(0, mute.Number($"{Root}.descendants().filter(n => n.dataset && n.dataset.voiceHear).length"));
    }

    [Fact]
    public void A_key_is_named_by_its_place_on_the_board_as_the_game_binds_it()
    {
        var page = new Page();
        Assert.Equal("q", page.Text("voiceGameKey('KeyQ')"));
        Assert.Equal("np_0", page.Text("voiceGameKey('Numpad0')"));
        Assert.Equal("ralt", page.Text("voiceGameKey('AltRight')"));
        Assert.Equal("f12", page.Text("voiceGameKey('F12')"));
        Assert.True(page.Truth("voiceGameKey('MediaPlayPause') === null"));
    }
}
