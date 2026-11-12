namespace Quantumwake.WebTests;

/// <summary>
/// The Armoury's Climate tab: each planet and moon's baseline from the game
/// files, set against every armour set's temperature range.
/// </summary>
/// <remarks>
/// The figures are 4.10.2's: Aberdeen 249.8 °C, microTech -7.1, Euterpe
/// -73.1, ArcCorp 29.9, and an unprotected body comfortable from 4 to 35 °C.
/// The armour is the Armoury fixture's: two cores that are sets, Testudo -50
/// to 80 and ADP -75 to 105, and Lynx arms, -31 to 61, which cover no body and
/// so are a piece, not a set. Aberdeen is the case the page has to say out
/// loud - nothing in the install is rated for it.
/// </remarks>
public class ArmouryClimateTests
{
    private const string Climate = """
        "climate":{"bodyMin":4,"bodyMax":35,"visorFrost":-20,"bodies":[
          {"id":"Stanton1b","name":"Aberdeen","system":"Stanton","parent":"Hurston","celsius":249.8,"pressure":1.45,"humidity":0,"nightCooling":-0.1},
          {"id":"Stanton3","name":"ArcCorp","system":"Stanton","parent":null,"celsius":29.9,"pressure":0.9,"humidity":0.3,"nightCooling":-2.509},
          {"id":"Stanton4","name":"microTech","system":"Stanton","parent":null,"celsius":-7.1,"pressure":1.1,"humidity":0.65,"nightCooling":-2.509},
          {"id":"Stanton4c","name":"Euterpe","system":"Stanton","parent":"microTech","celsius":-73.1,"pressure":0.8,"humidity":0.3,"nightCooling":-16}]},
        """;

    private static Page Opened(string? lang = null)
    {
        var page = lang is null ? new Page() : new Page(lang);
        page.Serve("/api/armoury", ArmouryPageTests.Model.Replace("{\"ready\":true,", "{\"ready\":true," + Climate));
        page.Do("await loadArmoury(); showArmouryPane('climate');");
        return page;
    }

    private const string Rows = "__dom.node('#armoury-climate tbody').querySelectorAll('tr')";

    private static string Row(Page page, int i) => page.Text($"{Rows}[{i}].textContent");

    [Fact]
    public void Every_body_gets_a_row_with_its_baseline_and_how_many_sets_cope()
    {
        var page = Opened();

        Assert.Equal(4, page.Count($"{Rows}.length"));
        Assert.Contains("Aberdeen", Row(page, 0));
        Assert.Contains("moon of Hurston", Row(page, 0));
        Assert.Contains("250 °C", Row(page, 0));
        Assert.Contains("Needs a suit", Row(page, 0));
        Assert.EndsWith("0", Row(page, 0));

        Assert.Contains("Comfortable", Row(page, 1));
        Assert.EndsWith("2", Row(page, 1));
        Assert.EndsWith("2", Row(page, 2));
        Assert.EndsWith("1", Row(page, 3));
    }

    [Fact]
    public void The_page_says_it_is_a_baseline_and_that_mixed_outfits_are_not_judged()
    {
        var note = Opened().NodeText("#armoury-climate-note");

        Assert.Contains("A baseline, not the weather", note);
        Assert.Contains("from 4 to 35 °C", note);
        Assert.Contains("a mixed outfit is not judged", note);
    }

    [Fact]
    public void A_body_nothing_is_rated_for_says_so_and_how_far_the_best_reach()
    {
        var page = Opened();
        page.Do("armouryOpenBody = 'Stanton1b'; renderArmouryClimate();");

        var text = page.NodeText("#armoury-climate tbody");
        Assert.Contains("No armour set in your game files is rated for this baseline", text);
        Assert.Contains("The hottest-rated reaches 105 °C, the coldest -75 °C.", text);
    }

    [Fact]
    public void A_body_lists_the_sets_rated_for_it_roomiest_first()
    {
        var page = Opened();
        page.Do("armouryOpenBody = 'Stanton4'; renderArmouryClimate();");

        var list = page.Text("__dom.node('#armoury-climate tbody').byClass('armoury-climate-sets')[0].textContent");

        // microTech at -7: ADP has 68 degrees of room, Testudo 43.
        var adp = list.IndexOf("ADP · -75 to 105 °C", StringComparison.Ordinal);
        var testudo = list.IndexOf("Testudo · -50 to 80 °C", StringComparison.Ordinal);
        Assert.True(adp >= 0 && adp < testudo, list);
        Assert.DoesNotContain("Lynx", list);
    }

    /// <summary>
    /// A helmet alone keeps nobody alive: on Euterpe half of what the install
    /// rates is a helmet whose undersuit is a separately named family. Those
    /// are counted apart, and said to be pieces.
    /// </summary>
    [Fact]
    public void A_piece_that_covers_no_body_is_said_apart_and_not_counted_as_a_set()
    {
        var page = Opened();
        page.Do("armouryOpenBody = 'Stanton4'; renderArmouryClimate();");

        Assert.Contains("1 helmet or other piece is rated on its own as well; it does not cover the body, so it is not counted as a set.",
            page.NodeText("#armoury-climate tbody"));
        Assert.EndsWith("2", Row(page, 2));
    }

    [Fact]
    public void An_armour_row_says_which_baselines_its_range_takes_in()
    {
        var page = Opened();

        Assert.Equal("Its range takes in the baseline of 2 of the 4 planets and moons: ArcCorp, microTech. A baseline, not the weather on the ground.",
            page.Text("armouryClimateLine(armouryModel.armour.find(a => a.family === 'Testudo'))"));
        Assert.Contains("Euterpe", page.Text("armouryClimateLine(armouryModel.armour.find(a => a.family === 'ADP'))"));
    }

    [Fact]
    public void The_climate_tab_lights_the_protection_card()
    {
        var page = Opened();

        Assert.True(page.Truth("__dom.node('#armoury-open-armour').classList.contains('active')"));
    }
}
