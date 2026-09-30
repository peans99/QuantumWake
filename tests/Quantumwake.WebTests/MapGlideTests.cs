namespace Quantumwake.WebTests;

/// <summary>The star map's eased moves between views.</summary>
public class MapGlideTests
{
    [Fact]
    public void A_glide_started_inside_the_last_frame_of_another_keeps_its_endpoints()
    {
        // Found on a never-used browser profile: the map starts zoomed right
        // out, the first search crosses into the detailed zoom, and re-laying
        // the bodies inside the old glide's final frame starts a new glide.
        // The old frame then cleared the new glide's endpoints, and its first
        // frame threw on null. Frames are run by hand here; the harness's own
        // requestAnimationFrame never calls back.
        var page = new Page();
        page.Do("""
            __frames = [];
            requestAnimationFrame = (f) => { __frames.push(f); return __frames.length; };
            cancelAnimationFrame = () => {};
            performance = { now: () => 0 };
            view = { x: 0, y: 0, w: 100, h: 100 };
            __nested = false;
            applyView = () => {
              if (__nested) return;
              __nested = true;
              animateViewTo({ x: 5, y: 5, w: 50, h: 50 }, 100);
            };
            animateViewTo({ x: 10, y: 10, w: 10, h: 10 }, 1);
            __frames.shift()(1000);
            __frames.shift()(100);
            """);

        Assert.Equal(50, page.Count("view.w"));
        Assert.Equal(5, page.Count("view.x"));
    }
}
