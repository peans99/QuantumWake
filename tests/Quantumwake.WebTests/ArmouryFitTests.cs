namespace Quantumwake.WebTests;

/// <summary>
/// Fitting attachments to a gun on the Armoury: a picker per slot the gun
/// has, listing what the server says fits, and the gun's figures with the
/// picks on it beside the bare ones.
/// </summary>
public class ArmouryFitTests
{
    /// <summary>The P4-AR's three slots as /api/armoury sends them, and the pieces they name.</summary>
    private const string Ports = """
        "ports":[
          {"slot":"Sight","name":"optics_attach","minSize":1,"maxSize":2,"fixed":false,"default":null,"fits":["behr_optics_tsco_x4_s2"]},
          {"slot":"Barrel","name":"barrel_attach","minSize":2,"maxSize":2,"fixed":false,"default":null,"fits":["arma_barrel_supp_s2","arma_barrel_comp_s2"]},
          {"slot":"Underbarrel","name":"underbarrel_attach","minSize":1,"maxSize":2,"fixed":false,"default":null,"fits":["nvtc_ubarrel_flsh_s1"]}],
        """;

    private const string Pieces = """
        "attachments":[
          {"class":"arma_barrel_supp_s2","name":"Tacit Suppressor2","kind":"Barrel","family":"Suppressor","size":2,"manufacturer":"ArmaMod","mass":0.1,
           "effect":{"damage":0.92,"fireRate":1,"spread":1,"recoilStrength":1,"recoilTime":1,"sound":0.4,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":0,"secondZoom":0,"zoomTime":1,"zeroingMax":0,"zeroingStep":0},
           "unchanged":false,"market":{"price":null,"shops":[]},"finishes":[]},
          {"class":"arma_barrel_comp_s2","name":"Sion Compensator2","kind":"Barrel","family":"Compensator","size":2,"manufacturer":"ArmaMod","mass":0.1,
           "effect":{"damage":1,"fireRate":1,"spread":1,"recoilStrength":0.7,"recoilTime":1,"sound":1.2,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":0,"secondZoom":0,"zoomTime":1,"zeroingMax":0,"zeroingStep":0},
           "unchanged":false,"market":{"price":null,"shops":[]},"finishes":[]},
          {"class":"behr_optics_tsco_x4_s2","name":"EE04 (4x Telescopic)","kind":"Sight","family":"Telescopic","size":2,"manufacturer":"Behring","mass":0.1,
           "effect":{"damage":1,"fireRate":1,"spread":1,"recoilStrength":1,"recoilTime":1,"sound":1,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":4,"secondZoom":6,"zoomTime":1.25,"zeroingMax":500,"zeroingStep":100},
           "unchanged":false,"market":{"price":null,"shops":[]},"finishes":[]},
          {"class":"nvtc_ubarrel_flsh_s1","name":"FieldLite Flashlight","kind":"Underbarrel","family":"Flashlight","size":1,"manufacturer":"NV-TAC","mass":0.1,
           "effect":{"damage":1,"fireRate":1,"spread":1,"recoilStrength":1,"recoilTime":1,"sound":1,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":0,"secondZoom":0,"zoomTime":1,"zeroingMax":0,"zeroingStep":0},
           "unchanged":true,"market":{"price":null,"shops":[]},"finishes":[]}],
        """;

    /// <summary>What the server works out for the P4-AR with the Tacit: 12 × 0.92 at 810 a minute.</summary>
    private const string WithTacit = """
        {"gun":"behr_rifle_ballistic_01","pieces":[{"class":"arma_barrel_supp_s2","name":"Tacit Suppressor2","kind":"Barrel","slot":"Barrel"}],
         "effect":{"damage":0.92,"fireRate":1,"spread":1,"recoilStrength":1,"recoilTime":1,"sound":0.4,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":0,"secondZoom":0,"zoomTime":1,"zeroingMax":0,"zeroingStep":0},
         "damage":{"physical":11.04,"total":11.04},"projectileSpeed":550,
         "modes":[{"name":"AUTO","kind":"Auto","roundsPerMinute":810,"pellets":1,"ammoPerShot":1,"burstShots":0,"heatPerShot":0,"condition":"","hit":{"physical":11.04,"total":11.04},"sustainedRoundsPerMinute":810,"damagePerShot":11.04,"damagePerSecond":149.04,"damagePerMagazine":441.6,"secondsToEmpty":2.96},
                  {"name":"SEMI","kind":"Semi","roundsPerMinute":810,"pellets":1,"ammoPerShot":1,"burstShots":0,"heatPerShot":0,"condition":"","hit":{"physical":11.04,"total":11.04},"sustainedRoundsPerMinute":810,"damagePerShot":11.04,"damagePerSecond":149.04,"damagePerMagazine":441.6,"secondsToEmpty":2.96}]}
        """;

