namespace Quantumwake.WebTests;

/// <summary>
/// The cross-page links (0.21.9): a commodity, an organisation, a session, a
/// person flown with and a contract's runs each open the page that holds more
/// about them, where before most were printed as words.
/// </summary>
/// <remarks>
/// The links are buttons classed <c>cross-link</c>, not <c>ref-link</c>: that
/// class means "opens a Reference card", and ItemLinksTests counts on it.
/// </remarks>
public class CrossLinkTests
{
    private static string Cross(string selector) => $"__dom.node('{selector}').byClass('cross-link')";

    /// <summary>
    /// The game files and UEX spell some commodities differently; a link
    /// resolves to the Market's own name before it opens the page.
    /// </summary>
    [Fact]
    public void A_commodity_named_elsewhere_resolves_to_the_markets_name()
    {
        var page = new Page();
        page.Do("marketEntries = [{ name: 'Laranite' }, { name: 'Quantanium' }, { name: 'Agricium' }];");

        Assert.Equal("Laranite", page.Text("marketNameFor('Laranite (Ore)')"));
        Assert.Equal("Laranite", page.Text("marketNameFor('LARANITE')"));
        Assert.Equal("Quantanium", page.Text("marketNameFor('Quantainium')"));
        Assert.Equal("Agricium", page.Text("marketNameFor('Agricium')"));
        // Nothing near it: the name as given, rather than a wrong page.
        Assert.Equal("Hadanite", page.Text("marketNameFor('Hadanite')"));
    }

