namespace Quantumwake.WebTests;

/// <summary>
/// The Itinerary page: the map panel's plans with room to drag them about.
/// </summary>
/// <remarks>
/// A drag is three events and a position, and the arithmetic between them is
/// where it goes wrong: a stop carried down the list lands one place short or
/// one too far, a line dropped on its own stop moves when it should not, or a
/// redraw from the live stream lands mid-drag and strands it. These drive the
/// events the browser would send and assert what the page asked the server.
/// </remarks>
public class ItineraryPageTests
{
    private const string Plans = """
        [{"id":"t1","title":"Ore run","tracked":true,"done":false,"archived":"No","flying":false,
          "stops":[
            {"id":"s1","placeId":"HUR_LOR","place":"Lorville","done":false,
             "actions":[{"id":"a1","kind":"load","text":"Crate","quantity":2,"unit":"SCU","done":false},
                        {"id":"a2","kind":"unload","text":"Crate","quantity":2,"unit":"SCU","done":false}]},
            {"id":"s2","placeId":"","place":"Wreck at the third moon","done":false,"actions":[]},
            {"id":"s3","placeId":"RR_MIC_L1","place":"MIC-L1","done":false,"actions":[]}]},
         {"id":"t2","title":"Shopping","tracked":false,"done":false,"archived":"No","flying":false,
          "stops":[{"id":"s9","placeId":"GrimHEX","place":"GrimHEX","done":false,"actions":[]}]}]
        """;

    private static Page Itinerary(string plans = Plans, string? haul = null)
    {
        var page = new Page();
        page.Serve("/api/trips", plans);
        page.Serve("/api/briefing", "{}");
        if (haul is not null) page.Serve("/api/haul/plan", haul);
        page.Do("""
            atlas = [
              { rawId: 'HUR_LOR', name: 'Lorville', system: 'Stanton', body: 'Hurston', kind: 'City', visits: 4 },
              { rawId: 'RR_MIC_L1', name: 'MIC-L1', system: 'Stanton', body: null, kind: 'Station', visits: 1 },
              { rawId: 'GrimHEX', name: 'GrimHEX', system: 'Stanton', body: 'Yela', kind: 'Station', visits: 2 },
            ];
            """);
        page.Do("await loadTrips(); showView('itinerary');");
        return page;
    }

    private const string Stops = "__dom.node('#itinerary-main').byClass('itinerary-stop')";

    [Fact]
    public void A_live_refresh_keeps_an_open_run_sheet_draft()
    {
        var page = Itinerary();
        page.Do($$"""
            {{Stops}}[0].byClass('ghost').find(b => b.textContent === '+ Line').click();
            const form = {{Stops}}[0].byClass('run-action-new-form')[0];
            form.byClass('search')[0].value = 'Load medical supplies';
            form.byClass('search')[1].value = '12';
            form.byClass('select')[1].value = 'SCU';
            await loadTrips();
            """);
        Assert.Equal("Load medical supplies", page.Text($"{Stops}[0].byClass('run-action-new-form')[0].byClass('search')[0].value"));
        Assert.Equal("12", page.Text($"{Stops}[0].byClass('run-action-new-form')[0].byClass('search')[1].value"));
        Assert.Equal("SCU", page.Text($"{Stops}[0].byClass('run-action-new-form')[0].byClass('select')[1].value"));
    }

    [Fact]
    public void A_failed_run_sheet_save_reports_the_error_and_keeps_the_draft()
    {
        var page = Itinerary();
        page.Fail("/api/trips/t1/stops/s1/actions", 500, "{}");
        page.Do($$"""
            {{Stops}}[0].byClass('ghost').find(b => b.textContent === '+ Line').click();
            const form = {{Stops}}[0].byClass('run-action-new-form')[0];
            form.byClass('search')[0].value = 'Load cargo';
            await form.byClass('ghost')[0].fire('click');
            """);
        Assert.Equal("Load cargo", page.Text($"{Stops}[0].byClass('run-action-new-form')[0].byClass('search')[0].value"));
        Assert.Contains("Could not save", page.NodeText("#itinerary-error"));
        Assert.False(page.Truth("__dom.node('#itinerary-error').hidden"));
    }

