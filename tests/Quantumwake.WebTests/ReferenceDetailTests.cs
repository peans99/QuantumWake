namespace Quantumwake.WebTests;

/// <summary>
/// The Reference modal: an item or a ship opened from a catalogue row, with
/// every part and hull in it a link to its own card and Back walking the
/// trail. The rows are this install's - the CF-337 Panther comes fitted to
/// the Gladius twice over, and the Gladius has twelve NPC versions of which
/// two are Criminal, told apart by the rest of their class.
/// </summary>
public class ReferenceDetailTests
{
    private const string Items = """
        [{"className":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","type":"WeaponGun","subType":"Gun",
          "size":3,"grade":1,"manufacturer":"Klaus & Werner","source":"install","price":36308,"stockedAt":29,
          "cheapestAt":"Area18 - Centermass","bestRoll":null,"npcs":0,"ships":215},
         {"className":"GoldFish","name":"Goldfish","type":"Misc","subType":"","size":0,"grade":0,
          "manufacturer":null,"source":"install","price":null,"stockedAt":0,"bestRoll":null,"npcs":0,"ships":0}]
        """;

    private const string Item = """
        {"className":"KLWE_LaserRepeater_S3","uex":true,
         "card":{"kind":"part","id":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","subtitle":"WeaponGun · Gun",
           "facts":[{"label":"Made by","value":"Klaus & Werner","source":"the game files"}],
           "holding":{"status":"not seen in your kit","detail":null},"price":null,"places":[],"actions":[],
           "blurb":"Size: 3\\nThe Panther"},
         "shops":[{"place":"Area18 - Centermass","price":36308},{"place":"New Babbage - Platinum Bay","price":38000}],
         "craft":{"output":"CF-337 Panther Repeater","craftSeconds":1680,"materials":["Agricium 1.16 SCU","Hadanite ×23"],"byDefault":false},
         "stockOn":[{"className":"AEGS_Gladius","name":"Aegis Gladius","count":2}],
         "where":{"item":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","tables":333,"placesRead":true,
           "hits":[],"places":[],"npcs":[],
           "ships":[{"ship":"AEGS_Gladius_PU_AI_CRIM_IK","name":"Aegis Gladius","crew":"Criminal","count":2},
                    {"ship":"AEGS_Gladius_PU_AI_CRIM","name":"Aegis Gladius","crew":"Criminal","count":2}]}}
        """;

