namespace Quantumwake.WebTests;

/// <summary>
/// The game's own paragraph about an item, opened from the parts table.
/// </summary>
/// <remarks>
/// 9,401 of the install's 26,028 items carry a description, and they run to
/// several lines, so the name opens one rather than the table growing a column
/// nobody could read at that width. The tags beside it are the game's own
/// labels — flightReady is how it marks what has actually shipped.
/// </remarks>
public class PartDetailTests
{
    private const string WithBlurb = """
        [{"className":"varipuck_s5","name":"VariPuck S5 Gimbal Mount","type":"Turret",
          "subType":"GunTurret","size":5,"grade":1,"manufacturer":"Flashfire Systems",
          "source":"install","description":"Item Type: Weapon Mount\nThe VariPuck holds one gun.",
          "tags":"gimbalMount flightReady","microScu":84000,"price":null,"stockedAt":0,
          "cheapestAt":null,"terminals":null}]
        """;

    private const string WithoutBlurb = """
        [{"className":"plain_01","name":"Plain Thing","type":"Misc","subType":"","size":1,
          "grade":1,"manufacturer":null,"source":"install","price":null,"stockedAt":0,
          "cheapestAt":null,"terminals":null}]
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/items", body);
        page.Do("await loadPartsRef();");
        return page;
    }

    [Fact]
    public void The_blurb_is_not_shown_until_it_is_asked_for()
    {
        var page = Loaded(WithBlurb);

        Assert.DoesNotContain("The VariPuck holds one gun", page.NodeText("#parts-table tbody"));
    }

    [Fact]
    public void Opening_an_item_shows_what_the_game_says_and_its_tags()
    {
        var page = Loaded(WithBlurb);
        page.Do("__dom.node('#parts-table tbody').querySelectorAll('.commodity-open')[0].click();");

        var body = page.NodeText("#parts-table tbody");

        Assert.Contains("The VariPuck holds one gun", body);
        Assert.Contains("flightReady", body);
    }

    /// <summary>
    /// The game writes these with a literal backslash-n between the header
    /// lines, which reads as noise if it is not split on.
    /// </summary>
    [Fact]
    public void The_header_lines_are_split_rather_than_run_together()
    {
        var page = Loaded(WithBlurb);
        page.Do("__dom.node('#parts-table tbody').querySelectorAll('.commodity-open')[0].click();");

        Assert.DoesNotContain("nThe VariPuck", page.NodeText("#parts-table tbody"));
        Assert.Equal(2, page.Count("__dom.node('#parts-table tbody').querySelectorAll('.part-blurb').length"));
    }

    /// <summary>
    /// Most items have nothing to say, and those must not look clickable.
    /// </summary>
    /// <summary>
    /// Millionths of an SCU is nobody's unit at this scale. A pistol is a few
    /// thousand and a ship gun is millions, so one unit cannot serve both
    /// without printing either 0.000004 SCU or 12,000,000.
    /// </summary>
    [Fact]
    public void Volume_reads_in_a_unit_that_suits_the_size()
    {
        var page = Loaded(WithBlurb);
        page.Do("__dom.node('#parts-table tbody').querySelectorAll('.commodity-open')[0].click();");

        Assert.Contains("8.4 centiSCU", page.NodeText("#parts-table tbody"));
    }

    [Fact]
    public void An_item_with_nothing_to_say_does_not_open()
    {
        var page = Loaded(WithoutBlurb);

        Assert.Equal(
            0, page.Count("__dom.node('#parts-table tbody').querySelectorAll('.commodity-open').length"));
    }
}
