namespace Quantumwake.WebTests;

/// <summary>
/// The Flight pages in French: Logbook, Sessions, Servers, Places, Points,
/// Crew and Casualties.
/// </summary>
/// <remarks>
/// Ships, places, shards, handles and items stay as the game and the pilot
/// wrote them. The server writes two kinds of English these pages show - a
/// logbook session line, and the live feed's sentences in a debrief's
/// highlights - and both are translated on the page by their shape, keeping
/// the names inside them.
/// </remarks>
public class FlightFrenchTests
{
    [Fact]
    public void The_logbook_translates_the_servers_session_lines_and_keeps_the_ship()
    {
        var page = new Page("fr");
        page.Serve("/api/logbook?days=0", """
            [
              { "at": "2026-09-27T15:00:00Z", "kind": "session", "what": "Session aboard Drake Corsair", "place": "Orison", "detail": "1h 20m · 3 jumps · 1 death", "amount": null },
              { "at": "2026-09-27T14:30:00Z", "kind": "session", "what": "Session on foot", "place": "", "detail": "0h 05m · 0 jumps", "amount": null },
              { "at": "2026-09-27T14:00:00Z", "kind": "sold", "what": "Agricium", "place": "Area18", "detail": "TDD", "amount": 250000 },
              { "at": "2026-09-27T12:00:00Z", "kind": "loot", "what": "Coda Pistol", "place": "Orison", "detail": "first seen", "amount": null }
            ]
            """);

        page.Do("await loadLogbook();");
        var feed = page.NodeText("#logbook-feed");

        // French puts a no-break space before a colon, as the catalogue does.
        Assert.Equal("Session à bord : Drake Corsair", page.NodeText("#logbook-latest-title"));
        Assert.Equal("2 sessions · 1 échange enregistré · 1 découverte", page.NodeText("#logbook-activity-title"));
        Assert.Contains("Solde de 1 échange enregistré", page.NodeText("#logbook-net-detail"));
        Assert.Contains("1h 20m · 3 sauts · 1 mort", feed);
        Assert.Contains("0h 05m · 0 saut", feed);
        Assert.Contains("Session à pied", feed);
        Assert.Contains("première apparition", feed);
        Assert.Contains("butin", feed);

        // The shop's name is not a sentence, and the item is the game's.
        Assert.Contains("TDD", feed);
        Assert.Contains("Coda Pistol", feed);
        Assert.DoesNotContain("jumps", feed);
    }

