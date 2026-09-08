namespace Quantumwake.WebTests;

/// <summary>
/// Opening an item from the parts table, which now opens the shared drawer.
/// </summary>
/// <remarks>
/// 9,401 of the install's 26,028 items carry a description, and they run to
/// several lines, so the name opens one rather than the table growing a column
/// nobody could read at that width. The tags beside it are the game's own
/// labels — flightReady is how it marks what has actually shipped. What used to
/// be an expanding row inside this one table is now the panel every other view
/// opens, so the item also arrives with whether it is already yours.
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

    private const string VariPuckCard = """
        {"kind":"part","id":"varipuck_s5","name":"VariPuck S5 Gimbal Mount",
         "subtitle":"Turret · GunTurret",
         "facts":[{"label":"Takes up","value":"8.4 centiSCU","source":"the game files"}],
         "holding":{"status":"not yours","detail":"never seen in your kit or a stash here"},
         "price":null,"places":[],"actions":["shopping","details"],
         "blurb":"Item Type: Weapon Mount\nThe VariPuck holds one gun.",
         "tags":["gimbalMount","flightReady"]}
        """;

    private static Page Loaded(string body)
    {
        var page = new Page();
        page.Serve("/api/reference/items", body);
        page.Do("await loadPartsRef();");
        return page;
    }

    private static Page Opened()
    {
        var page = Loaded(WithBlurb);
        page.Serve("/api/entity?kind=part&id=varipuck_s5", VariPuckCard);
        page.Do("await openEntity('part', 'varipuck_s5');");
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
        var page = Opened();

        Assert.False(page.Truth("__dom.node('#entity-drawer').hidden"));
        Assert.Contains("The VariPuck holds one gun", page.NodeText("#entity-blurb"));
        Assert.Contains("flightReady", page.NodeText("#entity-tags"));
    }

    /// <summary>
    /// The game writes these with a literal backslash-n between the header
    /// lines, which reads as noise if it is not split on.
    /// </summary>
    [Fact]
    public void The_header_lines_are_split_rather_than_run_together()
    {
        var page = Opened();

        Assert.DoesNotContain("nThe VariPuck", page.NodeText("#entity-blurb"));
        Assert.Equal(2, page.Count("__dom.node('#entity-blurb').byClass('part-blurb').length"));
    }

    /// <summary>
    /// Millionths of an SCU is nobody's unit at this scale. A pistol is a few
    /// thousand and a ship gun is millions, so one unit cannot serve both
    /// without printing either 0.000004 SCU or 12,000,000. The wording is the
    /// server's now, so this drawer and the map's own volume line cannot drift.
    /// </summary>
    [Fact]
    public void Volume_reads_in_a_unit_that_suits_the_size()
    {
        Assert.Contains("8.4 centiSCU", Opened().NodeText("#entity-facts"));
    }

    /// <summary>
    /// Every item opens now, where only the ones the game wrote a paragraph
    /// about used to. There is always something to say: what it is, whether it
    /// is already in your kit, what it costs and who stocks it.
    /// </summary>
    [Fact]
    public void An_item_with_no_paragraph_still_opens()
    {
        var page = Loaded(WithoutBlurb);

        Assert.Equal(
            1, page.Count("__dom.node('#parts-table tbody').querySelectorAll('.commodity-open').length"));
    }

    /// <summary>
    /// A card carrying no paragraph must not leave the last one on screen.
    /// </summary>
    [Fact]
    public void A_card_without_a_paragraph_hides_the_section()
    {
        var page = Opened();
        Assert.False(page.Truth("__dom.node('#entity-blurb').hidden"));

        page.Serve("/api/entity?kind=part&id=plain_01", """
            {"kind":"part","id":"plain_01","name":"Plain Thing","subtitle":"Misc",
             "facts":[],"holding":null,"price":null,"places":[],"actions":["details"]}
            """);
        page.Do("await openEntity('part', 'plain_01');");

        Assert.True(page.Truth("__dom.node('#entity-blurb').hidden"));
        Assert.True(page.Truth("__dom.node('#entity-tags').hidden"));
    }
}
