namespace Quantumwake.WebTests;

/// <summary>
/// The flight plan as the player sees it: the Now card, the panel that edits
/// it, and the numbered stops drawn over the map.
/// </summary>
public class FlightPlanTests
{
    private const string Plan = """
        [
          {"id":"t1","title":"Laranite run","createdAt":"2026-08-22T09:00:00Z","tracked":true,
           "stops":[
             {"id":"s1","placeId":"Stanton1_Lorville","place":"Lorville","note":"Buy 96 SCU","done":true},
             {"id":"s2","placeId":"RR_MIC_L1","place":"microTech L1","note":"Sell at 1,656","done":false},
             {"id":"s3","placeId":"GrimHEX","place":"GrimHEX","note":"Pick up armour","done":false}
           ]},
          {"id":"t2","title":"Old run","createdAt":"2026-08-01T09:00:00Z","tracked":false,
           "stops":[{"id":"s9","placeId":"GrimHEX","place":"GrimHEX","note":null,"done":true}]}
        ]
        """;

    private static Page Tracking()
    {
        var page = new Page();
        page.Serve("/api/trips", Plan);
        page.Do("await loadTrips();");
        return page;
    }

    [Fact]
    public void The_next_stop_is_the_first_one_not_crossed_off()
    {
        var page = Tracking();

        Assert.Equal("microTech L1", page.Text("nextStop(tracked()).place"));
    }

    [Fact]
    public void The_now_card_leads_with_where_to_jump_next()
    {
        var page = Tracking();

        Assert.Equal("Laranite run", page.NodeText("#now-trip-title"));
        Assert.Contains("Jump next", page.NodeText("#now-trip-next"));
        Assert.Contains("microTech L1", page.NodeText("#now-trip-next"));
        Assert.Contains("Sell at 1,656", page.NodeText("#now-trip-next"));
    }

    [Fact]
    public void The_now_card_lists_the_whole_run_in_order()
    {
        var page = Tracking();

        Assert.Equal(3, page.Count("__dom.node('#now-trip-stops').byClass('trip-stop').length"));
        Assert.Equal("✓", page.Text("__dom.node('#now-trip-stops').byClass('trip-number')[0].textContent"));
        Assert.Equal("2", page.Text("__dom.node('#now-trip-stops').byClass('trip-number')[1].textContent"));
        Assert.True(page.Truth(
            "__dom.node('#now-trip-stops').byClass('trip-stop')[1].classList.contains('next')"));
    }

    [Fact]
    public void A_finished_plan_says_so_rather_than_naming_a_next_stop()
    {
        var page = new Page();
        page.Serve("/api/trips", """
            [{"id":"t1","title":"Done run","createdAt":"2026-08-22T09:00:00Z","tracked":true,
              "stops":[{"id":"s1","placeId":"GrimHEX","place":"GrimHEX","note":null,"done":true}]}]
            """);

        page.Do("await loadTrips();");

        Assert.Contains("crossed off", page.NodeText("#now-trip-next"));
    }

    [Fact]
    public void No_tracked_plan_means_no_card()
    {
        var page = new Page();
        page.Serve("/api/trips", "[]");
        page.Do("await loadTrips();");

        Assert.True(page.Truth("__dom.node('#now-trip-card').hidden"));
    }

    /// <summary>Crossing a stop off is the thing done most; it must reach the server.</summary>
    [Fact]
    public void Clicking_a_number_crosses_that_stop_off()
    {
        var page = Tracking();

        page.Do("__dom.node('#now-trip-stops').byClass('trip-number')[2].fire('click');");

        Assert.Contains("POST /api/trips/t1/stops/s3/toggle", page.Fetched());
    }

    [Fact]
    public void The_panel_offers_the_plans_not_being_followed()
    {
        var page = Tracking();
        page.Do("showTripPanel();");

        var body = page.NodeText("#cargo-body");

        Assert.Contains("Other plans", body);
        Assert.Contains("Old run", body);
        Assert.Contains("1 of 1 stops", body);
    }

    [Fact]
    public void The_panel_says_what_to_do_when_there_is_no_plan()
    {
        var page = new Page();
        page.Serve("/api/trips", "[]");
        page.Do("await loadTrips(); showTripPanel();");

        Assert.Contains("Double-click a place", page.NodeText("#cargo-body"));
    }

    /// <summary>
    /// The map draws the run in order: a number per stop, a tick for one done,
    /// and a leg joining each to the next.
    /// </summary>
    [Fact]
    public void The_map_numbers_the_stops_it_can_place()
    {
        var page = Tracking();

        page.Do("""
            nodeAt.set('Stanton1_Lorville', { x: 10, y: 10 });
            nodeAt.set('RR_MIC_L1', { x: 50, y: 40 });
            nodeAt.set('GrimHEX', { x: 90, y: 20 });
            drawTripPath();
            """);

        // A badge reads as its number followed by the tooltip that names the stop.
        var marks = "__dom.node('#starmap').byClass('trip-mark')";

        Assert.Equal(3, page.Count($"{marks}.length"));
        Assert.Equal(2, page.Count("__dom.node('#starmap').byClass('trip-leg').length"));
        Assert.StartsWith("✓Stop 1: Lorville", page.Text($"{marks}[0].textContent"));
        Assert.StartsWith("2Stop 2: microTech L1", page.Text($"{marks}[1].textContent"));
        Assert.True(page.Truth(
            "__dom.node('#starmap').byClass('trip-mark')[1].classList.contains('next')"));
    }

    /// <summary>
    /// A stop from a terminal name the atlas could not place has no dot. It must
    /// still be on the plan, and must not take a number that belongs to another.
    /// </summary>
    [Fact]
    public void A_stop_the_map_cannot_place_is_left_off_the_map_only()
    {
        var page = Tracking();

        page.Do("""
            nodeAt.set('Stanton1_Lorville', { x: 10, y: 10 });
            nodeAt.set('GrimHEX', { x: 90, y: 20 });
            drawTripPath();
            """);

        Assert.Equal(2, page.Count("__dom.node('#starmap').byClass('trip-mark').length"));
        Assert.StartsWith("3Stop 3: GrimHEX",
            page.Text("__dom.node('#starmap').byClass('trip-mark')[1].textContent"));
        Assert.Equal(3, page.Count("__dom.node('#now-trip-stops').byClass('trip-stop').length"));
    }

    [Fact]
    public void Redrawing_does_not_stack_a_second_plan_on_the_map()
    {
        var page = Tracking();

        page.Do("""
            nodeAt.set('GrimHEX', { x: 90, y: 20 });
            drawTripPath();
            drawTripPath();
            """);

        Assert.Equal(1, page.Count("__dom.node('#starmap').byClass('trip-layer').length"));
    }

    [Fact]
    public void Adding_a_stop_sends_the_place_and_its_id()
    {
        var page = Tracking();

        page.Do("await addStop('GrimHEX', 'GrimHEX', 'Pick up armour');");

        var body = page.BodyOf("/api/trips/stops");

        Assert.Contains("\"placeId\":\"GrimHEX\"", body);
        Assert.Contains("\"note\":\"Pick up armour\"", body);
    }
}
