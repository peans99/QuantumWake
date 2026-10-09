namespace Quantumwake.WebTests;

/// <summary>The small overlay readout must always return to a known live view.</summary>
public class OverlayGlanceTests
{
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
}
