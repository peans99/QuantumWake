namespace Quantumwake.WebTests;

/// <summary>
/// The Garage after its review: a slow answer cannot land on a bench that
/// has moved on, a row is what is on screen, the search box keeps its focus,
/// an undone purchase leaves the Shopping page, a chosen destination is kept,
/// and a path never reaches the clipboard.
/// </summary>
public class GarageReviewTests
{
    private const string Garage = """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "mine":[{"class":"AEGS_Gladius","name":"Aegis Gladius","sorties":12,"lastFlown":"2026-09-13T00:00:00+00:00"}],
         "all":[{"class":"AEGS_Gladius","name":"Aegis Gladius","manufacturer":"Aegis Dynamics","role":"Light Fighter","size":2},
                {"class":"DRAK_Cutlass_Black","name":"Drake Cutlass Black","manufacturer":"Drake Interplanetary","role":"Medium Fighter","size":3}]}
        """;

    /// <summary>A Gladius with two cooler ports, both Bracers as it comes.</summary>
    private static string Ship(string cls, string name, string p1Class, string p1Name, bool p1Changed) => """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "ship":{"class":"@CLS@","name":"@NAME@","manufacturer":"Aegis Dynamics","role":"Light Fighter","career":"Combat",
                 "size":2,"crew":1,"hullMass":48552,"health":6110,"cargoScu":0,"quantumFuel":600,"hydrogenFuel":1600,
                 "flight":{"scm":226,"boost":520,"max":1193,"pitch":68,"yaw":52,"roll":200},"powerPools":{},"stockMass":55646},
         "sheet":{"class":"@CLS@","name":"@NAME@","mass":55646,
                  "shields":{"em":22078,"ir":8711,"emByGroup":{}},"quantum":{"em":37164,"ir":7263,"emByGroup":{}},
                  "power":{"available":16,"usedShields":14,"usedQuantum":12,"usedByGroup":{},"overShields":false,"overQuantum":false},
                  "cooling":{"generated":68,"usedShields":36.1,"usedQuantum":30.1,"loadShields":0.531,"loadQuantum":0.443},
                  "shield":{"hp":6336,"regen":1394,"generators":2,"poolLimit":2},
                  "weapons":{"fixedDps":1944.5,"fixedSustainedDps":1186.9,"fixedAlpha":119.4,"turretDps":0,"missileDamage":21000,"missiles":6,"guns":[]},
                  "quantumDrive":{"drive":"Beacon","speed":161410900,"spoolTime":4.4,"cooldown":11.9,"range":32223415682,"fuelPerGm":18.62},
                  "armorSignals":{"em":1.13,"ir":1.13,"crossSection":1},"crossSection":{"x":6923,"y":1731,"z":8654},"parts":[],"notes":[]},
         "ports":[{"portId":"p1","hardpoint":"hardpoint_cooler_01","group":"Cooler","minSize":1,"maxSize":1,"class":"@P1CLASS@","name":"@P1NAME@","stockClass":"COOL_AEGS_S01_Bracer_SCItem","changed":@P1CHANGED@,
                   "fitted":{"type":"Cooler","name":"@P1NAME@","size":1,"grade":2,"manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3}},
                  {"portId":"p2","hardpoint":"hardpoint_cooler_02","group":"Cooler","minSize":1,"maxSize":1,"class":"COOL_AEGS_S01_Bracer_SCItem","name":"Bracer","stockClass":"COOL_AEGS_S01_Bracer_SCItem","changed":false,
                   "fitted":{"type":"Cooler","name":"Bracer","size":1,"grade":2,"manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3}}]}
        """
        .Replace("@CLS@", cls).Replace("@NAME@", name).Replace("@P1CLASS@", p1Class).Replace("@P1NAME@", p1Name).Replace("@P1CHANGED@", p1Changed ? "true" : "false");

