namespace Quantumwake.WebTests;

/// <summary>
/// The Garage sheet: a ship's numbers, grouped, each group naming where it
/// came from, and the caveats said beside the figures they qualify.
/// </summary>
public class GaragePageTests
{
    private const string Garage = """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "mine":[{"class":"AEGS_Gladius","name":"Aegis Gladius","sorties":12,"lastFlown":"2026-09-13T00:00:00+00:00"}],
         "all":[{"class":"AEGS_Gladius","name":"Aegis Gladius","manufacturer":"Aegis Dynamics","role":"Light Fighter","size":2},
                {"class":"DRAK_Cutlass_Black","name":"Drake Cutlass Black","manufacturer":"Drake Interplanetary","role":"Medium Fighter","size":3}]}
        """;

    private const string Gladius = """
        {"known":true,"dump":"4.10.0-LIVE.12519617",
         "ship":{"class":"AEGS_Gladius","name":"Aegis Gladius","manufacturer":"Aegis Dynamics","role":"Light Fighter","career":"Combat",
                 "size":2,"crew":1,"hullMass":48552,"health":6110,"cargoScu":0,"quantumFuel":600,"hydrogenFuel":1600,
                 "flight":{"scm":226,"boost":520,"max":1193,"pitch":68,"yaw":52,"roll":200},"powerPools":{"Shield":2,"WeaponGun":4},"stockMass":55646},
         "sheet":{"class":"AEGS_Gladius","name":"Aegis Gladius","mass":55646,
                  "shields":{"em":22078,"ir":8711,"emByGroup":{"PowerPlant":13480,"Cooler":3367,"Shield":2802,"Radar":2034,"LifeSupportGenerator":354,"WeaponGun":41}},
                  "quantum":{"em":37164,"ir":7263,"emByGroup":{}},
                  "power":{"available":16,"usedShields":24.1,"usedQuantum":18.1,"usedByGroup":{},"overShields":true,"overQuantum":true},
                  "cooling":{"generated":68,"usedShields":36.1,"usedQuantum":30.1,"loadShields":0.531,"loadQuantum":0.443},
                  "shield":{"hp":6336,"regen":1394,"generators":2,"poolLimit":2},
                  "weapons":{"fixedDps":1944.5,"fixedSustainedDps":1186.9,"fixedAlpha":119.4,"turretDps":0,"missileDamage":21000,"missiles":6,
                             "guns":["CF-337 Panther Repeater","CF-337 Panther Repeater","Mantis GT-220 Gatling"]},
                  "quantumDrive":{"drive":"Beacon","speed":161410900,"spoolTime":4.4,"cooldown":11.9,"range":32223415682,"fuelPerGm":18.62},
                  "armorSignals":{"em":1.13,"ir":1.13,"crossSection":1},
                  "crossSection":{"x":6923,"y":1731,"z":8654},
                  "parts":[],"notes":["Draws 24.1 power segments with shields up; the plant provides 16. The game will brown out something."]},
         "ports":[{"portId":"p1","hardpoint":"Hardpoint_cooler_left","group":"Cooler","minSize":1,"maxSize":1,"class":"COOL_AEGS_S01_Bracer_SCItem","name":"Bracer","stockClass":"COOL_AEGS_S01_Bracer_SCItem","changed":false,
                   "fitted":{"type":"Cooler","name":"Bracer","size":1,"grade":2,"manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3}},
                  {"portId":"p2","hardpoint":"hardpoint_quantum_drive","group":"QuantumDrive","minSize":1,"maxSize":1,"class":"QDRV_WETK_S01_Beacon_SCItem","name":"Beacon","stockClass":"QDRV_WETK_S01_Beacon_SCItem","changed":false,
                   "fitted":{"type":"QuantumDrive","name":"Beacon","size":1,"grade":3,"manufacturer":"Wei-Tek","makerCode":"WETK","em":15000,"ir":0,"quantum":{"speed":161410900,"spoolTime":4.4,"fuelRate":1.862e-8}}}]}
        """;