    private const string FitUrl = "/api/armoury/fit?gun=behr_rifle_ballistic_01&with=arma_barrel_supp_s2";

    private static string Model(string ports = Ports) =>
        ArmouryPageTests.Model
            .Replace("\"mass\":2.65,", "\"mass\":2.65," + ports)
            .Replace("\"armour\":[", Pieces + "\"armour\":[");

    private static Page OpenP4(string ports = Ports)
    {
        var page = new Page();
        page.Serve("/api/armoury", Model(ports));
        page.Do("await loadArmoury(); armouryOpenGun = 'behr_rifle_ballistic_01'; renderArmouryGuns();");
        return page;
    }

    private const string Panel = "__dom.node('#armoury-guns tbody').byClass('armoury-fit')[0]";

    private static string Picker(string slot) =>
        $"{Panel}.descendants().find(n => n.tagName === 'select' && n.dataset.slot === '{slot}')";

    [Fact]
    public void An_opened_gun_has_a_picker_per_slot_listing_what_fits_it()
    {
        var page = OpenP4();

        var panel = page.Text($"{Panel}.textContent");
        Assert.Contains("Fit attachments", panel);
        Assert.Contains("Sight", panel);
        Assert.Contains("Underbarrel", panel);
        Assert.Contains("size 2", panel);
        Assert.Contains("size 1–2", panel);
        Assert.Contains("Pick a piece for a slot to see the gun with it.", panel);

        // Nothing, then the two barrels that fit, by name.
        var options = $"{Picker("Barrel")}.children";
        Assert.Equal(3, page.Count($"{options}.length"));
        Assert.Equal("Nothing", page.Text($"{options}[0].textContent"));
        Assert.Equal("Sion Compensator2", page.Text($"{options}[1].textContent"));
        Assert.Equal("Tacit Suppressor2", page.Text($"{options}[2].textContent"));
        // A gun shipped bare asks the server nothing until a piece is picked.
        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/api/armoury/fit"));
    }

    [Fact]
    public void Picking_a_suppressor_lays_the_fitted_figures_beside_the_bare_ones()
    {
        var page = OpenP4();
        page.Serve(FitUrl, WithTacit);

        page.Do($"const s = {Picker("Barrel")}; s.value = 'arma_barrel_supp_s2'; s.fire('change');");

        Assert.Contains($"GET {FitUrl}", page.Fetched());
        var panel = page.Text($"{Panel}.textContent");
        Assert.Contains("162 → 149", panel);
        Assert.Contains("480 → 442", panel);
        Assert.Contains("12 → 11.04", panel);
        // The rate did not move, so it is said once.
        Assert.DoesNotContain("810 rpm → ", panel);
        // What the figures do not carry is the handling line.
        Assert.Contains("Handling", panel);
        Assert.Contains("×0.4 sound", panel);
        Assert.DoesNotContain("×0.92 damage", panel);
        Assert.Contains("is not in the files", panel);
        // Less damage is a cost, marked as one.
        Assert.True(page.Truth($"{Panel}.byClass('armoury-fit-delta').every(n => n.classList.contains('cost'))"));
    }

    [Fact]
    public void The_pick_survives_the_row_being_drawn_again()
    {
        var page = OpenP4();
        page.Serve(FitUrl, WithTacit);
        page.Do($"const s = {Picker("Barrel")}; s.value = 'arma_barrel_supp_s2'; s.fire('change');");

        page.Do("renderArmouryGuns();");

        Assert.Equal("arma_barrel_supp_s2", page.Text($"{Picker("Barrel")}.value"));
        Assert.Contains("162 → 149", page.Text($"{Panel}.textContent"));
    }

