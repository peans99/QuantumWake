namespace Quantumwake.WebTests;

/// <summary>
/// The page for files other pilots have shared.
/// </summary>
/// <remarks>
/// What it has to make obvious: whose a file is, how old the data in it is
/// rather than how old the file is, what could not be read, and that removing it
/// is one click and takes the whole thing.
/// </remarks>
public class ImportPageTests
{
    private const string OneBatch = """
        {"batches":[{
          "id":"ee3ad3bf","importedAt":"2026-08-24T18:45:25+00:00",
          "exportedAt":"2026-03-02T09:00:00+00:00",
          "handle":"nekron","note":"Tuesday hauling","sourceName":"nekron-share.json",
          "formatVersion":1,"contentVersion":1,"producerVersion":"0.8.4",
          "classes":["receipts","blueprints"],
          "counts":{"receipts":41,"blueprints":7,"jobs":0,"checklists":0,"trips":0},
          "rejected":{"receipts":2,"blueprints":0,"jobs":0,"checklists":0,"trips":0},
          "truncated":{"receipts":0,"blueprints":0,"jobs":0,"checklists":0,"trips":0},
          "hidden":false,"readable":true}]}
        """;

    private static Page With(string json)
    {
        var page = new Page();
        page.Serve("/api/imports", json);
        page.Do("await loadImports();");
        return page;
    }

    [Fact]
    public void A_file_says_whose_it_is_and_what_is_in_it()
    {
        var page = With(OneBatch);
        var text = page.NodeText("#imports-list");

        Assert.Contains("nekron", text);
        Assert.Contains("Tuesday hauling", text);
        Assert.Contains("41 trades", text);
        Assert.Contains("7 blueprints", text);
        Assert.Contains("nekron-share.json", text);
    }

    /// <summary>
    /// A file taken this morning can hold a price from March, so both dates are
    /// on the card and the older one is not hidden behind the newer.
    /// </summary>
    [Fact]
    public void The_age_of_the_data_is_shown_beside_the_age_of_the_file()
    {
        var text = With(OneBatch).NodeText("#imports-list");

        Assert.Contains("imported", text);
        Assert.Contains("written", text);
        Assert.Contains("2026", text);
    }

    /// <summary>
    /// "Why does this say 41 when his file said 43" has to be answerable on
    /// screen a month later, so a drop is never silent.
    /// </summary>
    [Fact]
    public void What_could_not_be_read_is_said_rather_than_quietly_missing()
    {
        Assert.Contains("Could not be read: 2 trades", With(OneBatch).NodeText("#imports-list"));
    }

    [Fact]
    public void Removing_a_file_asks_the_server_to_remove_that_file()
    {
        var page = With(OneBatch);
        page.Do("__dom.node('#imports-list').byClass('import-card')[0].byClass('danger')[0].fire('click');");

        Assert.Contains("DELETE /api/imports/ee3ad3bf", page.Fetched());
    }

    [Fact]
    public void One_class_can_go_without_taking_the_rest()
    {
        var page = With(OneBatch);
        page.Do("__dom.node('#imports-list').byClass('import-classes')[0].children[0].fire('click');");

        Assert.Contains("DELETE /api/imports/ee3ad3bf/receipts", page.Fetched());
    }

    /// <summary>
    /// Hiding and removing answer different questions, and a page offering only
    /// the second gets used for the first.
    /// </summary>
    [Fact]
    public void Hiding_is_offered_separately_from_removing()
    {
        var page = With(OneBatch);
        page.Do("__dom.node('#imports-list').byClass('import-card')[0].byClass('tiny')[0].fire('click');");

        Assert.Contains("POST /api/imports/ee3ad3bf/hide", page.Fetched());
    }

    /// <summary>
    /// A session cache can be dropped because the logs rebuild it. A file from
    /// somebody else cannot, so it stays and explains itself.
    /// </summary>
    [Fact]
    public void A_file_this_build_cannot_read_still_says_what_it_is()
    {
        var page = With("""
            {"batches":[{
              "id":"aa11","importedAt":"2026-08-24T18:45:25+00:00",
              "exportedAt":"2026-08-24T18:00:00+00:00",
              "handle":"someone","sourceName":"newer.json",
              "formatVersion":9,"contentVersion":3,"producerVersion":"9.0.0",
              "classes":["receipts"],
              "counts":{"receipts":12,"blueprints":0,"jobs":0,"checklists":0,"trips":0},
              "rejected":{"receipts":0,"blueprints":0,"jobs":0,"checklists":0,"trips":0},
              "truncated":{"receipts":0,"blueprints":0,"jobs":0,"checklists":0,"trips":0},
              "hidden":false,"readable":false}]}
            """);

        var text = page.NodeText("#imports-list");

        Assert.Contains("cannot be read", text);
        Assert.Contains("format 9", text);
        Assert.Contains("kept rather than dropped", text);
        Assert.Contains("someone", text);
    }

    [Fact]
    public void An_empty_page_says_what_would_happen_rather_than_nothing()
    {
        var text = With("{\"batches\":[]}").NodeText("#imports-list");

        Assert.Contains("No shared files yet", text);
        Assert.Contains("separate from your own history", text);
    }

    /// <summary>
    /// Falling back to empty would have erased them, so an unreadable store is
    /// kept and the page is the thing that says so.
    /// </summary>
    [Fact]
    public void A_quarantined_store_is_reported_on_the_page()
    {
        var text = With("{\"batches\":[],\"quarantined\":\"imports.json.corrupt-20260824-184500\"}")
            .NodeText("#imports-list");

        Assert.Contains("could not be read", text);
        Assert.Contains("imports.json.corrupt-20260824-184500", text);
        Assert.Contains("rather than overwritten", text);
    }

    [Fact]
    public void Picking_a_file_posts_its_text_with_the_name_it_had()
    {
        var page = With("{\"batches\":[]}");
        page.Serve("/api/imports", "{\"batches\":[]}");
        page.Do("""
            __fetch.routes['/api/imports'] = { batch: {
              id: 'new1', importedAt: '2026-08-24T18:45:25+00:00', handle: 'nekron',
              counts: { receipts: 3, blueprints: 0, jobs: 0, checklists: 0, trips: 0 } } };
            await importFile({ name: 'friend.json', text: '{"format":"quantumwake.export"}' });
            """);

        var sent = page.BodyOf("/api/imports");
        Assert.Contains("quantumwake.export", sent);
        Assert.Contains("friend.json", sent);
        Assert.Contains("Imported 3 trades from nekron", page.NodeText("#imports-status"));
    }
}
