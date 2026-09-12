namespace Quantumwake.WebTests;

/// <summary>
/// The hangar: the fleet drawn to one scale from the install's own sizes and
/// silhouettes.
/// </summary>
/// <remarks>
/// What is defended is the scale. Every ship must be sized by its own length
/// against the longest pictured hull. A ship whose game icon is absent is
/// named with its verified dimensions below rather than given a made-up shape,
/// and an install whose game data is unread must be told so rather than shown
/// an empty deck.
/// </remarks>
public class HangarTests
{
    private const string Fleet = """
        {"available":true,"ships":[
          {"name":"Anvil C8X Pisces Expedition","className":"ANVL_C8X_Pisces_Expedition","sorties":12,"lastFlown":"2026-09-10T20:00:00Z","hours":3.5,
           "beam":12,"length":16,"height":8,"icon":true,"kind":"Spaceship"},
          {"name":"Drake Corsair","className":"DRAK_Corsair","sorties":40,"lastFlown":"2026-09-01T20:00:00Z","hours":30,
           "beam":30,"length":53,"height":25,"icon":true,"kind":"Spaceship"},
          {"name":"Drake Clipper","className":"DRAK_Clipper","sorties":2,"lastFlown":"2026-09-11T01:00:00Z","hours":1,
           "beam":18,"length":26.5,"height":21,"icon":false,"kind":"Spaceship"},
          {"name":"Greycat PTV","className":"GRIN_PTV","sorties":5,"lastFlown":"2026-09-02T20:00:00Z","hours":0.5,
           "beam":2.6,"length":4,"height":2.5,"icon":true,"kind":"Ground"},
          {"name":"Mystery Hull","className":"XXXX_Unknown","sorties":1,"lastFlown":"2026-08-01T20:00:00Z","hours":0.2,
           "beam":null,"length":null,"height":null,"icon":false}]}
        """;

    private static Page Loaded(string fleet = Fleet)
    {
        var page = new Page();
        page.Serve("/api/fleet/hangar", fleet);
        page.Do("__dom.node('#hangar-mode').value = 'scale'; __dom.node('#hangar-sort').value = 'length'; __dom.node('#hangar-zoom').value = '1'; shipPaints = {}; await loadHangar();");
        return page;
    }

    private static string Attr(Page page, int ship, string child, string name) =>
        page.Text($"__dom.node('#hangar-canvas').byClass('hangar-ship')[{ship}].querySelector('{child}').getAttribute('{name}')");