    [Fact]
    public void A_session_debrief_reads_in_french_with_its_feed_sentences_translated()
    {
        var page = new Page("fr");
        page.Serve("/api/sessions/s1", """
            {"id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z","gameVersion":"4.9",
             "ships":[{"displayName":"RSI Zeus","sorties":2}],
             "locations":[{"at":"2026-08-20T20:05:00Z","rawId":"RR_MIC_LEO","displayName":"Port Tressler","system":"Stanton","body":"microTech"}],
             "jumps":[],
             "contracts":[{"displayName":"Supply run","outcome":"Completed","steps":2,"stepsDone":2}],
             "timeline":[{"at":"2026-08-20T21:10:00Z","kind":"ship","text":"Left RSI Zeus"}],
             "purchases":[],"trades":[{"at":"2026-08-20T21:00:00Z","amount":4000,"quantity":8,"isSell":true}],
             "partyNotes":[{"at":"2026-08-20T20:20:00Z","handle":"B-Kon","moment":"Connected"}],
             "spend":0,"income":4000,"commoditySpend":0,"deaths":0}
            """);

        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300,
              primaryShip:'RSI Zeus', lastLocation:'Port Tressler', jumps:1, contracts:1, deaths:0, incapacitations:0 }];
            await toggleSessionDebrief('s1');
            """);
        var text = page.NodeText("#sessions-table tbody");

        Assert.Contains("RSI Zeus · 2 sorties", text);
        Assert.Contains("Cargaison vendue · 8 SCU", text);
        Assert.Contains("1 nommé", text);
        Assert.Contains("2 / 2 étapes", text);

        // One contract is "terminé", not the Contracts filter's plural.
        Assert.Contains("Terminé", text);
        Assert.DoesNotContain("Terminés", text);

        Assert.Contains("Débarqué : RSI Zeus", text);
        Assert.Contains("Supply run", text);
        Assert.DoesNotContain("Cargo sold", text);
        Assert.Equal("1 session · 1h 25m en jeu · 1 saut · 1 contrat", page.NodeText("#lib-summary"));
    }

    [Fact]
    public void Servers_read_in_french_and_the_pasted_report_stays_english()
    {
        var page = new Page("fr");
        page.Serve("/api/servers", """
            {"servers":[{"shard":"pub_use1b_12545750_150","region":"US East","deployment":"12545750","current":true,
              "visits":3,"time":5400,"last":"2026-08-20T21:00:00Z","lastEnding":"Left","lastDuration":1800,
              "endings":{"Left":2,"LogEnded":1},"places":[],"favorite":false,"note":null}],
             "newestDeployment":"12545750","current":null}
            """);

        page.Do("await loadServers();");
        var table = page.NodeText("#servers-table");

        Assert.Contains("Shards visités", page.NodeText("#servers-summary"));
        Assert.Contains("sur aucun shard", page.NodeText("#servers-summary"));
        Assert.Contains("vous êtes parti", table);
        Assert.Contains("2× vous êtes parti", table);
        Assert.Contains("1× journal interrompu", table);
        Assert.Contains("déploiement 12545750", table);
        Assert.Contains("aucun lieu nommé", table);
        Assert.Contains("pub_use1b_12545750_150", table);

        var report = page.Text("serverReport(serverRows[0])");
        Assert.Contains("Quantumwake server report", report);
        Assert.Contains("you left", report);
    }

    [Fact]
    public void Crew_reads_in_french_and_keeps_the_handles()
    {
        var page = new Page("fr");
        page.Serve("/api/crew?days=0", """
            [{"handle":"Velaris","sessions":6,"connected":1,"dropped":4,"ledParty":2,
              "joined":3,"left":4,"first":"2026-05-01T00:00:00+00:00","last":"2026-08-01T00:00:00+00:00"}]
            """);
        page.Serve("/api/crew/ships?days=0", "[]");

        page.Do("__dom.node('#crew-period').value = '0'; await loadCrew();");

        Assert.Contains("Personnes nommées", page.NodeText("#crew-summary"));
        Assert.Contains("6 sessions", page.NodeText("#crew-chart"));
        Assert.Contains("1 connexion", page.NodeText("#crew-chart"));
        Assert.Contains("Velaris", page.NodeText("#crew-table"));
    }

    [Fact]
    public void Casualties_read_in_french_with_the_beds_worded_around_their_counts()
    {
        var page = new Page("fr");
        page.Serve("/api/casualties?days=0", """
            {"deaths":12,"incapacitations":31,"sessionsWithDeaths":7,"estimatedFees":48000,
             "byPlace":[],"byShip":[],"wokeAt":[],"fees":[],
             "bedsUsed":[{"place":"Port Tressler","times":3,"afterDeath":1}],
             "bedKinds":{"afterDeath":1,"heal":0,"hab":0,"wake":3,"clinicsKnown":true},
             "lastWokeAt":null}
            """);

        page.Do("__dom.node('#casualties-period').value = '0'; await loadCasualties();");

        Assert.Contains("Sessions avec une mort", page.NodeText("#casualties-summary"));
        Assert.Contains("Port Tressler · 1 après une mort", page.NodeText("#casualties-beds"));
        Assert.Contains("réveils à la connexion : 3", page.NodeText("#beds-kinds"));
        Assert.Contains("Rien à déduire", page.NodeText("#casualties-woke-note"));
    }

    [Fact]
    public void A_point_reads_in_french_and_its_category_is_stored_in_english()
    {
        var page = new Page("fr");
        page.Serve("/api/screen/pins", """
            [{"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":14.99996,
              "believed":"Ruin Station","system":"Pyro","label":"Ruin mining shelf","category":"Meet point",
              "note":null,"believedBy":"Jump","believedAt":"2026-09-09T01:30:00Z"},
             {"sourceAt":"2026-09-08T20:00:00Z","x":1,"y":2,"z":3,"gigametres":0.001,
              "believed":null,"system":"Stanton","label":null,"category":"General","note":null}]
            """);
        page.Do("allSessions = []; await loadPoints();");
        var list = page.NodeText("#points-list");

        Assert.Contains("15.0000 Gm du centre du système", list);
        Assert.Contains("probablement Pyro › Ruin Station au moment de la copie", list);
        Assert.Contains("d’après un saut quantique 40 min plus tôt", list);
        Assert.Equal("2 conservés", page.NodeText("#points-count"));
        Assert.Contains("Point de rendez-vous · 1", page.NodeText("#points-categories"));

        // The option shows French, the value sent back is the stored English.
        Assert.Equal("Meet point", page.Text("__dom.node('#points-list').byClass('point-category')[0].value"));
    }
}
