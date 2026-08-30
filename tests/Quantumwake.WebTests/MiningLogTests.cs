namespace Quantumwake.WebTests;

/// <summary>
/// The mining record the pilot keeps, because the game keeps none.
/// </summary>
public class MiningLogTests
{
    private static Page Loaded(string runs)
    {
        var page = new Page();
        page.Serve("/api/mining/log", runs);
        page.Do("await loadMiningLog();");
        return page;
    }

    [Fact]
    public void A_recorded_run_is_listed()
    {
        var page = Loaded("""
            [{"id":"a1","at":"2026-08-29T10:00:00Z","place":"Aberdeen","resource":"Hadanite",
              "scu":12,"quality":740,"revenue":480000,"note":null}]
            """);

        var body = page.NodeText("#mining-log tbody");

        Assert.Contains("Hadanite", body);
        Assert.Contains("Aberdeen", body);
        Assert.Contains("740", body);
    }

    /// <summary>
    /// These figures are typed, and the ones above them on the page are read
    /// from logs. Saying which is which is the whole reason they can share a
    /// page at all.
    /// </summary>
    [Fact]
    public void The_total_says_it_is_your_own_record()
    {
        var page = Loaded("""
            [{"id":"a1","at":"2026-08-29T10:00:00Z","place":"Aberdeen","resource":"Hadanite",
              "scu":12,"quality":740,"revenue":480000,"note":null},
             {"id":"a2","at":"2026-08-28T10:00:00Z","place":"Daymar","resource":"Aphorite",
              "scu":8,"quality":null,"revenue":null,"note":null}]
            """);

        var note = page.NodeText("#mining-log-note");

        Assert.Contains("2 runs", note);
        Assert.Contains("20 SCU", note);
        Assert.Contains("your own record", note);
    }

    [Fact]
    public void Nothing_recorded_says_so_rather_than_showing_an_empty_table()
    {
        Assert.Contains("Nothing recorded", Loaded("[]").NodeText("#mining-log-note"));
    }
}
