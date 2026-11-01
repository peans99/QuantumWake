namespace Quantumwake.WebTests;

/// <summary>
/// The one search box.
/// </summary>
/// <remarks>
/// The server keeps its sources apart; the page can undo that by drawing one
/// flat list. It can also offer a result that does nothing when pressed, which
/// is worse than plainly showing an answer - so hits with somewhere to go are
/// buttons and the rest are not.
/// </remarks>
public class GlobalSearchTests
{
    private const string Results = """
        {"query":"pembroke","nothing":false,"groups":[
          {"source":"your logs","hits":[
            {"kind":"part","id":"Pembroke helmet","name":"Pembroke helmet","why":"on your character"}]},
          {"source":"things you wrote","hits":[
            {"kind":"kit","id":"k1","name":"Bounty kit","why":"4 items"}]},
          {"source":"the catalogue","hits":[
            {"kind":"part","id":"Pembroke light","name":"Pembroke light","why":"armour the catalogue knows"}]}]}
        """;

    private static Page Searched(string results = Results, string query = "pembroke")
    {
        var page = new Page();
        page.Serve($"/api/search?q={query}", results);
        page.Do($"__dom.node('#global-search').value = '{query}'; await runGlobalSearch();");
        return page;
    }

    [Fact]
    public void A_query_too_short_asks_the_server_nothing()
    {
        var page = new Page();
        page.Do("__dom.node('#global-search').value = 'p'; await runGlobalSearch();");

        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/api/search"));
        Assert.True(page.Truth("__dom.node('#global-results').hidden"));
    }

    /// <summary>
    /// Sources stay apart on the page too. One flat list would undo the whole
    /// point of the server grouping them.
    /// </summary>
    [Fact]
    public void Results_are_shown_under_where_they_came_from()
    {
        var text = Searched().NodeText("#global-results");

        Assert.Contains("your logs", text);
        Assert.Contains("things you wrote", text);
        Assert.Contains("the catalogue", text);
    }

    [Fact]
    public void Every_hit_says_why_it_matched()
    {
        var text = Searched().NodeText("#global-results");

        Assert.Contains("on your character", text);
        Assert.Contains("4 items", text);
        Assert.Contains("armour the catalogue knows", text);
    }

    /// <summary>
    /// A thing never seen still answers, from the catalogue, rather than the
    /// search reporting nothing and reading as broken.
    /// </summary>
    [Fact]
    public void Something_never_seen_still_comes_back_from_the_catalogue()
    {
        var text = Searched("""
            {"query":"railgun","nothing":false,"groups":[
              {"source":"your logs","hits":[]},
              {"source":"things you wrote","hits":[]},
              {"source":"the catalogue","hits":[
                {"kind":"part","id":"Railgun","name":"Railgun","why":"weapon the catalogue knows"}]}]}
            """, "railgun").NodeText("#global-results");

        Assert.Contains("Railgun", text);
        Assert.Contains("the catalogue", text);

        // The empty groups are not drawn as empty headings.
        Assert.DoesNotContain("your logs", text);
    }

    [Fact]
    public void Nothing_matching_is_said_rather_than_drawn_as_an_empty_panel()
    {
        var text = Searched("""{"query":"zzz","nothing":true,"groups":[]}""", "zzz").NodeText("#global-results");

        Assert.Contains("Nothing matches", text);
    }

    /// <summary>
    /// A result that does nothing when pressed is worse than one that is
    /// plainly just an answer, so only hits with somewhere to go are buttons.
    /// </summary>
    [Fact]
    public void Only_hits_with_somewhere_to_go_are_pressable()
    {
        var page = Searched("""
            {"query":"helmet","nothing":false,"groups":[
              {"source":"your logs","hits":[
                {"kind":"part","id":"A helmet","name":"A helmet","why":"on your character"},
                {"kind":"mystery","id":"m1","name":"Something new","why":"a kind this build does not know"}]}]}
            """, "helmet");

        Assert.Equal(2, page.Count("__dom.node('#global-results').byClass('search-hit').length"));
        Assert.Equal(1, page.Count("__dom.node('#global-results').querySelectorAll('button').length"));
    }

