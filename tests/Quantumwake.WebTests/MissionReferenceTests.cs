namespace Quantumwake.WebTests;

/// <summary>
/// The mission window and the Reference Missions page: every contract the
/// installed game defines, what it pays, and every time the logs show it taken.
/// </summary>
/// <remarks>
/// The mission is 4.10.2's Red Wind bulk haul from Ruin Station to Orbituary in
/// Pyro, as this install flew it on 2026-10-31: +1,000 Hauling and +50 Affinity
/// on completion, CrimeStat 0 to 2, Experienced to Master, the game's estimate
/// 44 minutes, and a mental load rated 7 - which is why the window does not say
/// "out of 5". Its title in the files is a template the game fills in; the
/// title the log saw is what a list shows. The Covalex row carries the fixed
/// 61,500 a broker record pays.
/// </remarks>
public class MissionReferenceTests
{
    private const string Id = "11d9e1f1-6c18-4369-812b-c373c47c14b7";

    internal const string Mission = """
        {"id":"11d9e1f1-6c18-4369-812b-c373c47c14b7","name":"RedWind_Pyro_BulkGrade_RegionD_CFP_RuinToStation_RegionB_Waste_CargoHauling_AtoB",
         "generator":"RedWind_Hauling","parent":null,"template":"HaulCargo_AtoB_Bulk_Waste",
         "title":"~mission(ReputationRank) Hauler Needed for Direct ~mission(CargoGradeToken) Shipment",
         "loggedTitle":"Experienced | DIRECT Stellar Large Haul | Ruin Station > Orbituary",
         "description":"RED WIND LINEHAUL\\n'YOUR GOODS IN GOOD HANDS'\\n\\nNeed a pilot for a waste transfer from <EM4>~mission(Location|Address)</EM4> to ~mission(Destination|name).",
         "contractor":"Red Wind Linehaul","organisation":"Red Wind Linehaul","type":"Hauling - Stellar",
         "areas":["Terminus"],"locations":[],"crime":{"min":0,"max":2},
         "gates":[{"organisation":"Red Wind Linehaul","ladder":"Hauling","min":"Experienced","max":"Master"}],
         "requiresCompleted":null,"minutes":44,"buyIn":null,"maxPlayers":1,"onceOnly":false,
         "retakeAfterAbandon":false,"retakeAfterFail":false,"illegal":false,"unreleased":false,
         "difficulty":{"mechanical":"Hard_PvE_or_Easy_PvP_action_5","mentalLoad":"Insane_complexity_NOT_soloable_7",
           "riskOfLoss":"Player_might_die_Ship_could_explode_5","knowledge":"Flight_mechanics_fly_dock_quantum_3"},
         "pay":{"known":true,"rep":[
           {"organisation":"Red Wind Linehaul","ladder":"Hauling","amount":1000,"when":["Completed"]},
           {"organisation":"Red Wind Linehaul","ladder":"Affinity","amount":50,"when":["Completed"]}],
           "blueprint":null,"reward":null},
         "blueprints":[],
         "runs":[{"at":"2026-10-31T20:00:00Z","title":"Experienced | DIRECT Stellar Large Haul | Ruin Station > Orbituary",
           "outcome":"Completed","minutes":43,"session":"s1","paid":null}],
         "summary":{"taken":1,"completed":1,"failed":0,"abandoned":0,"medianMinutes":43,"paid":0,"payouts":0},
         "related":[{"id":"r1","name":"RedWind_Pyro_BulkGrade_RegionA_CFP_StationToStation_Waste_CargoHauling_AtoB",
           "title":"~mission(ReputationRank) Hauler Needed for Direct ~mission(CargoGradeToken) Shipment","areas":["Pyro I, Monox"]}]}
        """;

