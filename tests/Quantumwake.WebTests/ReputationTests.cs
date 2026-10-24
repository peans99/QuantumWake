namespace Quantumwake.WebTests;

/// <summary>
/// The Reputation page: standing as the Rep app showed it, one screenshot at a
/// time.
/// </summary>
/// <remarks>
/// What is under test is mostly what the page admits. Reputation is an
/// estimate and says so with its margin; the contracts between two readings
/// are a floor and say so; a reading taken before ranks were read says that
/// rather than showing an empty bar; and an empty page says how to fill it.
/// </remarks>
public class ReputationTests
{
    /// <summary>
    /// Red Wind as this install photographed it: Junior 60% on 8 September,
    /// Member 54% on 24 October, with ten hauls finished in the logs between.
    /// </summary>
    private const string Board = """
        {"organisations":[
          {"name":"RED WIND LINEHAUL","standing":"NEUTRAL","section":"Hauling","rank":"MEMBER","progress":0.54,
           "place":{"scope":"ReputationScope_Hauling","section":"Hauling","rank":"Member","rankIndex":4,"rankCount":8,
                    "floor":5250,"next":27750,"estimate":17405,"resolution":132,"perk":null},
           "ladder":[{"name":"Not Eligible","perk":null,"minReputation":-1000,"fill":0},
                     {"name":"Trainee","perk":null,"minReputation":0,"fill":1},
                     {"name":"Rookie","perk":null,"minReputation":50,"fill":1},
                     {"name":"Junior","perk":null,"minReputation":250,"fill":1},
                     {"name":"Member","perk":null,"minReputation":5250,"fill":0.54},
                     {"name":"Experienced","perk":null,"minReputation":27750,"fill":0},
                     {"name":"Senior","perk":null,"minReputation":77750,"fill":0},
                     {"name":"Master","perk":null,"minReputation":237750,"fill":null}],
           "lastSeen":"2026-10-24T18:59:59Z","lastShot":"ScreenShot-2026-10-24_14-59-59-A70.jpg",
           "history":[{"at":"2026-09-09T01:49:03Z","shot":"a.jpg","rank":"JUNIOR","rankIndex":3,"progress":0.604,"estimate":3268},
                      {"at":"2026-10-24T18:59:59Z","shot":"b.jpg","rank":"MEMBER","rankIndex":4,"progress":0.54,"estimate":17405}],
           "change":{"from":"2026-09-09T01:49:03Z","to":"2026-10-24T18:59:59Z","ranks":1,"progress":null,"estimate":14137,
                     "work":{"issuer":"Red Wind","completed":10,"abandoned":0,"failed":0,"rep":0,"repFrom":0}},
           "work":{"issuer":"Red Wind","completed":11,"abandoned":0,"failed":0,"rep":0,"repFrom":0}},
          {"name":"COVALEX","standing":"NEUTRAL","section":null,"rank":null,"progress":null,"place":null,"ladder":[],
           "lastSeen":"2026-10-24T18:59:50Z","lastShot":"c.jpg",
           "history":[{"at":"2026-10-24T18:59:50Z","shot":"c.jpg","rank":null,"rankIndex":null,"progress":null,"estimate":null}],
           "change":null,"work":null}],
         "readings":3,"unmeasured":1,"unmeasurable":0,"canRead":true,"watching":true,"ladders":46}
        """;

    private static Page Loaded(string board = Board, string? lang = null)
    {
        var page = lang is null ? new Page() : new Page(lang);
        page.Serve("/api/reputation", board);
        page.Do("await loadReputation();");
        return page;
    }

    [Fact]
    public void An_organisation_shows_its_rank_its_estimate_and_what_is_left_to_the_next()
    {
        var list = Loaded().NodeText("#reputation-list");

        Assert.Contains("RED WIND LINEHAUL", list);
        Assert.Contains("Member · 54% of the way through · Hauling", list);
        Assert.Contains("about 17,405 rep (±132, estimate)", list);
        Assert.Contains("10,345 to Experienced at 27,750", list);
        Assert.Contains("They regard you as neutral", list);
        Assert.Contains("Red Wind in your logs: 11 finished", list);
    }

    /// <summary>The contracts in between are what the logs kept, so the page calls the count a floor.</summary>
    [Fact]
    public void Two_readings_make_a_change_and_the_contracts_between_are_a_floor()
    {
        var list = Loaded().NodeText("#reputation-list");

        Assert.Contains("Junior 60% → Member 54%", list);
        Assert.Contains("about +14,137 rep", list);
        Assert.Contains("at least 10 Red Wind contracts finished in between", list);
        Assert.Contains("2 readings,", list);
    }

    [Fact]
    public void The_ladder_marks_the_rank_held_and_fills_each_bar_as_read()
    {
        var page = Loaded();

        Assert.Equal(8, Convert.ToInt32(page.Eval("__dom.node('#reputation-list').byClass('rep-rung').length")));
        Assert.Equal("Member", page.Text("__dom.node('#reputation-list').byClass('held')[0].textContent"));
        Assert.Equal("54%", page.Text("__dom.node('#reputation-list').byClass('rep-rung-fill')[4].style.width"));
    }

    /// <summary>A reading from before the bars were read says so, rather than drawing an empty one.</summary>
    [Fact]
    public void A_reading_without_a_rank_says_why_and_the_notice_says_it_is_being_read_again()
    {
        var page = Loaded();

        Assert.Contains("Rank not read", page.NodeText("#reputation-list"));
        Assert.Contains("1 Rep screenshot taken before ranks were read is being read again.", page.NodeText("#reputation-notice"));
        Assert.Equal("2 organisations · 3 screenshots", page.NodeText("#reputation-count"));
    }

    [Fact]
    public void With_screenshot_reading_off_the_notice_says_where_to_turn_it_on()
    {
        var page = Loaded(Board.Replace("\"watching\":true", "\"watching\":false"));

        Assert.Contains("turn on screenshot reading in Overlay settings", page.NodeText("#reputation-notice"));
    }

    [Fact]
    public void An_empty_page_says_how_to_fill_it()
    {
        const string empty = """{"organisations":[],"readings":0,"unmeasured":0,"unmeasurable":0,"canRead":true,"watching":true,"ladders":46}""";

        Assert.Contains("open the mobiGlas Rep app", Loaded(empty).NodeText("#reputation-list"));
        Assert.Contains("the overlay does that",
            Loaded(empty.Replace("\"canRead\":true", "\"canRead\":false")).NodeText("#reputation-list"));
    }

    [Fact]
    public void Reputation_reads_in_french_and_keeps_the_games_names()
    {
        var page = Loaded(lang: "fr");
        var list = page.NodeText("#reputation-list").Replace(' ', ' ').Replace(' ', ' ');

        Assert.Contains("RED WIND LINEHAUL", list);
        Assert.Contains("Member · 54 % du chemin parcouru · Hauling", list);
        Assert.Contains("estimation", list);
        Assert.Contains("au moins 10 contrats Red Wind terminés entre-temps", list);
        Assert.Contains("Rang non lu", list);
        Assert.DoesNotContain("of the way through", list);
        Assert.Equal("2 organisations · 3 captures d’écran", page.NodeText("#reputation-count").Replace(' ', ' '));
    }
}
