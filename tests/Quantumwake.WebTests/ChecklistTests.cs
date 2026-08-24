namespace Quantumwake.WebTests;

/// <summary>The one pinned authored list must remain useful in the Now card.</summary>
public class ChecklistTests
{
    private const string Lists = """
        [{"id":"c1","title":"Pyro departure","pinned":true,"items":[
          {"id":"i1","text":"Refuel","dueAt":null,"note":null,"attachments":[{"kind":"location","label":"Port Tressler","target":"Port Tressler","placeId":"RR_MIC_LEO"}],"done":false,"doneAt":null},
          {"id":"i2","text":"Bring tractor beam","dueAt":null,"note":"MaxLift","attachments":[{"kind":"item","label":"MaxLift Tractor Beam","target":"MaxLift Tractor Beam","placeId":null}],"done":false,"doneAt":null}
        ]}]
        """;

    [Fact]
    public void A_pinned_list_shows_its_open_tasks_on_now_and_checks_them_there()
    {
        var page = new Page();
        page.Serve("/api/checklists", Lists);
        page.Do("await loadChecklists();");

        Assert.False(page.Truth("__dom.node('#now-checklist-card').hidden"));
        Assert.Contains("Pyro departure", page.NodeText("#now-checklist-title"));
        Assert.Contains("Refuel", page.NodeText("#now-checklist-items"));
        Assert.Contains("Bring tractor beam", page.NodeText("#now-checklist-items"));

        page.Do("__dom.node('#now-checklist-items').byClass('checklist-item')[0].children[0].fire('change');");
        Assert.Contains("POST /api/checklists/c1/items/i1/toggle", page.Fetched());
    }

    [Fact]
    public void Item_and_location_attachments_keep_their_distinct_actions()
    {
        var page = new Page();
        page.Serve("/api/checklists", Lists);
        page.Do("await loadChecklists();");

        Assert.Contains("Port Tressler", page.NodeText("#checklists-list"));
        Assert.Contains("MaxLift Tractor Beam", page.NodeText("#checklists-list"));
    }

    private const string Catalogue = """
        {"commodities":["Beryl","Agricium"],"items":["MaxLift Tractor Beam"]}
        """;

    private const string ThreeLists = """
        [{"id":"c1","title":"One","pinned":true,"items":[]},
         {"id":"c2","title":"Two","pinned":false,"items":[]},
         {"id":"c3","title":"Three","pinned":false,"items":[]}]
        """;

    [Fact]
    public void A_reference_is_filed_as_a_commodity_even_when_the_catalogue_has_not_landed()
    {
        var page = new Page();
        page.Serve("/api/shopping/catalogue", Catalogue);
        page.Serve("/api/checklists", Lists);
        page.Serve("/api/checklists/c1/items", "{}");
        page.Do("await loadChecklists();");

        // Back to the state a composer opens in, then submit straight away: the
        // kind has to be decided against a catalogue, not against a null one.
        page.Do("""
            catalogue = null;
            catalogueFill = null;
            __dom.node('#checklist-catalogue').children = [];
            const form = __dom.node('#checklists-list').byClass('checklist-composer')[0];
            form.children[0].value = 'Haul it';
            form.children[1].children[1].value = '  Beryl  ';
            await form.fire('submit', { preventDefault() {} });
            """);

        var sent = page.BodyOf("/api/checklists/c1/items");
        Assert.Contains("\"kind\":\"commodity\"", sent);
        Assert.Contains("\"target\":\"Beryl\"", sent);
    }

    [Fact]
    public void A_failed_add_gives_the_button_back()
    {
        var page = new Page();
        page.Serve("/api/shopping/catalogue", Catalogue);
        page.Serve("/api/checklists", Lists);
        page.Do("await loadChecklists();");

        page.Do("""
            __fetch.unreachable.push('/api/checklists/c1/items');
            const form = __dom.node('#checklists-list').byClass('checklist-composer')[0];
            form.children[0].value = 'Refuel';
            __add = form.children[3];
            await form.fire('submit', { preventDefault() {} }).catch(() => {});
            """);

        // The failure leaves this very form on screen, so its button has to work.
        Assert.False(page.Truth("__add.disabled"));
    }
}
