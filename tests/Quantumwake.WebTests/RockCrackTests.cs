namespace Quantumwake.WebTests;

/// <summary>
/// The rock calculator on the Mining page: the ship decides the heads, every
/// change asks the server, and the answer is called an estimate with the
/// rule's owner named. Nothing is computed on the page.
/// </summary>
public class RockCrackTests
{
    private const string Model = """
        {"ready":true,
         "constants":{"powerCapacityPerMass":10,"decayPerMass":0.2,"optimalWindowSize":0.1,"optimalWindowMaxSize":0.5},
         "lasers":[
           {"class":"Mining_Laser_GRIN_Arbor_S1","name":"Arbor MH1 Mining Laser","size":1,"power":2340,"slots":1,"modifiers":{"instability":-35,"windowSize":40,"resistance":25}},
           {"class":"Mining_Laser_THCN_Helix_S1","name":"Helix I Mining Laser","size":1,"power":3900,"extractionPower":1850,"filterModifier":30,"throttleMinimum":0.2,"slots":2,"modifiers":{"windowSize":-40,"resistance":-30},
            "market":{"price":55100,"shops":[{"terminal":"Tammany and Sons","place":"Lorville","system":"Stanton","price":55100},{"terminal":"Dumper's Area 18","place":"Area18","system":"Stanton","price":58000}]}},
           {"class":"Mining_Laser_SHIN_Klein_S1","name":"Klein-S1 Mining Laser","size":1,"power":3120,"slots":0,"modifiers":{"resistance":-45}},
           {"class":"Mining_Laser_GRIN_Arbor_S2","name":"Arbor MH2 Mining Laser","size":2,"power":2900,"slots":2,"modifiers":{}}],
         "modules":[{"class":"Mining_Modules_Active_Surge","name":"Surge Module","active":true,"lifetime":15,"charges":7,"powerMultiplier":1.5,"extractionMultiplier":1,"modifiers":{"resistance":-15.5,"instability":10},"market":{"price":1400,"shops":[]}}],
         "gadgets":[{"class":"Mining_Gadget_SHIN_Sabir","name":"Sabir","modifiers":{"resistance":-50,"windowSize":50,"instability":15},"market":{"price":null,"shops":[]}}],
         "itemPricesKnown":true,
         "minerals":[{"class":"Quantainium_Raw","name":"Quantainium (Raw)","resistance":0.95,"instability":1000,"windowMidpoint":0.5,"windowRandomness":0.2,"explosionMultiplier":260},
                     {"class":"Copper_Ore","name":"Copper (Ore)","resistance":-0.7,"instability":50,"windowMidpoint":0.6,"windowRandomness":0.15,"explosionMultiplier":-20}],
         "compositions":[
           {"class":"Asteroid_PType_Copper","name":"Asteroid (P-Type)","minimumDistinctElements":3,
            "parts":[{"element":"Copper_Ore","name":"Copper (Ore)","minPercent":30,"maxPercent":70,"probability":1,"sellPerScu":4200,"rawPerScu":1200,"yield":9,"yieldAt":"Refinement Processing - MIC-L5"},
                     {"element":"Quantainium_Raw","name":"Quantainium (Raw)","minPercent":20,"maxPercent":50,"probability":0.02,"sellPerScu":170000,"rawPerScu":null,"yield":null,"yieldAt":null}]},
           {"class":"Asteroid_PType_Tin","name":"Asteroid (P-Type)","minimumDistinctElements":3,"parts":[{"element":"Copper_Ore","name":"Copper (Ore)","minPercent":10,"maxPercent":20,"probability":0.5,"sellPerScu":4200}]}],
         "pricesKnown":true,
         "ships":[
           {"class":"ARGO_MOLE","name":"Argo MOLE","heads":[{"portId":"a","size":2,"stock":"Mining_Laser_GRIN_Arbor_S2"},{"portId":"b","size":2,"stock":"Mining_Laser_GRIN_Arbor_S2"},{"portId":"c","size":2,"stock":"Mining_Laser_GRIN_Arbor_S2"}],"flown":false},
           {"class":"MISC_Prospector","name":"MISC Prospector","heads":[{"portId":"p","size":1,"stock":"Mining_Laser_GRIN_Arbor_S1"}],"flown":true}],
         "rule":{"requiredWattsPerKg":0.36,"soloRatio":1.15,"gadgetRatio":0.7,"source":"scminer.rocks, 2026-09-15 - the community's line, not the game's"}}
        """;

