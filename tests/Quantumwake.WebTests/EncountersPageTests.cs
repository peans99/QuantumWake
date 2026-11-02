namespace Quantumwake.WebTests;

/// <summary>
/// The Reference Encounters page: every group of NPC ships the install
/// spawns in waves, what each ship's hold carries, and which mission script -
/// if any - names the group.
/// </summary>
/// <remarks>
/// The rows are 4.10.2's, cut down. WantedPolicing_Crusader_3 is one of the 32
/// the Stanton law missions' wave spawning names by id. Bounty_Wave_Hard_Stanton2
/// is one of the 136 no script names, so only its folder (ai_bounty) and its
/// name (hard, Stanton2 = Crusader) say what it is: two Nine Tails Cutlass
/// Blacks carry Criminal_25_High_001, distilled spirits at 25% of a 46 SCU
/// hold - 11 SCU each, rounded down - and two Buccaneers carry the same list
/// into a hull the install gives no cargo grid. The member with no ship class
/// is how Security_Blockade's Crusader Security ships are written. The
/// spirits' 1,000 aUEC is a round fixture price, not UEX's.
/// </remarks>
public class EncountersPageTests
{
    internal const string Model = """
        {"ready":true,"scriptsRead":4176,"uex":true,
         "encounters":[
          {"name":"Bounty_Wave_Hard_Stanton2","folder":"pu/mission/ai_bounty","scripts":[],"arena":false,"waves":[
            {"id":1,"ships":[
              {"ship":{"className":"DRAK_Cutlass_Black_PU_AI_NT","name":"Drake Cutlass Black","crew":"Nine Tails","hold":46,"holdFrom":"install"},
               "flown":null,"amount":2,"minAmount":null,"maxAmount":null,"skill":"HumanPilot60","cargo":["Criminal_25_High_001"]},
              {"ship":{"className":"DRAK_Buccaneer_PU_AI_NT","name":"Drake Buccaneer","crew":"Nine Tails","hold":null,"holdFrom":null},
               "flown":null,"amount":2,"minAmount":null,"maxAmount":null,"skill":"HumanPilot60","cargo":["Criminal_25_High_001"]}]},
            {"id":2,"ships":[
              {"ship":null,"flown":"Crusader Security","amount":2,"minAmount":null,"maxAmount":null,"skill":null,"cargo":["Security_25_Medium_001"]}]}]},
          {"name":"WantedPolicing_Crusader_3","folder":"pu/mission/ai_emscenarios/security/crusader",
           "scripts":["Missions/EnvironmentalMissionScenarios/LawEMs/CoreModule/WantedPolicing_Stanton/Functions/WaveSpawning/WaveSpawning.xml"],
           "arena":false,"waves":[
            {"id":1,"ships":[
              {"ship":{"className":"AEGS_Gladius_PU_AI_SEC","name":"Aegis Gladius","crew":"Security","hold":null,"holdFrom":null},
               "flown":null,"amount":3,"minAmount":null,"maxAmount":null,"skill":null,"cargo":[]}]}]},
          {"name":"EA_VanduulSwarm","folder":"ea","scripts":["Missions/AC/AC_Swarm/AC_Swarm/Functions/SetupAndBeginSwarm.xml"],"arena":true,"waves":[
            {"id":1,"ships":[
              {"ship":{"className":"VNCL_Glaive_EA_AI","name":"Vanduul Glaive","crew":null,"hold":null,"holdFrom":null},
               "flown":null,"amount":0,"minAmount":2,"maxAmount":4,"skill":null,"cargo":[]}]}]}],
         "manifests":{
          "Criminal_25_High_001":{"name":"Criminal_25_High_001","mode":"Even","minFill":0.25,"maxFill":0.25,
            "goods":[{"commodity":"Distilled Spirits","probability":1,"price":1000}]},
          "Security_25_Medium_001":{"name":"Security_25_Medium_001","mode":"Even","minFill":0.25,"maxFill":0.25,"goods":[]}},
         "unused":[
          {"name":"BlockadeRunner_Freelancer","mode":"Random","minFill":0.45,"maxFill":0.95,
            "goods":[{"commodity":"Zeta-Prolanide","probability":1,"price":null}]},
          {"name":"ShipHeist_Freelancer","mode":"Even","minFill":0,"maxFill":1,"goods":[]}]}
        """;

