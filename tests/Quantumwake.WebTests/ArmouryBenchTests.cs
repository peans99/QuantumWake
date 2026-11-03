namespace Quantumwake.WebTests;

public class ArmouryBenchTests
{
    private const string Gun = "behr_rifle_ballistic_01";
    private const string BareUrl = "/api/armoury/fit?gun=" + Gun + "&with=";
    private const string Ports = """
        "ports":[
          {"slot":"Sight","minSize":1,"maxSize":1,"fixed":true,"default":"scope","fits":["scope"]},
          {"slot":"Underbarrel","minSize":1,"maxSize":1,"fixed":false,"fits":["laser"]}],
        """;
    private const string Pieces = """
        "attachments":[
          {"class":"scope","name":"Fixed scope","kind":"Sight","size":1},
          {"class":"laser","name":"Laser pointer","kind":"Underbarrel","size":1,"effect":{"spread":0.885}},
          {"class":"wrong","name":"Incompatible barrel","kind":"Barrel","size":3}],
        """;
    private const string ScopeUrl = BareUrl + "scope";
    private const string ScopeLaserUrl = BareUrl + "scope,laser";
    private static string Model(bool ports = false) => ArmouryPageTests.Model
        .Replace("\"mass\":2.65,", "\"mass\":2.65," + (ports ? Ports : ""))
        .Replace("\"armour\":[", Pieces + "\"armour\":[");

    private static string Fit(double spread = 1, string gun = Gun) => $$"""
        {"gun":"{{gun}}","pieces":[],
         "effect":{"spread":{{spread.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"recoilStrength":0.7,"recoilTime":0.8,"sound":1.2,"zoomTime":1.25},
         "modes":[{"name":"AUTO","kind":"Auto","damagePerShot":11.04,"damagePerSecond":149.04,"sustainedRoundsPerMinute":810,"damagePerMagazine":441.6,"secondsToEmpty":2.96},
                  {"name":"SEMI","kind":"Semi","damagePerShot":10,"damagePerSecond":135,"sustainedRoundsPerMinute":810,"damagePerMagazine":400,"secondsToEmpty":2.96}]}
        """;

    private static Page Open(bool ports = false)
    {
        var page = new Page();
        page.Serve("/api/armoury", Model(ports));
        page.Serve(ports ? ScopeUrl : BareUrl, Fit());
        page.Serve(ScopeLaserUrl, Fit(0.885));
        page.Do("await loadArmoury(); showArmouryPane('bench');");
        return page;
    }

    private const string Root = "__dom.node('#armoury-bench')";
    private const string Result = Root + ".byClass('bench-readout')[0]";
    private static string Picker(string slot) => $"{Root}.descendants().find(n => n.tagName === 'select' && n.dataset.slot === '{slot}')";

    [Fact]
    public void Range_markers_distinguish_falloff_projectile_travel_and_beam_cutoff()
    {
        var page = Open();
        page.Do("""
            var projectileRange = armouryBenchRange(armouryModel.weapons[0], {projectileSpeed:500}, {kind:'Auto'});
            var beamRange = armouryBenchRange(armouryModel.weapons[1], {projectileSpeed:500}, armouryModel.weapons[1].modes[0]);
            """);
        Assert.Contains("Falloff starts at40 m", page.Text("projectileRange.textContent"));
        Assert.Contains("Damage floor at80 m", page.Text("projectileRange.textContent"));
        Assert.Contains("Projectile travel limit", page.Text("projectileRange.textContent"));
        Assert.Contains("Full damage through10 m", page.Text("beamRange.textContent"));
        Assert.Contains("Beam ends at25 m", page.Text("beamRange.textContent"));
        Assert.DoesNotContain("Projectile travel limit", page.Text("beamRange.textContent"));
    }

    [Fact]
    public void Scope_uses_both_zoom_levels_and_the_available_zeroing()
    {
        var page = Open();
        page.Do("var optic = armouryBenchScope({zoom:4,secondZoom:8,zoomTime:1.25,zeroingMax:500,zeroingStep:100,spread:0.8},true);");
        Assert.Contains("×4", page.Text("optic.scope.textContent"));
        Assert.Contains("×8", page.Text("optic.scope.textContent"));
        Assert.Contains("500 m by 100", page.Text("optic.scope.textContent"));
        page.Do("var opticZoom = optic.scope.descendants().find(n => n.tagName === 'select'); opticZoom.value = '8'; opticZoom.fire('change');");
        Assert.Equal("scale(8)", page.Text("optic.scope.byClass('bench-scope-lens')[0].children[0].style.transform"));
    }