    [Fact]
    public void Choosing_a_result_opens_it_and_clears_the_box()
    {
        var page = Searched();
        page.Serve("/api/entity?kind=part&id=Pembroke%20helmet", """{"kind":"part","name":"Pembroke helmet","facts":[]}""");

        page.Do("__dom.node('#global-results').byClass('search-hit')[0].click();");

        Assert.True(page.Truth("__dom.node('#global-results').hidden"));
        Assert.Equal("", page.Text("__dom.node('#global-search').value"));
    }

    /// <summary>
    /// Catalogue names are for people, but the drawer needs the engine class.
    /// Replacing the id with the display name makes a click appear to work and
    /// then close the drawer when it cannot resolve the item.
    /// </summary>
    [Fact]
    public void Choosing_a_catalogue_name_opens_its_engine_class()
    {
        var page = Searched("""
            {"query":"p4-ar","nothing":false,"groups":[
              {"source":"the catalogue","hits":[
                {"kind":"part","id":"behr_rifle_ballistic_01","name":"P4-AR Rifle",
                 "why":"weapon the catalogue knows"}]}]}
            """, "p4-ar");
        page.Serve("/api/reference/item?id=behr_rifle_ballistic_01",
            """{"className":"behr_rifle_ballistic_01","card":{"kind":"part","name":"P4-AR Rifle","facts":[]},"shops":[],"stockOn":[]}""");

        page.Do("__dom.node('#global-results').byClass('search-hit')[0].click();");

        // An item opens the Reference window (0.20.24), still by its class.
        Assert.Contains("GET /api/reference/item?id=behr_rifle_ballistic_01", page.Fetched());
    }

    /// <summary>
    /// A catalogue part with no display name comes back under its engine class.
    /// "Freelancer" answered four Controller_Flight_MISC_Freelancer_* rows that
    /// way; a worn item whose id is its display name must still show.
    /// </summary>
    [Fact]
    public void Engine_classes_with_no_name_are_not_offered()
    {
        var page = Searched("""
            {"query":"freelancer","nothing":false,"groups":[
              {"source":"your logs","hits":[
                {"kind":"part","id":"Freelancer Jacket","name":"Freelancer Jacket","why":"on your character"}]},
              {"source":"the catalogue","hits":[
                {"kind":"ship","id":"MISC Freelancer","name":"MISC Freelancer","why":"a ship the catalogue knows"},
                {"kind":"part","id":"ARMR_MISC_Freelancer","name":"Freelancer Ship Armor","why":"Armor the catalogue knows"},
                {"kind":"part","id":"Controller_Flight_MISC_Freelancer","name":"Controller_Flight_MISC_Freelancer",
                 "why":"FlightController the catalogue knows"}]}]}
            """, "freelancer");

        var text = page.NodeText("#global-results");
        Assert.DoesNotContain("Controller_Flight", text);
        Assert.Contains("Freelancer Jacket", text);
        Assert.Contains("Freelancer Ship Armor", text);
        Assert.Equal(3, page.Count("__dom.node('#global-results').byClass('search-hit').length"));
    }

    [Fact]
    public void A_search_that_only_found_engine_classes_says_nothing_matched()
    {
        var text = Searched("""
            {"query":"shield_r","nothing":false,"groups":[
              {"source":"the catalogue","hits":[
                {"kind":"part","id":"ARGO_ATLS_IKTI_Shield_R","name":"ARGO_ATLS_IKTI_Shield_R","why":"Misc the catalogue knows"}]}]}
            """, "shield_r").NodeText("#global-results");

        Assert.Contains("Nothing matches", text);
        Assert.DoesNotContain("the catalogue", text);
    }
}