    private const string Catalogue = """
        {"ready":true,"count":3,"missions":[
          {"id":"c1","name":"HaulCargo_AToB_Stanton1_Intro","title":null,"loggedTitle":null,"contractor":"Covalex Independent Contractors",
           "type":"Delivery","areas":["Hurston"],"systems":["Stanton"],"rep":null,"blueprint":null,"reward":61500,"rank":null,
           "illegal":false,"unreleased":false,"taken":0,"completed":0},
          {"id":"11d9e1f1-6c18-4369-812b-c373c47c14b7","name":"RedWind_Pyro_BulkGrade_RegionD_CFP_RuinToStation_RegionB_Waste_CargoHauling_AtoB",
           "title":"~mission(ReputationRank) Hauler Needed for Direct ~mission(CargoGradeToken) Shipment",
           "loggedTitle":"Experienced | DIRECT Stellar Large Haul | Ruin Station > Orbituary",
           "contractor":"Red Wind Linehaul","type":"Hauling - Stellar","areas":["Terminus"],"systems":["Pyro"],
           "rep":1000,"blueprint":null,"reward":null,"rank":{"ladder":"Hauling","min":"Experienced","max":"Master"},
           "illegal":false,"unreleased":false,"taken":1,"completed":1},
          {"id":"c3","name":"InterSec_Bounty_Nyx_Intro","title":"New Defense Contract: ~mission(TargetName) Elimination","loggedTitle":null,
           "contractor":"InterSec Defense Solutions","type":"Mercenary","areas":["Nyx"],"systems":["Nyx"],
           "rep":null,"blueprint":0.25,"reward":null,"rank":null,"illegal":false,"unreleased":false,"taken":0,"completed":0}]}
        """;

    private static Page Window(string? lang = null)
    {
        var page = lang is null ? new Page() : new Page(lang);
        page.Serve($"/api/reference/mission?id={Id}", Mission);
        page.Do($"await openRefDetail('mission', '{Id}');");
        return page;
    }

    private static Page Listed()
    {
        var page = new Page();
        page.Serve("/api/reference/missions", Catalogue);
        page.Do("await loadMissionsRef();");
        return page;
    }

    private const string Rows = "__dom.node('#missions-table tbody').querySelectorAll('tr')";

    [Fact]
    public void The_text_names_the_slots_the_game_fills_and_drops_its_markup()
    {
        var page = new Page();
        Assert.Equal("‹Location› to ‹Destination›", page.Text("missionText('<EM4>~mission(Location|Address)</EM4> to ~mission(Destination|name)')"));
        Assert.Equal("a\nb", page.Text("missionText('a\\\\nb')"));
        Assert.Equal("‹Target Name› Elimination", page.Text("missionText('~mission(TargetName) Elimination')"));
    }

    [Fact]
    public void A_template_title_gives_way_to_the_one_the_logs_saw()
    {
        var page = Window();
        Assert.Equal("Experienced | DIRECT Stellar Large Haul | Ruin Station > Orbituary", page.NodeText("#ref-modal-title"));
        Assert.Equal("Mission", page.NodeText("#ref-modal-kicker"));
        Assert.Equal("Red Wind Linehaul · Hauling - Stellar · Terminus", page.NodeText("#ref-modal-sub"));
    }

    [Fact]
    public void The_window_says_who_where_and_on_what_terms()
    {
        var text = Window().NodeText("#ref-modal-body");

        Assert.Contains("taken once in your logs", text);
        Assert.Contains("0 to 2", text);
        Assert.Contains("Red Wind Linehaul · Hauling: Experienced to Master", text);
        Assert.Contains("44 min, the game’s own estimate", text);
        Assert.Contains("43 min over 1 completed", text);
        Assert.Contains("not again after abandoning it · not again after failing it", text);
    }

    [Fact]
    public void It_pays_what_the_files_say_and_says_where_the_money_figure_is_not()
    {
        var text = Window().NodeText("#ref-modal-body");

        Assert.Contains("+1,000 rep on Hauling (Red Wind Linehaul), when completed", text);
        Assert.Contains("+50 rep on Affinity (Red Wind Linehaul), when completed", text);
        Assert.Contains("worked out by the game server", text);
    }