    private const string CoolerOptions = """
        {"port":{"portId":"p1","hardpoint":"hardpoint_cooler_01","kinds":["Cooler"],"minSize":1,"maxSize":1,"fitted":"COOL_AEGS_S01_Bracer_SCItem"},
         "pricesKnown":true,"marketKnown":false,
         "options":[
           {"part":{"class":"COOL_AEGS_S01_Bracer_SCItem","type":"Cooler","size":1,"grade":2,"name":"Bracer","manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3},"price":null,"listings":[],"shops":[]},
           {"part":{"class":"COOL_JUST_S01_Glacier_SCItem","type":"Cooler","size":1,"grade":1,"name":"Glacier","manufacturer":"Juggernaut","makerCode":"JUST","em":1490,"ir":7920,"coolantGen":38,"powerUseMax":3},"price":12000,"listings":[],"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","price":12000}]},
           {"part":{"class":"COOL_LPLT_S01_ColdSnap_SCItem","type":"Cooler","size":1,"grade":3,"name":"ColdSnap","manufacturer":"Lightning Power","makerCode":"LPLT","em":1300,"ir":6000,"coolantGen":30,"powerUseMax":3},"price":null,"listings":[],"shops":[]},
           {"part":{"class":"COOL_ACAS_S01_Endo_SCItem","type":"Cooler","size":1,"grade":4,"name":"Endo","manufacturer":"Ace Astrogation","makerCode":"ACAS","em":1200,"ir":5000,"coolantGen":18,"powerUseMax":2},"price":null,"listings":[],"shops":[]}]}
        """;

