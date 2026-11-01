namespace Quantumwake.WebTests;

/// <summary>
/// Every item and component name opens the same Reference window (0.20.32).
/// </summary>
/// <remarks>
/// Before this, a gun was a link in the Items table and in a checklist, plain
/// words in the Loadout, the Stash, the Ledger and the Logbook, a link to the
/// Loot page's own panel in a farm list, and a row toggle in the Armoury. The
/// same thing opened four ways or not at all depending on where it was met.
/// These tests pin each page to <c>refLink</c>: the name carries
/// <c>ref-link</c>, and clicking it opens the window on that item.
/// </remarks>
public class ItemLinksTests
{
    private static string Links(string selector) =>
        $"__dom.node('{selector}').byClass('ref-link')";

    private static void Click(Page page, string selector, int index = 0) =>
        page.Do($"{Links(selector)}[{index}].click();");

    private static void AssertOpenedOn(Page page, string name)
    {
        Assert.False(page.Truth("__dom.node('#ref-modal').hidden"));
        Assert.Equal(name, page.NodeText("#ref-modal-title"));
    }

    [Fact]
    public void A_loadout_item_opens_its_card()
    {
        var page = new Page();
        page.Do("""
            renderLoadout({ loadout: [
              { port: 'helmet_attach', category: 'Armour', label: 'Head', slotCount: 1,
                currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'ADP_Armor_Helmet', count: 1 }] }
            ] });
            """);

        Assert.Equal("ADP Armor Helmet", page.Text($"{Links("#loadout-grid")}[0].textContent"));
        Click(page, "#loadout-grid");
        AssertOpenedOn(page, "ADP Armor Helmet");
    }

    /// <summary>The stash row carries the class beside the name; the card is asked for by the class.</summary>
    [Fact]
    public void A_stash_item_opens_its_card_by_class()
    {
        var page = new Page();
        page.Do("""
            await renderStash({ stash: [
              { locationId: 'loc1', name: 'Port Tressler', lastSeen: '2026-08-25T20:00:00Z', itemCount: 1,
                groups: [{ category: 'Weapons', items: [{ name: 'P4-AR Rifle', itemClass: 'behr_rifle_ballistic_01', lastSeen: '2026-08-25T20:00:00Z' }] }] }
            ] });
            """);

        var link = $"{Links("#stash-grid")}[0]";
        Assert.Equal("P4-AR Rifle", page.Text($"{link}.textContent"));
        Assert.True(page.Truth($"{link}.classList.contains('slot-item')"));

        Click(page, "#stash-grid");
        AssertOpenedOn(page, "behr rifle ballistic 01");
        Assert.Contains("GET /api/reference/item?id=behr_rifle_ballistic_01", page.Fetched());
    }

    /// <summary>A bought item is a link; a cargo sale names a commodity and stays words.</summary>
    [Fact]
    public void A_ledger_purchase_opens_its_card_and_a_cargo_row_does_not()
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", """
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium · 8 SCU","where":"Port Tressler",
              "shop":"TDD","amount":288000,"quantity":8,"confirmed":true,"running":288000,"item":null},
             {"at":"2026-08-19T09:00:00+00:00","kind":"Item bought","what":"MedPen","where":"Lorville",
              "shop":"Cubby Blast","amount":-4000,"quantity":1,"confirmed":true,"running":284000,"item":"crlf_consumable_healing_01"}]
            """);
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");

        Assert.Equal(1, page.Count($"{Links("#ledger-table tbody")}.length"));
        Assert.Equal("MedPen", page.Text($"{Links("#ledger-table tbody")}[0].textContent"));

        Click(page, "#ledger-table tbody");
        Assert.Contains("GET /api/reference/item?id=crlf_consumable_healing_01", page.Fetched());
    }

    /// <summary>The logbook's item lines link by the class the server sends; a session line is prose.</summary>
    [Fact]
    public void Logbook_item_lines_open_their_card()
    {
        var page = new Page();
        page.Serve("/api/logbook?days=0", """
            [{ "at": "2026-09-27T15:00:00Z", "kind": "session", "what": "Session aboard Drake Corsair", "place": "Orison", "detail": "1h 20m", "amount": null, "subject": null },
             { "at": "2026-09-27T13:00:00Z", "kind": "bought", "what": "MedPen ×2", "place": "Orison", "detail": "Cubby Blast", "amount": -8000, "subject": "crlf_consumable_healing_01" },
             { "at": "2026-09-27T12:00:00Z", "kind": "loot", "what": "Coda Pistol", "place": "Orison", "detail": "first seen", "amount": null, "subject": "ksar_pistol_energy_01" }]
            """);
        page.Do("await loadLogbook();");

        Assert.Equal(2, page.Count($"{Links("#logbook-feed")}.length"));
        Assert.Equal("MedPen ×2", page.Text($"{Links("#logbook-feed")}[0].textContent"));

        Click(page, "#logbook-feed", 1);
        Assert.Contains("GET /api/reference/item?id=ksar_pistol_energy_01", page.Fetched());
    }

    /// <summary>
    /// On the Loot page the name used to open this page's own where-panel, and
    /// only for items a table rolls. Now every name opens the card and the odds
    /// open the panel, so the goldfish no table rolls is no longer dead text.
    /// </summary>
    [Fact]
    public void Loot_names_open_the_card_and_the_odds_open_where_it_drops()
    {
        var page = new Page();
        page.Serve("/api/loot?days=0", """
            [{"at":"2026-08-20T09:00:00+00:00","item":"S-38 Pistol","itemClass":"behr_pistol_ballistic_01",
              "place":"Port Tressler","category":"Weapons","price":1200,"bestRoll":0.01755,"bestFrom":"Loot · Military","lootSources":12},
             {"at":"2026-08-19T09:00:00+00:00","item":"Goldfish","itemClass":"GoldFish",
              "place":"Orison","category":"Other","price":null,"lootSources":0}]
            """);
        page.Serve("/api/loot/where?item=behr_pistol_ballistic_01", """{"item":"behr_pistol_ballistic_01","name":"S-38 Pistol","tables":1,"hits":[]}""");
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");

        Assert.Equal(2, page.Count($"{Links("#loot-table tbody")}.length"));
        Click(page, "#loot-table tbody", 1);
        AssertOpenedOn(page, "GoldFish");

        page.Do("await __dom.node('#loot-table tbody').byClass('link')[0].fire('click');");
        Assert.Contains("GET /api/loot/where?item=behr_pistol_ballistic_01", page.Fetched());
    }

    [Fact]
    public void A_spending_bar_opens_the_item_it_totals()
    {
        var page = new Page();
        page.Do("""
            renderSpending({ net: 0, income: 0, spend: 4000, commoditySpend: 0, purchaseCount: 1, tradeCount: 0,
              tradeShops: [], shops: [], items: [{ name: 'MedPen', total: 4000, quantity: 2 }] });
            """);

        var link = "__dom.node('#items-chart').byClass('place-link')[0]";
        Assert.Equal("MedPen", page.Text($"{link}.textContent"));
        Assert.Equal("Open this item", page.Text($"{link}.title"));

        page.Do($"{link}.click();");
        AssertOpenedOn(page, "MedPen");
    }

    [Fact]
    public void A_debrief_purchase_opens_its_card()
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", """
            {"id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z","ships":[],"locations":[],"jumps":[],
             "contracts":[],"timeline":[],"trades":[],"partyNotes":[],
             "purchases":[{"at":"2026-08-20T20:10:00Z","item":"crlf_consumable_healing_01","total":500,"quantity":2,"confirmed":true}]}
            """);
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300, primaryShip:'RSI Zeus', lastLocation:'HUR-L1', jumps:0, contracts:0, deaths:0, incapacitations:0 }];
            await toggleSessionDebrief('s1');
            """);

        Assert.Equal("crlf consumable healing 01 ×2", page.Text($"{Links("#sessions-table tbody")}[0].textContent"));
        Click(page, "#sessions-table tbody");
        Assert.Contains("GET /api/reference/item?id=crlf_consumable_healing_01", page.Fetched());
    }

    /// <summary>A feed line about one item links by the class the parser kept beside the sentence.</summary>
    [Fact]
    public void A_feed_line_about_an_item_opens_its_card()
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do("""
            renderNow({ connected:true, inGame:true, confidence:'None', recentEvents:[
              { at:'2026-08-20T20:10:00Z', kind:'bought', text:'Bought P4-AR Rifle', detail:'4,138 aUEC · Centermass', subject:'behr_rifle_ballistic_01' },
              { at:'2026-08-20T20:00:00Z', kind:'location', text:'Arrived at Port Tressler', detail:null, subject:null }
            ] });
            """);

        Assert.Equal(1, page.Count($"{Links("#now-feed")}.length"));
        Assert.Equal("Bought P4-AR Rifle", page.Text($"{Links("#now-feed")}[0].textContent"));

        Click(page, "#now-feed");
        Assert.Contains("GET /api/reference/item?id=behr_rifle_ballistic_01", page.Fetched());
    }

    /// <summary>The Armoury's row toggles its figures; the name on it opens the card without toggling the row.</summary>
    [Fact]
    public void An_armoury_gun_name_opens_its_card_without_opening_the_row()
    {
        var page = new Page();
        page.Serve("/api/armoury", ArmouryPageTests.Model);
        page.Do("await loadArmoury();");

        // The table sorts by its own figures, so the rifle is found by name.
        var link = $"{Links("#armoury-guns tbody")}.find(n => n.textContent === 'P4-AR Rifle')";
        Assert.True(page.Truth($"Boolean({link})"));

        page.Do($"{link}.click();");
        AssertOpenedOn(page, "behr rifle ballistic 01");
        Assert.True(page.Truth("armouryOpenGun === null"));
    }

    [Fact]
    public void An_armour_family_opens_its_plain_piece()
    {
        var page = new Page();
        page.Serve("/api/armoury", ArmouryPageTests.Model);
        page.Do("await loadArmoury(); showArmouryPane('armour');");

        var link = $"{Links("#armoury-armour tbody")}.find(n => n.textContent === 'Testudo')";
        Assert.True(page.Truth($"Boolean({link})"));
        page.Do($"{link}.click();");
        Assert.Contains("GET /api/reference/item?id=qrt_combat_medium_core_01_01_01", page.Fetched());
    }

    /// <summary>A farm list's items used to open the Loot page; they open the same card as everywhere now.</summary>
    [Fact]
    public void A_farm_list_item_opens_its_card()
    {
        var page = new Page();
        page.Serve("/api/checklists", """
            [{"id":"l1","title":"Pyro run","createdAt":"2026-10-01T00:00:00Z","pinned":false,"imported":null,
              "items":[{"id":"i1","text":"Gallant Rifle","dueAt":null,"note":null,"attachments":[],"done":false,"doneAt":null}]}]
            """);
        page.Serve("/api/loot/farm", """
            {"items":[{"written":"Gallant Rifle","itemClass":"klwe_rifle_energy_01","name":"Gallant Rifle","places":1,
                       "best":{"name":"Shady Glen Farms","body":"Wala","chance":0.0025},"npc":null,"npcs":0,"npcShips":0,"buy":null,"craft":null}],
             "places":[],"unmatched":[]}
            """);
        page.Do("await loadChecklists(); await __dom.node('#checklists-list').querySelectorAll('.checklist-farm-toggle')[0].fire('click');");

        Assert.Equal("Gallant Rifle", page.Text($"{Links("#checklists-list")}[0].textContent"));
        Click(page, "#checklists-list");
        Assert.Contains("GET /api/reference/item?id=klwe_rifle_energy_01", page.Fetched());
    }
}
