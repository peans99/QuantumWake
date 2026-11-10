namespace Quantumwake.WebTests;

/// <summary>The Log page's week of live frames, counted (1.2.24).</summary>
public class LiveFramesTests
{
    [Fact]
    public void Kinds_and_the_shapes_of_unplaced_frames_are_listed_in_plain_words()
    {
        var page = new Page();
        page.Serve("/api/live/frames", """
            {"from":"2026-11-06T02:00:00Z","to":"2026-11-10T03:00:00Z","frames":1000,
             "kinds":{"Unknown":600,"Target":300,"Contracts":100},
             "shapes":{"no-text":200,"chat":50}}
            """);
        page.Do("await renderLiveFrames();");

        var text = page.NodeText("#live-frames-body");
        Assert.Contains("1,000 frames read", text);
        Assert.Contains("600 · not placed (60%)", text);
        Assert.Contains("300 · target panel (30%)", text);
        Assert.Contains("Of the 250 not placed since shapes were noted:", text);
        Assert.Contains("200 · no text at all - a black or loading screen, or open space", text);
    }

    [Fact]
    public void Frames_from_before_shapes_were_noted_say_so()
    {
        var page = new Page();
        page.Serve("/api/live/frames", """{"from":"2026-11-06T02:00:00Z","to":"2026-11-09T03:00:00Z","frames":10,"kinds":{"Unknown":10},"shapes":{}}""");
        page.Do("await renderLiveFrames();");

        Assert.Contains("shapes are noted from 1.2.24 on", page.NodeText("#live-frames-body"));
    }

    [Fact]
    public void Read_again_reports_how_many_were_placed()
    {
        var page = new Page();
        page.Serve("/api/screen/readings/reread-unplaced", """{"read":42,"placed":4,"gone":1}""");
        page.Do("await __dom.node('#screen-reread-unplaced').fire('click');");

        Assert.Contains("POST /api/screen/readings/reread-unplaced", page.Fetched());
        Assert.Contains("42 read again · 4 placed this time · 1 no longer in the folder", page.NodeText("#screen-status"));
    }
}