    [Fact]
    public void Opposing_modifiers_show_both_pieces_even_if_the_combined_result_is_neutral()
    {
        var page = Open();
        page.Do("""
            armouryModel.attachments = [{class:'a',effect:{spread:0.8}},{class:'b',effect:{spread:1.25}}];
            var breakdown = armouryBenchContributions({pieces:[{class:'a',name:'Laser'},{class:'b',name:'Barrel'}],effect:{spread:1}});
            """);
        var text = page.Text("breakdown.textContent");
        Assert.Contains("Laser ×0.8", text);
        Assert.Contains("Barrel ×1.25", text);
        Assert.Contains("Combined ×1", text);
    }

    [Fact]
    public void Bench_is_opt_in_and_explains_the_limits_of_its_target()
    {
        var page = new Page();
        page.Serve("/api/armoury", Model());
        page.Do("await loadArmoury();");
        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/api/armoury/fit"));
        page.Serve(BareUrl, Fit());
        page.Do("showArmouryPane('bench');");
        Assert.Contains("not recorded shots", page.Text($"{Root}.textContent"));
        Assert.Contains("distances and hit probabilities are not simulated", page.Text($"{Root}.textContent"));
        Assert.Contains("Spread is unchanged", page.Text($"{Result}.textContent"));
        Assert.All(page.Fetched(), u => Assert.StartsWith("GET ", u));
    }

    [Fact]
    public void The_suggested_attachment_applies_a_real_fit_and_either_pattern_can_be_isolated()
    {
        var page = Open(true);
        page.Do($"{Root}.byClass('bench-example')[0].click();");
        Assert.Contains($"GET {ScopeLaserUrl}", page.Fetched());
        Assert.Contains("11.5% narrower than bare", page.Text($"{Result}.textContent"));
        page.Do($"{Result}.byClass('bench-legend-bare')[0].click();");
        Assert.Equal("bare", page.Text($"{Result}.byClass('bench-plot')[0].dataset.layer"));
        page.Do($"{Result}.byClass('bench-legend-fitted')[0].click();");
        Assert.Equal("fitted", page.Text($"{Result}.byClass('bench-plot')[0].dataset.layer"));
    }

    [Fact]
    public void Both_layers_use_the_same_sample_scaled_by_the_returned_multiplier()
    {
        var page = Open(true);
        page.Do($"{Picker("Underbarrel")}.value = 'laser'; {Picker("Underbarrel")}.fire('change');");
        Assert.Contains("88.5 · bare = 100", page.Text($"{Result}.textContent"));
        Assert.Equal(48, page.Count($"{Result}.byClass('bench-pattern-fitted')[0].byClass('bench-pattern-point').length"));
        page.Do($$"""
            var benchBare = {{Result}}.byClass('bench-pattern-bare')[0].byClass('bench-pattern-point');
            var benchFitted = {{Result}}.byClass('bench-pattern-fitted')[0].byClass('bench-pattern-point');
            """);
        // Compare each coordinate, not just the envelope: a new random sample
        // for one fit could appear tighter by chance without respecting the effect.
        Assert.True(page.Truth("benchBare.every((p,i) => Math.abs((+benchFitted[i].getAttribute('cx')-200)-(+p.getAttribute('cx')-200)*.885)<1e-9 && Math.abs((+benchFitted[i].getAttribute('cy')-180)-(+p.getAttribute('cy')-180)*.885)<1e-9)"));
        var before = page.Text("JSON.stringify(armouryBenchPattern(armouryBenchSeed))");
        page.Do($"{Root}.descendants().find(n => n.tagName === 'button' && n.textContent === 'New sample').click();");
        Assert.NotEqual(before, page.Text("JSON.stringify(armouryBenchPattern(armouryBenchSeed))"));
        Assert.Equal(2, page.Fetched().Count(u => u.Contains("/api/armoury/fit")));
    }

    [Theory]
    [InlineData(0.77)]
    [InlineData(1.2)]
    [InlineData(3)]
    public void A_wider_fit_stays_inside_the_target_without_changing_the_ratio(double spread)
    {
        var page = Open();
        page.Do($"var benchSvg = armouryBenchTarget({spread.ToString(System.Globalization.CultureInfo.InvariantCulture)});");
        Assert.InRange(page.Number("+benchSvg.byClass('bench-pattern-fitted')[0].byClass('bench-pattern-limit')[0].getAttribute('r')"), 0, 144);
        var ratio = page.Number("+benchSvg.byClass('bench-pattern-fitted')[0].byClass('bench-pattern-limit')[0].getAttribute('r') / +benchSvg.byClass('bench-pattern-bare')[0].byClass('bench-pattern-limit')[0].getAttribute('r')");
        Assert.Equal(spread, ratio, 10);
    }

