namespace Quantumwake.WebTests;

/// <summary>
/// The hangar: the fleet drawn to one scale from the install's own sizes and
/// silhouettes.
/// </summary>
/// <remarks>
/// What is defended is the scale. Every ship must be sized by its own length
/// against the longest, an icon-less ship must be a box at its size rather
/// than nothing, a ship the install cannot size must be named below rather
/// than drawn at a guess, and an install whose game data is unread must be
/// told so rather than shown an empty deck.
/// </remarks>
public class HangarTests
{
    private const string Fleet = """
        {"available":true,"ships":[
          {"name":"Anvil C8X Pisces Expedition","className":"ANVL_C8X_Pisces_Expedition","sorties":12,"lastFlown":"2026-09-10T20:00:00Z","hours":3.5,
           "beam":12,"length":16,"height":8,"icon":true},
          {"name":"Drake Corsair","className":"DRAK_Corsair","sorties":40,"lastFlown":"2026-09-01T20:00:00Z","hours":30,
           "beam":30,"length":53,"height":25,"icon":true},
          {"name":"Drake Clipper","className":"DRAK_Clipper","sorties":2,"lastFlown":"2026-09-11T01:00:00Z","hours":1,
           "beam":18,"length":26.5,"height":21,"icon":false},
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
        var pisces = double.Parse(Attr(page, 2, "image", "width"), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(53.0 / 16.0, corsair / pisces, 3);
        Assert.Contains("Drake Corsair", page.NodeText("#hangar-canvas"));
        Assert.Contains("/api/fleet/icons/DRAK_Corsair", Attr(page, 0, "image", "href"));
        // Four flown, three drawn: the count is of the fleet, the drawing of what can be sized.
        Assert.Equal("4 ships flown", page.NodeText("#hangar-count"));
    }

    [Fact]
    public void A_ship_without_a_silhouette_is_a_box_at_its_size_and_one_without_a_size_is_named_below()
    {
        var page = Loaded();

        // The Clipper (26.5 m) sits between the Corsair and the Pisces, as a box at
        // its length: the stub has no clientWidth, so the canvas is 1200 px and the
        // scale is 580 / 53 px a metre - the longest ship takes half the width.
        Assert.Equal(26.5 * 580 / 53, double.Parse(Attr(page, 1, "rect", "width"), System.Globalization.CultureInfo.InvariantCulture), 2);
        Assert.Contains("hangar-box", Attr(page, 1, "rect", "class"));
        Assert.Contains("Drake Clipper", page.NodeText("#hangar-canvas"));

        Assert.False(page.Truth("__dom.node('#hangar-unsized').hidden"));
        Assert.Contains("Mystery Hull", page.NodeText("#hangar-unsized"));
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
        Assert.Contains("Drake Clipper", page.Text("__dom.node('#hangar-canvas').byClass('hangar-ship')[0].textContent"));
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

        Assert.Equal(4, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-card').length")));
        Assert.Equal(0, Convert.ToInt32(page.Eval("__dom.node('#hangar-canvas').byClass('hangar-ship').length")));

        // The Corsair wears its chosen paint; the Pisces is the tinted silhouette.
        Assert.Contains("/api/fleet/paints/Paint_Corsair_Olive_Olive_Yellow/render",
            page.Text("__dom.node('#hangar-canvas').byClass('hangar-card')[0].byClass('ship-render')[0].src"));
        Assert.Equal("#8fd18a", page.Text("__dom.node('#hangar-canvas').byClass('hangar-card')[2].byClass('ship-outline')[0].style['--tint']"));

        var text = page.NodeText("#hangar-canvas");
        Assert.Contains("53 × 30 × 25 m", text);
        Assert.Contains("40 sorties", text);
        Assert.Contains("Mystery Hull", text);
        Assert.Contains("no size", text);
        Assert.Equal("", page.NodeText("#hangar-scale"));
        Assert.True(page.Truth("__dom.node('#hangar-zoom').hidden"));
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
