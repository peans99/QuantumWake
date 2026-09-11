namespace Quantumwake.WebTests;

/// <summary>
/// The Points page: the coordinates the pilot chose to keep, and why.
/// </summary>
/// <remarks>
/// The note is the whole reason the page exists - the Log aside already had a
/// name and a category - so the failures worth catching are a note that does
/// not reach the server, one that is lost by a rename, and a page that shows
/// nothing rather than saying there is nothing.
/// </remarks>
public class PointsTests
{
    private const string Pins = """
        [{"sourceAt":"2026-09-09T02:10:00Z","pinnedAt":"2026-09-09T02:12:00Z",
          "x":-9641671346.9,"y":-11490734321.2,"z":-91805.1,"gigametres":14.99996,
          "believed":"Ruin Station","system":"Pyro","label":"Ruin mining shelf","category":"Mining",
          "note":"Quantanium on the north face, two rocks left.",
          "believedBy":"Jump","believedAt":"2026-09-09T01:30:00Z"},
         {"sourceAt":"2026-09-08T20:00:00Z","pinnedAt":"2026-09-08T20:01:00Z",
          "x":1.5,"y":2.5,"z":3.5,"gigametres":0.001,
          "believed":null,"system":"Stanton","label":null,"category":"General","note":null}]
        """;

    private static Page Loaded(string pins = Pins)
    {
        var page = new Page();
        page.Serve("/api/screen/pins", pins);
        page.Do("""
            allSessions = [{id:'s1', startedAt:'2026-09-09T01:00:00Z', endedAt:'2026-09-09T03:00:00Z', primaryShip:'Drake Corsair'}];
            await loadPoints();
            """);
        return page;
    }

    private static int Cards(Page page) =>
        Convert.ToInt32(page.Eval("__dom.node('#points-list').byClass('point-card').length"));

    [Fact]
    public void Every_kept_point_is_a_card_with_its_name_note_and_coordinates()
    {
        var page = Loaded();
        var list = page.NodeText("#points-list");

        Assert.Equal(2, Cards(page));
        Assert.Contains("Quantanium on the north face", page.Text("__dom.node('#points-list').byClass('point-note')[0].value"));
        Assert.Equal("Ruin mining shelf", page.Text("__dom.node('#points-list').byClass('point-name')[0].value"));
        Assert.Contains("-9,641,671,347", list);
        Assert.Contains("15.0000 Gm from system centre", list);
        Assert.Contains("believed to be Pyro › Ruin Station when copied", list);
        Assert.Equal("2 kept", page.NodeText("#points-count"));
    }

    /// <summary>
    /// A point without a name is still shown by what the app believed it was,
    /// so it can be found before it is named.
    /// </summary>
    [Fact]
    public void An_unnamed_point_falls_back_to_what_was_believed()
    {
        var page = Loaded();

        Assert.Equal("Stanton", page.Text("__dom.node('#points-list').byClass('point-name')[1].placeholder"));
    }

    /// <summary>
    /// The session the copy fell in is named, and a moment no session covers
    /// says so instead of naming the nearest one.
    /// </summary>
    [Fact]
    public void The_session_around_the_copy_is_named_when_there_is_one()
    {
        var list = Loaded().NodeText("#points-list");

        Assert.Contains("during the session of", list);
        Assert.Contains("Drake Corsair", list);
        Assert.Contains("no session in the library spans that moment", list);
    }

    /// <summary>
    /// Opened straight from the URL, the page draws before the sessions have
    /// arrived; when they do, the cards must say which session it was rather
    /// than keep the answer they gave when they knew nothing.
    /// </summary>
    [Fact]
    public void Cards_drawn_before_the_sessions_arrived_learn_their_session_afterwards()
    {
        var page = new Page();
        page.Serve("/api/screen/pins", Pins);
        page.Do("allSessions = []; await loadPoints();");
        Assert.DoesNotContain("during the session of", page.NodeText("#points-list"));

        page.Do("""
            allSessions = [{id:'s1', startedAt:'2026-09-09T01:00:00Z', endedAt:'2026-09-09T03:00:00Z', primaryShip:'Drake Corsair'}];
            renderPoints();
            """);
        Assert.Contains("during the session of", page.NodeText("#points-list"));
    }

