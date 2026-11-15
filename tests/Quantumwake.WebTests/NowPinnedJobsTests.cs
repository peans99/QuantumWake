namespace Quantumwake.WebTests;

/// <summary>
/// Several shopping lists can be pinned to Now (1.2.43). The card is also what
/// the overlay shows mid-flight, so what matters is that it stays a glance:
/// one list reads as before, several are grouped by list, and a finished list
/// shrinks to a tick rather than taking lines from the others.
/// </summary>
public sealed class NowPinnedJobsTests
{
    private static string Job(string id, string title, bool pinned, params (string Name, bool Have)[] items) =>
        $$"""
        {"id":"{{id}}","title":"{{title}}","kind":"list","done":false,"pinned":{{(pinned ? "true" : "false")}},
         "haveCount":{{items.Count(i => i.Have)}},"totalCount":{{items.Length}},
         "items":[{{string.Join(",", items.Select(i => $$"""{"name":"{{i.Name}}","needed":1,"unit":"","have":{{(i.Have ? "true" : "false")}}}"""))}}]}
        """;

    [Fact]
    public void One_pinned_list_reads_as_it_always_did()
    {
        var page = new Page();
        page.Do($"await renderPinnedJob([{Job("a", "Hermes fit", true, ("SolarFlare", false), ("JS-400", true))}]);");

        Assert.False(page.Truth("__dom.node('#now-job-card').hidden"));
        Assert.Equal("Hermes fit", page.NodeText("#now-job-title"));
        Assert.Contains("SolarFlare", page.NodeText("#now-job-items"));
        Assert.DoesNotContain("JS-400", page.NodeText("#now-job-items"));
    }

    [Fact]
    public void Every_pinned_list_is_shown_under_its_own_name()
    {
        var page = new Page();
        page.Do($"""
            await renderPinnedJob([
              {Job("a", "Hermes fit", true, ("SolarFlare", false))},
              {Job("b", "Medical run", true, ("MedGel", false), ("Hemozal", true))},
              {Job("c", "Not pinned", false, ("Quantanium", false))}]);
            """);

        var items = page.NodeText("#now-job-items");
        Assert.Equal("2 pinned lists", page.NodeText("#now-job-title"));
        Assert.Contains("Hermes fit", items);
        Assert.Contains("SolarFlare", items);
        Assert.Contains("Medical run", items);
        Assert.Contains("MedGel", items);
        Assert.DoesNotContain("Quantanium", items);
        Assert.Contains("1 of 3 in hand", page.NodeText("#now-job-progress"));
    }

    [Fact]
    public void A_finished_pinned_list_shrinks_to_a_tick()
    {
        var page = new Page();
        page.Do($"""
            await renderPinnedJob([
              {Job("a", "Hermes fit", true, ("SolarFlare", true))},
              {Job("b", "Medical run", true, ("MedGel", false))}]);
            """);

        var items = page.NodeText("#now-job-items");
        Assert.Contains("all in hand", items);
        Assert.DoesNotContain("SolarFlare", items);
        Assert.Contains("MedGel", items);
    }

    /// <summary>Four lists of five missing items is not a glance: each gets its share of six lines.</summary>
    [Fact]
    public void Each_list_is_cut_to_its_share_of_the_card()
    {
        var page = new Page();
        var missing = Enumerable.Range(1, 5).Select(n => ($"Part{n}", false)).ToArray();
        page.Do($"await renderPinnedJob([{Job("a", "One", true, missing)},{Job("b", "Two", true, missing)},{Job("c", "Three", true, missing)}]);");

        Assert.Equal(3, page.Count("__dom.node('#now-job-items').textContent.split('+3 more').length - 1"));
        Assert.Contains("Part2", page.NodeText("#now-job-items"));
        Assert.DoesNotContain("Part3", page.NodeText("#now-job-items"));
    }
}