    [Fact]
    public void Plan_and_stop_deletion_can_be_cancelled()
    {
        var page = Itinerary();
        page.Do($$"""
            globalThis.confirm = () => false;
            __dom.node('#itinerary-main').byClass('danger')[0].click();
            {{Stops}}[0].byClass('ghost').find(b => b.title === 'Drop this stop').click();
            """);
        Assert.DoesNotContain(page.Fetched(), call => call.StartsWith("DELETE /api/trips/"));
    }

    [Fact]
    public void Confirmed_stop_deletion_is_sent()
    {
        var page = Itinerary();
        page.Serve("/api/trips/t1/stops/s1", "{}");
        page.Do($$"""
            globalThis.confirm = () => true;
            await {{Stops}}[0].byClass('ghost').find(b => b.title === 'Drop this stop').fire('click');
            """);
        Assert.Contains("DELETE /api/trips/t1/stops/s1", page.Fetched());
    }

    [Fact]
    public void A_saved_run_sheet_draft_closes_after_the_server_accepts_it()
    {
        var page = Itinerary();
        page.Serve("/api/trips/t1/stops/s1/actions", "{}");
        page.Do($$"""
            {{Stops}}[0].byClass('ghost').find(b => b.textContent === '+ Line').click();
            const form = {{Stops}}[0].byClass('run-action-new-form')[0];
            form.byClass('search')[0].value = 'Load cargo';
            await form.byClass('ghost')[0].fire('click');
            """);
        Assert.Equal(0, page.Count($"{Stops}[0].byClass('run-action-new-form').length"));
        Assert.Contains("Load cargo", page.BodyOf("/api/trips/t1/stops/s1/actions"));
    }

    [Fact]
    public void Line_handles_use_arrow_keys_to_reorder_and_move_between_stops()
    {
        var page = Itinerary();
        page.Serve("/api/trips/t1/stops/s1/actions/a1/move?toStop=s1&to=1", "{}");
        page.Serve("/api/trips/t1/stops/s1/actions/a1/move?toStop=s2&to=0", "{}");
        page.Do($$"""
            const grip = {{Stops}}[0].byClass('itinerary-line-grip')[0];
            await grip.fire('keydown', {key:'ArrowDown', preventDefault() {} });
            await {{Stops}}[0].byClass('itinerary-line-grip')[0].fire('keydown', {key:'ArrowRight', preventDefault() {} });
            """);
        Assert.Contains("POST /api/trips/t1/stops/s1/actions/a1/move?toStop=s1&to=1", page.Fetched());
        Assert.Contains("POST /api/trips/t1/stops/s1/actions/a1/move?toStop=s2&to=0", page.Fetched());
    }

    [Fact]
    public void A_failed_stop_save_reports_the_error_without_clearing_the_input()
    {
        var page = Itinerary();
        page.Fail("/api/trips/t1/stops", 500, "{}");
        page.Do("""
            const add = __dom.node('#itinerary-main').byClass('itinerary-add')[0];
            add.byClass('search')[0].value = 'Lorville';
            await add.byClass('ghost')[0].fire('click');
            """);
        Assert.Contains("Could not save", page.NodeText("#itinerary-error"));
        Assert.Equal("Lorville", page.Text("__dom.node('#itinerary-main').byClass('itinerary-place-input')[0].value"));
    }

    [Fact]
    public void Live_refresh_keeps_the_unfinished_plan_and_stop()
    {
        var page = Itinerary();
        page.Do("""
            __dom.node('#itinerary-plans').byClass('itinerary-new')[0].byClass('search')[0].value = 'Next run';
            __dom.node('#itinerary-main').byClass('itinerary-place-input')[0].value = 'Lorville';
            __dom.node('#itinerary-main').byClass('itinerary-note-input')[0].value = 'Pick up cargo';
            await loadTrips();
            """);

        Assert.Equal("Next run", page.Text("__dom.node('#itinerary-plans').byClass('itinerary-new')[0].byClass('search')[0].value"));
        Assert.Equal("Lorville", page.Text("__dom.node('#itinerary-main').byClass('itinerary-place-input')[0].value"));
        Assert.Equal("Pick up cargo", page.Text("__dom.node('#itinerary-main').byClass('itinerary-note-input')[0].value"));
    }

