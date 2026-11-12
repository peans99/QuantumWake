namespace Quantumwake.WebTests;

/// <summary>
/// The Command deck leads with work under way (1.2.37): the tracked plan,
/// the supplies still to find, an open checklist, refinery orders and the
/// event being played - each a card that opens its page, drawn only when
/// there is something to continue.
/// </summary>
public class ContinueWorkTests
{
    private const string Trips = """
        [{"id":"t1","title":"Agricium run","createdAt":"2026-11-11T09:00:00Z","tracked":true,"archived":"No",
          "stops":[{"id":"s1","placeId":"L","place":"Lorville","note":null,"done":true,"actions":[]},
                   {"id":"s2","placeId":"A","place":"Area18","note":null,"done":false,"actions":[]}]}]
        """;

    private const string Checklists = """
        [{"id":"c1","title":"Before a bunker","createdAt":"2026-11-10T09:00:00Z","pinned":true,
          "items":[{"id":"i1","text":"Medpens","done":true,"attachments":[]},{"id":"i2","text":"Ammo","done":false,"attachments":[]}]}]
        """;

    private static Page Deck(string trips = "[]", string checklists = "[]", string refinery = """{"orders":[],"unmatched":[]}""")
    {
        var page = new Page();
        page.Serve("/api/trips", trips);
        page.Serve("/api/checklists", checklists);
        page.Serve("/api/mining/refinery", refinery);
        page.Serve("/api/events", """{"events":[],"open":[]}""");
        return page;
    }

    private const string Host = "#operations-continue";

    [Fact]
    public void Work_under_way_leads_the_deck_one_card_each()
    {
        var page = Deck(Trips, Checklists);
        page.Do("""
            deckJobs = [{ id: 'j1', title: 'Bunker kit', kind: 'shopping', done: false, haveCount: 3, totalCount: 7 },
                        { id: 'j2', title: 'Paint', kind: 'shopping', done: false, haveCount: 0, totalCount: 1 }];
            await renderContinueWork();
            """);

        var text = page.NodeText(Host);
        Assert.Contains("Agricium run", text);
        Assert.Contains("Next: Area18", text);
        Assert.Contains("1 of 2 stops", text);
        Assert.Contains("Bunker kit", text);
        Assert.Contains("3 of 7 in hand", text);
        Assert.Contains("1 more list", text);
        Assert.Contains("Before a bunker", text);
        Assert.Contains("1 of 2 ticked", text);
        Assert.Equal(3, page.Count($"__dom.node('{Host}').byClass('continue-card').length"));
    }

    [Fact]
    public void A_refinery_order_waiting_is_a_card_of_its_own()
    {
        var page = Deck(refinery: """
            {"orders":[{"station":"ARC-L1 Wide Forest Station","seenAt":"2026-11-11T08:00:00Z","shot":"x.jpg","basis":"terminal",
                        "dueAt":"2026-11-11T09:00:00Z","completedAt":"2026-11-11T09:00:00Z","lots":[]}],"unmatched":[]}
            """);
        page.Do("await renderContinueWork();");

        var text = page.NodeText(Host);
        Assert.Contains("Refinery", text);
        Assert.Contains("ARC-L1 Wide Forest Station", text);
        Assert.True(page.Truth($"__dom.node('{Host}').byClass('continue-ready').length === 1"));
    }

    [Fact]
    public void Nothing_under_way_says_so_rather_than_drawing_empty_cards()
    {
        var page = Deck();
        page.Do("deckJobs = []; await renderContinueWork();");

        Assert.Contains("Nothing under way", page.NodeText(Host));
        Assert.Equal(0, page.Count($"__dom.node('{Host}').byClass('continue-card').length"));
    }

    [Fact]
    public void A_finished_plan_and_finished_lists_are_not_work_under_way()
    {
        var page = Deck(Trips.Replace("\"done\":false", "\"done\":true"), Checklists.Replace("\"done\":false", "\"done\":true"));
        page.Do("deckJobs = [{ id: 'j1', title: 'Done list', kind: 'shopping', done: true }, { id: 'j2', title: 'A build', kind: 'craft', done: false }]; await renderContinueWork();");

        Assert.Contains("Nothing under way", page.NodeText(Host));
    }
}
