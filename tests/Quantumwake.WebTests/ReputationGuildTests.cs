namespace Quantumwake.WebTests;

/// <summary>
/// Star Citizen 5.0's guilds on the Reputation page (1.3.4): listed from the
/// installed game before any screenshot has shown one, with the rank only
/// where a Rep app screenshot did.
/// </summary>
/// <remarks>
/// Figures are the 5.0 PTU's (5.0.202.46035): United Resource Workers' boards
/// carry Shubin, Adagio and Cornerstone, and 113 contracts pay it 100 to 250
/// standing; the Academy of Science has 6 contracts paying 100 each and no
/// logo in the files.
/// </remarks>
public class ReputationGuildTests
{
    private const string Ladder = """
        [{"name":"Applicant","perk":null,"minReputation":0,"fill":null},
         {"name":"Jr. Contractor","perk":null,"minReputation":513,"fill":null},
         {"name":"Contractor","perk":null,"minReputation":2051,"fill":null},
         {"name":"Sr. Contractor","perk":null,"minReputation":4615,"fill":null},
         {"name":"Veteran Contractor","perk":null,"minReputation":8205,"fill":null},
         {"name":"Head Contractor","perk":null,"minReputation":12820,"fill":null},
         {"name":"Elite Contractor","perk":null,"minReputation":18461,"fill":null}]
        """;

    private static string Board(string organisations, string urwRank = "null", string urwEstimate = "null", bool shubinOnPage = false) => $$"""
        {"organisations":{{organisations}},
         "guilds":[
          {"name":"United Resource Workers","logo":"/api/reputation/logo/FactionReputation_Guild_Industrial","ladder":{{Ladder}},
           "members":[{"name":"Shubin Interstellar","logo":null,"onPage":{{(shubinOnPage ? "true" : "false")}}},
                      {"name":"Adagio Holdings","logo":null,"onPage":false},
                      {"name":"Cornerstone Developments","logo":null,"onPage":false}],
           "contracts":113,"payLow":100,"payHigh":250,"rank":{{urwRank}},"estimate":{{urwEstimate}},"seen":null},
          {"name":"Academy of Science","logo":null,"banner":"/api/reputation/banner/FactionReputation_Guild_Science","ladder":{{Ladder}},
           "members":[{"name":"Rayari Incorporated","logo":null,"onPage":false}],
           "contracts":6,"payLow":100,"payHigh":100,"rank":null,"estimate":null,"seen":null}],
         "readings":0,"unmeasured":0,"unmeasurable":0,"canRead":true,"watching":true,"ladders":40}
        """;

    private static Page Loaded(string board)
    {
        var page = new Page();
        page.Serve("/api/reputation", board);
        page.Do("await loadReputation();");
        return page;
    }

    [Fact]
    public void Guilds_are_listed_from_the_game_before_any_screenshot_and_say_they_are_unread()
    {
        var page = Loaded(Board("[]"));
        var list = page.NodeText("#reputation-list");

        Assert.Contains("No Rep app screenshots yet", list);
        Assert.Contains("Guilds", list);
        Assert.Contains("United Resource Workers", list);
        Assert.Contains("Not read yet. Open United Resource Workers in the mobiGlas Rep app", list);
        Assert.Contains("113 contracts pay it 100–250 standing each when finished.", list);
        Assert.Contains("6 contracts pay it 100 standing each when finished.", list);
        Assert.Contains("Jr. Contractor 513", list);
        Assert.Contains("On its boards: Shubin Interstellar, Adagio Holdings, Cornerstone Developments", list);
        Assert.Equal(0, page.Count("__dom.node('#reputation-list').byClass('held').length"));
    }

    [Fact]
    public void A_guild_a_screenshot_showed_gives_its_rank_as_an_estimate_and_marks_it_on_the_ladder()
    {
        var page = Loaded(Board("[]", "\"CONTRACTOR\"", "3200"));

        Assert.Contains("Contractor, about 3,200 rep — an estimate from your last Rep app screenshot", page.NodeText("#reputation-list"));
        Assert.Equal("Contractor", page.Text("__dom.node('#reputation-list').byClass('held')[0].textContent"));
    }

    [Fact]
    public void A_member_links_to_its_card_only_when_a_screenshot_gave_it_one()
    {
        Assert.Equal(0, Loaded(Board("[]")).Count("__dom.node('#reputation-list').byClass('org-link').length"));
        Assert.Equal(1, Loaded(Board("[]", shubinOnPage: true)).Count("__dom.node('#reputation-list').byClass('org-link').length"));
    }

    /// <summary>
    /// The Academy of Science's own logo is not in the PTU's files; its board
    /// header, with its emblem, is - so a guild with no logo still shows its mark.
    /// </summary>
    [Fact]
    public void A_guild_shows_its_board_header_where_the_game_has_one()
    {
        var page = Loaded(Board("[]"));
        const string banners = "__dom.node('#reputation-list').byClass('rep-guild-banner')";

        Assert.Equal(1, page.Count($"{banners}.length"));
        Assert.Equal("/api/reputation/banner/FactionReputation_Guild_Science", page.Text($"{banners}[0].src"));
    }

    [Fact]
    public void Before_five_there_are_no_guilds_and_no_section()
    {
        var page = Loaded("""{"organisations":[],"guilds":[],"readings":0,"unmeasured":0,"unmeasurable":0,"canRead":true,"watching":true,"ladders":39}""");

        Assert.Equal(0, page.Count("__dom.node('#reputation-list').byClass('rep-guilds').length"));
    }
}