    [Fact]
    public void Saving_sends_the_name_category_and_note_together()
    {
        var page = Loaded();
        page.Serve("/api/screen/pins", """
            {"sourceAt":"2026-09-09T02:10:00Z","x":-9641671346.9,"y":-11490734321.2,"z":-91805.1,"gigametres":14.99996,
             "believed":"Ruin Station","system":"Pyro","label":"North face vein","category":"Mining",
             "note":"Bring the Prospector."}
            """);
        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"pins":[],"total":0,"pastes":0}""");

        page.Do("""
            const card = __dom.node('#points-list').byClass('point-card')[0];
            card.byClass('point-name')[0].value = 'North face vein';
            card.byClass('point-note')[0].value = 'Bring the Prospector.';
            await card.byClass('point-save')[0].fire('click');
            """);

        Assert.Contains("PUT /api/screen/pins", page.Fetched());
        var body = page.BodyOf("/api/screen/pins");
        Assert.Contains("\"sourceAt\":\"2026-09-09T02:10:00Z\"", body);
        Assert.Contains("\"label\":\"North face vein\"", body);
        Assert.Contains("\"category\":\"Mining\"", body);
        Assert.Contains("\"note\":\"Bring the Prospector.\"", body);

        // The card is redrawn from the server's answer, not from what was typed.
        Assert.Equal("Bring the Prospector.", page.Text("__dom.node('#points-list').byClass('point-note')[0].value"));
    }

    /// <summary>
    /// The belief is graded by its evidence, in the open: a quantum jump forty
    /// minutes before the copy is said to be one, and a copy the logs could
    /// not place asks for the system rather than showing nothing.
    /// </summary>
    [Fact]
    public void The_belief_says_what_it_rests_on()
    {
        var list = Loaded().NodeText("#points-list");

        Assert.Contains("believed to be Pyro › Ruin Station when copied, from a quantum jump 40 min earlier", list);
        Assert.Contains("not that it arrived", list);

        var unplaced = Loaded("""[{"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":0.1}]""")
            .NodeText("#points-list");
        Assert.Contains("the logs could not place this copy", unplaced);
    }

    [Fact]
    public void A_system_the_pilot_set_is_shown_as_theirs()
    {
        var list = Loaded("""
            [{"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":0.1,
              "system":"Nyx","believed":"Ruin Station","systemByPilot":true}]
            """).NodeText("#points-list");

        Assert.Contains("in Nyx, as you set it", list);
        Assert.Contains("the logs had said Ruin Station", list);
        Assert.DoesNotContain("believed to be", list);
    }

    /// <summary>
    /// The system goes to the server only when the pilot changed it. An
    /// unchanged select sent back would mark the logs' guess as the pilot's word.
    /// </summary>
    [Fact]
    public void The_system_is_sent_only_when_it_was_changed()
    {
        var page = Loaded();
        page.Serve("/api/screen/pins", """{"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":15,"system":"Pyro"}""");
        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"pins":[],"total":0,"pastes":0}""");

        page.Do("await __dom.node('#points-list').byClass('point-save')[0].fire('click');");
        Assert.DoesNotContain("\"system\":\"", page.BodyOf("/api/screen/pins"));

        page.Do("""
            const card = __dom.node('#points-list').byClass('point-card')[0];
            card.byClass('point-system')[0].value = 'Stanton';
            await card.byClass('point-save')[0].fire('click');
            """);
        Assert.Contains("\"system\":\"Stanton\"", page.BodyOf("/api/screen/pins"));
    }

    private const string FromHere = """
        {"from":{"at":"2026-09-11T00:58:00Z","x":0,"y":0,"z":0,"gigametres":0,"believed":"Ruin Station","system":"Pyro"},
         "points":[{"sourceAt":"2026-09-09T02:10:00Z","label":"Ruin mining shelf","system":"Pyro","metres":12400,"sameSystem":true},
                   {"sourceAt":"2026-09-08T20:00:00Z","label":"Stanton","system":"Stanton","metres":4,"sameSystem":false}]}
        """;

    /// <summary>
    /// Every card measured from where the pilot last copied, the banner saying
    /// where that was, and a point in another system named as not measurable
    /// rather than given a number from the wrong frame.
    /// </summary>
    [Fact]
    public void Cards_are_measured_from_where_the_pilot_last_copied()
    {
        var page = new Page();
        page.Serve("/api/screen/pins", Pins);
        page.Serve("/api/screen/pins/nearest", FromHere);
        page.Do("allSessions = []; await loadPoints();");

        Assert.False(page.Truth("__dom.node('#points-from').hidden"));
        var banner = page.NodeText("#points-from");
        Assert.Contains("Distances are from where you last copied a location", banner);
        Assert.Contains("believed to be Pyro › Ruin Station", banner);

        var list = page.NodeText("#points-list");
        Assert.Contains("12.4 km from where you last copied", list);
        Assert.Contains("not measurable from where you last copied — a different system", list);
        Assert.DoesNotContain("4 m from", list);
    }

    [Fact]
    public void With_nothing_copied_yet_no_distance_is_claimed()
    {
        var page = new Page();
        page.Serve("/api/screen/pins", Pins);
        page.Serve("/api/screen/pins/nearest", """{"from":null,"points":[]}""");
        page.Do("allSessions = []; await loadPoints();");

        Assert.True(page.Truth("__dom.node('#points-from').hidden"));
        Assert.DoesNotContain("from where you last copied", page.NodeText("#points-list"));
    }

    [Theory]
    [InlineData(412, "412 m")]
    [InlineData(12_400, "12.4 km")]
    [InlineData(1_234_567, "1,235 km")]
    [InlineData(3_996_000_000, "3.996 Gm")]
    public void A_distance_is_said_in_the_unit_a_pilot_would_use(double metres, string expected)
    {
        Assert.Equal(expected, new Page().Text($"distanceWord({metres})"));
    }

    /// <summary>
    /// Two notes half-typed, one saved: the other must still be on the page.
    /// Redrawing the whole list on save threw it away, with no undo.
    /// </summary>
    [Fact]
    public void Saving_one_card_keeps_what_was_typed_on_the_others()
    {
        var page = Loaded();
        page.Serve("/api/screen/pins", """
            {"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":15,
             "label":"Ruin mining shelf","category":"Mining","note":"Saved note."}
            """);
        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"pins":[],"total":0,"pastes":0}""");

        page.Do("""
            const cards = __dom.node('#points-list').byClass('point-card');
            cards[1].byClass('point-note')[0].value = 'Still being typed';
            cards[1].byClass('point-note')[0].fire('input');
            cards[0].byClass('point-note')[0].value = 'Saved note.';
            await cards[0].byClass('point-save')[0].fire('click');
            """);

        Assert.Equal("Saved note.", page.Text("__dom.node('#points-list').byClass('point-note')[0].value"));
        Assert.Equal("Saved.", page.Text("__dom.node('#points-list').byClass('point-said')[0].textContent"));
        Assert.Equal("Still being typed", page.Text("__dom.node('#points-list').byClass('point-note')[1].value"));
        Assert.True(page.Truth("__dom.node('#points-list').byClass('point-card')[1].classList.contains('dirty')"));
        Assert.False(page.Truth("__dom.node('#points-list').byClass('point-card')[0].classList.contains('dirty')"));

        // And the redrawn card is still live: a second save goes through it.
        page.Do("""
            const card = __dom.node('#points-list').byClass('point-card')[0];
            card.byClass('point-note')[0].value = 'Saved twice.';
            await card.byClass('point-save')[0].fire('click');
            """);
        Assert.Contains("\"note\":\"Saved twice.\"", page.BodyOf("/api/screen/pins"));
    }

    /// <summary>
    /// The only failure that loses a reason quietly: a save that fails must
    /// leave the typed note on the page and say so.
    /// </summary>
    [Fact]
    public void A_save_that_fails_keeps_the_note_on_the_page_and_says_so()
    {
        var page = Loaded();
        page.Fail("/api/screen/pins", 404, """{"trouble":"that point of interest is already gone"}""");

        page.Do("""
            const card = __dom.node('#points-list').byClass('point-card')[0];
            card.byClass('point-note')[0].value = 'Typed and not yet saved';
            await card.byClass('point-save')[0].fire('click');
            """);

        Assert.Equal("Typed and not yet saved", page.Text("__dom.node('#points-list').byClass('point-note')[0].value"));
        Assert.Contains("Could not save", page.Text("__dom.node('#points-list').byClass('point-said')[0].textContent"));
        Assert.False(page.Truth("__dom.node('#points-list').byClass('point-save')[0].disabled"));
    }

    [Fact]
    public void Removing_a_point_takes_its_card_away()
    {
        var page = Loaded();
        page.Serve("/api/screen/pins?at=2026-09-09T02%3A10%3A00Z", """{"removed":true}""");
        page.Serve("/api/screen/readings?take=50", """{"readings":[],"clipboard":[],"pins":[],"total":0,"pastes":0}""");

        page.Do("await __dom.node('#points-list').byClass('point-remove')[0].fire('click');");

        Assert.Contains("DELETE /api/screen/pins?at=2026-09-09T02%3A10%3A00Z", page.Fetched());
        Assert.Equal(1, Cards(page));
        Assert.Equal("1 kept", page.NodeText("#points-count"));
    }

    [Fact]
    public void The_search_reads_the_note_as_well_as_the_name()
    {
        var page = Loaded();

        page.Do("__dom.node('#points-search').value = 'two rocks'; renderPoints();");
        Assert.Equal(1, Cards(page));

        page.Do("__dom.node('#points-search').value = 'nothing like this'; renderPoints();");
        Assert.Contains("No point matches", page.NodeText("#points-list"));
    }

    [Fact]
    public void Categories_become_chips_only_when_there_is_more_than_one()
    {
        var page = Loaded();

        Assert.Contains("Mining · 1", page.NodeText("#points-categories"));
        Assert.Contains("All · 2", page.NodeText("#points-categories"));

        page.Do("__dom.node('#points-categories').byClass('ghost')[1].fire('click');");
        Assert.Equal(1, Cards(page));

        var one = Loaded("""[{"sourceAt":"2026-09-09T02:10:00Z","x":1,"y":2,"z":3,"gigametres":0.1,"category":"Mining"}]""");
        Assert.Equal("", one.NodeText("#points-categories"));
    }

    /// <summary>No points is a sentence about how to get one, never an empty page.</summary>
    [Fact]
    public void No_points_says_how_to_make_one()
    {
        var list = Loaded("[]").NodeText("#points-list");

        Assert.Contains("No points yet", list);
        Assert.Contains("/showlocation", list);
        Assert.Contains("Log", list);
    }
}