    private static Page Opened()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        page.Serve("/api/garage/AEGS_Gladius", Gladius);
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Do("await loadGarage();");
        return page;
    }

    [Theory]
    [InlineData("WeaponMining", "Mining laser", "Arbor MH1 Mining Laser")]
    [InlineData("SalvageHead", "Salvage head", "Baler Salvage Head")]
    public void Industrial_heads_are_visible_and_open_their_options(string kind, string label, string name)
    {
        var page = Opened();
        page.Do($$"""
            garageStock.ports = [{portId:'head', hardpoint:'industrial_head', group:'{{kind}}', minSize:1, maxSize:2,
                class:'stock_head', stockClass:'stock_head', name:'{{name}}', fitted:{type:'{{kind}}',name:'{{name}}',size:1} }];
            renderGarage(garageStock);
            """);
        Assert.Contains(name, page.NodeText("#garage-rig-right"));
        Assert.Contains(label, page.NodeText("#garage-bench-ports"));
        page.Serve("/api/garage/AEGS_Gladius/options?port=head", """{"port":{"portId":"head","kinds":[],"minSize":1,"maxSize":2},"options":[]}""");
        page.Do("await __dom.node('#garage-rig-right').descendants().find(n => n.classList.contains('garage-rig-slot')).fire('click');");
        Assert.Contains("GET /api/garage/AEGS_Gladius/options?port=head", page.Fetched());
    }

    [Fact]
    public void An_old_industrial_cache_explains_the_missing_heads()
    {
        var page = Opened();
        page.Do("garageStock.industrialPortsKnown = false; garageStock.ship.role = 'Light Salvage'; garageStock.sheet.notes = []; renderGarage(garageStock);");
        Assert.False(page.Truth("__dom.node('#garage-notes').hidden"));
        Assert.Contains("Refresh the community dataset", page.NodeText("#garage-notes"));
        page.Do("garageStock.industrialPortsKnown = true; renderGarage(garageStock);");
        Assert.DoesNotContain("Refresh the community dataset", page.NodeText("#garage-notes"));
    }

    [Fact]
    public void Fixed_heads_stay_visible_without_a_swap_control()
    {
        var page = Opened();
        page.Do("""
            garageStock.ports = [{portId:'fixed',hardpoint:'head',group:'WeaponMining',name:'Pitman',class:'pitman',stockClass:'pitman',minSize:1,maxSize:1,
                fixedReason:'Fixed Pitman head — bespoke to the Golem.'}];
            renderGarage(garageStock);
            """);
        Assert.Contains("Pitman", page.NodeText("#garage-rig-right"));
        Assert.True(page.Truth("__dom.node('#garage-rig-right').descendants().find(n => n.classList.contains('garage-rig-slot')).disabled"));
        page.Do("await selectBenchPort('fixed');");
        Assert.DoesNotContain("GET /api/garage/AEGS_Gladius/options?port=fixed", page.Fetched());
    }

    [Fact]
    public void The_most_flown_ship_opens_first_and_the_pickers_name_it()
    {
        var page = Opened();

        Assert.Contains("GET /api/garage/AEGS_Gladius", page.Fetched());
        Assert.Equal("Aegis Gladius", page.NodeText("#garage-title"));
        Assert.Contains("Light Fighter", page.NodeText("#garage-sub"));
        Assert.Contains("Military", page.NodeText("#garage-sub"));
        Assert.Equal("military", page.Text("__dom.node('#garage-sub').descendants().find(n => n.classList.contains('garage-duty')).dataset.discipline"));
        Assert.Contains("4.10.0-LIVE.12519617", page.NodeText("#garage-source"));
        Assert.Equal("AEGS_Gladius", page.Text("__dom.node('#garage-mine').value"));
    }

    [Fact]
    public void Ship_discipline_badges_are_derived_from_the_reference_career_and_role()
    {
        var page = Opened();

        Assert.Equal("Industrial|Civilian|Military",
            page.Text("['Industrial|Light Mining', 'Exploration|Pathfinder', 'Combat|Medium Fighter'].map(s => { const [career, role] = s.split('|'); return garageShipDiscipline(career, role).label; }).join('|')"));
    }

    [Fact]
    public void The_fitted_layout_centres_the_ship_and_keeps_components_clickable()
    {
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#garage-rig').hidden"));
        Assert.Contains("Bracer", page.NodeText("#garage-rig-left"));
        Assert.Contains("Beacon", page.NodeText("#garage-rig-left"));
        Assert.True(page.Truth("__dom.node('#garage-ship-model').descendants().some(n => n.classList.contains('ship-picture'))"));
        Assert.Contains("Power", page.NodeText("#garage-ship-summary"));
        Assert.Contains("8.1 short", page.NodeText("#garage-ship-summary"));

        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions);
        page.Do("await __dom.node('#garage-rig-left').descendants().find(n => n.classList.contains('garage-rig-slot')).fire('click');");

        Assert.Contains("GET /api/garage/AEGS_Gladius/options?port=p1", page.Fetched());
        Assert.True(page.Truth("__dom.node('#garage-rig-left').descendants().some(n => n.classList.contains('garage-rig-slot') && n.classList.contains('selected'))"));
        Assert.Equal("true", page.Text("__dom.node('#garage-rig-left').descendants().find(n => n.classList.contains('garage-rig-slot')).getAttribute('aria-pressed')"));
    }

    [Fact]
    public void Every_group_is_drawn_and_names_its_source()
    {
        var page = Opened();
        var text = page.NodeText("#garage-sheet");

        foreach (var group in new[] { "Hull", "Flight", "Weapons", "Defence", "Signature", "Systems", "Quantum" })
            Assert.Contains(group, text);

        Assert.Contains("signature model", text);
        Assert.Contains("ships.json", text);
    }

    [Fact]
    public void The_stat_cards_identify_the_question_each_group_answers()
    {
        var page = Opened();
        var cards = "__dom.node('#garage-sheet').descendants().filter(n => n.classList.contains('sheet-group'))";

        Assert.Equal("hull|flight|weapons|defence|signature|systems|quantum",
            page.Text($"{cards}.map(n => n.dataset.group).join('|')"));
        Assert.True(page.Truth($"{cards}.every(n => n.descendants().some(c => c.classList.contains('sheet-icon')))"));
        Assert.Equal("hull|flight|weapons|defence|signature|systems|quantum",
            page.Text($"{cards}.map(n => n.descendants().find(c => c.classList.contains('sheet-icon')).dataset.icon).join('|')"));
    }

    /// <summary>The figures a stealth pilot came for, with the scenario stated beside them.</summary>
    [Fact]
    public void Signatures_carry_their_scenario_and_their_breakdown()
    {
        var page = Opened();
        var text = page.NodeText("#garage-sheet");

        Assert.Contains("22,078", text);
        Assert.Contains("8,711", text);
        Assert.Contains("quantum drive idle", text);
        Assert.Contains("Power plant", text);
        Assert.Contains("13,480", text);
        Assert.Contains("EM ×1.13", text);
    }

    [Fact]
    public void Cross_section_is_shown_as_geometry_that_parts_do_not_move()
    {
        var page = Opened();
        var text = page.NodeText("#garage-sheet");

        Assert.Contains("6,923", text);
        Assert.Contains("parts do not change it", text);
    }

    [Fact]
    public void Quantum_and_speed_are_in_the_units_a_pilot_reads()
    {
        var page = Opened();
        var text = page.NodeText("#garage-sheet");

        Assert.Contains("161,411 km/s", text);
        Assert.Contains("32.2 Gm", text);
        Assert.Contains("Beacon", text);
    }

    /// <summary>A budget the ship cannot meet is said on the row and in the notes, not left as a number.</summary>
    [Fact]
    public void An_over_budget_ship_says_so_twice()
    {
        var page = Opened();

        Assert.Contains("Over budget", page.NodeText("#garage-sheet"));
        Assert.False(page.Truth("__dom.node('#garage-notes').hidden"));
        Assert.Contains("brown out", page.NodeText("#garage-notes"));
    }

    [Fact]
    public void A_reference_that_predates_the_garage_asks_for_a_refresh()
    {
        var page = new Page();
        page.Serve("/api/garage", """{"known":false,"dump":null,"mine":[],"all":[]}""");
        page.Do("await loadGarage();");

        Assert.False(page.Truth("__dom.node('#garage-unavailable').hidden"));
        Assert.Contains("Refresh the community dataset", page.NodeText("#garage-unavailable"));
        Assert.DoesNotContain("GET /api/garage/", string.Join("|", page.Fetched()));
    }

    /// <summary>
    /// With a stock sheet to compare against, a figure that moved shows the old
    /// one struck beside it, coloured by whether it got better for its kind:
    /// less IR is better, less DPS is not.
    /// </summary>
    [Fact]
    public void A_changed_figure_shows_what_it_was_and_which_way_it_went()
    {
        var page = Opened();
        page.Do("""
            const swapped = JSON.parse(JSON.stringify(garageStock));
            swapped.sheet.shields.ir = 8502;
            swapped.sheet.weapons.fixedDps = 1500;
            renderGarage(swapped, garageStock);
            """);

        var rows = "__dom.node('#garage-sheet').descendants().filter(n => n.classList.contains('sheet-row'))";
        var ir = $"{rows}.find(r => r.dataset.key === 'IR, shields up')";
        Assert.Contains("8,711", page.Text($"{ir}.textContent"));
        Assert.Contains("8,502", page.Text($"{ir}.textContent"));
        Assert.True(page.Truth($"{ir}.descendants().some(n => n.classList.contains('v') && n.classList.contains('up'))"));

        var dps = $"{rows}.find(r => r.dataset.key === 'Pilot DPS')";
        Assert.True(page.Truth($"{dps}.descendants().some(n => n.classList.contains('v') && n.classList.contains('down'))"));
    }

    // ---- the bench ----

    private const string CoolerOptions = """
        {"port":{"portId":"p1","hardpoint":"Hardpoint_cooler_left","kinds":["Cooler"],"minSize":1,"maxSize":1,"fitted":"COOL_AEGS_S01_Bracer_SCItem"},
         "pricesKnown":true,
         "options":[
           {"part":{"class":"COOL_AEGS_S01_Bracer_SCItem","type":"Cooler","size":1,"grade":2,"name":"Bracer","manufacturer":"Aegis Dynamics","makerCode":"AEGS","em":1490,"ir":7260,"coolantGen":25,"powerUseMax":3,"health":230},
            "price":null,"shops":[]},
           {"part":{"class":"COOL_JUST_S01_Glacier_SCItem","uuid":"c-glacier","type":"Cooler","size":1,"grade":1,"name":"Glacier","manufacturer":"Juggernaut","makerCode":"JUST","em":1490,"ir":7920,"coolantGen":38,"powerUseMax":3,"health":230},
            "price":12000,"shops":[{"terminal":"Dumper's Depot","placeId":"P1","place":"Area18","system":"Stanton","price":12000},{"terminal":"Cousin Crow's","placeId":"P2","place":"Orison","system":"Stanton","price":12400}]},
           {"part":{"class":"COOL_ACAS_S01_Endo_SCItem","type":"Cooler","size":1,"grade":4,"name":"Endo","manufacturer":"Ace Astrogation","makerCode":"ACAS","em":1200,"ir":5000,"coolantGen":18,"powerUseMax":2,"health":200},
            "price":null,"shops":[]}]}
        """;

    private static Page Bench()
    {
        var page = Opened();
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions);
        page.Serve("/api/garage/AEGS_Gladius/sheet", Gladius
            .Replace("\"ir\":8711", "\"ir\":8502")
            .Replace("\"name\":\"Bracer\",\"stockClass\":\"COOL_AEGS_S01_Bracer_SCItem\",\"changed\":false",
                "\"class\":\"COOL_JUST_S01_Glacier_SCItem\",\"name\":\"Glacier\",\"stockName\":\"Bracer\",\"stockClass\":\"COOL_AEGS_S01_Bracer_SCItem\",\"changed\":true"));
        return page;
    }

    /// <summary>
    /// The HUD draws a lead indicator per projectile speed. Two speeds among
    /// the pilot's guns is a row in amber that names which guns fire at which,
    /// and on the bench a gun that would add a pip - its speed matching none of
    /// the other pilot guns - wears a chip saying so; one that matches does not,
    /// and the gun in the port being changed is not counted against itself.
    /// </summary>
    [Fact]
    public void Two_projectile_speeds_are_two_pips_on_the_sheet_and_a_chip_on_the_bench()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        var gladius = Gladius
            .Replace("\"guns\":[\"CF-337 Panther Repeater\",\"CF-337 Panther Repeater\",\"Mantis GT-220 Gatling\"]",
                "\"guns\":[\"CF-337 Panther Repeater\",\"CF-337 Panther Repeater\",\"Mantis GT-220 Gatling\"],\"pips\":2,"
                + "\"speeds\":[{\"speed\":1480,\"guns\":[\"CF-337 Panther Repeater\",\"CF-337 Panther Repeater\"]},{\"speed\":1332,\"guns\":[\"Mantis GT-220 Gatling\"]}]")
            .Replace("\"ports\":[",
                "\"ports\":[{\"portId\":\"g3\",\"hardpoint\":\"hardpoint_gun_nose\",\"group\":\"WeaponGun\",\"minSize\":3,\"maxSize\":3,\"class\":\"GATS_BallisticGatling_S3\",\"name\":\"Mantis GT-220 Gatling\",\"stockClass\":\"GATS_BallisticGatling_S3\",\"changed\":false,"
                + "\"fitted\":{\"type\":\"WeaponGun\",\"name\":\"Mantis GT-220 Gatling\",\"size\":3,\"grade\":2,\"manufacturer\":\"Gallenson Tactical\",\"makerCode\":\"GATS\",\"em\":10,\"ir\":0,\"weapon\":{\"dps\":648,\"sustainedDps\":400,\"alpha\":30,\"range\":2400,\"ammoSpeed\":1332}}},");
        page.Serve("/api/garage/AEGS_Gladius", gladius);
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Serve("/api/garage/AEGS_Gladius/options?port=g3", """
            {"port":{"portId":"g3","hardpoint":"hardpoint_gun_nose","kinds":["WeaponGun"],"minSize":3,"maxSize":3,"fitted":"GATS_BallisticGatling_S3"},
             "pricesKnown":false,
             "options":[
               {"part":{"class":"GATS_BallisticGatling_S3","type":"WeaponGun","size":3,"grade":2,"name":"Mantis GT-220 Gatling","manufacturer":"Gallenson Tactical","makerCode":"GATS","em":10,"ir":0,"weapon":{"dps":648,"sustainedDps":400,"alpha":30,"range":2400,"ammoSpeed":1332}},"price":null,"shops":[]},
               {"part":{"class":"KLWE_LaserRepeater_S3","type":"WeaponGun","size":3,"grade":2,"name":"CF-337 Panther Repeater","manufacturer":"Klaus & Werner","makerCode":"KLWE","em":10,"ir":0,"weapon":{"dps":650,"sustainedDps":420,"alpha":40,"range":2100,"ammoSpeed":1480}},"price":null,"shops":[]},
               {"part":{"class":"BEHR_LaserCannon_S3","type":"WeaponGun","size":3,"grade":2,"name":"M5A Cannon","manufacturer":"Behring","makerCode":"BEHR","em":10,"ir":0,"weapon":{"dps":700,"sustainedDps":500,"alpha":120,"range":2600,"ammoSpeed":1184}},"price":null,"shops":[]}]}
            """);
        page.Do("await loadGarage();");

        var rowSel = "__dom.node('#garage-sheet').descendants().find(n => n.classList.contains('sheet-row') && n.dataset.key === 'Projectile speed')";
        Assert.Contains("2 speeds · 2 pips", page.Text($"{rowSel}.textContent"));
        Assert.Contains("CF-337 Panther Repeater ×2 at 1,480 m/s; Mantis GT-220 Gatling at 1,332 m/s", page.Text($"{rowSel}.textContent"));
        Assert.True(page.Truth($"{rowSel}.classList.contains('alert')"));

        page.Do("await selectBenchPort('g3');");
        var candidates = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate'))";
        var chipOn = (string name) => page.Truth($"{candidates}.find(n => n.textContent.includes('{name}')).descendants().some(n => n.classList.contains('chip') && n.classList.contains('pip'))");

        // The Panther joins the other two at 1,480: one pip. The M5A at 1,184 would be a second.
        Assert.False(chipOn("CF-337 Panther Repeater"));
        Assert.True(chipOn("M5A Cannon"));
        Assert.Contains("1,184 m/s", page.NodeText("#garage-bench-panel"));
    }

    [Fact]
    public void The_bench_lists_the_ports_by_kind_with_a_mark_and_the_figure_that_matters()
    {
        var page = Bench();
        var text = page.NodeText("#garage-bench-ports");

        Assert.Contains("Cooler", text);
        Assert.Contains("Bracer", text);
        Assert.Contains("S1 · B", text);
        Assert.Contains("25 coolant", text);
        Assert.Contains("Quantum drive", text);
        Assert.Contains("Beacon", text);

        // Aegis has a Fankit mark; the stub sees the image's alt text.
        Assert.True(page.Truth("__dom.node('#garage-bench-ports').descendants().some(n => n.tagName === 'img' && n.src === 'assets/manufacturers/AEGS.png')"));
        Assert.Equal("Stock fit", page.NodeText("#garage-changes"));
        Assert.True(page.Truth("__dom.node('#garage-reset').hidden"));
    }

    /// <summary>
    /// Picking a port lists what fits, best first for the kind, each figure
    /// beside how it compares with the part in the port now - more coolant in
    /// cyan, more IR in amber - and a price and shop when UEX knows one.
    /// </summary>
    [Fact]
    public void Picking_a_port_shows_candidates_compared_with_the_fitted_part()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1');");

        Assert.Contains("GET /api/garage/AEGS_Gladius/options?port=p1", page.Fetched());
        var panel = page.NodeText("#garage-bench-panel");

        Assert.Contains("Cooler · cooler left", panel);
        Assert.Contains("now fitted: Bracer", panel);
        Assert.Contains("Glacier", panel);
        Assert.Contains("Juggernaut", panel);
        Assert.Contains("12,000 aUEC", panel);
        Assert.Contains("Dumper's Depot", panel);
        Assert.Contains("+1", panel);

        // Glacier makes 38 coolant to the Bracer's 25: up, and coloured so.
        var glacier = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Glacier'))";
        Assert.True(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('d-up') && n.textContent.includes('+13'))"));
        Assert.True(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('d-down') && n.textContent.includes('+660'))"));

        // Best coolant first; the fitted one is marked, not offered.
        var names = page.Text("__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('c-name')).map(n => n.textContent).join('|')");
        Assert.StartsWith("Glacier", names);
        Assert.Contains("Stock", panel);

        // A maker with no Fankit mark, on a part with no picture to ask for: the
        // wiki's logo is asked for by code, and the monogram stands in when the
        // wiki has none.
        var endo = "__dom.node('#garage-bench-panel').descendants().find(n => n.classList.contains('maker-pic'))";
        Assert.Equal("/api/garage/maker/ACAS", page.Text($"{endo}.src"));
        page.Do($"{endo}.fire('error');");
        Assert.True(page.Truth("__dom.node('#garage-bench-panel').descendants().some(n => n.classList.contains('mono-mark') && n.textContent === 'ACAS')"));
    }

    [Fact]
    public void Fitting_a_part_posts_the_swap_and_moves_the_sheet()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1'); await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem');");

        Assert.Contains("POST /api/garage/AEGS_Gladius/sheet", page.Fetched());
        Assert.Contains("\"p1\":\"COOL_JUST_S01_Glacier_SCItem\"", page.BodyOf("/api/garage/AEGS_Gladius/sheet"));

        // The sheet shows what IR was, and the bench says one part changed.
        var ir = "__dom.node('#garage-sheet').descendants().filter(n => n.classList.contains('sheet-row')).find(r => r.dataset.key === 'IR, shields up')";
        Assert.Contains("8,711", page.Text($"{ir}.textContent"));
        Assert.Contains("8,502", page.Text($"{ir}.textContent"));
        Assert.Equal("1 part changed", page.NodeText("#garage-changes"));
        Assert.False(page.Truth("__dom.node('#garage-reset').hidden"));
        Assert.Contains("was Bracer", page.NodeText("#garage-bench-ports"));
    }

    [Fact]
    public void Reset_takes_the_bench_back_to_stock_without_asking_the_server()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1'); await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await resetGarage();");

        Assert.Equal("Stock fit", page.NodeText("#garage-changes"));
        Assert.Equal(1, page.Fetched().Count(f => f.StartsWith("POST /api/garage/AEGS_Gladius/sheet")));
        Assert.DoesNotContain("was Bracer", page.NodeText("#garage-bench-ports"));
    }

    /// <summary>Changing ships empties the bench: swaps belong to the hull they were made on.</summary>
    [Fact]
    public void Opening_another_ship_clears_the_swaps()
    {
        var page = Bench();
        page.Serve("/api/garage/DRAK_Cutlass_Black", Gladius.Replace("AEGS_Gladius", "DRAK_Cutlass_Black").Replace("Aegis Gladius", "Drake Cutlass Black"));
        page.Do("await selectBenchPort('p1'); await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await openGarage('DRAK_Cutlass_Black');");

        Assert.Equal("Drake Cutlass Black", page.NodeText("#garage-title"));
        Assert.Equal("Stock fit", page.NodeText("#garage-changes"));
        Assert.True(page.Truth("Object.keys(garageSwaps).length === 0"));
    }

    /// <summary>Sixteen missile ports carrying the same missile are one row and one decision.</summary>
    [Fact]
    public void Identical_ports_fold_into_one_row_and_are_fitted_together()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        var missiles = string.Join(",", Enumerable.Range(1, 4).Select(i =>
            "{\"portId\":\"m" + i + "\",\"hardpoint\":\"missile_0" + i + "_attach\",\"group\":\"Missile\",\"minSize\":3,\"maxSize\":3,\"class\":\"MISL_S03_A\",\"name\":\"Thunderbolt III\",\"stockClass\":\"MISL_S03_A\",\"changed\":false,\"fitted\":{\"type\":\"Missile\",\"name\":\"Thunderbolt III\",\"size\":3,\"grade\":1,\"makerCode\":\"FSKI\",\"missile\":{\"damage\":2900,\"speed\":858,\"lockTime\":1.2,\"range\":20000}}}"));
        page.Serve("/api/garage/AEGS_Gladius", Gladius.Replace("\"ports\":[", $"\"ports\":[{missiles},"));
        page.Serve("/api/garage/AEGS_Gladius/options?port=m1", """
            {"port":{"portId":"m1","hardpoint":"missile_01_attach","kinds":["Missile"],"minSize":3,"maxSize":3,"fitted":"MISL_S03_A"},"pricesKnown":false,
             "options":[{"part":{"class":"MISL_S03_B","type":"Missile","size":3,"grade":1,"name":"Arrester III","makerCode":"FSKI","missile":{"damage":3200,"speed":700,"lockTime":1.6,"range":22000}},"price":null,"shops":[]}]}
            """);
        page.Serve("/api/garage/AEGS_Gladius/sheet", Gladius);
        page.Do("await loadGarage();");

        var rows = "__dom.node('#garage-bench-ports').descendants().filter(n => n.classList.contains('bench-port'))";
        Assert.Equal("1", page.Text($"String({rows}.filter(r => r.textContent.includes('Thunderbolt')).length)"));
        Assert.Contains("×4", page.NodeText("#garage-bench-ports"));
        Assert.Contains("and 3 more", page.NodeText("#garage-bench-ports"));

        page.Do("await selectBenchPort('m1'); await fitPart('m1', 'MISL_S03_B');");

        Assert.Contains("4 ports alike, fitted together", page.NodeText("#garage-bench-panel"));
        var body = page.BodyOf("/api/garage/AEGS_Gladius/sheet");
        foreach (var id in new[] { "m1", "m2", "m3", "m4" })
            Assert.Contains($"\"{id}\":\"MISL_S03_B\"", body);
    }

    /// <summary>A maker with no Fankit mark gets its initials, not the first four letters of its name.</summary>
    [Fact]
    public void A_maker_without_a_mark_is_a_monogram_of_initials()
    {
        var page = new Page();
        Assert.Equal("WCP", page.Text("monogram(null, 'Wen-Cassel Propulsion')"));
        Assert.Equal("KLWE", page.Text("monogram('KLWE', 'Klaus & Werner')"));
        Assert.Equal("JS", page.Text("monogram(null, 'Juno Starwerk')"));
    }

    // ---- saved builds ----

    private const string Builds = """
        [{"id":"b1","shipClass":"AEGS_Gladius","name":"Quiet fit","swaps":{"p1":"COOL_JUST_S01_Glacier_SCItem"},
          "createdAt":"2026-09-14T10:00:00+00:00","updatedAt":"2026-09-14T10:00:00+00:00"}]
        """;

    private static Page WithBuilds()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        page.Serve("/api/garage/AEGS_Gladius", Gladius);
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", "[]");
        page.Serve("/api/garage/builds?ship=AEGS_Gladius", Builds);
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions);
        page.Serve("/api/garage/AEGS_Gladius/sheet", Gladius
            .Replace("\"ir\":8711", "\"ir\":8502")
            .Replace("\"name\":\"Bracer\",\"stockClass\":\"COOL_AEGS_S01_Bracer_SCItem\",\"changed\":false",
                "\"class\":\"COOL_JUST_S01_Glacier_SCItem\",\"name\":\"Glacier\",\"stockName\":\"Bracer\",\"stockClass\":\"COOL_AEGS_S01_Bracer_SCItem\",\"changed\":true"));
        page.Do("await loadGarage();");
        return page;
    }

    [Fact]
    public void The_ships_builds_are_listed_and_one_opens_onto_the_bench()
    {
        var page = WithBuilds();

        Assert.Contains("GET /api/garage/builds?ship=AEGS_Gladius", page.Fetched());
        Assert.False(page.Truth("__dom.node('#garage-builds').hidden"));
        Assert.Contains("Quiet fit", page.NodeText("#garage-builds"));
        Assert.Contains("1 part", page.NodeText("#garage-builds"));
        Assert.Contains("Quiet fit", page.NodeText("#garage-compare"));

        page.Do("await openBuild('b1');");

        Assert.Equal("COOL_JUST_S01_Glacier_SCItem", page.Text("garageSwaps.p1"));
        Assert.Equal("1 part changed", page.NodeText("#garage-changes"));
        Assert.False(page.Truth("__dom.node('#garage-update').hidden"));
        Assert.True(page.Truth("__dom.node('#garage-builds').descendants().some(n => n.classList.contains('build-chip') && n.classList.contains('open'))"));
    }

    [Fact]
    public void Saving_posts_the_bench_and_the_new_build_is_the_open_one()
    {
        var page = WithBuilds();
        page.Serve("/api/garage/builds", """{"id":"b2","shipClass":"AEGS_Gladius","name":"Loud fit","swaps":{"p1":"COOL_JUST_S01_Glacier_SCItem"},"createdAt":"2026-09-14T11:00:00+00:00","updatedAt":"2026-09-14T11:00:00+00:00"}""");
        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await saveBuild('Loud fit');");

        var body = page.BodyOf("/api/garage/builds");
        Assert.Contains("\"shipClass\":\"AEGS_Gladius\"", body);
        Assert.Contains("\"name\":\"Loud fit\"", body);
        Assert.Contains("\"p1\":\"COOL_JUST_S01_Glacier_SCItem\"", body);
        Assert.Contains("Loud fit", page.NodeText("#garage-builds"));
        Assert.Equal("b2", page.Text("garageOpenBuild"));
    }

    [Fact]
    public void Updating_writes_the_bench_into_the_open_build()
    {
        var page = WithBuilds();
        page.Serve("/api/garage/builds/b1", """{"id":"b1","shipClass":"AEGS_Gladius","name":"Quiet fit","swaps":{},"createdAt":"2026-09-14T10:00:00+00:00","updatedAt":"2026-09-14T12:00:00+00:00"}""");
        page.Do("await openBuild('b1'); await resetGarage(); await updateBuild();");

        Assert.Contains("PUT /api/garage/builds/b1", page.Fetched());
        Assert.Contains("\"swaps\":{}", page.BodyOf("/api/garage/builds/b1"));
        Assert.Contains("0 parts", page.NodeText("#garage-builds"));
    }

    [Fact]
    public void Deleting_the_open_build_returns_the_bench_to_stock()
    {
        var page = WithBuilds();
        page.Serve("/api/garage/builds/b1", """{"id":"b1"}""");
        page.Do("await openBuild('b1'); await deleteBuild('b1');");

        Assert.Contains("DELETE /api/garage/builds/b1", page.Fetched());
        Assert.True(page.Truth("__dom.node('#garage-builds').hidden"));
        Assert.True(page.Truth("garageOpenBuild === null"));
    }

    /// <summary>
    /// Comparing against a build measures the struck figures from that build's
    /// sheet rather than stock: the bench at stock then shows what the build
    /// changes, in reverse.
    /// </summary>
    [Fact]
    public void Compare_against_a_build_measures_from_its_sheet()
    {
        var page = WithBuilds();
        page.Do("__dom.node('#garage-compare').value = 'b1'; garageCompare = 'b1'; await refitGarage();");

        // The bench is stock (IR 8,711); the build's sheet says 8,502; the row shows the build's figure struck.
        var ir = "__dom.node('#garage-sheet').descendants().filter(n => n.classList.contains('sheet-row')).find(r => r.dataset.key === 'IR, shields up')";
        Assert.Contains("8,502", page.Text($"{ir}.textContent"));
        Assert.Contains("8,711", page.Text($"{ir}.textContent"));
        Assert.Equal(1, page.Fetched().Count(f => f.StartsWith("POST /api/garage/AEGS_Gladius/sheet")));
    }

    [Fact]
    public void An_untagged_part_is_offered_without_a_readiness_warning()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions.Replace("\"price\":12000", "\"flightReady\":false,\"price\":12000"));
        page.Do("await selectBenchPort('p1');");

        var glacier = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Glacier'))";
        Assert.False(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('unready'))"));
    }

    [Fact]
    public void A_component_class_and_recipe_are_distinguished_from_a_terminal_purchase()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions.Replace(
            "\"price\":null,\"shops\":[]",
            "\"componentClass\":\"Military\",\"craftable\":true,\"price\":null,\"shops\":[]"));
        page.Do("await selectBenchPort('p1');");

        var bracer = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Bracer'))";
        Assert.Contains("Military", page.Text($"{bracer}.textContent"));
        Assert.Contains("Blueprint recipe", page.Text($"{bracer}.textContent"));
        Assert.Contains("No terminal aUEC seller recorded", page.Text($"{bracer}.textContent"));
    }

    /// <summary>
    /// The game no longer names a stealth component class consistently, so the
    /// bench marks the option its own EM and IR figures make quietest instead.
    /// A tie is not highlighted: it would turn a missing distinction into one.
    /// </summary>
    [Fact]
    public void The_quietest_compatible_component_is_marked_as_a_stealth_pick()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1');");

        var candidates = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate'))";
        var endo = $"{candidates}.find(n => n.textContent.includes('Endo'))";
        var bracer = $"{candidates}.find(n => n.textContent.includes('Bracer'))";

        Assert.Contains("Stealth pick", page.Text($"{endo}.textContent"));
        Assert.True(page.Truth($"{endo}.descendants().some(n => n.classList.contains('stealth'))"));
        Assert.False(page.Truth($"{bracer}.descendants().some(n => n.classList.contains('stealth'))"));
    }

    /// <summary>The Fleet card's button opens the Garage on that ship rather than a panel of its own.</summary>
    [Fact]
    public void The_fleet_card_opens_the_garage_on_the_ship()
    {
        var page = new Page();
        page.Serve("/api/garage", Garage);
        page.Serve("/api/garage/DRAK_Cutlass_Black", Gladius.Replace("AEGS_Gladius", "DRAK_Cutlass_Black").Replace("Aegis Gladius", "Drake Cutlass Black"));
        page.Serve("/api/garage/builds?ship=DRAK_Cutlass_Black", "[]");
        page.Do("window.scrollTo = () => {}; openGarageFor('DRAK_Cutlass_Black'); await loadGarage();");

        Assert.Equal("DRAK_Cutlass_Black", page.Text("garageClass"));
        Assert.Contains("GET /api/garage/DRAK_Cutlass_Black", page.Fetched());
        Assert.True(page.Truth("__dom.node('#view-garage').classList.contains('active')"));
    }

    // ---- the shopping list ----

    private const string Shopped = """
        {"job":{"id":"j1","title":"Aegis Gladius fit","kind":"list","source":"garage:AEGS_Gladius","destination":"Area18","destinationId":"P1",
                "items":[{"name":"Glacier","needed":1,"unit":""}]},
         "proposal":{"terminal":"Dumper's Depot","place":"Area18","placeId":"P1","system":"Stanton","covered":1,"of":1,"total":12000,"missing":[],"pricesKnown":true}}
        """;

    /// <summary>The button follows the bench: a stock fit has nothing to buy.</summary>
    [Fact]
    public void Shopping_is_offered_only_once_the_bench_differs_from_stock()
    {
        var page = Bench();
        Assert.True(page.Truth("__dom.node('#garage-shop').hidden"));

        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem');");
        Assert.False(page.Truth("__dom.node('#garage-shop').hidden"));

        page.Do("await resetGarage();");
        Assert.True(page.Truth("__dom.node('#garage-shop').hidden"));
    }

    [Fact]
    public void Adding_to_the_list_posts_the_swaps_and_says_where_to_go()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped);
        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await shopForBench();");

        var body = page.BodyOf("/api/garage/AEGS_Gladius/shop");
        Assert.Contains("\"p1\":\"COOL_JUST_S01_Glacier_SCItem\"", body);
        Assert.Contains("\"title\":\"Aegis Gladius fit\"", body);

        var result = page.NodeText("#garage-shop-result");
        Assert.False(page.Truth("__dom.node('#garage-shop-result').hidden"));
        Assert.Contains("Aegis Gladius fit", result);
        Assert.Contains("1 part on 1 line", result);
        Assert.Contains("Dumper's Depot, Area18", result);
        Assert.Contains("1 of 1 line", result);
        Assert.Contains("12,000 aUEC", result);
        Assert.Contains("Open Shopping", result);
    }

    [Fact]
    public void Fitting_a_buyable_candidate_updates_the_ship_fit_list()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped);
        page.Do("""
            await selectBenchPort('p1');
            const glacier = __dom.node('#garage-bench-panel').descendants().find(n => n.classList.contains('candidate') && n.textContent.includes('Glacier'));
            await glacier.descendants().find(n => n.tagName === 'button' && n.textContent === 'Fit').fire('click');
            """);

        var body = page.BodyOf("/api/garage/AEGS_Gladius/shop");
        Assert.Contains("\"p1\":\"COOL_JUST_S01_Glacier_SCItem\"", body);
        Assert.Contains("\"title\":\"Aegis Gladius fit\"", body);
        Assert.Contains("Added \"Aegis Gladius fit\"", page.NodeText("#garage-shop-result"));
        Assert.DoesNotContain(page.Fetched(), url => url.StartsWith("POST /api/jobs"));
    }

    [Fact]
    public void Fitting_an_unlisted_candidate_does_not_create_an_unroutable_list()
    {
        var page = Bench();
        page.Do("""
            await selectBenchPort('p1');
            const endo = __dom.node('#garage-bench-panel').descendants().find(n => n.classList.contains('candidate') && n.textContent.includes('Endo'));
            await endo.descendants().find(n => n.tagName === 'button' && n.textContent === 'Fit').fire('click');
            """);

        Assert.DoesNotContain(page.Fetched(), url => url.StartsWith("POST /api/jobs"));
    }

    // ---- loadout optimisation ----

    [Fact]
    public void Optimiser_picks_the_quietest_part_and_explains_what_it_changed()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/options?port=p1", CoolerOptions.Replace("\"name\":\"Endo\"", "\"flightReady\":false,\"name\":\"Endo\""));
        page.Do("__dom.node('#garage-optimise-goal').value = 'stealth'; await optimiseGarage();");

        Assert.False(page.Truth("__dom.node('#garage-optimizer').hidden"));
        Assert.Contains("\"p1\":\"COOL_ACAS_S01_Endo_SCItem\"", page.BodyOf("/api/garage/AEGS_Gladius/sheet"));
        Assert.Contains("Optimised 1 port for stealth", page.NodeText("#garage-optimise-status"));
    }

    [Fact]
    public void Optimiser_can_limit_the_fit_to_parts_UEX_knows_are_for_sale()
    {
        var page = Bench();
        page.Do("__dom.node('#garage-optimise-goal').value = 'stealth'; __dom.node('#garage-optimise-buyable').checked = true; await optimiseGarage();");

        Assert.Contains("\"p1\":\"COOL_JUST_S01_Glacier_SCItem\"", page.BodyOf("/api/garage/AEGS_Gladius/sheet"));
        Assert.Contains("terminal aUEC sellers", page.NodeText("#garage-optimise-status"));
    }

    [Fact]
    public void Terminal_aUEC_only_filters_an_open_bench_but_keeps_the_fitted_part_for_comparison()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1'); __dom.node('#garage-optimise-buyable').checked = true; __dom.node('#garage-optimise-buyable').fire('change');");

        var bench = page.NodeText("#garage-bench-panel");
        Assert.Contains("Bracer", bench);
        Assert.Contains("Glacier", bench);
        Assert.DoesNotContain("Endo", bench);
        Assert.Contains("terminal aUEC only", bench);
    }

    [Theory]
    [InlineData("alpha", "{weapon:{alpha:91,sustainedDps:42}}", "91")]
    [InlineData("sustained", "{weapon:{alpha:91,sustainedDps:42}}", "42")]
    [InlineData("missile", "{missile:{damage:3200}}", "3200")]
    [InlineData("shield", "{shield:{hp:18000}}", "18000")]
    [InlineData("quantumSpeed", "{quantum:{speed:200000000}}", "200000000")]
    [InlineData("quantumRange", "{quantum:{fuelRate:2}}", "300")]
    [InlineData("cooling", "{type:'Cooler',coolantGen:38}", "38")]
    [InlineData("power", "{type:'PowerPlant',powerGen:16}", "16")]
    public void Optimiser_uses_the_measure_that_matches_its_goal(string goal, string part, string expected)
    {
        var page = new Page();
        Assert.Equal(expected, page.Text($"String(optimiseScore({part}, '{goal}', {{quantumFuel:600}}))"));
    }

    /// <summary>A list made from an open build carries the build's name, so the two can be told apart later.</summary>
    [Fact]
    public void A_list_from_an_open_build_is_named_after_it()
    {
        var page = WithBuilds();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped.Replace("Aegis Gladius fit", "Quiet fit"));
        page.Do("await openBuild('b1'); await shopForBench();");

        Assert.Contains("\"title\":\"Quiet fit\"", page.BodyOf("/api/garage/AEGS_Gladius/shop"));
    }

    /// <summary>What the stop lacks is said, not left for the Shopping page to discover.</summary>
    [Fact]
    public void A_stop_that_lacks_part_of_the_list_says_which()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped
            .Replace("\"items\":[{\"name\":\"Glacier\",\"needed\":1,\"unit\":\"\"}]", "\"items\":[{\"name\":\"Glacier\",\"needed\":1,\"unit\":\"\"},{\"name\":\"Endo\",\"needed\":2,\"unit\":\"\"}]")
            .Replace("\"of\":1", "\"of\":2")
            .Replace("\"missing\":[]", "\"missing\":[\"Endo\"]"));
        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await shopForBench();");

        var result = page.NodeText("#garage-shop-result");
        Assert.Contains("3 parts on 2 lines", result);
        Assert.Contains("1 of 2 lines", result);
        Assert.Contains("Not sold there: Endo", result);
    }

    [Fact]
    public void A_list_nobody_sells_is_written_without_a_destination()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped
            .Replace("\"terminal\":\"Dumper's Depot\"", "\"terminal\":null")
            .Replace("\"covered\":1", "\"covered\":0"));
        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await shopForBench();");

        Assert.Contains("No terminal UEX knows sells any of it", page.NodeText("#garage-shop-result"));
    }

    /// <summary>With UEX off there can be no destination, and the line says that is why.</summary>
    [Fact]
    public void Without_prices_the_missing_destination_is_explained()
    {
        var page = Bench();
        page.Serve("/api/garage/AEGS_Gladius/shop", Shopped
            .Replace("\"terminal\":\"Dumper's Depot\"", "\"terminal\":null")
            .Replace("\"pricesKnown\":true", "\"pricesKnown\":false"));
        page.Do("await fitPart('p1', 'COOL_JUST_S01_Glacier_SCItem'); await shopForBench();");

        Assert.Contains("need UEX prices", page.NodeText("#garage-shop-result"));
    }

    // ---- the part's picture ----

    /// <summary>
    /// A part the wiki may have a picture of shows the picture, and names the
    /// maker's mark as what to fall back to; a part with no uuid to ask about
    /// shows the mark straight away.
    /// </summary>
    [Fact]
    public void A_candidate_shows_its_picture_and_falls_back_to_the_makers_mark()
    {
        var page = Bench();
        page.Do("await selectBenchPort('p1');");

        var glacier = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Glacier'))";
        Assert.Equal("/api/garage/picture/c-glacier", page.Text($"{glacier}.descendants().find(n => n.classList.contains('part-pic')).src"));
        Assert.True(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('part-mark') && n.classList.contains('pic'))"));

        // The 404: the picture goes, the monogram comes, the frame shrinks back.
        page.Do($"const pic = {glacier}.descendants().find(n => n.classList.contains('part-pic')); pic.fire('error');");
        Assert.False(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('part-pic'))"));
        // Juno Starwerk has no Fankit mark, so the maker's face is the wiki's logo, and the monogram after that.
        Assert.Equal("/api/garage/maker/JUST", page.Text($"{glacier}.descendants().find(n => n.classList.contains('maker-pic')).src"));
        page.Do($"{glacier}.descendants().find(n => n.classList.contains('maker-pic')).fire('error');");
        Assert.Contains("JUST", page.Text($"{glacier}.descendants().find(n => n.classList.contains('part-mark')).textContent"));
        Assert.False(page.Truth($"{glacier}.descendants().some(n => n.classList.contains('part-mark') && n.classList.contains('pic'))"));

        // Bracer's card carries no uuid, so its mark is the maker's from the start.
        var bracer = "__dom.node('#garage-bench-panel').descendants().filter(n => n.classList.contains('candidate')).find(n => n.textContent.includes('Bracer'))";
        Assert.False(page.Truth($"{bracer}.descendants().some(n => n.classList.contains('part-pic'))"));
    }
}
