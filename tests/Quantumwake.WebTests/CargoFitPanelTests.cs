namespace Quantumwake.WebTests;

/// <summary>
/// The Garage's cargo-fit panel: the hull's grids drawn from the dataset,
/// the load typed once and kept, the answer posted to the server and drawn
/// crate by crate, and the fleet told what fits without a roll-call.
/// </summary>
public class CargoFitPanelTests
{
    private const string Hermes = """
        {"class":"RSI_Hermes","name":"RSI Hermes","cargoScu":288,"gridsKnown":true,
         "grids":[{"class":"RSI_Hermes_CargoInventory_Main","x":5,"y":22.5,"z":2.5,"scu":144,"minBox":{"x":1.25,"y":1.25,"z":1.25},"maxBox":{"x":1.25,"y":1.25,"z":1.25},"external":false},
                  {"class":"RSI_Hermes_CargoInventory_Main","x":5,"y":22.5,"z":2.5,"scu":144,"minBox":{"x":1.25,"y":1.25,"z":1.25},"maxBox":{"x":1.25,"y":1.25,"z":1.25},"external":false}],
         "crates":[{"scu":1,"x":1.25,"y":1.25,"z":1.25},{"scu":32,"x":2.5,"y":10,"z":2.5}],"cratesFromInstall":true}
        """;

    private const string Fit = """
        {"ship":{"class":"RSI_Hermes","name":"RSI Hermes","cargoScu":288,"fits":true,"left":{},"reasons":[],"capacityScu":288,"loadScu":160,"placedScu":160,
                 "grids":[{"grid":{"class":"RSI_Hermes_CargoInventory_Main","x":5,"y":22.5,"z":2.5,"scu":144,"maxBox":{"x":1.25,"y":1.25,"z":1.25}},"cells":{"w":4,"l":18,"h":2},
                           "placed":[{"scu":32,"x":0,"y":0,"z":0,"dx":2,"dy":8,"dz":2},{"scu":32,"x":2,"y":0,"z":0,"dx":2,"dy":8,"dz":2},{"scu":32,"x":0,"y":8,"z":0,"dx":2,"dy":8,"dz":2},{"scu":32,"x":2,"y":8,"z":0,"dx":2,"dy":8,"dz":2},{"scu":16,"x":0,"y":16,"z":0,"dx":4,"dy":2,"dz":2}],
                           "usedScu":144,"ruleIgnored":true},
                          {"grid":{"class":"RSI_Hermes_CargoInventory_Main","x":5,"y":22.5,"z":2.5,"scu":144,"maxBox":{"x":1.25,"y":1.25,"z":1.25}},"cells":{"w":4,"l":18,"h":2},
                           "placed":[{"scu":16,"x":0,"y":0,"z":0,"dx":2,"dy":4,"dz":2}],"usedScu":16,"ruleIgnored":true}]},
         "load":{"crates":{"32":4,"16":2},"scu":160,"count":6},
         "yours":[{"class":"RSI_Hermes","name":"RSI Hermes","cargoScu":288,"fits":true,"placedScu":160,"capacityScu":288,"flown":true,"hours":8.3},
                  {"class":"DRAK_Corsair","name":"Drake Corsair","cargoScu":72,"fits":false,"placedScu":0,"capacityScu":72,"flown":true,"hours":1.2}],
         "smallest":[{"class":"ARGO_RAFT","name":"Argo RAFT","cargoScu":192,"fits":true,"placedScu":160,"capacityScu":192,"flown":false,"hours":0}],
         "cratesFromInstall":true}
        """;

    private static Page Opened()
    {
        var page = new Page();
        page.Serve("/api/cargo/RSI_Hermes", Hermes);
        page.Serve("/api/cargo/fit", Fit);
        page.Do("garageCargoLoad = {}; await loadGarageCargo('RSI_Hermes');");
        return page;
    }

