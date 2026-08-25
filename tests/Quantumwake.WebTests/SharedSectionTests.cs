namespace Quantumwake.WebTests;

/// <summary>
/// Other people's trades and blueprints, in sections of their own.
/// </summary>
/// <remarks>
/// The rule these pin is that an imported row never arrives in the same payload
/// as a number the page adds up. Cargo computes four totals and the Blueprints
/// picker feeds "Set as goal"; a filter remembered in five places is one
/// forgotten in exactly one of them, and the result is a lifetime earnings
/// figure counting somebody else's sales.
/// </remarks>
public class SharedSectionTests
{
    private const string Receipts = """
        [{"at":"2026-08-20T09:00:00+00:00","isSell":true,"place":"Port Tressler","scu":96,
          "amount":288000,"unitPrice":3000,"commodity":"Agricium",
          "imported":{"batchId":"9f2c1ab3","handle":"bob","importedAt":"2026-08-24T18:00:00+00:00"}},
         {"at":"2026-08-19T09:00:00+00:00","isSell":false,"place":"Lorville","scu":10,
          "amount":1000,"unitPrice":100,"commodity":null,
          "imported":{"batchId":"9f2c1ab3","handle":"bob","importedAt":"2026-08-24T18:00:00+00:00"}}]
        """;

    private const string Blueprints = """
        [{"at":"2026-02-02T00:00:00+00:00","name":"Omnisky IX",
          "imported":{"batchId":"9f2c1ab3","handle":"bob","importedAt":"2026-08-24T18:00:00+00:00"}},
         {"at":"2026-03-02T00:00:00+00:00","name":"Omnisky IX",
          "imported":{"batchId":"aa11bb22","handle":"kate","importedAt":"2026-08-24T18:00:00+00:00"}}]
        """;

    private static Page Showing()
    {
        var page = new Page();
        page.Serve("/api/imports/receipts?imported=all", Receipts);
        page.Serve("/api/imports/blueprints?imported=all", Blueprints);
        page.Do("showImported = 'all';");
        return page;
    }

    [Fact]
    public void Shared_trades_are_drawn_in_their_own_block_and_say_whose()
    {
        var page = Showing();
        page.Do("await renderSharedReceipts();");

        var text = page.NodeText("#cargo-shared");
        Assert.Contains("Trades from shared files", text);
        Assert.Contains("bob", text);
        Assert.Contains("Agricium", text);
        Assert.Contains("Port Tressler", text);
        Assert.Contains("kept out of your own totals", text);
    }

    /// <summary>
    /// A name this install cannot resolve is not an error - their dataset knew
    /// something ours does not, or the reverse.
    /// </summary>
    [Fact]
    public void A_trade_nobody_could_name_is_shown_as_unnamed_rather_than_dropped()
    {
        var page = Showing();
        page.Do("await renderSharedReceipts();");

        Assert.Contains("unnamed", page.NodeText("#cargo-shared"));
    }

    /// <summary>
    /// "Who can craft this" is the question, so the same blueprint held by two
    /// people is one line naming both.
    /// </summary>
    [Fact]
    public void Blueprints_are_grouped_by_what_they_make_and_name_who_holds_them()
    {
        var page = Showing();
        page.Do("await renderSharedBlueprints();");

        var text = page.NodeText("#blueprints-shared");
        Assert.Contains("Held by others", text);
        Assert.Contains("Omnisky IX", text);
        Assert.Contains("bob, kate", text);

        Assert.Equal(1, page.Count("__dom.node('#blueprints-shared').descendants()"
            + ".filter(n => n.tagName === 'li').length"));
    }

    /// <summary>
    /// The picker above builds a tracked build from a blueprint. One the reader
    /// does not hold would be a plan they cannot carry out.
    /// </summary>
    [Fact]
    public void Shared_blueprints_never_reach_the_goal_picker()
    {
        var page = Showing();
        page.Do("await renderSharedBlueprints();");

        Assert.DoesNotContain("Omnisky IX", page.NodeText("#jobs-blueprint"));
    }

    [Fact]
    public void With_the_switch_off_nothing_is_drawn_and_nothing_is_asked_for()
    {
        var page = new Page();
        page.Do("showImported = 'none'; await renderSharedReceipts(); await renderSharedBlueprints();");

        Assert.Equal("", page.NodeText("#cargo-shared"));
        Assert.Equal("", page.NodeText("#blueprints-shared"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/imports/receipts"));
    }

    /// <summary>
    /// The Cargo page's own totals come from a payload these rows are not in,
    /// so they cannot be counted by a page that forgot to filter.
    /// </summary>
    [Fact]
    public void The_pages_own_endpoint_is_never_asked_for_imported_rows()
    {
        var page = Showing();
        page.Do("await renderSharedReceipts();");

        Assert.DoesNotContain(page.Fetched(), url => url.StartsWith("GET /api/commodities")
            && url.Contains("imported="));
    }
}
