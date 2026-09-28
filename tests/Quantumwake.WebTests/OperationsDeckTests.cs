namespace Quantumwake.WebTests;

/// <summary>
/// The Operations deck must describe live work, rather than becoming six
/// decorative links that leave an active list or contract hidden below it.
/// </summary>
public class OperationsDeckTests
{
    [Fact]
    public void Supply_card_opens_the_composer_and_the_header_can_close_it()
    {
        var page = new Page();

        page.Do("__dom.node('#operations-new-list').fire('click');");
        Assert.False(page.Truth("__dom.node('#job-form').hidden"));

        page.Do("__dom.node('#jobs-new').fire('click');");
        Assert.True(page.Truth("__dom.node('#job-form').hidden"));
    }

    [Fact]
    public void Shopping_card_names_open_lists_and_the_items_still_missing()
    {
        var page = new Page();
        page.Serve("/api/jobs", """
            [{"id":"restock","title":"Restock","kind":"list","done":false,
              "haveCount":1,"totalCount":4,"items":[]}]
            """);

        page.Do("await loadJobList();");

        Assert.Equal("1 active list · 3 items to find",
            page.NodeText("#operations-shopping-status"));
    }

    [Fact]
    public void Contract_card_says_when_no_live_session_can_supply_work()
    {
        var page = new Page();
        page.Serve("/api/now", """{"connected":true,"inGame":false}""");

        page.Do("await loadJobContracts();");

        Assert.Equal("Waiting for a game session",
            page.NodeText("#operations-contract-status"));
    }

    [Fact]
    public void Contract_card_counts_the_work_that_is_live_in_this_session()
    {
        var page = new Page();
        page.Serve("/api/now", """{"connected":true,"inGame":true,"sessionStarted":"2026-09-27T12:00:00Z"}""");
        page.Serve("/api/contracts?days=2", """
            [{"at":"2026-09-27T12:05:00Z","name":"Recover cargo","issuer":"Covalex",
              "type":"Recovery","system":"Stanton","outcome":"InProgress","steps":0,
              "stepsDone":0,"hauling":false}]
            """);
        page.Serve("/api/haul/plan", "null");

        page.Do("await loadJobContracts();");

        Assert.Equal("1 live contract", page.NodeText("#operations-contract-status"));
    }
}