    private static Page Opened()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        page.Serve("/api/garage/AEGS_Gladius", Ship("AEGS_Gladius", "Aegis Gladius", "COOL_AEGS_S01_Bracer_SCItem", "Bracer", false));
        page.Serve("/api/garage/DRAK_Cutlass_Black", Ship("DRAK_Cutlass_Black", "Drake Cutlass Black", "COOL_AEGS_S01_Bracer_SCItem", "Bracer", false));
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Serve("/api/garage/builds?ship=DRAK_Cutlass_Black", "[]");
        page.Serve("/api/garage/AEGS_Gladius/market", """{"enabled":false,"fetchedAt":null,"total":0,"components":0,"listings":[]}""");
        page.Serve("/api/garage/DRAK_Cutlass_Black/market", """{"enabled":false,"fetchedAt":null,"total":0,"components":0,"listings":[]}""");
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions);
        page.Do("await loadGarage();");
        return page;
    }

    /// <summary>
    /// Ship B picked while Ship A's sheet is still in flight: when A's answer
    /// finally lands it is dropped, and the bench, the title and the class a
    /// build would save under are all B's.
    /// </summary>
    [Fact]
    public void A_slow_sheet_for_the_previous_ship_does_not_land_on_the_new_one()
    {
        var page = Opened();
        page.Do("""
            const orig = globalThis.fetch;
            globalThis.fetch = (u, o) => u === '/api/garage/AEGS_Gladius'
              ? new Promise((resolve) => { window.__release = () => resolve(orig(u, o)); })
              : orig(u, o);
            const slow = openGarage('AEGS_Gladius');
            await openGarage('DRAK_Cutlass_Black');
            window.__release();
            await slow;
            globalThis.fetch = orig;
            """);

        Assert.Equal("DRAK_Cutlass_Black", page.Text("garageClass"));
        Assert.Equal("DRAK_Cutlass_Black", page.Text("garageStock.ship.class"));
        Assert.Equal("Drake Cutlass Black", page.NodeText("#garage-title"));
    }

    /// <summary>A port's candidates answered after another port was picked belong to nobody.</summary>
    [Fact]
    public void A_slow_options_answer_for_an_earlier_port_is_dropped()
    {
        var page = Opened();
        page.Serve("/api/garage/AEGS_Gladius/options?port=p2", CoolerOptions.Replace("\"portId\":\"p1\",\"hardpoint\":\"hardpoint_cooler_01\"", "\"portId\":\"p2\",\"hardpoint\":\"hardpoint_cooler_02\""));
        page.Do("""
            const orig = globalThis.fetch;
            globalThis.fetch = (u, o) => u.endsWith('options?port=p1')
              ? new Promise((resolve) => { window.__release = () => resolve(orig(u, o)); })
              : orig(u, o);
            const slow = selectBenchPort('p1');
            await selectBenchPort('p2');
            window.__release();
            await slow;
            globalThis.fetch = orig;
            """);

        Assert.Equal("p2", page.Text("garageOptions.port.portId"));
        Assert.Contains("cooler 02", page.NodeText("#garage-bench-panel"));
    }

    /// <summary>
    /// Two coolers that started alike and were changed apart are two rows on
    /// the bench, and a part fitted to one goes on that one. The rows used to
    /// fold on the stock fit, so cooler 2 changed along with cooler 1.
    /// </summary>
    [Fact]
    public void Changing_one_of_two_coolers_fitted_apart_changes_only_that_one()
    {
        var page = Opened();
        // The bench as it stands: cooler 1 already a Glacier, cooler 2 still stock.
        page.Serve("/api/garage/AEGS_Gladius/sheet", Ship("AEGS_Gladius", "Aegis Gladius", "COOL_JUST_S01_Glacier_SCItem", "Glacier", true));
        page.Do("garageSwaps = { p1: 'COOL_JUST_S01_Glacier_SCItem' }; await refitGarage(); await selectBenchPort('p1');");
        Assert.Equal(2, (int)page.Number("__dom.node('#garage-rig-left').byClass('garage-rig-slot').length"));
        Assert.Equal("p1", page.Text("siblingsOf('p1').join('|')"));

        page.Do("await fitPart('p1', 'COOL_ACAS_S01_Endo_SCItem');");

        var body = page.BodyOf("/api/garage/AEGS_Gladius/sheet");
        Assert.Contains("\"p1\":\"COOL_ACAS_S01_Endo_SCItem\"", body);
        Assert.DoesNotContain("\"p2\"", body);
        Assert.Equal("COOL_ACAS_S01_Endo_SCItem", page.Text("garageSwaps.p1"));
        Assert.Equal("undefined", page.Text("String(garageSwaps.p2)"));
    }

    /// <summary>Typing into the filter redraws the rows around the box, never the box.</summary>
    [Fact]
    public void The_bench_filter_keeps_its_input_while_the_rows_change()
    {
        var page = Opened();
        page.Do("await selectBenchPort('p1'); window.__box = __dom.node('#garage-bench-panel').querySelector('.bench-search');");
        Assert.Equal(4, (int)page.Number("__dom.node('#garage-bench-panel').byClass('candidate').length"));

        page.Do("window.__box.value = 'cold'; window.__box.fire('input');");

        Assert.True(page.Truth("__dom.node('#garage-bench-panel').querySelector('.bench-search') === window.__box"));
        Assert.Equal("cold", page.Text("__dom.node('#garage-bench-panel').querySelector('.bench-search').value"));
        Assert.Equal(1, (int)page.Number("__dom.node('#garage-bench-panel').byClass('candidate').length"));
        Assert.Contains("ColdSnap", page.NodeText("#garage-bench-panel"));
        Assert.Contains("1 of 4 fit", page.NodeText("#garage-bench-panel"));

        // Another port is another box.
        page.Serve("/api/garage/AEGS_Gladius/options?port=p2", CoolerOptions.Replace("\"portId\":\"p1\"", "\"portId\":\"p2\""));
        page.Do("await selectBenchPort('p2');");
        Assert.False(page.Truth("__dom.node('#garage-bench-panel').querySelector('.bench-search') === window.__box"));
    }

    /// <summary>
    /// A purchase undone on the bench leaves Shopping too: putting the only
    /// changed port back to stock reconciles the list the Garage keeps, which
    /// removes it, and says so. A bench that never made a list makes none.
    /// </summary>
    [Fact]
    public void Putting_a_fit_back_to_stock_takes_its_list_off_shopping()
    {
        var page = Opened();
        page.Serve("/api/garage/AEGS_Gladius/sheet", Ship("AEGS_Gladius", "Aegis Gladius", "COOL_JUST_S01_Glacier_SCItem", "Glacier", true));
        page.Serve("/api/garage/AEGS_Gladius/shop", """{"job":null,"removed":true,"removedTitle":"Aegis Gladius fit"}""");
        page.Do("garageSwaps = { p1: 'COOL_JUST_S01_Glacier_SCItem', p2: 'COOL_JUST_S01_Glacier_SCItem' }; await refitGarage(); await selectBenchPort('p1');");

        page.Do("await fitPart('p1', null);");

        Assert.Contains("POST /api/garage/AEGS_Gladius/shop", page.Fetched());
        Assert.Contains("\"onlyIfExists\":true", page.BodyOf("/api/garage/AEGS_Gladius/shop"));
        Assert.Contains("taken off Shopping", page.NodeText("#garage-shop-result"));

        // No list to keep: the reconcile answers nothing and the page says nothing.
        page.Serve("/api/garage/AEGS_Gladius/shop", """{"job":null,"removed":false}""");
        page.Do("garageSwaps = { p1: 'COOL_JUST_S01_Glacier_SCItem' }; await resetGarage();");
        Assert.True(page.Truth("__dom.node('#garage-shop-result').hidden"));
    }

    /// <summary>A destination the pilot chose is kept, and the proposal offered beside it rather than written over it.</summary>
    [Fact]
    public void A_chosen_destination_is_kept_and_the_proposal_offered_beside_it()
    {
        var page = Opened();
        page.Serve("/api/garage/AEGS_Gladius/sheet", Ship("AEGS_Gladius", "Aegis Gladius", "COOL_JUST_S01_Glacier_SCItem", "Glacier", true));
        page.Serve("/api/garage/AEGS_Gladius/shop", """
            {"job":{"id":"j1","title":"Aegis Gladius fit","items":[{"name":"Glacier","needed":1}],"destination":"Orison","destinationId":"P9"},
             "created":false,"consolidated":0,"destinationKept":true,
             "proposal":{"terminal":"Dumper's Depot","place":"Area18","covered":1,"of":1,"total":12000,"missing":[],"pricesKnown":true}}
            """);
        page.Do("garageSwaps = { p1: 'COOL_JUST_S01_Glacier_SCItem' }; await refitGarage(); await shopForBench();");

        var text = page.NodeText("#garage-shop-result");
        Assert.Contains("Destination kept as Orison (chosen on Shopping)", text);
        Assert.Contains("UEX would propose Dumper's Depot, Area18", text);
    }

    [Theory]
    [InlineData(@"Could not open C:\Users\nicolas\AppData\Local\Quantumwake\names.json: access denied", "Could not open <path>: access denied")]
    [InlineData(@"Access to the path 'D:\Games\StarCitizen\LIVE\Data.p4k' is denied.", "Access to the path '<path>' is denied.")]
    // A path may hold spaces, so a bare one swallows what follows: over-redacted, never leaked.
    [InlineData(@"\\NAS\share\Quantumwake\cache locked", "<path>")]
    [InlineData("failed at /home/nicolas/.local/share/qw/x.json again", "failed at <path> again")]
    [InlineData("failed at ~/qw/x.json again", "failed at <path> again")]
    [InlineData("The archive could not be read: bad central directory", "The archive could not be read: bad central directory")]
    public void The_summary_redacts_every_path_in_a_problem(string problem, string expected)
    {
        var page = new Page();
        Assert.Equal(expected, page.Text($"redactPaths({System.Text.Json.JsonSerializer.Serialize(problem)})"));

        page.Do($"window.__s = diagnosticSummary(null, {{state:'failed', problem:{System.Text.Json.JsonSerializer.Serialize(problem)}}}, null, [], {{}});");
        var summary = page.Text("window.__s");
        Assert.Contains($"Game data: failed - {expected}", summary);
        Assert.DoesNotContain("nicolas", summary);
    }
}
