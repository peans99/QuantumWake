namespace Quantumwake.WebTests;

/// <summary>The pinned authored lists must remain useful in the Now card.</summary>
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

    /// <summary>
    /// Two pinned checklists share the card (1.2.46): each under its own name,
    /// a finished one as a tick, and ticking still reaches the right list.
    /// </summary>
    [Fact]
    public void Pinned_lists_share_the_now_card_under_their_names()
    {
        var page = new Page();
        page.Serve("/api/checklists", """
            [{"id":"c1","title":"Pyro departure","pinned":true,"items":[
               {"id":"i1","text":"Refuel","attachments":[],"done":false},
               {"id":"i2","text":"Bring tractor beam","attachments":[],"done":false}]},
             {"id":"c2","title":"Bunker op","pinned":true,"items":[
               {"id":"i3","text":"Medpens","attachments":[],"done":false}]},
             {"id":"c3","title":"Hangar tidy","pinned":true,"items":[
               {"id":"i4","text":"Sell junk","attachments":[],"done":true}]},
             {"id":"c4","title":"Not pinned","pinned":false,"items":[
               {"id":"i5","text":"Hidden task","attachments":[],"done":false}]}]
            """);
        page.Do("await loadChecklists();");

        var items = page.NodeText("#now-checklist-items");
        Assert.Equal("3 pinned checklists", page.NodeText("#now-checklist-title"));
        Assert.Contains("Pyro departure", items);
        Assert.Contains("Refuel", items);
        Assert.Contains("Bunker op", items);
        Assert.Contains("Medpens", items);
        Assert.Contains("all done", items);
        Assert.DoesNotContain("Sell junk", items);
        Assert.DoesNotContain("Hidden task", items);

        page.Do("__dom.node('#now-checklist-items').byClass('checklist-item').find(r => r.textContent.includes('Medpens')).children[0].fire('change');");
        Assert.Contains("POST /api/checklists/c2/items/i3/toggle", page.Fetched());
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

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/shopping/catalogue", Catalogue);
        page.Serve("/api/checklists", Lists);
        page.Do("await loadChecklists();");
        return page;
    }

    private const string DeleteButton =
        "__dom.node('#checklists-list').byClass('checklist-card')[0].byClass('danger')[0]";

    /// <summary>Delete took a whole checklist on one click until 1.2.54.</summary>
    [Fact]
    public void Delete_asks_first_and_a_no_deletes_nothing()
    {
        var page = Loaded();
        page.Do($"globalThis.confirm = () => false; await {DeleteButton}.fire('click');");

        Assert.DoesNotContain("DELETE /api/checklists/c1", page.Fetched());
    }

    [Fact]
    public void A_confirmed_delete_is_sent()
    {
        var page = Loaded();
        page.Serve("/api/checklists/c1", "{}");
        page.Do($"globalThis.confirm = (q) => {{ __asked = q; return true; }}; await {DeleteButton}.fire('click');");

        Assert.Contains("DELETE /api/checklists/c1", page.Fetched());
        Assert.Contains("Pyro departure", page.Text("__asked"));
        Assert.Contains("2 tasks", page.Text("__asked"));
    }

    [Fact]
    public void A_tick_the_server_refuses_is_said_rather_than_swallowed()
    {
        var page = Loaded();
        page.Fail("/api/checklists/c1/items/i1/toggle", 500, "{}");

        page.Do("await __dom.node('#checklists-list').byClass('checklist-item')[0].children[0].fire('change');");

        Assert.Contains("did not save", page.NodeText("#checklists-alerts"));
    }

    [Fact]
    public void A_task_that_does_not_save_keeps_what_was_typed()
    {
        var page = Loaded();
        page.Fail("/api/checklists/c1/items", 500, "{}");

        page.Do("""
            __form = __dom.node('#checklists-list').byClass('checklist-composer')[0];
            __form.children[0].value = 'Bring medpens';
            await __form.fire('submit', { preventDefault() {} });
            """);

        Assert.Contains("still here", page.NodeText("#checklists-alerts"));
        Assert.Equal("Bring medpens",
            page.Text("__dom.node('#checklists-list').byClass('checklist-composer')[0].children[0].value"));
    }

    [Fact]
    public void A_checklist_that_does_not_save_keeps_its_title()
    {
        var page = Loaded();
        page.Fail("/api/checklists", 500, "{}");

        page.Do("""
            $('#checklist-title').value = 'Bunker op';
            await $('#checklist-create').fire('click');
            """);

        Assert.Equal("Bunker op", page.Text("$('#checklist-title').value"));
        Assert.Contains("still here", page.NodeText("#checklists-alerts"));
    }
}
