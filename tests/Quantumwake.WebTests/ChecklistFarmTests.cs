namespace Quantumwake.WebTests;

/// <summary>
/// "Where to get these" on a checklist: the places that yield the most of the
/// list, then each line with where to find, buy or craft it. Rows from this
/// install: the Gallant rifle and the FS-9 magazine both come out of
/// Dupree Industrial's crates, and Hurston guards always carry the rifle.
/// </summary>
public class ChecklistFarmTests
{
    private const string Farm = """
        {"uex":true,"tied":14,
         "items":[
          {"written":"Gallant Rifle","itemClass":"klwe_rifle_energy_01","name":"Gallant Rifle","places":476,
           "best":{"name":"Dupree Industrial Manufacturing Facility","body":"Hurston","chance":0.0025},
           "npcs":42,"npc":{"label":"Hurston · Guard · Heavy","share":1},"ships":0,
           "buy":{"place":"Center Mass","price":4120},
           "paid":{"price":4500,"times":3,"last":"2026-10-21T00:00:00Z","shop":"Centermass, New Babbage"},"craft":null},
          {"written":"FS-9 Magazine (75 cap)","itemClass":"behr_lmg_ballistic_01_mag","name":"FS-9 Magazine (75 cap)","places":456,
           "best":{"name":"Dupree Industrial Manufacturing Facility","body":"Hurston","chance":0.1598},
           "npcs":0,"npc":null,"ships":0,"buy":null,"craft":{"craftSeconds":180,"materials":["Iron"]}}
         ],
         "places":[
          {"name":"Dupree Industrial Manufacturing Facility","body":"Hurston","id":"Stanton1_DC","spots":25,"score":0.1623,
           "items":[{"itemClass":"behr_lmg_ballistic_01_mag","name":"FS-9 Magazine (75 cap)","chance":0.1598},
                    {"itemClass":"klwe_rifle_energy_01","name":"Gallant Rifle","chance":0.0025}]},
          {"name":null,"body":"Cellin","id":"Stanton2a","spots":4,"score":0.16,
           "items":[{"itemClass":"behr_lmg_ballistic_01_mag","name":"FS-9 Magazine (75 cap)","chance":0.16}]}
         ],
         "unmatched":["Agricium"]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/checklists", """
            [{"id":"l1","title":"Pyro run","createdAt":"2026-10-01T00:00:00Z","pinned":false,"imported":null,
              "items":[
               {"id":"i1","text":"Get a rifle","dueAt":null,"note":null,
                "attachments":[{"kind":"item","label":"Gallant Rifle","target":"Gallant Rifle"}],"done":false,"doneAt":null},
               {"id":"i2","text":"FS-9 Magazine (75 cap)","dueAt":null,"note":null,"attachments":[],"done":false,"doneAt":null},
               {"id":"i3","text":"Agricium","dueAt":null,"note":null,"attachments":[],"done":false,"doneAt":null},
               {"id":"i4","text":"Medpen","dueAt":null,"note":null,"attachments":[],"done":true,"doneAt":"2026-10-02T00:00:00Z"}
              ]}]
            """);
        page.Serve("/api/loot/farm", Farm);
        page.Do("await loadChecklists();");
        return page;
    }

    private static void Open(Page page) =>
        page.Do("await __dom.node('#checklists-list').querySelectorAll('.checklist-farm-toggle')[0].fire('click');");

    /// <summary>
    /// The line's linked item is asked for by its name, an unlinked line by
    /// its text, and a ticked line not at all.
    /// </summary>
    [Fact]
    public void The_open_lines_are_asked_for_by_what_they_name()
    {
        var page = Loaded();
        Open(page);

        var sent = page.BodyOf("/api/loot/farm");
        Assert.Contains("\"Gallant Rifle\"", sent);
        Assert.Contains("\"FS-9 Magazine (75 cap)\"", sent);
        Assert.Contains("\"Agricium\"", sent);
        Assert.DoesNotContain("Medpen", sent);
        Assert.DoesNotContain("Get a rifle", sent);
    }

    [Fact]
    public void The_best_places_for_the_list_come_first()
    {
        var page = Loaded();
        Open(page);

        var body = "__dom.node('#checklists-list').querySelectorAll('tbody')[0]";
        Assert.Equal(2, page.Count($"{body}.children.length"));

        var first = page.Text($"{body}.children[0].textContent");
        Assert.Contains("Dupree Industrial Manufacturing Facility", first);
        Assert.Contains("2 of 2", first);
        Assert.Contains("Unmarked spot", page.Text($"{body}.children[1].textContent"));
        Assert.Contains("14 places tie for the best", page.NodeText("#checklists-list"));
    }

    [Fact]
    public void Each_line_says_where_to_find_buy_or_craft_it()
    {
        var page = Loaded();
        Open(page);

        var body = "__dom.node('#checklists-list').querySelectorAll('tbody')[1]";
        var rifle = page.Text($"{body}.children[0].textContent");
        Assert.Contains("Hurston · Guard · Heavy always", rifle);
        Assert.Contains("+41 more", rifle);
        Assert.Contains("Center Mass", rifle);
        // What this install was charged, under the UEX price: the one price
        // that needs no feed.
        Assert.Contains("(UEX)", rifle);
        Assert.Contains("you paid", rifle);
        Assert.Contains("at Centermass, New Babbage", rifle);

        var magazine = page.Text($"{body}.children[1].textContent");
        Assert.Contains("16%", magazine);
        Assert.Contains("not sold", magazine);
        Assert.Contains("3 min", magazine);

        // A commodity is still on the list; it is named, not dropped.
        Assert.Contains("Not matched to an item: Agricium", page.NodeText("#checklists-list"));
    }

    /// <summary>
    /// Ticking a line redraws every card. A panel that closed itself each
    /// time would have to be asked for again after every tick.
    /// </summary>
    [Fact]
    public void The_panel_stays_open_when_the_list_is_redrawn()
    {
        var page = Loaded();
        Open(page);
        page.Do("await loadChecklists();");

        Assert.False(page.Truth("__dom.node('#checklists-list').querySelectorAll('.checklist-farm')[0].hidden"));
        Assert.Contains("Best places for this list", page.NodeText("#checklists-list"));
    }
}