    private const string Ship = """
        {"className":"AEGS_Gladius","name":"Aegis Gladius","npc":false,"crew":null,"baseHull":null,
         "community":{"career":"Combat","role":"Light Fighter","crew":1,"isSpaceship":true,"expeditedCost":2335,
           "standardClaimTime":5.83,"scmSpeed":226,"maxSpeed":1193,"shieldHp":6336,"health":6110},
         "size":{"beam":17.5,"length":21,"height":5.5},"cargo":null,"flown":null,"uex":true,
         "shops":[{"price":1200000,"terminal":"Astro Armada"}],"rentals":[],
         "parts":[{"className":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","type":"WeaponGun","size":3,"grade":1,"count":2},
                  {"className":"COOL_AEGS_S01_Bracer_SCItem","name":"Bracer","type":"Cooler","size":1,"grade":3,"count":2}],
         "npcVersions":[
           {"className":"AEGS_Gladius_PU_AI_CRIM","crew":"Criminal","parts":[{"className":"KLWE_LaserRepeater_S3","name":"CF-337","type":"WeaponGun","size":3,"grade":1,"count":2}]},
           {"className":"AEGS_Gladius_PU_AI_CRIM_IK","crew":"Criminal","parts":[]}]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/reference/items", Items);
        page.Serve("/api/reference/item?id=KLWE_LaserRepeater_S3", Item);
        page.Serve("/api/reference/ship?id=AEGS_Gladius", Ship);
        page.Do("await loadPartsRef();");
        return page;
    }

    private static Page Opened()
    {
        var page = Loaded();
        page.Do("await __dom.node('#parts-table tbody').querySelectorAll('.commodity-open')[0].fire('click');");
        return page;
    }

    private const string Links = "__dom.node('#ref-modal-body').querySelectorAll('.ref-link')";

    [Fact]
    public void The_items_table_says_where_a_thing_drops()
    {
        var text = Loaded().NodeText("#parts-table tbody");

        Assert.Contains("on 215 NPC ships", text);
    }

    [Fact]
    public void Can_be_found_leaves_out_what_no_file_puts_anywhere()
    {
        var page = Loaded();
        page.Do("__dom.node('#parts-found').checked = true; renderPartsRef();");

        var text = page.NodeText("#parts-table tbody");
        Assert.Contains("CF-337", text);
        Assert.DoesNotContain("Goldfish", text);
    }

    /// <summary>
    /// One click on a name answers every way to get it: every shop, the
    /// recipe, the hulls it comes fitted to, and where the files spawn it.
    /// </summary>
    [Fact]
    public void An_item_opens_with_its_shops_recipe_hulls_and_spawns()
    {
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#ref-modal').hidden"));
        Assert.Equal("CF-337 Panther Repeater", page.NodeText("#ref-modal-title"));

        var text = page.NodeText("#ref-modal-body");
        Assert.Contains("Area18 - Centermass", text);
        Assert.Contains("New Babbage - Platinum Bay", text);
        Assert.Contains("28m", text);
        Assert.Contains("Hadanite", text);
        Assert.Contains("Comes fitted to", text);
        Assert.Contains("On NPC ships", text);
        Assert.Contains("Track it", text);
    }

    /// <summary>
    /// The modal names the item itself, so the loot panel inside it does not
    /// print a second title with a second Close beside it.
    /// </summary>
    [Fact]
    public void The_spawn_panel_inside_the_modal_has_no_head_of_its_own()
    {
        var page = Opened();

        Assert.Equal(0, page.Count("__dom.node('#ref-modal-body').querySelectorAll('.loot-where-head').length"));
    }

    /// <summary>
    /// Dig down and come back: item, the ship it comes fitted to, a part on
    /// that ship, then Back twice lands on the ship and then the item.
    /// </summary>
    [Fact]
    public void A_ship_in_an_item_opens_and_back_walks_the_trail()
    {
        var page = Opened();

        page.Do($"await {Links}[0].fire('click');");
        Assert.Equal("Aegis Gladius", page.NodeText("#ref-modal-title"));
        Assert.Contains("Light Fighter", page.NodeText("#ref-modal-sub"));
        Assert.False(page.Truth("__dom.node('#ref-modal-back').hidden"));

        var text = page.NodeText("#ref-modal-body");
        Assert.Contains("Comes with", text);
        Assert.Contains("Bracer", text);
        Assert.Contains("Astro Armada", text);
        Assert.Contains("21 × 17.5 × 5.5 m", text);

        page.Do("await __dom.node('#ref-modal-back').fire('click');");
        Assert.Equal("CF-337 Panther Repeater", page.NodeText("#ref-modal-title"));
        Assert.True(page.Truth("__dom.node('#ref-modal-back').hidden"));
    }

    /// <summary>
    /// Two Criminal Gladiuses are different classes; the rest of the class
    /// is shown so the two rows are not the same words twice.
    /// </summary>
    [Fact]
    public void Npc_versions_are_told_apart_by_the_rest_of_their_class()
    {
        var page = Opened();
        page.Do($"await {Links}[0].fire('click');");

        var text = page.NodeText("#ref-modal-body");
        Assert.Contains("Flown by NPCs", text);
        Assert.Contains("PU_AI_CRIM_IK", text);
        Assert.Contains("PU_AI_CRIM · 2 parts", text);
    }

    /// <summary>The text table writes a line break as a backslash and an n; the card breaks the line.</summary>
    [Fact]
    public void The_games_paragraph_breaks_its_lines()
    {
        var page = Opened();

        Assert.Equal(2, page.Count("__dom.node('#ref-modal-body').byClass('part-blurb').length"));
        Assert.DoesNotContain("nThe Panther", page.NodeText("#ref-modal-body"));
    }

    [Fact]
    public void Close_hides_the_modal_and_forgets_the_trail()
    {
        var page = Opened();
        page.Do($"await {Links}[0].fire('click');");
        page.Do("__dom.node('#ref-modal-close').fire('click');");

        Assert.True(page.Truth("__dom.node('#ref-modal').hidden"));

        page.Do("await __dom.node('#parts-table tbody').querySelectorAll('.commodity-open')[0].fire('click');");
        Assert.True(page.Truth("__dom.node('#ref-modal-back').hidden"));
    }

    /// <summary>A ship catalogue row opens the ship by its class, not its name.</summary>
    [Fact]
    public void A_ship_row_opens_the_ship_card()
    {
        var page = new Page();
        page.Serve("/api/reference/ships", """
            [{"className":"AEGS_Gladius","source":"community","name":"Aegis Gladius","career":"Combat","role":"Light Fighter",
              "crew":1,"isSpaceship":true,"cargoScu":0,"price":null,"shops":[],"rentals":[],"parts":11},
             {"className":"RSI_Skylark","source":"install","name":"RSI Skylark","crew":0,"isSpaceship":null,
              "cargoScu":0,"price":null,"shops":[],"rentals":[],"parts":4}]
            """);
        page.Serve("/api/reference/ship?id=AEGS_Gladius", Ship);
        page.Do("await loadShipsRef();");

        var table = page.NodeText("#ships-table tbody");
        Assert.Contains("install", table);
        Assert.DoesNotContain("ground", table);

        page.Do("await __dom.node('#ships-table tbody').querySelectorAll('.ref-link')[0].fire('click');");
        Assert.Contains("GET /api/reference/ship?id=AEGS_Gladius", page.Fetched());
        Assert.Equal("Aegis Gladius", page.NodeText("#ref-modal-title"));
    }

    /// <summary>
    /// "Get CF-337 Panther Repeater" over a chip reading the same name said
    /// it twice; the line's own words are the link now, and they open the
    /// item's card rather than a search on the Items page.
    /// </summary>
    [Fact]
    public void A_checklist_line_that_names_its_item_is_the_link()
    {
        var page = Loaded();
        page.Serve("/api/checklists", """
            [{"id":"l1","title":"Hunt","createdAt":"2026-08-01T00:00:00Z","pinned":false,"imported":null,
              "items":[{"id":"i1","text":"Get CF-337 Panther Repeater","done":false,
                "attachments":[{"kind":"item","label":"CF-337 Panther Repeater","target":"KLWE_LaserRepeater_S3"},
                               {"kind":"location","label":"Area18","target":"Area18"}]}]}]
            """);
        page.Do("await loadChecklists();");

        var list = "__dom.node('#checklists-list')";
        Assert.Equal(1, page.Count($"{list}.byClass('checklist-attachment').length"));
        Assert.DoesNotContain("▦", page.NodeText("#checklists-list"));

        page.Do($"await {list}.byClass('checklist-item-link')[0].fire('click');");
        Assert.Equal("CF-337 Panther Repeater", page.NodeText("#ref-modal-title"));
    }

    /// <summary>The Loot page's panel leads into the same card.</summary>
    [Fact]
    public void The_loot_panel_has_a_details_button_into_reference()
    {
        var page = Loaded();
        page.Serve("/api/loot/where?item=KLWE_LaserRepeater_S3", """
            {"item":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","tables":333,"hits":[],"ships":[],"npcs":[]}
            """);
        page.Do("await showLootWhere('KLWE_LaserRepeater_S3');");
        page.Do("await __dom.node('#loot-where').querySelectorAll('.loot-where-more')[0].fire('click');");

        Assert.Equal("CF-337 Panther Repeater", page.NodeText("#ref-modal-title"));
    }
}
