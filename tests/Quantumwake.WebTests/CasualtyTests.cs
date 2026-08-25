namespace Quantumwake.WebTests;

/// <summary>
/// Casualties: what the logs can and cannot say about dying.
/// </summary>
/// <remarks>
/// This page is the clearest case in the app of a number that must not be
/// rounded into a claim. Deaths are inferred from corpse bursts rather than
/// logged, claim fees are an estimate against a table, and 4.9 removed combat
/// telemetry outright - so a zero here usually means "the game stopped saying"
/// rather than "it did not happen". The page has to keep those apart.
/// </remarks>
public class CasualtyTests
{
    private const string Data = """
        {"deaths":12,"incapacitations":31,"sessionsWithDeaths":7,"estimatedFees":48000,
         "byPlace":[{"place":"Port Tressler","deaths":5},{"place":"Lorville","deaths":3}],
         "byShip":[{"ship":"Drake Cutlass Black","deaths":4}],
         "bedsUsed":[],"bedKinds":[],"wokeAt":[],
         "fees":[{"name":"Drake Cutlass Black","fee":24000}]}
        """;

    private static Page Loaded(string data = Data)
    {
        var page = new Page();
        page.Serve("/api/casualties?days=0", data);
        page.Do("__dom.node('#casualties-period').value = '0'; await loadCasualties();");
        return page;
    }

    [Fact]
    public void Deaths_incapacitations_and_sessions_are_reported_apart()
    {
        var summary = Loaded().NodeText("#casualties-summary");

        Assert.Contains("Deaths", summary);
        Assert.Contains("12", summary);
        Assert.Contains("Incapacitations", summary);
        Assert.Contains("31", summary);
        Assert.Contains("Sessions with a death", summary);
    }

    /// <summary>
    /// A fee of nothing is not a free death - it is a claim table that had
    /// nothing to say about the ship, so it draws as a dash.
    /// </summary>
    [Fact]
    public void An_unpriced_claim_is_a_dash_rather_than_zero_aUEC()
    {
        var summary = Loaded("""
            {"deaths":2,"incapacitations":0,"sessionsWithDeaths":1,"estimatedFees":0,
             "byPlace":[],"byShip":[],"bedsUsed":[],"bedKinds":[],"wokeAt":[],"fees":[]}
            """).NodeText("#casualties-summary");

        Assert.Contains("—", summary);
        Assert.DoesNotContain("0 aUEC", summary);
    }

    [Fact]
    public void Where_and_what_you_died_in_are_charted_separately()
    {
        var page = Loaded();

        Assert.Contains("Port Tressler", page.NodeText("#casualties-places"));
        Assert.Contains("Lorville", page.NodeText("#casualties-places"));
        Assert.Contains("Drake Cutlass Black", page.NodeText("#casualties-ships"));

        // Places are clickable through to the map; ships are not a place.
        Assert.DoesNotContain("Drake Cutlass Black", page.NodeText("#casualties-places"));
    }

    /// <summary>
    /// The server being unreachable must leave the page as it was rather than
    /// blanking it, since an empty Casualties page reads as "you never died".
    /// </summary>
    [Fact]
    public void A_failed_fetch_leaves_the_page_alone_rather_than_emptying_it()
    {
        var page = Loaded();
        Assert.Contains("12", page.NodeText("#casualties-summary"));

        page.Do("""
            delete __fetch.routes['/api/casualties?days=0'];
            await loadCasualties();
            """);

        Assert.Contains("12", page.NodeText("#casualties-summary"));
    }

    /// <summary>
    /// Every other read on this page is guarded, and this one was not: a payload
    /// arriving without its fee table would have thrown and left the whole page
    /// blank rather than one table empty.
    /// </summary>
    [Fact]
    public void An_answer_missing_a_table_still_draws_the_rest()
    {
        var page = Loaded("""
            {"deaths":4,"incapacitations":2,"sessionsWithDeaths":2,"estimatedFees":1000,
             "byPlace":[],"byShip":[],"bedsUsed":[],"bedKinds":[],"wokeAt":[]}
            """);

        Assert.Contains("4", page.NodeText("#casualties-summary"));
    }
}
