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
        page.Do("shipPaints = {}; hullPaints.clear(); shipPictureStyle = 'paint';");
        return page;
    }

    [Fact]
    public void Without_a_chosen_paint_and_none_pictured_the_silhouette_is_a_mask_in_the_makers_tint()
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
        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render-wrap').length")));
        Assert.Equal("#f0954a", page.Text("__dom.node('#t').byClass('ship-render-wrap')[0].style['--tint']"));
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
        // Unpicked, the list opens on the paint that is standing in, and says so.
        Assert.Equal("Paint_Corsair_Black_Black_Gold_Camo", page.Text("__dom.node('#t').byClass('ship-paint-select')[0].value"));
        Assert.Contains("shown until you pick", page.Text("__dom.node('#t').byClass('ship-paint-select')[0].options[1].textContent"));

        page.Do("rememberShipPaint('DRAK_Corsair', 'Paint_Corsair_Commando');");
        Assert.Equal("Paint_Corsair_Commando", page.Text("shipPaints.DRAK_Corsair"));

        page.Do("rememberShipPaint('DRAK_Corsair', null);");
        Assert.True(page.Truth("shipPaints.DRAK_Corsair === undefined"));
    }

    /// <summary>
    /// Nothing picked and the game pictures paints for the hull: the first
    /// one stands in, labelled as a stand-in - which paint a ship wears is not
    /// in the logs, so the card cannot claim it is the pilot's.
    /// </summary>
    [Fact]
    public void Without_a_chosen_paint_the_first_the_game_pictures_stands_in()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Corsair", """
            [{"item":"Paint_Corsair_BIS2953_Purple_Blue_Cyan","name":"Corsair 2953 Best in Show Livery"},
             {"item":"Paint_Corsair_Commando","name":"Corsair Commando Livery"}]
            """);
        page.Do($"__dom.node('#t').append(shipPicture({Corsair}, {Maker})); await paintsForHull('DRAK_Corsair'); await Promise.resolve();");

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
        Assert.Contains("Paint_Corsair_BIS2953_Purple_Blue_Cyan/render", page.Text("__dom.node('#t').byClass('ship-render')[0].src"));
        Assert.Contains("the files hold no picture of its default livery", page.Text("__dom.node('#t').byClass('ship-render')[0].title"));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
        // Standing in is not picking: nothing is remembered for the hull.
        Assert.True(page.Truth("shipPaints.DRAK_Corsair === undefined"));
    }

    /// <summary>
    /// When the files hold the hull's default livery, the server lists it
    /// first, and the card says it is the default rather than a stand-in.
    /// </summary>
    [Fact]
    public void A_default_livery_the_files_hold_is_shown_as_the_default()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Clipper", """
            [{"item":"paint_clipper_default","name":"Default livery","stock":true},
             {"item":"Paint_Clipper_Black_Cream_Red","name":"Clipper Auspicious Livery","stock":false}]
            """);
        page.Do("__dom.node('#t').append(shipPicture({name:'Drake Clipper', className:'DRAK_Clipper'}, {code:'DRAK', name:'Drake Interplanetary', model:'Clipper'})); await paintsForHull('DRAK_Clipper'); await Promise.resolve();");

        Assert.Contains("paint_clipper_default/render", page.Text("__dom.node('#t').byClass('ship-render')[0].src"));
        Assert.Contains("in its default livery", page.Text("__dom.node('#t').byClass('ship-render')[0].title"));
        Assert.DoesNotContain("not necessarily", page.Text("__dom.node('#t').byClass('ship-render')[0].title"));
    }

    [Fact]
    public void The_maker_tint_preference_keeps_an_unpicked_ship_as_a_silhouette()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Corsair", """[{"item":"Paint_Corsair_Commando","name":"Corsair Commando Livery"}]""");
        page.Do($"shipPictureStyle = 'tint'; __dom.node('#t').append(shipPicture({Corsair}, {Maker})); await Promise.resolve();");

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
        Assert.True(page.Truth("shipPaints.DRAK_Corsair === undefined"));
    }

    /// <summary>The pilot can still ask for the silhouette, and that choice is kept apart from never having picked.</summary>
    [Fact]
    public void Choosing_the_silhouette_over_the_stand_in_is_remembered()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Corsair", """[{"item":"Paint_Corsair_Commando","name":"Corsair Commando Livery"}]""");
        page.Do($"""
            const box = shipPicture({Corsair}, {Maker});
            __dom.node('#t').append(box);
            await openPaintChooser({Corsair}, box, box.byClass('ship-paint')[0]);
            const select = box.byClass('ship-paint-select')[0];
            select.value = 'silhouette';
            select.fire('change');
            """);

        Assert.Equal("silhouette", page.Text("shipPaints.DRAK_Corsair"));
        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
    }

    /// <summary>
    /// A pick redraws the card it was made on, wherever that card sits. It
    /// used to redraw the Fleet cards only, so a pick on the Hangar showed
    /// after a reload and not before.
    /// </summary>
    [Fact]
    public void Picking_a_paint_redraws_the_card_it_was_picked_on()
    {
        var page = Fresh();
        page.Serve("/api/fleet/paints/DRAK_Corsair", """[{"item":"Paint_Corsair_Commando","name":"Corsair Commando Livery"}]""");
        page.Do($"""
            const box = shipPicture({Corsair}, {Maker});
            __dom.node('#t').append(box);
            await openPaintChooser({Corsair}, box, box.byClass('ship-paint')[0]);
            const select = box.byClass('ship-paint-select')[0];
            select.value = 'Paint_Corsair_Commando';
            select.fire('change');
            """);

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-render').length")));
        Assert.Contains("Paint_Corsair_Commando/render", page.Text("__dom.node('#t').byClass('ship-render')[0].src"));
        Assert.Equal("Paint_Corsair_Commando", page.Text("shipPaints.DRAK_Corsair"));
    }

    /// <summary>A render that fails to load shows the silhouette for now and keeps the pick.</summary>
    [Fact]
    public void A_render_that_fails_keeps_the_pick_and_shows_the_silhouette()
    {
        var page = Fresh();
        page.Do($$"""
            shipPaints = {DRAK_Corsair: 'Paint_Corsair_Commando'};
            const box = shipPicture({{Corsair}}, {{Maker}});
            __dom.node('#t').append(box);
            box.byClass('ship-render')[0].fire('error');
            """);

        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#t').byClass('ship-outline').length")));
        Assert.Equal("Paint_Corsair_Commando", page.Text("shipPaints.DRAK_Corsair"));
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
