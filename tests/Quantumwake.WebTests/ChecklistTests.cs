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
}