    [Fact]
    public void Every_sized_ship_is_drawn_at_its_length_against_the_longest()
    {
        var page = Loaded();

        Assert.Equal(3, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-ship').length")));

        // Longest first, and the Corsair's drawn width is 53/16 of the Pisces's.
        var corsair = double.Parse(Attr(page, 0, "image", "width"), System.Globalization.CultureInfo.InvariantCulture);
        var pisces = double.Parse(Attr(page, 1, "image", "width"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(53.0 / 16.0, corsair / pisces, 3);
        Assert.Contains("Drake Corsair", page.NodeText("#hangar-canvas"));
        Assert.Contains("/api/fleet/icons/DRAK_Corsair", Attr(page, 0, "image", "href"));
        // Five flown, three pictured: the count is of the fleet, the drawing of what has a shape.
        Assert.Equal("5 ships", page.NodeText("#hangar-count"));
    }

    [Fact]
    public void Two_long_ships_fit_on_the_same_shelf_at_the_normal_zoom()
    {
        var page = Loaded();

        // "Two big ships across" must not wrap the second one because its
        // inter-ship gap was left out of the scale calculation.
        var first = page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[0].getAttribute('transform')");
        var second = page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[1].getAttribute('transform')");
        Assert.Equal(first.Split(' ')[1], second.Split(' ')[1]);
    }

    [Fact]
    public void A_ship_without_a_game_silhouette_is_listed_below_the_scale_drawing()
    {
        var page = Loaded();

        Assert.False(page.Truth("__dom.node('#hangar-unsized').hidden"));
        var omitted = page.NodeText("#hangar-unsized");
        Assert.Contains("Drake Clipper (26.5 × 18 × 21 m)", omitted);
        Assert.Contains("Mystery Hull", omitted);
        Assert.DoesNotContain("Drake Clipper", page.NodeText("#hangar-canvas"));
        Assert.DoesNotContain("Mystery Hull", page.NodeText("#hangar-canvas"));
    }

    /// <summary>
    /// The icon is white; the tint is the maker's colour through an SVG filter
    /// that keeps the shape exact - and it is on the attribute, because a CSS
    /// filter on the image would win over it and put the tint back to white.
    /// </summary>
    [Fact]
    public void A_silhouette_is_tinted_by_its_maker_through_a_filter_on_the_image()
    {
        var page = Loaded();

        Assert.Contains("url(#hangar-tint-DRAK_Corsair)", Attr(page, 0, "image", "filter"));
        Assert.Equal("#f0954a", page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[0].querySelector('filter').children[0].getAttribute('flood-color')"));
        Assert.Contains("tinted by maker", page.NodeText("#hangar-scale"));
    }

    [Fact]
    public void The_scale_drawing_uses_the_paint_chosen_on_the_fleet_card()
    {
        var page = new Page();
        page.Serve("/api/fleet/hangar", Fleet);
        page.Do("""
            __dom.node('#hangar-mode').value = 'scale'; __dom.node('#hangar-sort').value = 'length';
            shipPaints = {DRAK_Corsair: 'Paint_Corsair_Olive_Olive_Yellow'};
            await loadHangar();
            """);

        Assert.Contains("/api/fleet/paints/Paint_Corsair_Olive_Olive_Yellow/render", Attr(page, 0, "image", "href"));
        Assert.Equal("", Attr(page, 0, "image", "filter"));
        page.Do("shipPaints = {};");
    }

    [Fact]
    public void The_scale_bar_names_a_round_number_of_metres()
    {
        var scale = Loaded().NodeText("#hangar-scale");

        Assert.Matches(@"\b(10|25|50|100|200) m\b", scale);
        Assert.Contains("bounding boxes", scale);
    }

    [Fact]
    public void Sorting_by_sorties_puts_the_most_flown_first()
    {
        var page = Loaded();
        page.Do("__dom.node('#hangar-sort').value = 'sorties'; renderHangar();");

        Assert.Contains("Drake Corsair", page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[0].textContent"));

        page.Do("__dom.node('#hangar-sort').value = 'recent'; renderHangar();");
        Assert.Contains("Anvil C8X Pisces", page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[0].textContent"));
    }

    /// <summary>
    /// The gallery is the default: one card a ship, the Fleet card's picture
    /// larger - the chosen paint's render, or the tinted silhouette - with
    /// the size as a fact under it. Not to scale, and it does not claim to be:
    /// no scale bar, and the unsized ship is a card that says it has no size.
    /// </summary>
    [Fact]
    public void The_gallery_shows_every_ship_as_a_card_with_its_picture_and_size()
    {
        var page = new Page();
        page.Serve("/api/fleet/hangar", Fleet);
        page.Do("""
            __dom.node('#hangar-mode').value = 'gallery'; __dom.node('#hangar-sort').value = 'length';
            shipPaints = {DRAK_Corsair: 'Paint_Corsair_Olive_Olive_Yellow'};
            await loadHangar();
            """);

        Assert.Equal(5, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-card').length")));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-ship').length")));

        // The Corsair wears its chosen paint; the Pisces is the tinted silhouette.
        Assert.Contains("/api/fleet/paints/Paint_Corsair_Olive_Olive_Yellow/render",
            page.Text("__dom.node('#hangar-canvas').byClass('hangar-card')[0].byClass('ship-render')[0].src"));
        Assert.Equal(1, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-card')[0].byClass('ship-render-wrap').length")));
        Assert.Equal("#8fd18a", page.Text("__dom.node('#hangar-canvas').byClass('hangar-card')[2].byClass('ship-outline')[0].style['--tint']"));

        var text = page.NodeText("#hangar-canvas");
        Assert.Contains("53 × 30 × 25 m", text);
        Assert.Contains("40 sorties", text);
        Assert.Contains("Mystery Hull", text);
        Assert.Contains("no size", text);
        Assert.Equal("", page.NodeText("#hangar-scale"));
        Assert.True(page.Truth("__dom.node('#hangar-zoom').hidden"));
    }

    /// <summary>
    /// Ships on one shelf and ground vehicles on another, at the same scale:
    /// the PTV's 4 m against the Corsair's 53 m is the point of the drawing.
    /// </summary>
    [Fact]
    public void Ships_and_ground_vehicles_are_shelved_apart_at_one_scale()
    {
        var page = Loaded();

        Assert.Equal(2, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-group').length")));
        Assert.Contains("Ships · 2", page.Text("__dom.node('#hangar-canvas').byClass('hangar-group')[0].textContent"));
        Assert.Contains("Ground vehicles · 1", page.Text("__dom.node('#hangar-canvas').byClass('hangar-group')[1].textContent"));

        var corsair = double.Parse(Attr(page, 0, "image", "width"), System.Globalization.CultureInfo.InvariantCulture);
        var ptv = double.Parse(Attr(page, 2, "image", "width"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(53.0 / 4.0, corsair / ptv, 3);
    }

    /// <summary>The Fleet roster tick rules the hangar too, and the count says how many it hid.</summary>
    [Fact]
    public void A_ship_unticked_on_Fleet_is_not_in_the_hangar()
    {
        var page = new Page();
        page.Serve("/api/fleet/hangar", Fleet);
        page.Do("""
            __dom.node('#hangar-mode').value = 'gallery'; __dom.node('#hangar-sort').value = 'length';
            excludedShips = new Set(['Drake Clipper']); shipPaints = {};
            await loadHangar();
            """);

        Assert.Equal(4, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-card').length")));
        Assert.DoesNotContain("Drake Clipper", page.NodeText("#hangar-canvas"));
        Assert.Equal("4 ships · 1 unticked on Fleet", page.NodeText("#hangar-count"));
        page.Do("excludedShips = new Set();");
    }

    [Fact]
    public void Without_the_game_files_the_page_says_why_rather_than_showing_an_empty_deck()
    {
        var canvas = Loaded("""{"available":false,"ships":[]}""").NodeText("#hangar-canvas");

        Assert.Contains("has not been read yet", canvas);
    }

    [Fact]
    public void With_nothing_flown_the_deck_says_so()
    {
        var canvas = Loaded("""{"available":true,"ships":[]}""").NodeText("#hangar-canvas");

        Assert.Contains("No ship has been flown", canvas);
    }
}
