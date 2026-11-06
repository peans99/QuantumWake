using Jint;

namespace Quantumwake.WebTests;

/// <summary>The fracture HUD read live: the Now page's Fracture card and the MFD's Charge page.</summary>
public class FracturePagesTests
{
    private static string Live(string fracture) => $$$"""
        {"available":true,"unavailable":null,"asked":true,"enabled":true,"answeredAt":null,"ask":false,"voice":true,"canSpeak":true,
         "status":{"state":"reading","frames":40,"kept":0,"lastReadMs":120,"fracture":{{{fracture}}}}}
        """;

    private const string InWindow = """{"active":true,"state":"in","fill":0.57,"belowBy":null,"cracked":2,"lastCrackAt":"2026-08-31T02:10:00Z","callouts":[{"at":"2026-08-31T02:10:30Z","kind":"window","text":"In the window"}]}""";
    private const string Charging = """{"active":true,"state":"below","fill":0,"belowBy":2.5,"cracked":0,"lastCrackAt":null,"callouts":[]}""";
    private const string Idle = """{"active":false,"state":null,"fill":0,"belowBy":null,"cracked":3,"lastCrackAt":"2026-08-31T02:10:00Z","callouts":[]}""";
    private const string Never = """{"active":false,"state":null,"fill":0,"belowBy":null,"cracked":0,"lastCrackAt":null,"callouts":[]}""";

    private static Page Loaded(string fracture)
    {
        var page = new Page();
        page.Serve("/api/live", Live(fracture));
        page.Do("await loadLiveScreen();");
        return page;
    }

    [Fact]
    public void In_the_window_the_card_says_so_and_shows_the_crack()
    {
        var page = Loaded(InWindow);
        Assert.False(page.Truth("__dom.node('#now-fracture-card').hidden"));
        Assert.Equal("In the window", page.NodeText("#now-fracture"));
        Assert.True(page.Truth("__dom.node('#now-fracture-card').classList.contains('fracture-in')"));
        Assert.Equal("57%", page.Text("__dom.node('#now-fracture-fill').style.width"));
        Assert.Contains("2 cracked so far", page.NodeText("#now-fracture-detail"));
    }

    [Fact]
    public void Under_the_window_it_says_how_far()
    {
        Assert.Contains("2.5 bars under the window", Loaded(Charging).NodeText("#now-fracture"));
    }

    [Fact]
    public void Between_rocks_it_counts_the_cracks_and_before_any_it_stays_hidden()
    {
        Assert.Equal("3 rocks cracked", Loaded(Idle).NodeText("#now-fracture"));
        Assert.True(Loaded(Idle).Truth("__dom.node('#now-fracture-meter').hidden"));
        Assert.True(Loaded(Never).Truth("__dom.node('#now-fracture-card').hidden"));
    }

    private static string Pinged(string matches) =>
        "{\"active\":false,\"state\":null,\"fill\":0,\"belowBy\":null,\"cracked\":0,\"lastCrackAt\":null,\"callouts\":[],"
        + "\"ping\":{\"value\":10155,\"matches\":" + matches + "},\"pingAt\":\"" + DateTimeOffset.UtcNow.ToString("o") + "\"}";

    [Fact]
    public void A_recent_ping_leads_the_card_with_what_it_is()
    {
        var page = Loaded(Pinged("[{\"count\":3,\"name\":\"Riccite\",\"tier\":\"Epic\",\"signature\":3385}]"));
        Assert.False(page.Truth("__dom.node('#now-fracture-card').hidden"));
        Assert.Contains("10,155", page.NodeText("#now-fracture-label"));
        Assert.Equal("3 × Riccite (Epic)", page.NodeText("#now-fracture"));

        Assert.Equal("No whole number of one rock", Loaded(Pinged("[]")).NodeText("#now-fracture"));
    }

    [Fact]
    public void The_charge_page_shows_a_recent_ping_between_rocks()
    {
        var json = Mfd(Pinged("[{\"count\":3,\"name\":\"Riccite\",\"tier\":\"Epic\",\"signature\":3385}]"))
            .Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('charge'),{},null,false,{extra:{live}}))").AsString();
        Assert.Contains("PING · 10,155", json);
        Assert.Contains("3 × Riccite", json);
    }

    private static Engine Mfd(string fracture)
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        engine.Execute($"var live = {Live(fracture)};");
        return engine;
    }

    [Fact]
    public void The_charge_page_reads_the_window_row_by_row()
    {
        var json = Mfd(InWindow).Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('charge'),{},null,false,{extra:{live}}))").AsString();
        Assert.Contains("IN THE WINDOW", json);
        Assert.Contains("57%", json);
        Assert.Contains("\"CRACKED\",\"2\"", json);

        Assert.Contains("2.5 bars under the window",
            Mfd(Charging).Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('charge'),{},null,false,{extra:{live}}))").AsString());
        Assert.Contains("3 rocks cracked this session",
            Mfd(Idle).Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('charge'),{},null,false,{extra:{live}}))").AsString());
    }

    [Fact]
    public void Charge_sits_under_resources_beside_mining()
    {
        var e = Mfd(Never);
        Assert.Equal("home/resources/charge", e.Evaluate("QwMfd.trail('charge').join('/')").AsString());
        Assert.Equal(5, e.Evaluate("QwMfd.menuItems('resources').length").AsNumber());
    }
}
