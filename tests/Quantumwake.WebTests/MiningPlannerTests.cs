namespace Quantumwake.WebTests;

/// <summary>
/// Ranking rocks by what they are worth rather than what they sell for.
/// </summary>
/// <remarks>
/// A high price on an ore that is 2% of the rock is not a good rock, which is
/// what sorting on best sell alone used to say. The middle of the ore range is
/// used rather than the top: ice runs 9.7% to 84.3% depending on the rock, and
/// quoting the ceiling would rank every wide band above every reliable one.
/// </remarks>
public class MiningPlannerTests
{
    private const string Rows = """
        [{"resource":"Cheap but rich","deposit":null,"minPercent":40,"maxPercent":60,
          "kind":"mineable","location":"Daymar","system":"Stanton","group":"Mineables",
          "groupChance":0.5,"share":0.4,"bestSell":1000,"quality":null,"respawnSeconds":3600,
          "source":"install"},
         {"resource":"Dear but scarce","deposit":null,"minPercent":1,"maxPercent":3,
          "kind":"mineable","location":"Yela","system":"Stanton","group":"Mineables",
          "groupChance":0.5,"share":0.4,"bestSell":20000,"quality":null,"respawnSeconds":3600,
          "source":"install"}]
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/resources", body);
        page.Do("await loadMiningRef();");
        return page;
    }

    private static string Cell(Page page, int index) =>
        page.Text($"__dom.node('#mining-table tbody').querySelectorAll('td')[{index}].textContent");

    /// <summary>
    /// 50% of a 1,000 rock beats 2% of a 20,000 one, and the old sort had it
    /// the other way round.
    /// </summary>
    [Fact]
    public void The_richer_rock_outranks_the_dearer_ore()
    {
        var page = Loaded(Rows);

        Assert.Contains("Cheap but rich", Cell(page, 0));
    }

    [Fact]
    public void Worth_is_the_middle_of_the_range_times_the_price()
    {
        // 40-60% of a rock selling at 1,000 is 500 a SCU.
        Assert.Contains("500", Cell(Loaded(Rows), 10));
    }

    /// <summary>
    /// The two odds are one question - how much of this place is this - so they
    /// are multiplied rather than shown as two numbers to multiply by hand.
    /// </summary>
    [Fact]
    public void Find_multiplies_the_two_odds()
    {
        // Half the spawns are this group, and 40% of that group is this: 20%.
        Assert.Equal("20%", Cell(Loaded(Rows), 9));
    }

    /// <summary>
    /// An ore with no price, or no known share of the rock, has no worth to
    /// show - and a dash says that where a zero would claim it is worthless.
    /// </summary>
    [Fact]
    public void Nothing_priced_shows_a_dash_rather_than_nothing_worth()
    {
        var page = Loaded("""
            [{"resource":"Wreckage","deposit":null,"minPercent":null,"maxPercent":null,
              "kind":"salvageable","location":"Daymar","system":"Stanton","group":"Salvage",
              "groupChance":0.5,"share":0.4,"bestSell":null,"quality":null,
              "respawnSeconds":null,"source":"install"}]
            """);

        Assert.Equal("—", Cell(page, 10));
    }
}
