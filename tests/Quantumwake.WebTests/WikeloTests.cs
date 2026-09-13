namespace Quantumwake.WebTests;

/// <summary>
/// The Wikelo page: the emporium as the game files state it, against the stash.
/// </summary>
/// <remarks>
/// The page's honesty is the thing under test. A rank gate is a fact about the
/// trade and not about the pilot; "have" is a stash sighting, never a count;
/// a retired trade is counted and not shown; an install whose game data has
/// not been read is told so, not shown an empty emporium.
/// </remarks>
public class WikeloTests
{
    private const string Emporium = """
        {"available":true,
         "standings":[{"id":"000","name":"New Customer","minReputation":0},{"id":"001","name":"Very Good Customer","minReputation":340}],
         "trades":[
          {"id":"TheCollector_Vehicle_Small_Fortune","title":"Fortune ship for you","description":"Wikelo make Fortune better.",
           "group":"vehicles","reputation":30,"minStanding":null,"retired":false,
           "rewards":[{"class":"MISC_Fortune_Collector_Industrial","name":"MISC Fortune Wikelo Special","count":1,"unit":""}],
           "requirements":[{"class":"banu_favour","name":"Wikelo Favor","count":3,"unit":"","have":true,"where":["Ruin Station"]},
                           {"class":"CarinitePure","name":"Carinite (Pure)","count":1,"unit":"","have":false,"where":[]}],
           "trackedJobId":null,"trackedDone":false},
          {"id":"TheCollector_Vehicle_Small_Kruger_Wolf_Unique","title":"Most Special Wolf","description":"",
           "group":"vehicles","reputation":0,"minStanding":"001","retired":false,
           "rewards":[],"requirements":[{"class":"banu_favour","name":"Wikelo Favor","count":5,"unit":"","have":true,"where":["Ruin Station"]}],
           "trackedJobId":"j9","trackedDone":false},
          {"id":"TheCollector_Favours_PolarisParts","title":"Want Polaris? Need something special.","description":"",
           "group":"favours","reputation":0,"minStanding":null,"retired":false,
           "rewards":[{"class":"banu_favour_special","name":"Polaris Bit","count":1,"unit":""}],
           "requirements":[{"class":"Quantanium","name":"Quantainium","count":24,"unit":"SCU","have":false,"where":[]}],
           "trackedJobId":null,"trackedDone":false},
          {"id":"TheCollector_SB_NavyArm(DO_NOT_USE_NOW_LOOT)","title":"Make space navy armor","description":"",
           "group":"items","reputation":0,"minStanding":null,"retired":true,
           "rewards":[],"requirements":[],"trackedJobId":null,"trackedDone":false}]}
        """;

    private static Page Loaded(string emporium = Emporium)
    {
        var page = new Page();
        page.Serve("/api/wikelo", emporium);
        page.Do("wikeloGroup = 'vehicles'; await loadWikelo();");
        return page;
    }

    [Fact]
    public void A_trade_shows_what_it_wants_what_it_gives_and_what_the_stash_has_seen()
    {
        var page = Loaded();
        var list = page.NodeText("#wikelo-list");

        Assert.Contains("Fortune ship for you", list);
        Assert.Contains("MISC Fortune Wikelo Special", list);
        Assert.Contains("+30 rep", list);
        Assert.Contains("3× Wikelo Favor · seen at Ruin Station", list);
        Assert.Contains("1× Carinite (Pure)", list);
        Assert.Contains("1 of 2 seen in your stash", list);
        Assert.Contains("Track as a goal", list);
    }

    /// <summary>
    /// A gate is said in the file's words with its threshold, and the sentence
    /// says the page does not know the pilot's own standing.
    /// </summary>
    [Fact]
    public void A_rank_gate_is_a_fact_about_the_trade_not_the_pilot()
    {
        var list = Loaded().NodeText("#wikelo-list");

        Assert.Contains("Needs Very Good Customer (340 rep) or better", list);
        Assert.Contains("Your standing is not in the logs", list);
    }

    [Fact]
    public void A_trade_already_tracked_offers_the_list_rather_than_a_second_one()
    {
        var page = Loaded();

        Assert.Contains("Tracking — open the list", page.NodeText("#wikelo-list"));
        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#wikelo-list').byClass('wikelo-open').length")));
    }

    [Fact]
    public void Groups_are_chips_and_the_retired_are_counted_but_not_shown()
    {
        var page = Loaded();

        Assert.Contains("Ships & vehicles · 2", page.NodeText("#wikelo-groups"));
        Assert.Contains("Favors & the way in · 1", page.NodeText("#wikelo-groups"));
        Assert.DoesNotContain("Armour & weapons", page.NodeText("#wikelo-groups"));
        Assert.Contains("3 trades · 1 retired in the file, not shown", page.NodeText("#wikelo-count"));
        Assert.DoesNotContain("space navy", page.NodeText("#wikelo-list"));

        page.Do("wikeloGroup = 'favours'; renderWikelo();");
        Assert.Contains("24 SCU Quantainium", page.NodeText("#wikelo-list"));
        Assert.DoesNotContain("Fortune", page.NodeText("#wikelo-list"));
    }

    [Fact]
    public void Search_crosses_groups_and_reads_the_requirements()
    {
        var page = Loaded();
        page.Do("__dom.node('#wikelo-search').value = 'quantainium'; renderWikelo();");

        Assert.Contains("Want Polaris?", page.NodeText("#wikelo-list"));
        Assert.DoesNotContain("Fortune ship", page.NodeText("#wikelo-list"));
    }

    [Fact]
    public void Tracking_posts_the_trade_and_says_where_the_list_went()
    {
        var page = Loaded();
        page.Serve("/api/wikelo/TheCollector_Vehicle_Small_Fortune/track", """{"job":{"id":"j1"},"existed":false}""");
        page.Serve("/api/jobs", "[]");

        page.Do("await __dom.node('#wikelo-list').byClass('wikelo-track')[0].fire('click');");

        Assert.Contains("POST /api/wikelo/TheCollector_Vehicle_Small_Fortune/track", page.Fetched());
        Assert.Contains("Listed on Jobs", page.Text("__dom.node('#wikelo-list').byClass('point-said')[0].textContent"));
    }

    [Fact]
    public void One_trade_can_be_kept_as_the_current_goal_across_groups()
    {
        var page = Loaded();
        page.Do("wikeloGoalId = null; __dom.node('#wikelo-list').byClass('wikelo-pin')[0].fire('click');");

        Assert.Contains("Current goal", page.NodeText("#wikelo-goal"));
        Assert.Contains("Fortune ship for you", page.NodeText("#wikelo-goal"));
        Assert.Equal("TheCollector_Vehicle_Small_Fortune", page.Text("wikeloGoalId"));

        page.Do("wikeloGroup = 'favours'; renderWikelo();");
        Assert.Contains("Fortune ship for you", page.NodeText("#wikelo-goal"));
        Assert.Contains("Want Polaris?", page.NodeText("#wikelo-list"));
    }

    /// <summary>An install whose game data is not read is told so, never shown an empty emporium.</summary>
    [Fact]
    public void Without_the_game_files_the_page_says_why_rather_than_listing_nothing()
    {
        var list = Loaded("""{"available":false,"standings":[],"trades":[]}""").NodeText("#wikelo-list");

        Assert.Contains("has not been read yet", list);
        Assert.Contains("Nothing here comes from a website", list);
    }
}
