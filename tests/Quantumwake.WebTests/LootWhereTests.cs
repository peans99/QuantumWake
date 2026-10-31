namespace Quantumwake.WebTests;

/// <summary>
/// Where the game's own loot tables say an item comes from, beside what the
/// logs say was found. The rows are this install's: the S-38 reads 1.755% a
/// roll from a small weapons box, three times over in one preset because it
/// lists the box under Common, Uncommon and Rare.
/// </summary>
public class LootWhereTests
{
    private const string Pickups = """
        [{"at":"2026-08-20T09:00:00+00:00","item":"S-38 Pistol","itemClass":"behr_pistol_ballistic_01",
          "place":"Orison","category":"Weapons","price":null,
          "bestRoll":0.01755,"bestFrom":"Distribution Centres · High Security","lootSources":12},
         {"at":"2026-08-19T09:00:00+00:00","item":"Goldfish","itemClass":"GoldFish",
          "place":"Orison","category":"Other","price":null,"lootSources":0}]
        """;

    private const string Where = """
        {"item":"behr_pistol_ballistic_01","name":"S-38 Pistol","tables":333,"hits":[
          {"kind":"slot","preset":"V3SlotPreset_Loot_Military","label":"Loot · Military","group":"Weapons Small Rare",
           "container":"Lootable_Container_Weapons_Small","containerName":"Weapons box","slotChance":1,
           "table":"V3LootTable_Weapons_Small","perRoll":0.01755,"via":"Small weapons / Pistols"},
          {"kind":"slot","preset":"V3SlotPreset_Loot_Military","label":"Loot · Military","group":"Weapons Small Common",
           "container":"Lootable_Container_Weapons_Small","containerName":"Weapons box","slotChance":1,
           "table":"V3LootTable_Weapons_Small","perRoll":0.01755,"via":"Small weapons / Pistols"},
          {"kind":"slot","preset":"LootGeneration_SlotPreset_Station","label":"Station","container":"Lootable_Generated_Container_06_dirty",
           "containerName":"Standard Container","slotChance":0.4,"respawnSeconds":1200,
           "table":"GeneralLoot","perRoll":0.00232,"via":"LootArchetype_Weapons / Pistol"},
          {"kind":"body","preset":"Corpse_Civilian","label":"Civilian","container":"Corpse_Civilian",
           "containerName":"Corpse_Civilian","slotChance":1,"table":"GeneralLoot","perRoll":0.00232,"via":"LootArchetype_Weapons / Pistol"}
        ]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/loot?days=0", Pickups);
        page.Serve("/api/loot/where?item=behr_pistol_ballistic_01", Where);
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");
        return page;
    }

    [Fact]
    public void A_pickup_shows_its_best_roll_beside_its_price()
    {
        Assert.Contains("1.8%", Loaded().NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// The goldfish came out of no table - it is a hangar decoration - so its
    /// odds cell is a dash, not "0%", which would claim it cannot be had.
    /// </summary>
    [Fact]
    public void An_item_no_table_rolls_gets_a_dash_and_no_link()
    {
        var page = Loaded();

        Assert.Equal(1, page.Count("__dom.node('#loot-table tbody').querySelectorAll('.link').length"));
        Assert.DoesNotContain("0%", page.NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// One preset listing the same box under three rarity groups is one place
    /// to go, not three; the panel rolls hits up by kind of place.
    /// </summary>
    [Fact]
    public void Where_an_item_comes_from_reads_one_row_per_kind_of_place()
    {
        var page = Loaded();
        page.Do("await showLootWhere('behr_pistol_ballistic_01');");

        Assert.False(page.Truth("__dom.node('#loot-where').hidden"));
        Assert.Equal(3, page.Count("__dom.node('#loot-where').querySelectorAll('tbody')[0].children.length"));

        var text = page.NodeText("#loot-where");
        Assert.Contains("Loot · Military", text);
        Assert.Contains("20 min", text);
        Assert.Contains("2 kinds of place", text);
        Assert.Contains("1 kind of body", text);
    }

    /// <summary>
    /// The map files place the same military box at named security centres and
    /// at unmarked spots - caves - on moons. Real rows from this install's
    /// LMG magazine, which has 456 places; fourteen are enough to need the
    /// button.
    /// </summary>
    private static string Placed()
    {
        var rows = new List<string>
        {
            """{"name":"Dupree Industrial Manufacturing Facility","body":"Hurston","id":"Stanton1_DistributionCentre_DupreeIndustrial_ManufacturingFacility","spots":25,"presets":1,"chance":0.1598,"perRoll":0.1598,"label":"Distribution Centres · High Security","containerName":"Weapons crate","respawnSeconds":null}""",
            """{"name":null,"body":"Cellin","id":"Stanton2a","spots":4,"presets":1,"chance":0.1598,"perRoll":0.1598,"label":"Loot · Military","containerName":"Weapons crate","respawnSeconds":null}""",
        };
        for (var i = 1; i <= 12; i++)
            rows.Add($$"""{"name":"MT SecurityCenter {{i:00}}","body":"Calliope","id":"MT_{{i}}","spots":1,"presets":1,"chance":0.05,"perRoll":0.05,"label":"Loot · Military","containerName":"Weapons crate","respawnSeconds":1200}""");

        return $$"""
            {"item":"behr_lmg_ballistic_01_mag","name":"LMG magazine","tables":333,"placesRead":true,
             "places":[{{string.Join(",", rows)}}],
             "hits":[
              {"kind":"slot","preset":"V3SlotPreset_Loot_Military","label":"Loot · Military","container":"Lootable_Container_Weapons_Large",
               "containerName":"Weapons crate","slotChance":1,"table":"V3LootTable_Weapons_Large","perRoll":0.1598,"via":"Magazines","places":13},
              {"kind":"slot","preset":"LootGeneration_SlotPreset_Station","label":"Station","container":"Lootable_Generated_Container_06_dirty",
               "containerName":"Standard Container","slotChance":0.4,"table":"GeneralLoot","perRoll":0.01,"via":"Ammo","places":0}
            ]}
            """;
    }

    private static Page Opened(string where)
    {
        var page = Loaded();
        page.Serve("/api/loot/where?item=behr_lmg_ballistic_01_mag", where);
        page.Do("await showLootWhere('behr_lmg_ballistic_01_mag');");
        return page;
    }

    private const string PlacesBody = "__dom.node('#loot-where').querySelectorAll('tbody')[0]";

    /// <summary>
    /// "Where do I go" is answered by names, so the places come first: the
    /// best twelve, the star map's names as written, and a cave as an
    /// unmarked spot on its moon rather than a blank.
    /// </summary>
    [Fact]
    public void Named_places_come_first_and_stop_at_twelve()
    {
        var page = Opened(Placed());

        Assert.Equal(12, page.Count($"{PlacesBody}.children.length"));

        var first = page.Text($"{PlacesBody}.children[0].textContent");
        Assert.Contains("Dupree Industrial Manufacturing Facility", first);
        Assert.Contains("Hurston", first);
        Assert.Contains("16%", first);
        Assert.Contains("25", first);

        Assert.Contains("Unmarked spot", page.Text($"{PlacesBody}.children[1].textContent"));
        Assert.Contains("13 named places · unmarked spots on 1 body", page.NodeText("#loot-where"));
        Assert.Contains("Show all 14", page.NodeText("#loot-where"));
    }

    [Fact]
    public void Show_all_lists_every_place_and_a_body_narrows_them()
    {
        var page = Opened(Placed());

        page.Do("__dom.node('#loot-where').querySelectorAll('.loot-places-more')[0].click();");
        Assert.Equal(14, page.Count($"{PlacesBody}.children.length"));

        page.Do("""
            const pick = __dom.node('#loot-where').querySelectorAll('.loot-places-body')[0];
            pick.value = 'Hurston';
            pick.fire('change');
            """);
        Assert.Equal(1, page.Count($"{PlacesBody}.children.length"));
        Assert.True(page.Truth("__dom.node('#loot-where').querySelectorAll('.loot-places-more')[0].hidden"));
    }

    /// <summary>
    /// The station preset is in the tables but no map file sets it down on
    /// this build, so its row says so instead of implying it can be found.
    /// </summary>
    [Fact]
    public void A_kind_of_place_no_map_file_sets_down_says_so()
    {
        var text = Opened(Placed()).NodeText("#loot-where");

        Assert.Contains("13 places", text);
        Assert.Contains("not placed", text);
    }

    [Fact]
    public void An_item_only_unplaced_presets_roll_says_no_map_file_sets_it_down()
    {
        var page = Opened("""
            {"item":"behr_lmg_ballistic_01_mag","name":"LMG magazine","tables":333,"placesRead":true,"places":[],
             "hits":[{"kind":"slot","preset":"LootGeneration_SlotPreset_Station","label":"Station","container":"c",
               "containerName":"Standard Container","slotChance":0.4,"table":"GeneralLoot","perRoll":0.01,"via":"Ammo","places":0}]}
            """);

        Assert.Contains("No map file on this build sets down a container that rolls this", page.NodeText("#loot-where"));
    }

    /// <summary>
    /// A power plant comes out of no crate; it flies in NPC ships. Rows from
    /// this install: the Charger is fitted to the criminal and civilian
    /// Gladius and to a Sabre.
    /// </summary>
    private const string Charger = """
        {"item":"POWR_AEGS_S01_Charger_SCItem","name":"Charger","tables":333,"placesRead":true,"places":[],"hits":[],
         "ships":[
          {"ship":"AEGS_Gladius_PU_AI_CRIM","name":"Gladius","crew":"Criminal","count":1},
          {"ship":"AEGS_Gladius_PU_AI_CIV","name":"Gladius","crew":"Civilian","count":1},
          {"ship":"AEGS_Sabre_Raven_PU_AI_CRIM","name":"Sabre Raven","crew":"Criminal","count":2}
         ]}
        """;

    [Fact]
    public void A_ship_part_lists_the_npc_ships_that_fly_with_it()
    {
        var page = Loaded();
        page.Serve("/api/loot/where?item=POWR_AEGS_S01_Charger_SCItem", Charger);
        page.Do("await showLootWhere('POWR_AEGS_S01_Charger_SCItem');");

        var text = page.NodeText("#loot-where");
        Assert.Contains("On NPC ships", text);
        Assert.DoesNotContain("No container or body", text);
        Assert.Contains("2 ships · 3 NPC variants", text);

        // The two Gladius variants are one ship, flown by two kinds of crew.
        Assert.Equal(2, page.Count($"{PlacesBody}.children.length"));
        var gladius = page.Text($"{PlacesBody}.children[0].textContent");
        Assert.Contains("Gladius", gladius);
        Assert.Contains("Civilian, Criminal", gladius);
        Assert.Contains("×2", page.Text($"{PlacesBody}.children[1].textContent"));
    }

    [Fact]
    public void A_search_hit_only_ships_carry_says_how_many_and_no_odds()
    {
        var page = new Page();
        page.Serve("/api/loot/search?q=charger", """[{"itemClass":"POWR_AEGS_S01_Charger_SCItem","name":"Charger","bestRoll":null,"sources":0,"ships":23}]""");
        page.Do("__dom.node('#loot-find').value = 'charger'; await findLoot();");

        var text = page.NodeText("#loot-find-results");
        Assert.Contains("23 NPC ships", text);
        Assert.DoesNotContain("%", text);
    }

    /// <summary>
    /// Armour colours sit near a thousandth of a percent. Rounded to whole
    /// percent they would all read "0%".
    /// </summary>
    [Theory]
    [InlineData(0.01755, "1.8%")]
    [InlineData(0.2174, "22%")]
    [InlineData(0.0011, "0.11%")]
    [InlineData(0.0000091, "0.001%")]
    [InlineData(0.000000001, "<0.001%")]
    public void Small_odds_keep_enough_places_to_say_something(double perRoll, string shown)
    {
        Assert.Equal(shown, new Page().Text($"lootPercent({perRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)})"));
    }
}