    [Fact]
    public void Handling_indices_keep_fractional_changes_above_the_bare_index()
    {
        var page = Open();
        Assert.Equal("107.5", page.Text("armouryBenchIndex(1.075)"));
    }

    [Fact]
    public void Pickers_preserve_fixed_slots_and_never_offer_an_incompatible_piece()
    {
        var page = Open(true);
        Assert.True(page.Truth($"{Picker("Sight")}.disabled"));
        Assert.Equal("scope", page.Text($"{Picker("Sight")}.value"));
        Assert.DoesNotContain("Incompatible barrel", page.Text($"{Root}.textContent"));
        page.Do($"{Picker("Underbarrel")}.value = 'laser'; {Picker("Underbarrel")}.fire('change');");
        Assert.Equal("laser", page.Text("armouryFitChoice(armouryModel.weapons[0]).Underbarrel"));
        page.Do($"{Root}.descendants().find(n => n.tagName === 'button' && n.textContent === 'Factory fit').click();");
        Assert.Equal("", page.Text($"{Picker("Underbarrel")}.value"));
        Assert.Equal("scope", page.Text($"{Picker("Sight")}.value"));
    }

    [Fact]
    public void Mode_switches_use_the_same_modes_bare_figures_without_a_new_request()
    {
        var page = Open();
        page.Do($"var benchMode = {Root}.descendants().find(n => n.id === 'armoury-bench-mode'); benchMode.value = '1'; benchMode.fire('change');");
        Assert.Contains("135", page.Text($"{Result}.textContent"));
        Assert.Contains("162", page.Text($"{Result}.textContent"));
        Assert.DoesNotContain("149", page.Text($"{Result}.textContent"));
        Assert.Single(page.Fetched(), u => u.Contains("/api/armoury/fit"));
    }

    [Fact]
    public void Beam_and_missing_effects_do_not_invent_scatter_or_handling_values()
    {
        var page = Open();
        page.Do($$$"""
            renderArmouryBenchReadout(armouryModel.weapons[1], {modes:[{kind:'Beam',damagePerSecond:275}],effect:{}}, {{{Result}}});
            """);
        Assert.Contains("A beam has no shot pattern", page.Text($"{Result}.textContent"));
        Assert.Equal(0, page.Count($"{Result}.byClass('bench-plot').length"));
        Assert.Contains("Not read", page.Text($"{Result}.textContent"));
        Assert.DoesNotContain("Per shot", page.Text($"{Result}.textContent"));
    }

    [Fact]
    public void A_late_response_cannot_replace_a_new_fit_or_an_error()
    {
        var page = Open(true);
        page.Do("var benchPending = []; fetch = () => new Promise(resolve => benchPending.push(resolve)); renderArmouryBench();");
        Assert.DoesNotContain("149", page.Text($"{Result}.textContent"));
        page.Do($"{Picker("Underbarrel")}.value = 'laser'; {Picker("Underbarrel")}.fire('change');");
        page.Do($"benchPending[1]({{ok:true,json:async()=>({Fit(0.885)})}});");
        Assert.Contains("88.5 · bare", page.Text($"{Result}.textContent"));
        page.Do($"benchPending[0]({{ok:true,json:async()=>({Fit(1.2)})}});");
        Assert.Contains("88.5 · bare", page.Text($"{Result}.textContent"));
        page.Do($"{Picker("Underbarrel")}.value = ''; {Picker("Underbarrel")}.fire('change'); benchPending[2]({{ok:false,status:400,json:async()=>({{problem:'Fit unavailable'}})}});");
        Assert.Contains("Fit unavailable", page.Text($"{Result}.textContent"));
        Assert.Equal(0, page.Count($"{Result}.byClass('bench-plot').length"));
    }

    [Fact]
    public void A_guns_shortcut_carries_the_same_fit_into_the_bench()
    {
        var page = Open(true);
        page.Do("showArmouryPane('guns'); armouryOpenGun = 'behr_rifle_ballistic_01'; renderArmouryGuns();");
        page.Do("__dom.node('#armoury-guns tbody').descendants().find(n => n.tagName === 'button' && n.textContent === 'Test this fit').click();");
        Assert.Equal("bench", page.Text("armouryPane"));
        Assert.Equal(Gun, page.Text("armouryBenchGun"));
        Assert.Equal("scope", page.Text($"{Picker("Sight")}.value"));
    }
}
