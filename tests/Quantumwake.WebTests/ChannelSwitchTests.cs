namespace Quantumwake.WebTests;

/// <summary>
/// LIVE, PTU and the other channels as the page shows them: the top-bar
/// switcher, the Settings list, and the notice that the game is running on a
/// channel the app is not reading.
/// </summary>
public class ChannelSwitchTests
{
    private static string Channels(bool pinned = false, bool playing = true, bool ptu = true)
    {
        var now = DateTimeOffset.UtcNow;
        var ptuRow = ptu
            ? $$""",{"channel":"PTU","root":"D:\\SC\\PTU","current":false,"hasGameLog":true,"lastPlayed":"{{now.AddMinutes(-3):O}}","logs":4}"""
            : "";
        var playingRow = playing && ptu
            ? $$"""{"channel":"PTU","root":"D:\\SC\\PTU","lastPlayed":"{{now.AddMinutes(-3):O}}"}"""
            : "null";
        return $$"""
            {
              "current": "LIVE", "pinned": {{(pinned ? "true" : "false")}}, "canRestart": true,
              "channels": [
                {"channel":"LIVE","root":"D:\\SC\\LIVE","current":true,"hasGameLog":true,"lastPlayed":"{{now.AddHours(-20):O}}","logs":151}{{ptuRow}}
              ],
              "playing": {{playingRow}}
            }
            """;
    }

    private static Page Loaded(string channels)
    {
        var page = new Page();
        page.Serve("/api/install/channels", channels);
        page.Do("await loadChannels();");
        return page;
    }

    /// <summary>Settings lists every channel, says which is read, and offers the others.</summary>
    [Fact]
    public void Settings_lists_the_channels_with_a_switch_for_the_others()
    {
        var page = Loaded(Channels());

        var list = page.NodeText("#channel-list");
        Assert.Contains("LIVE", list);
        Assert.Contains("reading", list);
        Assert.Contains("151 logs", list);
        Assert.Contains("PTU", list);
        Assert.Contains("game running", list);
        Assert.Contains("Switch to PTU", list);
        Assert.DoesNotContain("Switch to LIVE", list);
        Assert.True(page.Truth("__dom.node('#install').classList.contains('install-switch')"));
    }

    /// <summary>
    /// The game started on PTU while the app reads LIVE is the moment nobody
    /// thinks to look, so the top of the page says it.
    /// </summary>
    [Fact]
    public void A_game_running_on_another_channel_is_said_at_the_top()
    {
        var page = Loaded(Channels());

        Assert.False(page.Truth("__dom.node('#channel-notice').hidden"));
        Assert.Contains("Star Citizen is running on PTU", page.NodeText("#channel-notice-title"));
        Assert.Contains("Quantum Wake is reading LIVE", page.NodeText("#channel-notice-detail"));
        Assert.Contains("Switch to PTU", page.NodeText("#channel-notice-actions"));
        Assert.True(page.Truth("__dom.node('#install').classList.contains('install-elsewhere')"));
    }

    /// <summary>Waved away, it stays away for that game session.</summary>
    [Fact]
    public void Not_now_keeps_the_notice_away()
    {
        var page = Loaded(Channels());

        page.Do("dismissChannel(channelState.playing); renderChannels();");

        Assert.True(page.Truth("__dom.node('#channel-notice').hidden"));
    }

    /// <summary>One channel on the machine: nothing to switch to, so nothing offered.</summary>
    [Fact]
    public void A_single_channel_offers_nothing()
    {
        var page = Loaded(Channels(playing: false, ptu: false));

        Assert.False(page.Truth("__dom.node('#install').classList.contains('install-switch')"));
        Assert.DoesNotContain("Switch to", page.NodeText("#channel-list"));
        Assert.True(page.Truth("__dom.node('#channel-notice').hidden"));
    }

    /// <summary>
    /// Started with --path, the app always comes back to that folder, so a
    /// switch would restart for nothing: the page says why instead.
    /// </summary>
    [Fact]
    public void A_folder_named_on_the_command_line_is_not_switched_away_from()
    {
        var page = Loaded(Channels(pinned: true));

        Assert.True(page.Truth("__dom.node('#channel-notice').hidden"));
        Assert.False(page.Truth("__dom.node('#channel-note').hidden"));
        Assert.Contains("--path", page.NodeText("#channel-note"));
    }

    /// <summary>A server that cannot restart itself saves the choice and says to restart.</summary>
    [Fact]
    public void Switching_sends_the_folder_and_says_what_happens_next()
    {
        var page = Loaded(Channels());
        page.Serve("/api/install/channel", """{"channel":"PTU","restarting":false}""");

        page.Do("""
            __said = '';
            await switchChannel({ channel: 'PTU', root: 'D:\\SC\\PTU' }, null, (t) => { __said = t; });
            """);

        Assert.Contains("PTU", page.BodyOf("/api/install/channel"));
        Assert.Equal("Saved. Restart Quantum Wake to read PTU.", page.Text("__said"));
    }
}