    [Fact]
    public void A_stop_draft_does_not_move_to_another_plan()
    {
        var page = Itinerary();
        page.Do("""
            __dom.node('#itinerary-main').byClass('itinerary-place-input')[0].value = 'Lorville';
            __dom.node('#itinerary-main').byClass('itinerary-note-input')[0].value = 'Pick up cargo';
            __dom.node('#itinerary-plans').byClass('itinerary-plan')[1].click();
            """);

        Assert.Equal("", page.Text("__dom.node('#itinerary-main').byClass('itinerary-place-input')[0].value"));
        Assert.Equal("", page.Text("__dom.node('#itinerary-main').byClass('itinerary-note-input')[0].value"));
    }

    /// <summary>Picks a stop up by its grip, the only way a card starts to drag.</summary>
    private static void LiftStop(Page page, int index) => page.Do($$"""
        {
          const __card = {{Stops}}[{{index}}];
          __card.byClass('itinerary-grip')[0].fire('mousedown');
          __card.fire('dragstart', { dataTransfer: null });
        }
        """);

    private static void LiftLine(Page page, int stop, int line) => page.Do($$"""
        {
          const __line = {{Stops}}[{{stop}}].byClass('itinerary-line')[{{line}}];
          __line.byClass('itinerary-line-grip')[0].fire('mousedown');
          __line.fire('dragstart', { dataTransfer: null, stopPropagation() {} });
        }
        """);

    /// <summary>Hovers and lets go: the stub's boxes are 700 tall, so 600 is the lower half.</summary>
    private static void DropOn(Page page, string target, int clientY) => page.Do($$"""
        const __target = {{target}};
        const __event = { clientY: {{clientY}}, dataTransfer: null, preventDefault() {}, stopPropagation() {} };
        __target.fire('dragover', __event);
        await __target.fire('drop', __event);
        """);

    [Fact]
    public void The_page_opens_on_the_tracked_plan_and_lists_the_others()
    {
        var page = Itinerary();

        Assert.Contains("Ore run", page.NodeText("#itinerary-main"));
        Assert.Equal(3, page.Count($"{Stops}.length"));

        var plans = page.NodeText("#itinerary-plans");
        Assert.Contains("Ore run", plans);
        Assert.Contains("tracked", plans);
        Assert.Contains("Shopping", plans);
        Assert.Equal("2 plans", page.NodeText("#itinerary-count"));
    }

    /// <summary>
    /// The card says where a stop is when the atlas knows, and says that it
    /// does not when the stop is only a name - a blank there would read like
    /// a place with nothing known about it.
    /// </summary>
    [Fact]
    public void Each_stop_says_where_it_is_or_that_the_map_does_not_know()
    {
        var page = Itinerary();

        Assert.Contains("Hurston · Stanton", page.Text($"{Stops}[0].textContent"));
        Assert.Contains("a name only, not placed on the map", page.Text($"{Stops}[1].textContent"));
        Assert.Contains("Stanton", page.Text($"{Stops}[2].textContent"));
    }

    /// <summary>Looking at another plan is not following it.</summary>
    [Fact]
    public void Opening_another_plan_shows_it_without_tracking_it()
    {
        var page = Itinerary();
        page.Do("__dom.node('#itinerary-plans').byClass('itinerary-plan')[1].click();");

        Assert.Contains("GrimHEX", page.NodeText("#itinerary-main"));
        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/track"));
    }

    /// <summary>
    /// The server counts the position after the stop is lifted out, so the
    /// first stop let go below the third is position 2, not 3.
    /// </summary>
    [Fact]
    public void A_stop_dragged_below_another_lands_after_it()
    {
        var page = Itinerary();
        LiftStop(page, 0);
        DropOn(page, $"{Stops}[2]", clientY: 600);

        Assert.Contains("POST /api/trips/t1/stops/s1/place?to=2", page.Fetched());
    }

    [Fact]
    public void A_stop_dragged_above_another_lands_before_it()
    {
        var page = Itinerary();
        LiftStop(page, 2);
        DropOn(page, $"{Stops}[0]", clientY: 100);

        Assert.Contains("POST /api/trips/t1/stops/s3/place?to=0", page.Fetched());
    }

    /// <summary>Let go where it started, a stop has not moved, and nothing is sent.</summary>
    [Fact]
    public void A_stop_let_go_where_it_was_sends_nothing()
    {
        var page = Itinerary();
        LiftStop(page, 0);
        DropOn(page, $"{Stops}[1]", clientY: 100);

        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/place"));
    }

