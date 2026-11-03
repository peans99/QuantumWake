namespace Quantumwake.WebTests;

/// <summary>
/// Mining → Radar: a ping in, the rock counts that read it out.
/// </summary>
public class RadarPaneTests
{
    // The install's own figures (4.10.2) for the rows these tests need.
    private const string Model = """
        {"ready":true,"read":260,"unplaced":59,"rows":[
          {"signature":3000,"kind":"hand","name":"Hand-mining deposit","tier":null,"ores":["Aphorite","Hadanite"],"surface":true,"asteroid":false,"stones":[]},
          {"signature":3170,"kind":"ship","name":"Quantainium","tier":"Legendary","ores":["Quantainium"],"surface":true,"asteroid":true,"stones":[]},
          {"signature":3200,"kind":"ship","name":"Savrilium","tier":"Legendary","ores":["Savrilium"],"surface":true,"asteroid":true,"stones":[]},
          {"signature":3840,"kind":"ship","name":"Aslarite","tier":"Uncommon","ores":["Aslarite"],"surface":true,"asteroid":true,"stones":[]},
          {"signature":4000,"kind":"ground","name":"Ground-vehicle deposit","tier":null,"ores":["Carinite","Glacosite"],"surface":true,"asteroid":false,"stones":[]},
          {"signature":4700,"kind":"asteroid","name":"C-type asteroid","tier":null,"ores":["Copper","Iron"],"surface":false,"asteroid":true,"stones":[]}
        ]}
        """;

    private static Page Loaded(string model = Model)
    {
        var page = new Page();
        page.Serve("/api/mining/signatures", model);
        page.Do("await loadRadar(); showMiningPane('radar');");
        return page;
    }

    private static string Ping(Page page, int ping)
    {
        page.Do($"__dom.node('#radar-ping').value = '{ping}'; renderRadarMatches();");
        return page.NodeText("#radar-matches");
    }

    [Fact]
    public void Two_quantainium_rocks_read_as_two_quantainium_rocks()
    {
        var text = Ping(Loaded(), 6340);

        Assert.Contains("2 ×", text);
        Assert.Contains("Quantainium", text);
        Assert.Contains("Legendary", text);
        Assert.DoesNotContain("cannot tell", text);
    }

    /// <summary>
    /// 19,200 is six Savrilium and five Aslarite. The page lists both and says
    /// the number cannot choose, rather than picking the first.
    /// </summary>
    [Fact]
    public void A_ping_two_ores_share_lists_both_and_says_so()
    {
        var page = Loaded();
        var text = Ping(page, 19200);

        Assert.Contains("Savrilium", text);
        Assert.Contains("Aslarite", text);
        Assert.Contains("the figure alone cannot tell them apart", text);
        // Fewest rocks first.
        Assert.Equal("5 ×", page.Text("__dom.node('#radar-matches').querySelector('.radar-count').textContent"));
    }

    /// <summary>
    /// A C-type reads 4700 whatever is inside; the answer names the class and
    /// what it might hold, not an ore.
    /// </summary>
    [Fact]
    public void An_asteroid_class_answers_with_what_it_might_hold()
    {
        var text = Ping(Loaded(), 9400);

        Assert.Contains("2 × C-type asteroid", text);
        Assert.Contains("one of Copper, Iron", text);
    }

    /// <summary>
    /// 3185 is no rock count of anything here; 3170 is Quantainium 15 below.
    /// The offset is shown, because that is what says a typo from a new ore.
    /// </summary>
    [Fact]
    public void A_ping_that_matches_nothing_shows_the_nearest_with_the_offset()
    {
        var text = Ping(Loaded(), 3185);

        Assert.Contains("No rock count reads exactly 3,185", text);
        Assert.Contains("Quantainium", text);
        Assert.Contains("+15 from 3,170", text);
    }

    [Fact]
    public void The_table_lists_every_row_with_its_multiples()
    {
        var page = Loaded();

        Assert.Equal(6, page.Count("__dom.node('#radar-table tbody').querySelectorAll('tr').length"));
        var body = page.NodeText("#radar-table tbody");
        Assert.Contains("6,340", body);   // Quantainium ×2
        Assert.Contains("Surface & asteroid fields", body);
        Assert.Contains("6 rows from 260 rocks in the install. 59 more are templates", page.NodeText("#radar-note"));
        Assert.Equal("Radar", page.NodeText("#mining-workspace-title"));
        Assert.Equal("6 signatures from the install.", page.NodeText("#mining-workspace-status"));
    }

    /// <summary>
    /// An install read before this release has a cache with no signatures: the
    /// page says the game data is not ready instead of drawing an empty table.
    /// </summary>
    [Fact]
    public void An_install_without_signatures_says_so()
    {
        var page = Loaded("""{"ready":false,"read":0,"unplaced":0,"rows":[]}""");

        Assert.False(page.Truth("__dom.node('#radar-unready').hidden"));
        Assert.True(page.Truth("__dom.node('#radar-table').hidden"));
        Assert.Equal("", Ping(page, 6340));
    }

    [Fact]
    public void The_radar_pane_is_remembered_like_the_others()
    {
        var page = new Page();
        page.Do("showMiningPane('radar');");

        Assert.True(page.Truth("__dom.node('#mining-pane-radar').classList.contains('active')"));
        Assert.False(page.Truth("__dom.node('#mining-pane-go').classList.contains('active')"));
        Assert.Equal("radar", page.Text("localStorage.getItem('qw-mining-pane')"));
        Assert.True(page.Truth("__dom.node('#mining-kind').hidden"));
    }
}
