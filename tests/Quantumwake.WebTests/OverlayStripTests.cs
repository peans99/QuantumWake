namespace Quantumwake.WebTests;

/// <summary>
/// The overlay's compact view is a strip of two or three lines (1.2.4): where
/// you are and in what, the next thing to do, and a third line only while
/// something needs the eyes. A line with nothing to say is left out, and the
/// page tells the shell its height so the window fits it.
/// </summary>
public class OverlayStripTests
{
    private static Page Live()
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        return page;
    }

    private const string Here = "{ connected:true, inGame:true, location:'Port Tressler', ship:'ANVL Hornet F7CM Mk2', confidence:'High', recentEvents:[] }";

    [Fact]
    public void Line_one_is_the_place_and_the_ship_and_nothing_else_is_said_twice()
    {
        var page = Live();
        Assert.Equal("Port Tressler", page.Text($"overlayStripLines({Here}, null, null, null, null).where"));
        Assert.Equal("ANVL Hornet F7CM Mk2", page.Text($"overlayStripLines({Here}, null, null, null, null).ship"));
        // "At location" would only repeat line one.
        Assert.Equal("", page.Text($"overlayStripLines({Here}, null, null, null, null).next"));
        Assert.True(page.Truth($"overlayStripLines({Here}, null, null, null, null).alert === null"));
    }

    [Fact]
    public void In_quantum_the_place_is_where_the_jump_goes()
    {
        var page = Live();
        Assert.Equal("In quantum → Hurston",
            page.Text("overlayStripLines({ inGame:true, travelling:true, travellingTo:'Hurston', ship:'Cutlass' }, null, null, null, null).where"));
        Assert.Equal("", page.Text("overlayStripLines({ inGame:true, travelling:true, travellingTo:'Hurston' }, null, null, null, null).next"));
    }

    [Fact]
    public void Line_two_is_the_next_stop_of_the_plan()
    {
        var page = Live();
        Assert.Equal("Next stop · Lorville",
            page.Text($"overlayStripLines({Here}, {{ stops:[{{ place:'Lorville', done:false }}] }}, null, null, null).next"));
    }

    [Fact]
    public void A_lock_outranks_a_fracture_an_event_and_cash()
    {
        var page = Live();
        page.Do("var stripLive = { enabled:true, status:{ combat:{ target:{ name:'Gladius', stance:'hostile', rangeKm:2.4 } }, fracture:{ active:true, state:'in', fill:.4 } } };");
        page.Do("var stripEvent = { text:'RSI Discovery · 38,048 pts' };");
        page.Do("var stripCash = { balance:1200000, movementsSince:0 };");

        Assert.Equal("hostile", page.Text($"overlayStripLines({Here}, null, stripLive, stripEvent, stripCash).alert.tone"));
        Assert.StartsWith("Gladius · Hostile", page.Text($"overlayStripLines({Here}, null, stripLive, stripEvent, stripCash).alert.text"));

        page.Do("stripLive.status.combat.target = null;");
        Assert.Equal("Fracture · In the window · 40%", page.Text($"overlayStripLines({Here}, null, stripLive, stripEvent, stripCash).alert.text"));

        page.Do("stripLive.status.fracture = null;");
        Assert.Equal("RSI Discovery · 38,048 pts", page.Text($"overlayStripLines({Here}, null, stripLive, stripEvent, stripCash).alert.text"));

        Assert.Equal("cash", page.Text($"overlayStripLines({Here}, null, stripLive, null, stripCash).alert.tone"));
        // Live reading off: no lock is believed, however stale the last one.
        Assert.Equal("cash", page.Text($"overlayStripLines({Here}, null, {{ enabled:false, status:{{ combat:{{ target:{{ name:'Gladius' }} }} }} }}, null, stripCash).alert.tone"));
    }

    [Fact]
    public void Empty_lines_are_hidden_and_the_shell_hears_the_height_in_the_strip_view()
    {
        var page = Live();
        page.Do("window.stripPosted = []; window.chrome = { webview: { postMessage: (m) => window.stripPosted.push(m) } };");
        page.Do("__dom.node('#overlay-strip').getBoundingClientRect = () => ({ height: 41.2 });");
        page.Do("document.body.classList.add('glance');");

        page.Do($"renderNow({Here});");

        Assert.Equal("Port Tressler", page.NodeText("#strip-where"));
        Assert.Equal(" · ANVL Hornet F7CM Mk2", page.NodeText("#strip-ship"));
        Assert.True(page.Truth("__dom.node('#strip-next-line').hidden"));
        Assert.True(page.Truth("__dom.node('#strip-alert-line').hidden"));
        Assert.True(page.Truth("window.stripPosted.some(m => m.kind === 'strip' && m.height === 42)"));
    }

    [Fact]
    public void Outside_the_strip_view_nothing_is_posted()
    {
        var page = Live();
        page.Do("window.stripPosted = []; window.chrome = { webview: { postMessage: (m) => window.stripPosted.push(m) } };");
        page.Do("__dom.node('#overlay-strip').getBoundingClientRect = () => ({ height: 41 });");

        page.Do($"renderNow({Here});");

        Assert.Equal(0, page.Count("window.stripPosted.length"));
    }
}