    /// <summary>A ledger cargo row's commodity opens its page; the shop is on the map.</summary>
    [Fact]
    public void A_ledger_cargo_row_opens_its_commodity()
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", """
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium · 8 SCU","where":"Port Tressler",
              "shop":"TDD - Trade and Development Division - Port Tressler","amount":288000,"quantity":8,"confirmed":true,"running":288000,"item":null}]
            """);
        page.Do("marketEntries = [{ name: 'Agricium' }]; __dom.node('#ledger-period').value = '0'; await loadLedger();");

        var links = Cross("#ledger-table tbody");
        Assert.Equal("Agricium", page.Text($"{links}.find(b => b.classList.contains('commodity-link')).textContent"));
        Assert.Contains("Agricium · 8 SCU", page.NodeText("#ledger-table tbody"));
        Assert.True(page.Truth($"{links}.some(b => b.classList.contains('terminal-link'))"));

        page.Do($"{links}.find(b => b.classList.contains('commodity-link')).click(); await new Promise(r => setTimeout(r, 0));");
        Assert.Equal("Agricium", page.Text("openCommodityName"));
    }

    /// <summary>
    /// An organisation's contracts: the list narrowed to it, saying so, with
    /// the way back to all of them.
    /// </summary>
    [Fact]
    public void Contracts_narrow_to_one_issuer_and_say_so()
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", """
            [{"at":"2026-08-20T09:00:00Z","name":"Patrol","issuer":"Crusader Security","outcome":"Completed"},
             {"at":"2026-08-19T09:00:00Z","name":"Haul","issuer":"Covalex","outcome":"Completed"},
             {"at":"2026-08-18T09:00:00Z","name":"Patrol","issuer":"CRUSADER SECURITY","outcome":"Failed"}]
            """);
        page.Do("__dom.node('#contracts-period').value = '0'; contractsOnly = { issuer: 'Crusader Security' }; await loadContractList();");

        Assert.Equal(2, page.Count("__dom.node('#contracts-table tbody').children.length"));
        Assert.False(page.Truth("__dom.node('#contracts-only').hidden"));
        Assert.Contains("Only contracts from Crusader Security: 2 in this range.", page.NodeText("#contracts-only"));
        // The issuer is itself a link, to its Reputation card.
        Assert.True(page.Truth("__dom.node('#contracts-table tbody').byClass('org-link').length === 2"));

        page.Do("__dom.node('#contracts-only').querySelectorAll('button')[0].click(); await new Promise(r => setTimeout(r, 0));");
        Assert.Equal(3, page.Count("__dom.node('#contracts-table tbody').children.length"));
        Assert.True(page.Truth("__dom.node('#contracts-only').hidden"));
    }

    /// <summary>An organisation's name elsewhere lands on its Reputation card, lit.</summary>
    [Fact]
    public void An_organisation_lands_on_its_reputation_card()
    {
        var page = new Page();
        page.Do("""
            reputationAll = { organisations: [{
              name: 'CRUSADER SECURITY', standing: 'NEUTRAL', section: null, rank: 'MEMBER', progress: 0.5,
              place: null, ladder: [{ name: 'Member', perk: null, minReputation: 0, fill: 0.5 }],
              lastSeen: '2026-10-24T18:59:59Z', lastShot: 'a.jpg',
              history: [{ at: '2026-10-24T18:59:59Z', shot: 'a.jpg', rank: 'MEMBER', rankIndex: 0, progress: 0.5 }],
              change: null, work: { issuer: 'Crusader Security', completed: 3, abandoned: 0, failed: 1 }
            }], readings: 1, unmeasured: 0, canRead: true, watching: true, ladders: 0 };
            reputationLandOn = 'Crusader Security';
            renderReputation();
            """);

        Assert.True(page.Truth("__dom.node('#reputation-list').byClass('rep-card')[0].classList.contains('landed')"));
        Assert.Equal("", page.Text("reputationLandOn ?? ''"));
        // The card's count of their contracts opens them on Contracts.
        Assert.Contains("Crusader Security in your logs: 3 finished", page.Text($"{Cross("#reputation-list")}[0].textContent"));
    }

    /// <summary>
    /// Crew and Servers land on Sessions searched for a handle or a shard,
    /// which needs the session rows to carry both.
    /// </summary>
    [Fact]
    public void Sessions_are_searched_by_crew_and_by_server()
    {
        var page = new Page();
        page.Do("""
            allSessions = [
              { id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300, primaryShip:'RSI Zeus', lastLocation:'HUR-L1',
                shard:'pub_euw1b_123', shardNames:['pub_euw1b_123'], crew:['B-Kon'], jumps:0, contracts:0, deaths:0, incapacitations:0 },
              { id:'s2', startedAt:'2026-08-21T20:00:00Z', inGame:5100, menu:300, primaryShip:'Drake Cutlass Black', lastLocation:'Area18',
                shard:'pub_use1a_9', shardNames:['pub_use1a_9'], crew:[], jumps:0, contracts:0, deaths:0, incapacitations:0 }];
            __dom.node('#sessions-period').value = '0';
            __dom.node('#sessions-search').value = 'b-kon';
            """);

        Assert.Equal("s1", page.Text("filteredSessions().map(s => s.id).join(',')"));
        page.Do("__dom.node('#sessions-search').value = 'use1a';");
        Assert.Equal("s2", page.Text("filteredSessions().map(s => s.id).join(',')"));
    }

    /// <summary>
    /// A session opened from elsewhere is found even when the search and the
    /// page would have hidden it.
    /// </summary>
    [Fact]
    public void A_session_link_clears_what_would_hide_it_and_opens_it()
    {
        var page = new Page();
        page.Serve("/api/sessions/s30", """{"id":"s30","ships":[],"locations":[],"jumps":[],"contracts":[],"timeline":[],"purchases":[],"trades":[],"partyNotes":[]}""");
        page.Do("""
            allSessions = Array.from({ length: 40 }, (_, i) => ({ id: `s${i}`, startedAt: new Date(Date.UTC(2026, 7, 20 - i)).toISOString(),
              inGame: 60, menu: 0, primaryShip: 'RSI Zeus', lastLocation: 'HUR-L1', jumps: 0, contracts: 0, deaths: 0, incapacitations: 0 }));
            __dom.node('#sessions-period').value = '0';
            __dom.node('#sessions-search').value = 'nothing matches this';
            sessionPage = 0;
            openSession('s30');
            await new Promise(r => setTimeout(r, 0));
            """);

        Assert.Equal("", page.Text("__dom.node('#sessions-search').value"));
        Assert.Equal(1, page.Count("sessionPage"));
        Assert.Equal("s30", page.Text("expandedSessionId"));
    }

    /// <summary>The handle in a debrief opens that person's row on Crew.</summary>
    [Fact]
    public void A_debrief_names_its_crew_and_servers_as_links()
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", """
            {"id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z",
             "ships":[{"displayName":"RSI Zeus","model":"RSI_Zeus","sorties":2}],"locations":[],"jumps":[],"contracts":[],"timeline":[],
             "purchases":[],"trades":[],"partyNotes":[{"at":"2026-08-20T20:20:00Z","handle":"B-Kon","moment":"Connected"}],
             "shards":[{"shard":"pub_euw1b_123","joinedAt":"2026-08-20T20:00:00Z","leftAt":"2026-08-20T21:00:00Z","ending":"Left"}]}
            """);
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300, primaryShip:'RSI Zeus', lastLocation:'HUR-L1',
              shard:'pub_euw1b_123', jumps:0, contracts:0, deaths:0, incapacitations:0 }];
            __dom.node('#sessions-period').value = '0';
            await toggleSessionDebrief('s1');
            """);

        var table = "#sessions-table tbody";
        Assert.True(page.Truth($"__dom.node('{table}').byClass('crew-link').some(b => b.textContent === 'B-Kon')"));
        Assert.True(page.Truth($"__dom.node('{table}').byClass('server-link').some(b => b.textContent === 'pub_euw1b_123')"));
        // The row's own last place flies to the map, as a place does everywhere.
        Assert.True(page.Truth($"__dom.node('{table}').byClass('place-link').some(b => b.textContent === 'HUR-L1')"));

        page.Serve("/api/crew?days=0", """[{"handle":"B-Kon","sessions":1,"joined":1,"left":0,"connected":1,"dropped":0,"ledParty":0,"first":"2026-08-20T20:20:00Z","last":"2026-08-20T20:20:00Z"}]""");
        page.Serve("/api/crew/ships?days=0", "[]");
        // The click loads Crew itself; a second load would find the landing spent.
        page.Do($"__dom.node('#crew-period').value = '0'; __dom.node('{table}').byClass('crew-link')[0].click();"
            + " for (let i = 0; i < 5; i++) await new Promise(r => setTimeout(r, 0));");
        Assert.True(page.Truth("__dom.node('#crew-table').querySelector('tbody').children.some(tr => tr.classList?.contains('landed'))"));
    }

    /// <summary>
    /// A link inside a translated sentence sits where the translation puts the
    /// word, and the sentence around it is still translated.
    /// </summary>
    [Fact]
    public void A_link_inside_a_sentence_stays_in_the_translation()
    {
        var page = new Page("fr");
        page.Do("""
            window.__parts = txLinks('rentable {price} at {place}', { price: '5 000 aUEC', place: placeLink('Lorville') });
            """);

        // "louable 5 000 aUEC à " and the link: the French puts the place last.
        Assert.Equal(2, page.Count("__parts.length"));
        Assert.Equal("louable 5 000 aUEC à ", page.Text("__parts[0]"));
        Assert.Equal("Lorville", page.Text("__parts[1].textContent"));
    }

    /// <summary>
    /// The mission card's runs: each date opens its session, and the runs
    /// together open on Contracts.
    /// </summary>
    [Fact]
    public void A_missions_runs_open_their_sessions_and_contracts()
    {
        var page = new Page();
        page.Serve("/api/reference/mission?id=m1", """
            {"id":"m1","name":"Patrol_Stanton","title":"Patrol","contractor":"Crusader Security","areas":["Stanton"],"locations":["Orison"],
             "summary":{"taken":1,"completed":1,"payouts":1,"paid":12000},
             "runs":[{"at":"2026-08-20T20:30:00Z","title":"Patrol","outcome":"Completed","minutes":20,"paid":12000,"session":"s1"}]}
            """);
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300, primaryShip:'RSI Zeus', lastLocation:'HUR-L1', jumps:0, contracts:0, deaths:0, incapacitations:0 }];
            await openRefDetail('mission', 'm1');
            """);

        var body = "#ref-modal-body";
        Assert.True(page.Truth($"__dom.node('{body}').byClass('session-link').length === 1"));
        Assert.True(page.Truth($"__dom.node('{body}').byClass('org-link').some(b => b.textContent === 'Crusader Security')"));
        Assert.True(page.Truth($"__dom.node('{body}').byClass('place-link').some(b => b.textContent === 'Orison')"));

        page.Do($"{Cross(body)}.find(b => b.textContent === 'Show them on Contracts').click();");
        Assert.Equal("m1,Patrol_Stanton", page.Text("contractsOnly.missions.join(',')"));
        Assert.True(page.Truth("__dom.node('#ref-modal').hidden"));
    }

    /// <summary>An item the Armoury has a row for says so on its card.</summary>
    [Fact]
    public void A_gun_card_opens_the_armoury()
    {
        var page = new Page();
        page.Serve("/api/reference/item?id=behr_rifle_tan", """
            {"className":"behr_rifle_tan","card":{"name":"P4-AR Rifle Tan"},"armoury":{"kind":"gun","class":"behr_rifle"},"shops":[],"stockOn":[]}
            """);
        page.Do("await openRefDetail('item', 'behr_rifle_tan');");

        page.Do("__dom.node('#ref-modal-body').descendants().find(n => n.tagName === 'button' && n.textContent === 'Armoury').click();");
        Assert.Equal("behr_rifle", page.Text("armouryOpenGun"));
        Assert.True(page.Truth("__dom.node('#ref-modal').hidden"));
    }
}