    /// <summary>
    /// A card is draggable only while its grip is held, or no text on it
    /// could be selected; a drag that starts anywhere else carries nothing.
    /// </summary>
    [Fact]
    public void A_card_does_not_drag_unless_its_grip_is_held()
    {
        var page = Itinerary();
        page.Do($$"""{{Stops}}[0].fire('dragstart', { dataTransfer: null });""");

        Assert.True(page.Truth("itineraryDrag === null"));
    }

    [Fact]
    public void A_line_dropped_on_another_stop_joins_the_end_of_its_run_sheet()
    {
        var page = Itinerary();
        LiftLine(page, stop: 0, line: 1);
        DropOn(page, $"{Stops}[2]", clientY: 300);

        Assert.Contains("POST /api/trips/t1/stops/s1/actions/a2/move?toStop=s3&to=0", page.Fetched());
    }

    [Fact]
    public void A_line_dragged_below_its_neighbour_on_the_same_stop_swaps_with_it()
    {
        var page = Itinerary();
        LiftLine(page, stop: 0, line: 0);
        DropOn(page, $"{Stops}[0].byClass('itinerary-line')[1]", clientY: 600);

        Assert.Contains("POST /api/trips/t1/stops/s1/actions/a1/move?toStop=s1&to=1", page.Fetched());
    }

    [Fact]
    public void A_line_let_go_on_its_own_stop_where_it_already_is_sends_nothing()
    {
        var page = Itinerary();
        LiftLine(page, stop: 0, line: 1);
        DropOn(page, $"{Stops}[0]", clientY: 300);

        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/move"));
    }

    /// <summary>
    /// The live stream re-reads the plans while a card is being carried. A
    /// redraw then would replace the card, the browser would never send its
    /// dragend, and the drag would be stranded - so the redraw waits.
    /// </summary>
    [Fact]
    public void A_redraw_during_a_drag_waits_until_the_drag_ends()
    {
        var page = Itinerary();
        LiftStop(page, 0);
        page.Do($"globalThis.__itineraryHeld = {Stops}[0]; renderItinerary();");

        Assert.True(page.Truth($"{Stops}[0] === __itineraryHeld"));

        page.Do("__itineraryHeld.fire('dragend');");
        Assert.False(page.Truth($"{Stops}[0] === __itineraryHeld"));
        Assert.True(page.Truth("itineraryDrag === null"));
    }

    /// <summary>
    /// A stop typed on the page goes to the plan on the page, which need not be
    /// the tracked one; a name the atlas knows carries its place id.
    /// </summary>
    [Fact]
    public void A_stop_added_here_goes_to_the_plan_on_screen_with_the_atlas_place()
    {
        var page = Itinerary();
        page.Do("__dom.node('#itinerary-plans').byClass('itinerary-plan')[1].click();");
        page.Do("""
            const __add = __dom.node('#itinerary-main').byClass('itinerary-add')[0];
            __add.byClass('search')[0].value = 'lorville';
            __add.byClass('search')[1].value = 'Drop the medpens';
            await __add.byClass('ghost')[0].click();
            """);

        var sent = page.BodyOf("/api/trips/t2/stops");
        Assert.Contains("\"placeId\":\"HUR_LOR\"", sent);
        Assert.Contains("\"place\":\"Lorville\"", sent);
        Assert.Contains("Drop the medpens", sent);
    }

    [Fact]
    public void A_place_the_map_does_not_know_is_kept_as_typed()
    {
        var page = Itinerary();
        page.Do("""
            const __add = __dom.node('#itinerary-main').byClass('itinerary-add')[0];
            __add.byClass('search')[0].value = 'Derelict Reclaimer';
            await __add.byClass('ghost')[0].click();
            """);

        var sent = page.BodyOf("/api/trips/t1/stops");
        Assert.Contains("\"placeId\":\"\"", sent);
        Assert.Contains("\"place\":\"Derelict Reclaimer\"", sent);
    }

    /// <summary>
    /// The map's panel only shows the tracked plan, so sending another plan
    /// there tracks it first rather than open the map on something else.
    /// </summary>
    [Fact]
    public void Showing_an_untracked_plan_on_the_map_tracks_it_first()
    {
        var page = Itinerary();
        page.Do("__dom.node('#itinerary-plans').byClass('itinerary-plan')[1].click();");
        page.Do("""
            const __map = __dom.node('#itinerary-main').byClass('ghost').find((b) => b.textContent === 'Show on the map');
            await __map.click();
            """);

        Assert.Contains("POST /api/trips/t2/track", page.Fetched());
    }

