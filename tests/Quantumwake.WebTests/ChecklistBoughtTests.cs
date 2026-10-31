namespace Quantumwake.WebTests;

/// <summary>
/// A shopping line the logs show was bought.
/// </summary>
/// <remarks>
/// Shown as bought rather than ticked in the store. The list belongs to whoever
/// wrote it, and a name matching a receipt is something the logs observed about
/// it — not licence to edit it. Ticking stays the reader's to do.
/// </remarks>
public class ChecklistBoughtTests
{
    private static Page Loaded(string items)
    {
        var page = new Page();
        page.Serve("/api/checklists", $$"""
            [{"id":"l1","title":"Shopping","createdAt":"2026-08-01T00:00:00Z",
              "pinned":true,"items":{{items}},"imported":null}]
            """);
        page.Do("await loadChecklists();");
        return page;
    }

    [Fact]
    public void A_line_whose_item_was_bought_says_so()
    {
        var page = Loaded("""
            [{"id":"i1","text":"7MA 'Lorica'","dueAt":null,"note":null,
              "attachments":[{"kind":"item","label":"7MA 'Lorica'","target":"7MA 'Lorica'"}],
              "done":false,"doneAt":null,"bought":"2026-08-29T10:00:00Z"}]
            """);

        Assert.Contains("bought", page.NodeText("#checklists-list"));
    }

    /// <summary>
    /// Nothing bought must read exactly as it did before, or every line on
    /// every list gains a note about a purchase that did not happen.
    /// </summary>
    [Fact]
    public void A_line_with_no_purchase_is_untouched()
    {
        var page = Loaded("""
            [{"id":"i1","text":"Buy a shield","dueAt":null,"note":null,
              "attachments":[],"done":false,"doneAt":null,"bought":null}]
            """);

        Assert.DoesNotContain("bought", page.NodeText("#checklists-list"));
    }

    /// <summary>
    /// A line already ticked is finished, and saying it was also bought adds
    /// nothing but noise.
    /// </summary>
    [Fact]
    public void A_line_already_ticked_says_nothing_further()
    {
        var page = Loaded("""
            [{"id":"i1","text":"7MA 'Lorica'","dueAt":null,"note":null,
              "attachments":[],"done":true,"doneAt":"2026-08-29T11:00:00Z",
              "bought":"2026-08-29T10:00:00Z"}]
            """);

        Assert.DoesNotContain("bought", page.NodeText("#checklists-list"));
    }

    /// <summary>
    /// An item that turned up in an inventory since the line was written is
    /// noted - not struck, because a listing is not a receipt.
    /// </summary>
    [Fact]
    public void A_line_whose_item_turned_up_says_so_without_striking_it()
    {
        var page = Loaded("""
            [{"id":"i1","text":"Gallant Rifle","dueAt":null,"note":null,
              "attachments":[{"kind":"item","label":"Gallant Rifle","target":"Gallant Rifle"}],
              "done":false,"doneAt":null,"bought":null,"found":"2026-10-29T10:00:00Z","foundBefore":true}]
            """);

        var list = page.NodeText("#checklists-list");
        Assert.Contains("in your inventory", list);
        Assert.Equal(0, page.Count("__dom.node('#checklists-list').querySelectorAll('.bought').length"));
        Assert.Contains("already had", page.Text("__dom.node('#checklists-list').querySelectorAll('.checklist-found')[0].title"));
    }
}