    private static Page Opened(string? lang = null)
    {
        var page = lang is null ? new Page() : new Page(lang);
        page.Serve("/api/reference/encounters", Model);
        page.Do("await loadEncounters();");
        return page;
    }

    private const string Rows = "__dom.node('#encounters-table tbody').querySelectorAll('tr')";

    private static string Row(Page page, int i) => page.Text($"{Rows}[{i}].textContent");

    [Fact]
    public void Arena_Commander_is_left_out_of_the_universe_and_has_its_own_filter()
    {
        var page = Opened();
        Assert.Equal(2, page.Count($"{Rows}.length"));
        Assert.DoesNotContain("Vanduul", page.NodeText("#encounters-table tbody"));

        page.Do("__dom.node('#encounters-kind').value = 'arena'; renderEncounters();");
        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("Arena Commander: Swarm", Row(page, 0));
        // A range of ships counts at its most.
        Assert.Contains("4", Row(page, 0));
    }

    [Fact]
    public void The_note_says_how_many_a_script_names_and_that_the_logs_cannot_check_it()
    {
        var note = Opened().NodeText("#encounters-note");

        Assert.Contains("3 groups of NPC ships in your game files, 1 of them Arena Commander’s", note);
        Assert.Contains("1 of the 2 in the universe are named by a mission script", note);
        Assert.Contains("The other 1 are named by no script", note);
        Assert.Contains("none of this is checked against your play", note);
    }

    [Fact]
    public void A_scripted_encounter_names_its_mission_and_an_unscripted_one_says_only_its_name_speaks()
    {
        var page = Opened();

        var bounty = Row(page, 0);
        Assert.Contains("Bounty missions", bounty);
        Assert.Contains("No script names it; its name says hard · Crusader", bounty);

        var law = Row(page, 1);
        Assert.Contains("Stanton law missions", law);
        Assert.DoesNotContain("No script", law);
    }

    [Fact]
    public void Cargo_is_counted_only_where_there_is_a_hold_and_a_commodity()
    {
        var bounty = Row(Opened(), 0);

        // Two Cutlasses at floor(46 x 25%) = 11 SCU; the Buccaneers have no
        // grid and the security list names nothing, so they add none.
        Assert.Contains("22 SCU", bounty);
        Assert.Contains("22,000 aUEC", bounty);
        Assert.Contains("Nine Tails, Crusader Security", bounty);
    }

    [Fact]
    public void An_opened_encounter_says_ship_by_ship_what_its_hold_carries_or_why_not()
    {
        var page = Opened();
        page.Do("encounterOpen = 'Bounty_Wave_Hard_Stanton2'; renderEncounters();");
        var text = page.NodeText("#encounters-table tbody");

        Assert.Contains("Wave 1", text);
        Assert.Contains("2 × Drake Cutlass Black", text);
        Assert.Contains("pilot 60", text);
        Assert.Contains("Distilled Spirits, 25% of a 46 SCU hold: up to 11 SCU, worth up to 11,000 aUEC at the best UEX sell", text);
        Assert.Contains("Its cargo list has nothing to fill: the files give this hull no cargo grid.", text);
        Assert.Contains("a ship the files do not name", text);
        Assert.Contains("Its cargo list names no commodity.", text);
        Assert.Contains("named by no mission script the game ships", text);
    }

