namespace Quantumwake.WebTests;

/// <summary>
/// The shared flight line and feed arrival signal must follow live state
/// without treating the initial history snapshot as a new notification.
/// </summary>
public class FlightTelemetryTests
{
    private static Page Live()
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        return page;
    }

    [Fact]
    public void The_flight_line_summarises_the_current_fix_hull_and_link()
    {
        var page = Live();

        page.Do("renderNow({ connected:true, inGame:true, locationSystem:'Stanton', location:'Port Tressler', ship:'ANVL Hornet F7CM Mk2', confidence:'High', recentEvents:[] });");

        Assert.Equal("Stanton", page.NodeText("#telemetry-system"));
        Assert.Equal("Port Tressler", page.NodeText("#telemetry-location"));
        Assert.Equal("ANVL Hornet F7CM Mk2", page.NodeText("#telemetry-ship"));
        Assert.Equal("Live", page.NodeText("#telemetry-link"));
        Assert.Equal("live", page.Text("__dom.node('#flight-telemetry').dataset.link"));
    }

    [Fact]
    public void The_feed_only_marks_an_event_that_arrives_after_its_history()
    {
        var page = Live();
        const string history = "{at:'2026-10-03T23:37:58Z',kind:'location',text:'Port Tressler'}";
        const string arrival = "{at:'2026-10-03T23:39:33Z',kind:'disconnect',text:'Disconnected'}";

        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{history}] }});");
        Assert.Equal(0, page.Count("__dom.node('#now-feed').querySelectorAll('.fresh').length"));

        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{arrival},{history}] }});");
        Assert.Equal(1, page.Count("__dom.node('#now-feed').querySelectorAll('.fresh').length"));

        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{arrival},{history}] }});");
        Assert.Equal(0, page.Count("__dom.node('#now-feed').querySelectorAll('.fresh').length"));
    }
}