    [Fact]
    public void The_hulls_grids_are_drawn_empty_with_the_datasets_own_figures()
    {
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#garage-cargo').hidden"));
        var sub = page.NodeText("#garage-cargo-sub");
        Assert.Contains("2 grids, 288 SCU by the dataset", sub);
        Assert.Contains("2 × 5 × 22.5 × 2.5 m", sub);

        var grids = "__dom.node('#garage-cargo-grids').children";
        Assert.Equal(2, page.Count($"{grids}.length"));
        Assert.Contains("4 × 18 × 2 cells, 144 SCU", page.Text($"{grids}[0].textContent"));
        Assert.Contains("placeholder on a hold this size", page.Text($"{grids}[0].textContent"));
        // Two layers drawn, no crates yet, nothing posted.
        Assert.Equal(2, page.Count($"{grids}[0].querySelectorAll('svg').length"));
        Assert.Equal(0, page.Count($"{grids}[0].querySelectorAll('rect').length") - 2);
        Assert.DoesNotContain("POST /api/cargo/fit", page.Fetched());

        // Seven sizes to type, the install's crates named as the source.
        Assert.Equal(7, page.Count("__dom.node('#garage-cargo-load').querySelectorAll('input').length"));
        Assert.Contains("read from your install", page.NodeText("#garage-cargo-note"));
        Assert.Contains("a fit not found is not proof", page.NodeText("#garage-cargo-note"));
    }

    /// <summary>
    /// Grids read from the install say so: the hull's own loadout places
    /// them, not a downloaded dataset.
    /// </summary>
    [Fact]
    public void Grids_read_from_the_install_are_credited_to_it()
    {
        var page = new Page();
        page.Serve("/api/cargo/RSI_Hermes", Hermes.Replace("\"gridsKnown\":true", "\"gridsKnown\":true,\"gridsFrom\":\"install\""));
        page.Do("garageCargoLoad = {}; await loadGarageCargo('RSI_Hermes');");

        var sub = page.NodeText("#garage-cargo-sub");
        Assert.Contains("2 grids, 288 SCU as your install’s loadout places them", sub);
        Assert.DoesNotContain("dataset", sub);
    }

    [Fact]
    public void Typing_a_load_and_fitting_it_posts_the_crates_and_draws_where_each_went()
    {
        var page = Opened();

        page.Do("garageCargoLoad = {32: 4, 16: 2}; renderCargoLoadInputs(); await fitCargo();");

        Assert.Contains("\"ship\":\"RSI_Hermes\"", page.BodyOf("/api/cargo/fit"));
        Assert.Contains("\"32\":4", page.BodyOf("/api/cargo/fit"));
        Assert.Contains("\"16\":2", page.BodyOf("/api/cargo/fit"));
        Assert.Contains("6 crates, 160 SCU", page.NodeText("#garage-cargo-load"));

        var verdict = page.NodeText("#garage-cargo-verdict");
        Assert.Contains("Fits: 6 crates, 160 of 288 SCU, with 128 SCU to spare", verdict);
        Assert.Contains("fits", page.Text("__dom.node('#garage-cargo-verdict').className"));

        var grids = "__dom.node('#garage-cargo-grids').children";
        // Five crates in the first hold, one in the second; a two-high crate
        // shows on both layers and its size on the floor layer only.
        Assert.Equal(5, page.Count($"{grids}[0].querySelectorAll('svg')[0].querySelectorAll('rect').length") - 1);
        Assert.Equal(5, page.Count($"{grids}[0].querySelectorAll('svg')[1].querySelectorAll('rect').length") - 1);
        Assert.Equal(5, page.Count($"{grids}[0].querySelectorAll('svg')[0].querySelectorAll('text').length"));
        Assert.Equal(0, page.Count($"{grids}[0].querySelectorAll('svg')[1].querySelectorAll('text').length"));
        Assert.Contains("144 of 144 SCU used", page.Text($"{grids}[0].textContent"));
        Assert.Contains("16 of 144 SCU used", page.Text($"{grids}[1].textContent"));
    }