    [Fact]
    public void A_gun_that_ships_with_a_scope_opens_with_it_fitted()
    {
        var page = new Page();
        page.Serve("/api/armoury/fit?gun=behr_rifle_ballistic_01&with=behr_optics_tsco_x4_s2", """
            {"gun":"behr_rifle_ballistic_01","pieces":[],"effect":{"damage":1,"fireRate":1,"spread":1,"recoilStrength":1,"recoilTime":1,"sound":1,"heat":1,"ammoCost":1,"chargeTime":1,"projectileSpeed":1,"pellets":0,"burstShots":0,"zoom":4,"secondZoom":6,"zoomTime":1.25,"zeroingMax":500,"zeroingStep":100},
             "modes":[{"name":"AUTO","kind":"Auto","roundsPerMinute":810,"sustainedRoundsPerMinute":810,"damagePerShot":12,"damagePerSecond":162,"damagePerMagazine":480,"secondsToEmpty":2.96}]}
            """);
        page.Serve("/api/armoury", Model(Ports.Replace("\"default\":null,\"fits\":[\"behr_optics", "\"default\":\"behr_optics_tsco_x4_s2\",\"fits\":[\"behr_optics")));
        page.Do("await loadArmoury(); armouryOpenGun = 'behr_rifle_ballistic_01'; renderArmouryGuns();");

        Assert.Equal("behr_optics_tsco_x4_s2", page.Text($"{Picker("Sight")}.value"));
        Assert.Contains("EE04 (4x Telescopic) · ships with it", page.Text($"{Picker("Sight")}.textContent"));
        var panel = page.Text($"{Panel}.textContent");
        Assert.Contains("×4 / ×6 zoom", panel);
        Assert.Contains("zero to 500 m by 100", panel);
        // A sight moves no damage figure: the bare number stands alone.
        Assert.DoesNotContain("→", panel);
    }

    [Fact]
    public void A_refusal_says_why_rather_than_showing_the_bare_figures_as_fitted()
    {
        var page = OpenP4();
        page.Fail(FitUrl, 400, """{"problem":"Tacit Suppressor2 does not fit the P4-AR Rifle."}""");

        page.Do($"const s = {Picker("Barrel")}; s.value = 'arma_barrel_supp_s2'; s.fire('change');");

        var panel = page.Text($"{Panel}.textContent");
        Assert.Contains("The figures could not be worked out: Tacit Suppressor2 does not fit the P4-AR Rifle.", panel);
        Assert.DoesNotContain("162", panel);
    }

    /// <summary>
    /// A saved kit's gun opens on the Armoury with the kit's own pieces
    /// fitted, and a filter left over from last time does not hide it.
    /// </summary>
    [Fact]
    public void A_kit_opens_its_gun_on_the_armoury_with_its_attachments_on()
    {
        var page = new Page();
        page.Serve("/api/kits", """[{"id":"k1","name":"Bounty kit","items":[{"name":"P4-AR Rifle","quantity":1},{"name":"Tacit Suppressor2","quantity":1}]}]""");
        page.Serve("/api/kits/guns", """
            [{"kitId":"k1","guns":[{"class":"behr_rifle_ballistic_01","name":"P4-AR Rifle",
              "with":[{"slot":"Barrel","class":"arma_barrel_supp_s2","name":"Tacit Suppressor2"}]}]}]
            """);
        page.Serve("/api/armoury", Model());
        page.Serve(FitUrl, WithTacit);
        page.Do("await loadKits(); __dom.node('#armoury-gun-kind').value = 'Pistol';");

        const string button = "__dom.node('#kit-list').descendants().find(n => n.tagName === 'button' && n.textContent.includes('in the Armoury'))";
        Assert.Equal("P4-AR Rifle in the Armoury", page.Text($"{button}.textContent"));
        Assert.Contains("Tacit Suppressor2 fitted", page.Text($"{button}.title"));

        page.Do($"{button}.click();");

        Assert.Equal("", page.Text("__dom.node('#armoury-gun-kind').value"));
        Assert.Equal("arma_barrel_supp_s2", page.Text($"{Picker("Barrel")}.value"));
        Assert.Equal("", page.Text($"{Picker("Sight")}.value"));
        Assert.Contains("162 → 149", page.Text($"{Panel}.textContent"));
    }

    [Fact]
    public void A_kit_without_a_gun_gets_no_armoury_button()
    {
        var page = new Page();
        page.Serve("/api/kits", """[{"id":"k1","name":"Med kit","items":[{"name":"MedPen","quantity":4}]}]""");
        page.Serve("/api/kits/guns", "[]");
        page.Do("await loadKits();");

        Assert.DoesNotContain("in the Armoury", page.NodeText("#kit-list"));
    }

    [Fact]
    public void A_gun_with_no_slots_says_so()
    {
        var page = OpenP4();

        page.Do("armouryOpenGun = 'volt_smg_energy_01'; renderArmouryGuns();");

        Assert.Contains("The files give this gun no attachment slots.", page.Text($"{Panel}.textContent"));
    }
}