    [Fact]
    public void The_map_panel_opens_its_plan_on_the_itinerary_page()
    {
        var page = Itinerary();
        page.Do("showTripPanel();");
        page.Do("__dom.node('#cargo-body').byClass('ghost').find((b) => b.textContent === 'Itinerary').click();");

        Assert.Equal("t1", page.Text("itineraryTripId"));
        Assert.True(page.Truth("shownView === 'itinerary'"));
    }

    private const string StalePlan = """
        [{"id":"t1","title":"Hauling run · 6 contracts","tracked":true,"done":false,"archived":"No","flying":false,
          "endedMissions":["old"],
          "stops":[
            {"id":"s1","placeId":"RR_P5_L2","place":"Gaslight","done":false,
             "actions":[{"id":"a1","kind":"load","text":"Carbon","done":false,"link":{"missionId":"old","legIds":["leg-1"]}},
                        {"id":"a2","kind":"load","text":"Potassium","done":false,"link":{"missionId":"new","legIds":["leg-1"]}}]}]}]
        """;

    /// <summary>
    /// A plan whose contracts the game has dropped says so, marks the lines,
    /// and offers to take them off rather than leaving the pilot to find them.
    /// </summary>
    [Fact]
    public void Lines_for_ended_contracts_are_marked_and_can_be_cleared()
    {
        var page = Itinerary(StalePlan);
        page.Serve("/api/trips/t1/ended/remove", """{"id":"t1","removed":1}""");

        Assert.Contains("1 contract on this plan is no longer open", page.NodeText("#itinerary-main"));
        Assert.Equal(1, page.Count("__dom.node('#itinerary-main').byClass('itinerary-ended').length"));

        page.Do("await __dom.node('#itinerary-main').byClass('trip-ended')[0].byClass('ghost')[0].fire('click');");
        Assert.Contains("POST /api/trips/t1/ended/remove", page.Fetched());
    }

    private const string RemovedPlan = """
        [{"id":"t1","title":"Hauling run","tracked":true,"done":false,"archived":"No","flying":false,
          "stops":[{"id":"s2","placeId":"RuinStation","place":"Ruin Station","done":false,"actions":[]}],
          "removed":[
            {"stopId":"s1","placeId":"RR_P5_L2","place":"Gaslight","stopAt":0,"lineAt":0,"removedAt":"2026-10-26T10:00:00Z",
             "action":{"id":"a1","kind":"load","text":"Carbon — Red Wind haul","done":false,"link":{"missionId":"old 1","legIds":["leg-1"]}}},
            {"stopId":"s2","placeId":"RuinStation","place":"Ruin Station","stopAt":1,"lineAt":0,"removedAt":"2026-10-26T10:00:00Z",
             "action":{"id":"a2","kind":"unload","text":"Carbon — Red Wind haul","done":false,"link":{"missionId":"old 1","legIds":["leg-1"]}}}]}]
        """;

    /// <summary>
    /// What "Remove ended lines" took off stays on the plan, one contract to a
    /// row, and Put back asks for that contract by its mission id.
    /// </summary>
    [Fact]
    public void Removed_contracts_are_listed_and_can_be_put_back()
    {
        var page = Itinerary(RemovedPlan);
        page.Serve("/api/trips/t1/ended/restore?mission=old%201", """{"id":"t1","restored":2}""");

        var text = page.NodeText("#itinerary-main");
        Assert.Contains("Removed with ended contracts · 1 contract", text);
        Assert.Contains("Red Wind haul", text);
        Assert.Contains("2 lines", text);
        Assert.Contains("Load · Carbon — Red Wind haul · Gaslight", text);

        page.Do("await __dom.node('#itinerary-main').byClass('trip-removed')[0].byClass('ghost')[0].fire('click');");
        Assert.Contains("POST /api/trips/t1/ended/restore?mission=old%201", page.Fetched());
    }

    private const string HaulPlan = """
        [{"id":"t1","title":"Carbon run","tracked":true,"done":false,"archived":"No","flying":false,
          "stops":[
            {"id":"s1","placeId":"RR_P5_L2","place":"Gaslight","done":false,
             "actions":[{"id":"a1","kind":"load","text":"Carbon","done":false,"link":{"missionId":"m1","legIds":["leg-1"]}}]}]}]
        """;

