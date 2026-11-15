namespace Quantumwake.WebTests;

/// <summary>
/// The gaps a review of Shopping found on 2026-11-15, each closed: a saved list
/// could not be edited, no line could be ticked by hand, deleting asked
/// nothing, failures were silent, nothing totalled the list, "+ list" could not
/// choose among several, and several pinned lists made one plan each.
/// </summary>
public sealed class ShoppingListCardTests
{
    private const string Lists = """
        [{"id":"a","title":"Hermes fit","kind":"list","done":false,"pinned":true,"haveCount":1,"totalCount":3,
          "destination":"Area18","destinationId":"Stanton3_Area18",
          "items":[{"name":"SolarFlare","needed":2,"unit":"","have":false,"got":false,"where":[],"buyPrice":1000,"buyAt":"Centermass"},
                   {"name":"JS-400","needed":1,"unit":"","have":true,"got":true,"where":[]},
                   {"name":"Hadanite","needed":23,"unit":"","have":false,"got":false,"where":["Area18"],"inventoryUnconfirmed":true}]},
         {"id":"b","title":"Medical run","kind":"list","done":false,"pinned":true,"haveCount":0,"totalCount":1,
          "destination":"Lorville","destinationId":"Stanton1_Lorville",
          "items":[{"name":"SolarFlare","needed":1,"unit":"","have":false,"got":false,"where":[],"buyPrice":1000,"buyAt":"Centermass"}]}]
        """;

    private static Page Shopping()
    {
        var page = new Page();
        page.Serve("/api/jobs", Lists);
        page.Serve("/api/shopping/catalogue", """{"commodities":["Hadanite"],"items":["JS-400","SolarFlare"]}""");
        page.Do("atlas = []; await loadJobList();");
        return page;
    }

    private const string Cards = "__dom.node('#jobs-list').byClass('job-card')";

    private static string Button(int card, string label) =>
        $"{Cards}[{card}].byClass('job-head')[0].children.find(b => b.textContent === {Page.Quote(label)})";

    [Fact]
    public void A_line_ticked_by_hand_says_so_and_can_be_unticked()
    {
        var page = Shopping();
        page.Serve("/api/jobs/a/got", """{"id":"a","name":"JS-400"}""");

        Assert.Contains("marked in hand", page.NodeText("#jobs-list"));
        page.Do($"{Cards}[0].byClass('job-got').find(b => b.textContent === '✓').fire('click');");

        Assert.Equal("""{"name":"JS-400"}""", page.BodyOf("/api/jobs/a/got"));
    }

    [Fact]
    public void A_line_still_missing_can_be_ticked()
    {
        var page = Shopping();
        page.Serve("/api/jobs/a/got", """{"id":"a","name":"SolarFlare"}""");

        page.Do($"{Cards}[0].byClass('job-got')[0].fire('click');");

        Assert.Equal("""{"name":"SolarFlare"}""", page.BodyOf("/api/jobs/a/got"));
    }

    [Fact]
    public void Edit_writes_the_list_back_as_it_was_typed_and_saves_the_rewrite()
    {
        var page = Shopping();
        page.Serve("/api/jobs/a/edit", """{"id":"a"}""");

        page.Do($"await {Button(0, "Edit")}.fire('click');");
        Assert.Equal("SolarFlare 2\nJS-400 1\nHadanite 23\n", page.Text($"{Cards}[0].byClass('job-edit')[0].children[1].value"));

        page.Do($$"""
            const form = {{Cards}}[0].byClass('job-edit')[0];
            form.children[0].value = 'Hermes refit';
            form.children[1].value = 'JS-400 2\nFR-86';
            await form.fire('submit');
            """);

        Assert.Equal(
            """{"title":"Hermes refit","items":[{"name":"JS-400","needed":2,"unit":""},{"name":"FR-86","needed":0,"unit":""}]}""",
            page.BodyOf("/api/jobs/a/edit"));
    }

    [Fact]
    public void Delete_asks_first_and_a_no_deletes_nothing()
    {
        var page = Shopping();
        page.Do("globalThis.confirm = () => false;");

        page.Do($"await {Button(0, "Delete")}.fire('click');");

        Assert.DoesNotContain("DELETE /api/jobs/a", page.Fetched());
    }

    [Fact]
    public void A_change_the_server_refuses_is_said_rather_than_swallowed()
    {
        var page = Shopping();
        page.Fail("/api/jobs/a/toggle", 500, "{}");

        page.Do($"await {Button(0, "Mark done")}.fire('click');");

        Assert.Contains("did not save", page.NodeText("#jobs-alerts"));
    }

    [Fact]
    public void A_list_says_what_the_rest_should_cost_and_what_it_cannot_price()
    {
        var page = Shopping();

        var cost = page.Text($"{Cards}[0].byClass('job-cost')[0].textContent");
        Assert.Contains("at least", cost);
        Assert.Contains("1 line has no price or amount", cost);
    }

    [Fact]
    public void Two_pinned_lists_offer_one_trip_for_both_with_their_lines_added_together()
    {
        var page = Shopping();

        Assert.Contains("2 lists on Now", page.NodeText("#jobs-list"));
        Assert.Equal(
            """{"title":"Hermes fit + Medical run","destination":null,"items":[["SolarFlare",3],["Hadanite",23]]}""",
            page.Text("""
                (() => {
                  const run = pinnedRunJob(deckJobs.filter((j) => j.pinned));
                  return JSON.stringify({ title: run.title, destination: run.destination, items: run.items.map((i) => [i.name, i.needed]) });
                })()
                """));
    }

    [Fact]
    public void Plus_list_asks_which_list_when_there_are_several()
    {
        var page = Shopping();
        page.Serve("/api/jobs/collect", """{"id":"b","title":"Medical run"}""");
        page.Do("__dom.node('#test-card').append(trackButton('MedGel'));");

        page.Do("await __dom.node('#test-card').byClass('track')[0].fire('click');");
        page.Do("""
            const box = __dom.node('#test-card').byClass('track-box')[0];
            box.children[1].value = 'b';
            await box.children[2].fire('click');
            """);

        Assert.Contains("\"jobId\":\"b\"", page.BodyOf("/api/jobs/collect"));
        Assert.Contains("Medical run", page.NodeText("#test-card"));
    }

    [Fact]
    public void A_list_that_does_not_save_keeps_what_was_typed()
    {
        var page = Shopping();
        page.Fail("/api/jobs", 500, "{}");

        page.Do("""
            $('#job-items').value = 'JS-400 2';
            await $('#job-form').fire('submit');
            """);

        Assert.Equal("JS-400 2", page.Text("$('#job-items').value"));
        Assert.Contains("still here", page.NodeText("#jobs-alerts"));
    }
}
