namespace Quantumwake.WebTests;

/// <summary>Log → Read from the game: the switch that keeps pictures of false mining bars (1.2.58).</summary>
public sealed class LiveStraysTests
{
    [Fact]
    public void The_switch_posts_on_its_own_and_shows_only_with_reading_on()
    {
        var page = new Page();
        page.Serve("/api/live/strays", """{"available":true,"enabled":true,"catchStrayBars":true,"strayPictures":0,"status":{"state":"reading"}}""");
        page.Do("""
            liveState = { available:true, enabled:true, catchStrayBars:false, strayPictures:0, status:{ state:'reading' } };
            renderLiveScreen();
            """);
        Assert.Equal(0, page.Count("__dom.node('#live-strays-row').hidden ? 1 : 0"));
        Assert.Equal(1, page.Count("__dom.node('#live-strays-kept').hidden ? 1 : 0"));

        page.Do("""
            const box = __dom.node('#live-strays');
            box.checked = true;
            await box.fire('change');
            """);
        Assert.Contains("POST /api/live/strays", page.Fetched());
        Assert.Contains("\"on\":true", page.BodyOf("/api/live/strays"));
        Assert.Contains("None kept yet", page.NodeText("#live-strays-kept"));

        page.Do("""
            liveState = { available:true, enabled:false, catchStrayBars:true, strayPictures:0, status:{ state:'off' } };
            renderLiveScreen();
            """);
        Assert.Equal(1, page.Count("__dom.node('#live-strays-row').hidden ? 1 : 0"));
    }

    /// <summary>Pictures already kept stay findable with reading off, and the cap is said.</summary>
    [Fact]
    public void Kept_pictures_are_counted_with_their_folder_even_with_reading_off()
    {
        var page = new Page();
        page.Do("""
            liveState = { available:true, enabled:false, catchStrayBars:false, strayPictures:20,
                          strayFolder:'C:\Users\p\AppData\Local\Quantumwake\live\stray-bars', status:{ state:'off' } };
            renderLiveScreen();
            """);

        Assert.Equal(0, page.Count("__dom.node('#live-strays-row').hidden ? 1 : 0"));
        var line = page.NodeText("#live-strays-kept");
        Assert.Contains("20 windows pictured so far", line);
        Assert.Contains("stray-bars", line);
        Assert.Contains("no more are taken", line);
    }
}