    private const string Verdict = """
        {"verdict":{"powerDelivered":2340,"powerRequired":1800,"ratio":1.3,"verdict":"solo",
                    "effectiveResistancePercent":0,"effectiveInstabilityPercent":9.8,"windowPercent":14,"maxCrackableMassKg":6500,
                    "energyCapacity":50000,"energyDecayPerSecond":1000,"modifiers":{},
                    "notes":["By the game's constants the rock holds 50,000 energy and sheds 1,000 a second; the window is 14% of the gauge."]},
         "matrix":[
           {"laser":"Mining_Laser_THCN_Helix_S1","name":"Helix I Mining Laser","power":3900,"powerDelivered":3900,"ratio":2.167,"verdict":"solo","maxCrackableMassKg":10833},
           {"laser":"Mining_Laser_GRIN_Arbor_S1","name":"Arbor MH1 Mining Laser","power":2340,"powerDelivered":2340,"ratio":1.3,"verdict":"solo","maxCrackableMassKg":6500}]}
        """;

    private static Page Opened()
    {
        var page = new Page();
        page.Serve("/api/mining/model", Model);
        page.Serve("/api/mining/crack", Verdict);
        // The markup's defaults (5,000 kg, 0%, 15%) are not in the stub; set them as the page would find them.
        page.Do("__dom.node('#crack-mass').value = '5000'; __dom.node('#crack-resistance').value = '0'; __dom.node('#crack-instability').value = '15'; await loadCrackModel();");
        return page;
    }

    [Fact]
    public void A_fixed_golem_head_keeps_its_modules_and_does_not_offer_other_lasers()
    {
        var page = Opened();
        page.Do("""
            crackModel.lasers.push({class:'Mining_Laser_DRAK_Golem_S1',name:'Pitman',size:1,power:3900,slots:2,bespoke:true});
            crackModel.ships.push({class:'DRAK_Golem',name:'Drake Golem',heads:[{portId:'g',size:1,stock:'Mining_Laser_DRAK_Golem_S1',editable:false,note:'Fixed Pitman head — bespoke to the Golem. Mining modules remain configurable.'}]});
            renderCrackHeads();
            """);
        Assert.DoesNotContain("Pitman", page.NodeText("#crack-heads"));
        page.Do("__dom.node('#crack-ship').value = 'DRAK_Golem'; renderCrackHeads(); await assessCrack();");
        var laser = "__dom.node('#crack-heads').querySelector('.crack-laser')";
        Assert.True(page.Truth($"{laser}.disabled"));
        Assert.Equal(1, page.Count($"{laser}.options.length"));
        Assert.Equal(2, page.Count("__dom.node('#crack-heads').querySelectorAll('.crack-module').length"));
        Assert.Contains("Fixed Pitman head", page.NodeText("#crack-heads"));
        Assert.Contains("Stock head only", page.NodeText("#crack-matrix-note"));
        Assert.Contains("\"ship\":\"DRAK_Golem\"", page.BodyOf("/api/mining/crack"));
    }

    [Fact]
    public void The_flown_ship_comes_first_and_its_own_head_is_picked()
    {
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#crack').hidden"));
        Assert.Equal("MISC_Prospector", page.Text("__dom.node('#crack-ship').value"));
        Assert.Contains("flown", page.Text("__dom.node('#crack-ship').options[0].textContent"));

        var heads = "__dom.node('#crack-heads').querySelectorAll('.crack-head')";
        Assert.Equal(1, page.Count($"{heads}.length"));
        Assert.Equal("Mining_Laser_GRIN_Arbor_S1", page.Text($"{heads}[0].querySelectorAll('.crack-laser')[0].value"));
        // Only S1 heads are offered on an S1 port, and the Arbor MH1 has one module slot.
        Assert.Equal(3, page.Count($"{heads}[0].querySelectorAll('.crack-laser')[0].options.length"));
        Assert.Equal(1, page.Count($"{heads}[0].querySelectorAll('.crack-module').length"));
        Assert.Contains("Current fit · 1 head · 0/1 module slot fitted", page.NodeText("#crack-fit-summary"));

        Assert.Contains("0.36 W per kilogram", page.NodeText("#crack-rule"));
        Assert.Contains("scminer.rocks", page.NodeText("#crack-rule"));
    }

