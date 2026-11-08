namespace Quantumwake.WebTests;

/// <summary>The small overlay readout must always return to a known live view.</summary>
public class OverlayGlanceTests
{
    [Fact]
    public void Glance_reports_its_content_height_to_the_native_shell()
    {
        var page = new Page();
        page.Do("""
            document.body.classList.add('overlay', 'glance');
            __dom.node('main').getBoundingClientRect = () => ({ height: 183.5 });
            window.chrome = { webview: { postMessage: message => { globalThis.sizeMessage = message; } } };
            reportOverlaySize();
            """);

        Assert.Equal("overlay-content-size", page.Text("sizeMessage.type"));
        Assert.Equal("184", page.Text("String(sizeMessage.height)"));
    }

    [Theory]
    [InlineData("'overlay'")]
    [InlineData("'overlay', 'glance', 'expanded'")]
    [InlineData("'glance'")]
    public void Other_views_do_not_ask_to_resize_the_window(string classes)
    {
        var page = new Page();
        page.Do($$"""
            document.body.classList.add({{classes}});
            __dom.node('main').getBoundingClientRect = () => ({ height: 183.5 });
            globalThis.sentSize = false;
            window.chrome = { webview: { postMessage: () => { sentSize = true; } } };
            reportOverlaySize();
            """);

        Assert.False(page.Truth("sentSize"));
    }

    [Fact]
    public void A_browser_without_the_native_bridge_can_still_use_glance()
    {
        var page = new Page();
        page.Do("document.body.classList.add('overlay', 'glance'); reportOverlaySize();");
    }

    [Fact]
    public void Glance_view_reduces_the_overlay_to_current_status_and_can_be_left()
    {
        var page = new Page();

        // The test DOM supplies URL for download blobs, not navigation. The
        // shell only needs a mutable query object while it changes its own URL.
        page.Do("window.URL = function () { this.searchParams = { set() {}, delete() {} }; };"
            + "window.scShowView('map'); window.scOverlayGlance(true);");

        Assert.True(page.Truth("document.body.classList.contains('glance')"));
        Assert.True(page.Truth("__dom.node('#view-now').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#view-map').classList.contains('active')"));

        page.Do("window.scOverlayGlance(false);");

        Assert.False(page.Truth("document.body.classList.contains('glance')"));
    }

    private static Page Laid(string cards)
    {
        var page = new Page();
        page.Serve("/api/overlay/layout", $$$"""{"reloadToken":1,"current":{"tabs":["now"],"cards":{{{cards}}},"density":"normal"}}""");
        page.Do("await applyOverlayLayout();");
        return page;
    }

    /// <summary>
    /// The Current status card gathered the location, ship, session, handle
    /// and respawn cards, and every layout still names those. Matching on its
    /// own name, which no layout has, switched it off in every overlay - and
    /// left the glance view, which is that card alone, empty.
    /// </summary>
    [Fact]
    public void The_status_card_is_on_when_the_layout_asks_for_any_part_of_it()
    {
        var page = Laid("""["location","ship","feed"]""");

        Assert.False(page.Truth("__dom.node('#now-status-card').classList.contains('layout-off')"));
        Assert.False(page.Truth("__dom.node('#now-feed-card').classList.contains('layout-off')"));
        Assert.True(page.Truth("__dom.node('#now-job-card').classList.contains('layout-off')"));
    }

    [Fact]
    public void A_layout_with_none_of_its_parts_still_switches_the_status_card_off()
    {
        var page = Laid("""["feed","job"]""");

        Assert.True(page.Truth("__dom.node('#now-status-card').classList.contains('layout-off')"));
    }
}