    [Fact]
    public void The_fleet_answer_leads_with_what_fits_and_folds_the_rest_into_a_line()
    {
        var page = Opened();
        page.Do("garageCargoLoad = {32: 4, 16: 2}; await fitCargo();");

        var fleet = page.NodeText("#garage-cargo-fleet");
        Assert.False(page.Truth("__dom.node('#garage-cargo-fleet').hidden"));
        Assert.Contains("Your ships", fleet);
        Assert.Contains("RSI Hermes", fleet);
        Assert.Contains("fits, 288 SCU hold, flown 8.3 h", fleet);
        Assert.Contains("Not in: Drake Corsair (72 SCU)", fleet);
        Assert.Contains("Smallest hulls in the reference that take it", fleet);
        Assert.Contains("Argo RAFT", fleet);
        Assert.Equal(2, page.Count("__dom.node('#garage-cargo-fleet').querySelectorAll('li').length"));
    }

    [Fact]
    public void A_load_that_does_not_fit_says_why_in_the_datasets_terms()
    {
        var page = Opened();
        page.Serve("/api/cargo/fit", """
            {"ship":{"class":"DRAK_Corsair","name":"Drake Corsair","cargoScu":72,"fits":false,"left":{"32":1},
                     "reasons":["A 32 SCU crate is bigger than any box this hull's grids accept - the largest they take is 5 × 7.5 × 2.5 m."],
                     "capacityScu":72,"loadScu":32,"placedScu":0,"grids":[]},
             "load":{"crates":{"32":1},"scu":32,"count":1},"yours":[],"smallest":[]}
            """);
        page.Do("garageCargoLoad = {32: 1}; await fitCargo();");

        var verdict = page.NodeText("#garage-cargo-verdict");
        Assert.Contains("Does not fit: 1 × 32 SCU left out.", verdict);
        Assert.Contains("largest they take is 5 × 7.5 × 2.5 m", verdict);
        Assert.Contains("None of the ships the logs have seen you fly has a cargo grid", page.NodeText("#garage-cargo-fleet"));

        // Room rather than a rule: the packer says it is not proof.
        page.Serve("/api/cargo/fit", """
            {"ship":{"class":"MISC_Freelancer","name":"MISC Freelancer","cargoScu":66,"fits":false,"left":{"32":1},"reasons":[],
                     "capacityScu":66,"loadScu":64,"placedScu":32,"grids":[]},
             "load":{"crates":{"32":2},"scu":64,"count":2},"yours":[],"smallest":[]}
            """);
        page.Do("garageCargoLoad = {32: 2}; await fitCargo();");
        Assert.Contains("No packing found for 1 × 32 SCU: 32 of 64 SCU placed in 66", page.NodeText("#garage-cargo-verdict"));
        Assert.Contains("not proof it cannot be done", page.NodeText("#garage-cargo-verdict"));
    }

    [Fact]
    public void A_digest_without_grids_asks_for_a_refresh_and_a_hull_without_any_says_so()
    {
        var page = new Page();
        page.Serve("/api/cargo/RSI_Hermes", Hermes.Replace("\"gridsKnown\":true", "\"gridsKnown\":false").Replace("\"grids\":[{", "\"grids\":[],\"was\":[{"));
        page.Do("garageCargoLoad = {}; await loadGarageCargo('RSI_Hermes');");
        Assert.Contains("predates cargo grids", page.NodeText("#garage-cargo-sub"));
        Assert.DoesNotContain("POST /api/cargo/fit", page.Fetched());

        page.Serve("/api/cargo/AEGS_Gladius", """{"class":"AEGS_Gladius","name":"Aegis Gladius","cargoScu":0,"gridsKnown":true,"grids":[],"crates":[],"cratesFromInstall":false}""");
        page.Do("await loadGarageCargo('AEGS_Gladius');");
        Assert.Contains("places no cargo grid on the Aegis Gladius", page.NodeText("#garage-cargo-sub"));
        Assert.Contains("table read from it on 16 Sep 2026", page.NodeText("#garage-cargo-note"));
    }
}