    [Fact]
    public void The_rock_is_posted_as_scanned_and_the_verdict_is_called_an_estimate()
    {
        var page = Opened();

        var body = page.BodyOf("/api/mining/crack");
        Assert.Contains("\"massKg\":5000", body);
        Assert.Contains("\"resistance\":0", body);
        Assert.Contains("\"laser\":\"Mining_Laser_GRIN_Arbor_S1\"", body);

        var verdict = page.NodeText("#crack-verdict");
        Assert.Contains("Breaks on this fit", verdict);
        Assert.Contains("2,340 delivered against 1,800 needed", verdict);
        Assert.Contains("130%", verdict);
        Assert.Contains("estimate, community rule", verdict);
        Assert.Contains("6,500 kg", verdict);
        Assert.Contains("50,000", verdict);

        var matrix = page.NodeText("#crack-matrix tbody");
        Assert.Contains("Helix I Mining Laser", matrix);
        Assert.Contains("10,833", matrix);
        Assert.Contains("only the laser changes", page.NodeText("#crack-matrix-note"));
    }

    /// <summary>The slots follow the head: two on a Helix I, none on a Klein-S1 - and a pick survives where its slot does.</summary>
    [Fact]
    public void Module_slots_follow_the_head()
    {
        var page = Opened();
        var row = "__dom.node('#crack-heads').querySelectorAll('.crack-head')[0]";

        page.Do($"{row}.querySelectorAll('.crack-module')[0].value = 'Mining_Modules_Active_Surge'; {row}.querySelectorAll('.crack-laser')[0].value = 'Mining_Laser_THCN_Helix_S1'; renderCrackSlots({row});");
        Assert.Equal(2, page.Count($"{row}.querySelectorAll('.crack-module').length"));
        Assert.Equal("Mining_Modules_Active_Surge", page.Text($"{row}.querySelectorAll('.crack-module')[0].value"));

        page.Do($"{row}.querySelectorAll('.crack-laser')[0].value = 'Mining_Laser_SHIN_Klein_S1'; renderCrackSlots({row});");
        Assert.Equal(0, page.Count($"{row}.querySelectorAll('.crack-module').length"));
        Assert.Contains("no module slots", page.Text($"{row}.textContent"));
    }

    /// <summary>
    /// A deposit shows the game's mix - share when present, chance of being
    /// present - with the refined price a SCU, a rough worth of a SCU of the
    /// mix, and the honest gap: nothing read turns kilograms into SCU.
    /// </summary>
    [Fact]
    public void A_deposit_shows_its_mix_and_says_what_per_rock_would_need()
    {
        var page = Opened();

        // Two presets share the HUD name, so the class's mineral tells them apart.
        Assert.Contains("Asteroid (P-Type) · copper", page.Text("[...__dom.node('#crack-deposit').options].map(o => o.textContent).join('|')"));

        page.Do("__dom.node('#crack-deposit').value = 'Asteroid_PType_Copper'; renderCrackDeposit();");

        Assert.False(page.Truth("__dom.node('#crack-deposit-panel').hidden"));
        var mix = page.NodeText("#crack-mix tbody");
        Assert.Contains("Copper (Ore)", mix);
        Assert.Contains("30–70%", mix);
        Assert.Contains("100%", mix);
        Assert.Contains("4,200", mix);
        Assert.Contains("res -0.7", mix);
        Assert.Contains("Quantainium (Raw)", mix);
        Assert.Contains("2%", mix);

        var note = page.NodeText("#crack-mix-note");
        Assert.Contains("at least 3 minerals a rock", note);
        // Refined: 0.5 × 1 × 4,200 + 0.35 × 0.02 × 170,000 = 2,100 + 1,190 = 3,290, before the
        // refinery's yield, which no file or feed carries; raw: 0.5 × 1 × 1,200 = 600, the
        // quantainium having no raw price. The station's +9% is a bonus on the yield and is
        // shown signed as one, never multiplied in as if it were the yield.
        Assert.Contains("Roughly 3,290 aUEC a SCU of the mix at refined prices, before the refinery's yield", note);
        Assert.Contains("600 aUEC sold raw", note);
        Assert.Contains("1,200", mix);
        Assert.Contains("+9%", mix);
        Assert.DoesNotContain("2,870", note);
        Assert.Contains("Per rock is not given", note);

        // The likeliest mineral is picked for the notes.
        Assert.Equal("Copper_Ore", page.Text("__dom.node('#crack-mineral').value"));
    }