    [Fact]
    public void The_briefing_reads_as_the_mobiglas_does()
    {
        var text = Window().NodeText("#ref-modal-body");

        Assert.Contains("RED WIND LINEHAUL\n'YOUR GOODS IN GOOD HANDS'", text);
        Assert.Contains("from ‹Location› to ‹Destination›", text);
        Assert.DoesNotContain("<EM4>", text);
        Assert.DoesNotContain("\\n", text);
    }

    [Fact]
    public void Difficulty_is_the_games_own_rating_and_not_out_of_five()
    {
        var text = Window().NodeText("#ref-modal-body");

        Assert.Contains("7 · Insane complexity NOT soloable", text);
        Assert.DoesNotContain("out of 5", text);
    }

    [Fact]
    public void Its_other_versions_are_told_apart_by_name_when_their_title_is_a_template()
    {
        var text = Window().NodeText("#ref-modal-body");

        Assert.Contains("RedWind Pyro BulkGrade RegionA CFP StationToStation Waste CargoHauling AtoB · Pyro I, Monox", text);
        Assert.DoesNotContain("‹Reputation Rank› Hauler", text);
    }

    [Fact]
    public void The_page_puts_what_you_flew_first_and_shows_a_fixed_sum()
    {
        var page = Listed();

        Assert.Equal(3, page.Count($"{Rows}.length"));
        Assert.Contains("Ruin Station > Orbituary", page.Text($"{Rows}[0].textContent"));
        Assert.Contains("1 / 1", page.Text($"{Rows}[0].textContent"));
        Assert.Contains("Hauling: Experienced to Master", page.Text($"{Rows}[0].textContent"));
        Assert.Contains("61,500 aUEC", page.NodeText("#missions-table tbody"));
        Assert.Equal("3 contracts", page.NodeText("#missions-count"));
    }

    [Fact]
    public void The_filters_narrow_by_contractor_system_and_what_you_flew()
    {
        var page = Listed();

        page.Do("__dom.node('#missions-flown').checked = true; renderMissionsRef();");
        Assert.Equal(1, page.Count($"{Rows}.length"));

        page.Do("__dom.node('#missions-flown').checked = false; __dom.node('#missions-system').value = 'Nyx'; renderMissionsRef();");
        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("New Defense Contract: ‹Target Name› Elimination", page.Text($"{Rows}[0].textContent"));

        page.Do("__dom.node('#missions-system').value = ''; __dom.node('#missions-search').value = 'covalex'; renderMissionsRef();");
        Assert.Equal(1, page.Count($"{Rows}.length"));
    }

    [Fact]
    public void A_logged_contract_opens_its_mission_from_the_contracts_page()
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", $$"""
            [{ "at": "2026-10-31T20:00:00Z", "name": "Experienced | DIRECT Stellar Large Haul | Ruin Station > Orbituary",
               "issuer": "Red Wind Linehaul", "type": "Hauling", "difficulty": null, "system": "Pyro", "outcome": "Completed",
               "definitionId": "{{Id}}", "contract": "RedWind_Pyro_BulkGrade" }]
            """);
        page.Serve($"/api/reference/mission?id={Id}", Mission);
        page.Do("await loadContractList();");
        page.Do("await __dom.node('#contracts-table tbody').byClass('ref-link')[0].fire('click');");

        Assert.Contains($"GET /api/reference/mission?id={Id}", page.Fetched());
        Assert.Contains("What it pays", page.NodeText("#ref-modal-body"));
    }

    [Fact]
    public void A_contract_without_an_id_opens_by_its_debug_name_and_one_without_either_stays_words()
    {
        var page = new Page();
        Assert.True(page.Truth("missionLink('Haul', null, 'Covalex_Haul').className.includes('ref-link')"));
        Assert.True(page.Truth("!(missionLink('Haul', null, null).className || '').includes('ref-link')"));
        Assert.Equal("Haul", page.Text("missionLink('Haul', null, null).textContent"));
    }
}