    [Fact]
    public void The_cargo_lists_nothing_carries_are_listed_apart()
    {
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#encounters-unused').hidden"));
        Assert.Equal("2 cargo lists no encounter carries", page.NodeText("#encounters-unused-summary"));
        var list = page.NodeText("#encounters-unused-list");
        Assert.Contains("BlockadeRunner_Freelancer · 45–95% · Zeta-Prolanide", list);
        Assert.Contains("ShipHeist_Freelancer · 0–100%", list);
    }

    [Fact]
    public void Search_finds_an_encounter_by_a_ship_in_it()
    {
        var page = Opened();
        page.Do("__dom.node('#encounters-search').value = 'gladius'; renderEncounters();");

        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("WantedPolicing Crusader 3", Row(page, 0));
    }

    [Fact]
    public void Search_takes_a_name_as_the_page_writes_it_with_spaces()
    {
        var page = Opened();
        page.Do("__dom.node('#encounters-search').value = 'bounty wave hard'; renderEncounters();");

        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("Bounty Wave Hard Stanton2", Row(page, 0));
    }

    [Fact]
    public void The_page_reads_in_french()
    {
        var page = Opened("fr");
        var bounty = Row(page, 0);

        Assert.Contains("Missions de prime", bounty);
        Assert.Contains("Aucun script ne la nomme ; son nom dit difficile · Crusader", bounty);
        Assert.Contains("2 rencontres", page.NodeText("#encounters-count"));
    }

    [Fact]
    public void An_npc_ship_card_says_which_encounters_spawn_it_and_what_it_carries()
    {
        var page = new Page();
        page.Serve("/api/reference/ship?id=DRAK_Cutlass_Black_PU_AI_NT", """
            {"className":"DRAK_Cutlass_Black_PU_AI_NT","name":"Drake Cutlass Black","npc":true,"crew":"Nine Tails",
             "baseHull":{"className":"DRAK_Cutlass_Black","name":"Drake Cutlass Black"},"community":null,"size":null,
             "cargo":{"scu":46,"from":"install","grids":2},"flown":null,"uex":true,"shops":[],"rentals":[],"parts":[],"npcVersions":[],
             "encounters":{"names":[{"name":"Bounty_Wave_Hard_Stanton2","arena":false,"linked":false},
                                    {"name":"Pirate_Cutlass_Black","arena":false,"linked":true}],"hold":46,"holdFrom":"install","uex":true,
               "cargo":[{"name":"Criminal_25_High_001","mode":"Even","minFill":0.25,"maxFill":0.25,
                 "goods":[{"commodity":"Distilled Spirits","probability":1,"price":1000}]},
                        {"name":"Criminal_25_Medium_001","mode":"Even","minFill":0.25,"maxFill":0.25,
                 "goods":[{"commodity":"Distilled Spirits","probability":1,"price":1000}]},
                        {"name":"Criminal_01_Low_001","mode":"Even","minFill":0.01,"maxFill":0.01,
                 "goods":[{"commodity":"Distilled Spirits","probability":1,"price":1000}]}]}}
            """);
        page.Do("await openRefDetail('ship', 'DRAK_Cutlass_Black_PU_AI_NT');");
        var text = page.NodeText("#ref-modal-body");

        Assert.Contains("Turns up in", text);
        Assert.Contains("Bounty Wave Hard Stanton2", text);
        // Only the scripted one is marked; the rest are the rule, not news.
        Assert.Contains("Pirate Cutlass Black · named by a mission script", text);
        Assert.DoesNotContain("Stanton2 · named", text);

        // Two lists that load the same are one line, and a 1% fill of 46 SCU is under a box.
        const string load = "Distilled Spirits, 25% of a 46 SCU hold: up to 11 SCU, worth up to 11,000 aUEC at the best UEX sell";
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, System.Text.RegularExpressions.Regex.Escape(load)));
        Assert.Contains("Distilled Spirits, 1% of a 46 SCU hold: under 1 SCU", text);
        Assert.DoesNotContain("up to 0 SCU", text);
        Assert.DoesNotContain("worth up to 0 aUEC", text);
    }
}