    [Fact]
    public void Picking_the_MOLE_gives_three_S2_heads_and_a_module_and_gadget_go_into_the_request()
    {
        var page = Opened();
        page.Do("""
            __dom.node('#crack-ship').value = 'ARGO_MOLE'; renderCrackHeads();
            const rows = __dom.node('#crack-heads').querySelectorAll('.crack-head');
            rows[0].querySelectorAll('.crack-module')[0].value = 'Mining_Modules_Active_Surge';
            __dom.node('#crack-gadget').value = 'Mining_Gadget_SHIN_Sabir';
            __dom.node('#crack-resistance').value = '40';
            renderCrackFitSummary();
            await assessCrack();
            """);

        var heads = "__dom.node('#crack-heads').querySelectorAll('.crack-head')";
        Assert.Equal(3, page.Count($"{heads}.length"));
        Assert.Equal("Mining_Laser_GRIN_Arbor_S2", page.Text($"{heads}[1].querySelectorAll('.crack-laser')[0].value"));
        Assert.Contains("Current fit · 3 heads · 1/6 module slots fitted", page.NodeText("#crack-fit-summary"));

        var body = page.BodyOf("/api/mining/crack");
        Assert.Contains("\"resistance\":40", body);
        Assert.Contains("\"modules\":[\"Mining_Modules_Active_Surge\"]", body);
        Assert.Contains("\"gadget\":\"Mining_Gadget_SHIN_Sabir\"", body);
        // Three heads posted, the other two without modules.
        Assert.Equal(3, body.Split("\"laser\":").Length - 1);
    }

    /// <summary>The mineral is for the notes: the game's own figures for it, and a warning where an overcharge is the rock gone.</summary>
    [Fact]
    public void A_mineral_adds_the_games_figures_for_it_to_the_notes()
    {
        var page = Opened();
        page.Do("__dom.node('#crack-mineral').value = 'Quantainium_Raw'; await assessCrack();");

        var verdict = page.NodeText("#crack-verdict");
        Assert.Contains("Quantainium (Raw): element resistance 0.95, instability 1000", verdict);
        Assert.Contains("an overcharge here is the rock gone", verdict);
    }

    /// <summary>Every head, module and gadget with the game's figures and UEX's cheapest terminal - a blank price is no terminal, not free.</summary>
    [Fact]
    public void The_fittings_tables_carry_the_figures_and_the_price_and_where()
    {
        var page = Opened();

        var heads = page.NodeText("#crack-heads-table tbody");
        Assert.Contains("Helix I Mining Laser", heads);
        Assert.Contains("3,900", heads);
        Assert.Contains("1,850", heads);
        Assert.Contains("−30% resistance, −40% window", heads);
        Assert.Contains("55,100 aUEC", heads);
        Assert.Contains("Tammany and Sons, Lorville +1", heads);

        var modules = page.NodeText("#crack-modules-table tbody");
        Assert.Contains("Surge Module", modules);
        Assert.Contains("active", modules);
        Assert.Contains("×1.5", modules);
        Assert.Contains("15s × 7", modules);
        Assert.Contains("1,400 aUEC", modules);
        Assert.Contains("no terminal recorded", modules);

        var gadgets = page.NodeText("#crack-gadgets-table tbody");
        Assert.Contains("Sabir", gadgets);
        Assert.Contains("−50% resistance", gadgets);
        Assert.Contains("—", gadgets);

        // The matrix carries the head's price too.
        Assert.Contains("55,100 aUEC", page.NodeText("#crack-matrix tbody"));
    }

