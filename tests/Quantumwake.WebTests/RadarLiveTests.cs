using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// 1.1.15: Mining → Radar's live block - the place pings are read against and
/// the wanted rocks - and the sweep on the Fracture card and the MFD.
/// </summary>
public class RadarLiveTests
{
    private const string Model = """
        {"ready":true,"read":260,"unplaced":59,"rows":[
          {"signature":3000,"kind":"hand","name":"Hand-mining deposit","tier":null,"ores":["Aphorite","Hadanite"],"surface":true,"asteroid":false,"stones":[]},
          {"signature":3170,"kind":"ship","name":"Quantainium","tier":"Legendary","ores":["Quantainium"],"surface":true,"asteroid":true,"stones":[]},
          {"signature":4700,"kind":"asteroid","name":"C-type asteroid","tier":null,"ores":["Copper","Iron"],"surface":false,"asteroid":true,"stones":[]}
        ]}
        """;

    private static string Live(string pings, string fracture = "null") => $$"""
        {"available":true,"unavailable":null,"asked":true,"enabled":true,"answeredAt":null,"ask":false,
         "voice":true,"canSpeak":true,
         "status":{"state":"reading","frames":40,"kept":0,"lastReadMs":120,"fracture":{{fracture}}},
         "pings":{{pings}}}
        """;

    private const string Auto = """
        {"place":null,"here":"Daymar","wanted":["Quantainium"],"wantedOnly":false,
         "places":[{"name":"Daymar","system":"Stanton","asteroids":false},{"name":"Lagrange A","system":null,"asteroids":true}]}
        """;

    private static Page Loaded(string pings = Auto)
    {
        var page = new Page();
        page.Serve("/api/mining/signatures", Model);
        page.Serve("/api/live", Live(pings));
        page.Do("await loadRadar(); showMiningPane('radar'); await loadLiveScreen();");
        return page;
    }

    private static string Box(string name) =>
        $"__dom.node('#radar-table tbody').descendants().find(n => n.tagName === 'input' && n.dataset.wanted === '{name}')";

    [Fact]
    public void The_place_picker_offers_the_logs_place_every_rock_and_each_table()
    {
        var page = Loaded();
        Assert.False(page.Truth("__dom.node('#radar-live').hidden"));
        Assert.Equal("auto", page.Text("__dom.node('#radar-place').value"));
        Assert.Equal(4, page.Number("__dom.node('#radar-place').children.length"));
        Assert.Contains("Daymar", page.Text("__dom.node('#radar-place').children[0].textContent"));
        Assert.Contains("asteroid field", page.Text("__dom.node('#radar-place').children[3].textContent"));
        Assert.Contains("where the log puts you", page.NodeText("#radar-place-note"));
    }

    [Fact]
    public void Without_a_place_from_the_log_the_note_says_every_rock()
    {
        var page = Loaded(Auto.Replace("\"here\":\"Daymar\"", "\"here\":null"));
        Assert.Contains("read against every rock", page.NodeText("#radar-place-note"));
    }

    [Fact]
    public void Picking_a_place_posts_it()
    {
        var page = Loaded();
        page.Serve("/api/live/ping", Live(Auto.Replace("\"place\":null", "\"place\":\"Lagrange A\"")));
        page.Do("""
            const select = __dom.node('#radar-place');
            select.value = 'Lagrange A';
            select.fire('change');
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Contains("\"place\":\"Lagrange A\"", page.BodyOf("/api/live/ping"));
        Assert.Contains("as picked here", page.NodeText("#radar-place-note"));
    }

    [Fact]
    public void Each_rock_has_a_wanted_box_named_as_the_voice_says_it()
    {
        var page = Loaded();
        Assert.True(page.Truth($"{Box("Quantainium")}.checked"));
        Assert.False(page.Truth($"{Box("C-type asteroid")}.checked"));
        Assert.False(page.Truth($"{Box("Hand-mining deposit")}.disabled"));
    }

    [Fact]
    public void Ticking_a_rock_posts_the_whole_wanted_list()
    {
        var page = Loaded();
        page.Serve("/api/live/ping", Live(Auto.Replace("[\"Quantainium\"]", "[\"Quantainium\",\"C-type asteroid\"]")));
        page.Do($$"""
            const box = {{Box("C-type asteroid")}};
            box.checked = true;
            __dom.node('#radar-table').fire('change', { target: box });
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Contains("\"wanted\":[\"Quantainium\",\"C-type asteroid\"]", page.BodyOf("/api/live/ping"));
    }

    [Fact]
    public void Wanted_only_posts_on_its_own()
    {
        var page = Loaded();
        page.Serve("/api/live/ping", Live(Auto.Replace("\"wantedOnly\":false", "\"wantedOnly\":true")));
        page.Do("""
            const box = __dom.node('#radar-wanted-only');
            box.checked = true;
            box.fire('change');
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Equal("{\"wantedOnly\":true}", page.BodyOf("/api/live/ping"));
    }

    // ---- the sweep ----

    private static string Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        string Ping(double value, string matches, int ago, bool wanted = false) =>
            $$"""{"value":{{value}},"matches":{{matches}},"place":"Daymar","chosen":false,"wanted":{{(wanted ? "true" : "false")}},"at":"{{now.AddSeconds(-ago):o}}"}""";
        var newest = Ping(6340, """[{"count":2,"name":"Quantainium","tier":"Legendary","signature":3170,"wanted":true}]""", 5, true);
        var older = Ping(9000, """[{"count":3,"name":"Hand-mining deposit","tier":null,"signature":3000,"wanted":false}]""", 60);
        return $$"""{"active":false,"state":null,"fill":0,"belowBy":null,"cracked":0,"lastCrackAt":null,"callouts":[],"ping":{{newest}},"pingAt":"{{now.AddSeconds(-5):o}}","pings":[{{newest}},{{older}}]}""";
    }

    [Fact]
    public void The_fracture_card_lists_the_sweep_with_wanted_rocks_starred()
    {
        var page = new Page();
        page.Serve("/api/live", Live(Auto, Sweep()));
        page.Do("await loadLiveScreen();");

        Assert.False(page.Truth("__dom.node('#now-fracture-pings').hidden"));
        Assert.Equal(2, page.Number("__dom.node('#now-fracture-pings').children.length"));
        Assert.Contains("★ 2 × Quantainium", page.NodeText("#now-fracture"));
        Assert.True(page.Truth("__dom.node('#now-fracture-pings').children[0].classList.contains('fracture-ping-wanted')"));
        Assert.Contains("Read against the rocks at Daymar", page.NodeText("#now-fracture-detail"));
    }

    [Fact]
    public void The_charge_page_gives_the_place_and_the_earlier_pings()
    {
        var e = new Engine();
        e.Execute("var window = globalThis;");
        e.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        e.Execute("var live = " + Live(Auto, Sweep()) + ";");
        var json = e.Evaluate("JSON.stringify(QwMfd.rows(QwMfd.pageIds.indexOf('charge'),{},null,false,{extra:{live}}))").AsString();

        Assert.Contains("★ 2 × Quantainium", json);
        Assert.Contains("READ AGAINST", json);
        Assert.Contains("DAYMAR", json);
        Assert.Contains("EARLIER · 9,000", json);
    }
}