    private const string Run = """
        {"inGame":true,"planId":"run1","contracts":[{"title":"Haul A"},{"title":"Haul B"}],
         "stops":[
           {"place":"Gaslight","placeId":"RR_P5_L2","body":"Pyro V","actions":[
             {"kind":"load","commodity":"Carbon","contract":0,"contractTitle":"Haul A","missionId":"m1","legIds":["leg-1"]}]},
           {"place":"Ruin Station","placeId":"","actions":[
             {"kind":"unload","commodity":"Carbon","scu":158,"contract":0,"contractTitle":"Haul A","missionId":"m1","legIds":["leg-1"]},
             {"kind":"unload","commodity":"Waste","contract":1,"contractTitle":"Haul B","missionId":"m2","legIds":["title-route"]}]}]}
        """;

    private const string HaulLines = "__dom.node('#itinerary-main').byClass('itinerary-haul-line')";

    /// <summary>
    /// The live run sits beside the plan with a tick per load and drop. A line
    /// the plan already carries is marked and cannot be added again - the two
    /// sets of the same contracts that started this - and a drop left unticked
    /// stays out.
    /// </summary>
    [Fact]
    public void Ticked_run_lines_are_merged_into_the_open_plan_and_put_in_order()
    {
        var page = Itinerary(HaulPlan, Run);
        page.Serve("/api/trips/t1/haul", """{"id":"t1","added":1,"alreadyThere":0,"newStops":1}""");
        page.Serve("/api/trips/t1/optimise", """{"id":"t1","moved":0}""");

        Assert.Equal(3, page.Count($"{HaulLines}.length"));
        Assert.True(page.Truth($"{HaulLines}[0].classList.contains('there')"));
        Assert.True(page.Truth($"{HaulLines}[0].querySelectorAll('input')[0].disabled"));
        Assert.Contains("Add 2 lines to this plan", page.NodeText("#itinerary-main"));

        // Only the Carbon drop: the Waste haul is for another day.
        page.Do($$"""
            const tick = {{HaulLines}}[2].querySelectorAll('input')[0];
            tick.checked = false;
            tick.fire('change');
            """);
        Assert.Contains("Add 1 line to this plan", page.NodeText("#itinerary-main"));

        page.Do("await __dom.node('#itinerary-main').byClass('itinerary-haul-tools')[0].byClass('primary')[0].fire('click');");

        var sent = page.BodyOf("/api/trips/t1/haul");
        Assert.Contains("\"planId\":\"run1\"", sent);
        Assert.Contains("\"picks\":[{\"stop\":1,\"action\":0}]", sent);
        Assert.Contains("POST /api/trips/t1/optimise", page.Fetched());
    }

    /// <summary>With no plan open, the same ticks make a new one.</summary>
    [Fact]
    public void With_no_plan_the_run_becomes_a_new_one()
    {
        var page = Itinerary("[]", Run);
        page.Serve("/api/trips/new/haul", """{"id":"t9","added":3,"alreadyThere":0,"newStops":2}""");

        Assert.Contains("Make a plan from 3 lines", page.NodeText("#itinerary-main"));
        page.Do("await __dom.node('#itinerary-main').byClass('itinerary-haul-tools')[0].byClass('primary')[0].fire('click');");
        Assert.Contains("POST /api/trips/new/haul", page.Fetched());
        Assert.Equal("t9", page.Text("itineraryTripId"));
    }

    [Fact]
    public void Put_in_order_asks_the_server_to_order_the_plan()
    {
        var page = Itinerary();
        page.Serve("/api/trips/t1/optimise", """{"id":"t1","moved":2}""");

        page.Do("await __dom.node('#itinerary-main').byClass('itinerary-tools')[0].byClass('ghost').find(b => b.textContent === 'Put in order').fire('click');");
        Assert.Contains("POST /api/trips/t1/optimise", page.Fetched());
    }

    [Fact]
    public void Out_of_the_game_there_is_no_run_to_merge()
    {
        var page = Itinerary(Plans, """{"inGame":false,"contracts":[],"stops":[]}""");
        Assert.Equal(0, page.Count("__dom.node('#itinerary-main').byClass('itinerary-haul').length"));
    }

    [Fact]
    public void With_no_plans_the_page_says_how_to_start_one()
    {
        var page = Itinerary("[]");

        Assert.Contains("No plan yet", page.NodeText("#itinerary-main"));
        Assert.Equal("0 plans", page.NodeText("#itinerary-count"));
    }
}