    /// <summary>
    /// A scan-results screenshot the Log tab read fills the form - mass,
    /// resistance, the primary mineral - each only where the engine read it,
    /// and instability is left alone because the panel's figure and the
    /// rule's percentage are not known to be one scale. The Log entry says
    /// the rock and offers the calculator.
    /// </summary>
    [Fact]
    public void The_last_scanned_rock_fills_the_form_where_it_read()
    {
        var page = Opened();
        page.Serve("/api/screen/readings?take=50", """
            {"readings":[
               {"shot":"ScreenShot-2026-09-16_01-10-00-AAA.jpg","shotAt":"2026-09-16T05:10:00Z","kind":"Mining","summary":"a rock scanned: Quantainium (Raw), 6,295 kg, 12% resistance, 21.07 SCU","checks":[],"lines":[],"tookMs":150,
                "mining":{"primaryRead":"QUANTAINIUM (RAW)","primary":"Quantainium (Raw)","massKg":6295,"resistancePercent":12,"instability":1.75,"scu":21.07,"difficulty":"HARD",
                          "parts":[{"read":"QUANTAINIUM (RAW)","mineral":"Quantainium (Raw)","percent":31.2,"quality":812},{"read":"WERT MATERIALS","mineral":"Inert materials","percent":68.8,"quality":0}]}},
               {"shot":"older.jpg","shotAt":"2026-09-10T05:10:00Z","kind":"Mining","summary":"older","checks":[],"lines":[],"tookMs":150,
                "mining":{"primaryRead":"IRON","primary":null,"massKg":100,"resistancePercent":null,"instability":null,"scu":null,"difficulty":null,"parts":[]}}],
             "clipboard":[],"total":2,"pastes":0}
            """);
        page.Do("__dom.node('#crack-instability').value = '15'; await useLastScan();");

        Assert.Equal("6295", page.Text("__dom.node('#crack-mass').value"));
        Assert.Equal("12", page.Text("__dom.node('#crack-resistance').value"));
        Assert.Equal("15", page.Text("__dom.node('#crack-instability').value"));
        Assert.Equal("Quantainium_Raw", page.Text("__dom.node('#crack-mineral').value"));
        var note = page.NodeText("#crack-scan-note");
        Assert.Contains("21.07 SCU by the game's own count", note);
        Assert.Contains("Instability is left as typed", note);
        Assert.Contains("\"massKg\":6295", page.BodyOf("/api/mining/crack"));

        // The Log tab's entry for the same reading.
        page.Do("""
            const box = __dom.node('#t'); box.replaceChildren();
            renderSighting(box, {"kind":"Mining","shot":"x.jpg","shotAt":"2026-09-16T05:10:00Z","checks":[],
              "mining":{"primary":"Quantainium (Raw)","massKg":6295,"resistancePercent":12,"instability":1.75,"scu":21.07,"difficulty":"HARD",
                        "parts":[{"read":"Q","mineral":"Quantainium (Raw)","percent":31.2,"quality":812}]}}, true);
            """);
        var entry = page.NodeText("#t");
        Assert.Contains("a rock scanned: Quantainium (Raw)", entry);
        Assert.Contains("6,295 kg · 12% resistance · instability 1.75 · 21.07 SCU · hard", entry);
        Assert.Contains("31.2% · quality 812", entry);
        Assert.Contains("Mining fit", entry);
    }

    [Fact]
    public void Before_the_install_is_read_it_says_so_instead_of_showing_a_form()
    {
        var page = new Page();
        page.Serve("/api/mining/model", """{"ready":false,"lasers":[],"modules":[],"gadgets":[],"minerals":[],"ships":[]}""");
        page.Do("gameDataState = {state:'reading'}; await loadCrackModel();");

        Assert.True(page.Truth("__dom.node('#crack').hidden"));
        Assert.Contains("Still reading", page.NodeText("#crack-unready"));
    }
}
