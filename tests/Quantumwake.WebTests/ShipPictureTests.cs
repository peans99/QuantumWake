namespace Quantumwake.WebTests;

/// <summary>
/// The ship's picture on a Fleet card: the game's silhouette tinted by maker,
/// or the game's render of the paint the pilot chose.
/// </summary>
/// <remarks>
/// The app must never pick a paint - which one a ship wears is not in the
/// logs - so the choice is the pilot's, kept in this browser like the roster
/// tick, and the tint is said to be a hint at the maker rather than a finish.
/// </remarks>
public class ShipPictureTests
{
    private const string Corsair = "{name:'Drake Corsair', className:'DRAK_Corsair'}";
    private const string Maker = "{code:'DRAK', name:'Drake Interplanetary', model:'Corsair'}";

    private static Page Fresh()
    {
        var page = new Page();
        page.Do("shipPaints = {};");
        return page;
    }

    [Fact]
    public void Without_a_chosen_paint_the_silhouette_is_a_mask_in_the_makers_tint()
    {
        var page = Fresh();
        page.Do($"__dom.node('#t').append(shipPicture({Corsair}, {Maker}));");

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
        Assert.Equal("#f0954a", page.Text("__dom.node('#t').byClass('ship-outline')[0].style['--tint']"));
        Assert.Contains("/api/fleet/icons/DRAK_Corsair", page.Text("__dom.node('#t').byClass('ship-outline')[0].style['--outline']"));
        Assert.Contains("tinted Drake Interplanetary's colour", page.Text("__dom.node('#t').byClass('ship-outline')[0].title"));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
    }

    [Fact]
    public void A_chosen_paint_shows_the_games_render_of_it()
    {
        var page = Fresh();
        page.Do($"shipPaints = {{DRAK_Corsair: 'Paint_Corsair_Black_Black_Gold_Camo'}}; __dom.node('#t').append(shipPicture({Corsair}, {Maker}));");

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
        Assert.Contains("/api/fleet/paints/Paint_Corsair_Black_Black_Gold_Camo/render", page.Text("__dom.node('#t').byClass('ship-render')[0].src"));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
    }

    /// <summary>
    /// The chooser lists what the game pictures for the hull, with the
    /// silhouette as the first choice, and remembers the pick per hull.
    /// </summary>
    [Fact]
    public void The_chooser_offers_the_games_paints_and_remembers_the_pick()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Corsair", """
            [{"item":"Paint_Corsair_Black_Black_Gold_Camo","name":"Corsair Black Gold Camo Livery"},
             {"item":"Paint_Corsair_Commando","name":"Corsair Commando Livery"}]
            """);
        page.Do($"""
            const box = shipPicture({Corsair}, {Maker});
            __dom.node('#t').append(box);
            await openPaintChooser({Corsair}, box, box.byClass('ship-paint')[0]);
            """);

        Assert.Equal(3, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-paint-select')[0].options.length")));
        Assert.Contains("Silhouette", page.Text("__dom.node('#t').byClass('ship-paint-select')[0].options[0].textContent"));
        Assert.Contains("Corsair Commando Livery", page.Text("__dom.node('#t').byClass('ship-paint-select')[0].options[2].textContent"));

        page.Do("rememberShipPaint('DRAK_Corsair', 'Paint_Corsair_Commando');");
        Assert.Equal("Paint_Corsair_Commando", page.Text("shipPaints.DRAK_Corsair"));

        page.Do("rememberShipPaint('DRAK_Corsair', null);");
        Assert.True(page.Truth("shipPaints.DRAK_Corsair === undefined"));
    }

    [Fact]
    public void A_hull_the_game_pictures_no_paint_for_says_so()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/GRIN_Nothing", "[]");
        page.Do("""
            const box = shipPicture({name:'Greycat Nothing', className:'GRIN_Nothing'}, {code:'GRIN', name:'Greycat', model:'Nothing'});
            __dom.node('#t').append(box);
            await openPaintChooser({name:'Greycat Nothing', className:'GRIN_Nothing'}, box, box.byClass('ship-paint')[0]);
            """);

        Assert.Contains("pictures no paint", page.NodeText("#t"));
    }

    [Fact]
    public void An_unknown_maker_gets_the_apps_own_cyan()
    {
        Assert.Equal("#7fe4ff", new Page().Text("makerTint('ZZZZ')"));
        Assert.Equal("#9adcf2", new Page().Text("makerTint('MISC')"));
    }
}
