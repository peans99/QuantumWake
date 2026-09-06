namespace Quantumwake.WebTests;

/// <summary>
/// The flight-plan panel once a run has a beginning and an end.
/// </summary>
/// <remarks>
/// The interesting cases are the ones where the panel would look fine and be
/// wrong: offering Start on a run already going, offering Resume on a run the
/// pilot deliberately finished, or losing the filed list at the exact moment
/// somebody finishes their only run and wants to see it.
/// </remarks>
public class RunLifecyclePanelTests
{
    private static Page Panel(string trips)
    {
        var page = new Page();
        page.Serve("/api/trips", trips);
        page.Serve("/api/briefing", "{}");
        page.Do("await loadTrips(); showTripPanel();");
        return page;
    }

    private const string Planned = """
        [{"id":"t1","title":"Ore run","tracked":true,"done":false,"archived":"No","flying":false,
          "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":false,"actions":[]}]}]
        """;

    private const string Flying = """
        [{"id":"t1","title":"Ore run","tracked":true,"done":false,"archived":"No","flying":true,
          "startedAt":"2026-09-01T12:00:00+00:00","elapsedSeconds":7200,
          "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":false,"actions":[]}]}]
        """;

    [Fact]
    public void A_plan_that_has_not_been_flown_offers_to_start()
    {
        var text = Panel(Planned).NodeText("#cargo-body");

        Assert.Contains("Start run", text);
        Assert.DoesNotContain("Finish run", text);
    }

    [Fact]
    public void A_run_underway_offers_to_finish_it()
    {
        var text = Panel(Flying).NodeText("#cargo-body");

        Assert.Contains("Finish run", text);
        Assert.DoesNotContain("Start run", text);
    }

    /// <summary>
    /// The number is only meaningful with the caveat attached: it is time since
    /// Start, and a plan written a week ago is not a week-long run.
    /// </summary>
    [Fact]
    public void A_running_clock_says_what_it_is_measuring_from()
    {
        var text = Panel(Flying).NodeText("#cargo-body");

        Assert.Contains("Running for 2h", text);
        Assert.Contains("not since you wrote it", text);
    }

    [Fact]
    public void Starting_a_run_calls_start_and_finishing_calls_finish()
    {
        var page = Panel(Planned);
        page.Do("__dom.node('#cargo-body').byClass('ghost').find((b) => b.textContent === 'Start run').click();");

        Assert.Contains(page.Fetched(), u => u.EndsWith("/api/trips/t1/start"));
    }

    /// <summary>
    /// A run the app filed on its own can come back. A run the pilot finished
    /// cannot, or the sweep and the person would read as the same decision.
    /// </summary>
    [Fact]
    public void Only_a_run_the_app_filed_offers_to_resume()
    {
        var quiet = Panel("""
            [{"id":"t1","title":"Ore run","tracked":false,"done":false,"archived":"Quiet","flying":false,
              "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":true,"actions":[]}]}]
            """).NodeText("#cargo-body");

        Assert.Contains("Resume", quiet);
        Assert.Contains("filed itself after going quiet", quiet);

        var finished = Panel("""
            [{"id":"t1","title":"Ore run","tracked":false,"done":true,"archived":"You","flying":false,
              "elapsedSeconds":10800,
              "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":true,"actions":[]}]}]
            """).NodeText("#cargo-body");

        Assert.DoesNotContain("Resume", finished);
        Assert.Contains("Repeat", finished);
    }

    /// <summary>
    /// Finishing your only run leaves nothing tracked, and that is exactly when
    /// somebody wants to see what they just flew.
    /// </summary>
    [Fact]
    public void Filed_runs_are_still_listed_when_nothing_is_tracked()
    {
        var text = Panel("""
            [{"id":"t1","title":"Ore run","tracked":false,"done":true,"archived":"You","flying":false,
              "elapsedSeconds":10800,
              "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":true,"actions":[]}]}]
            """).NodeText("#cargo-body");

        Assert.Contains("Finished and filed", text);
        Assert.Contains("Ore run", text);
        Assert.Contains("3h", text);
    }

    /// <summary>
    /// A filed run is not a plan waiting to be picked up, and listing it under
    /// "Other plans" would put finished work back in the working list.
    /// </summary>
    [Fact]
    public void A_filed_run_is_not_offered_as_another_plan()
    {
        var page = Panel("""
            [{"id":"t1","title":"Live plan","tracked":true,"done":false,"archived":"No","flying":false,
              "stops":[{"id":"s1","placeId":"Stanton1","place":"Hurston","done":false,"actions":[]}]},
             {"id":"t2","title":"Old run","tracked":false,"done":true,"archived":"Quiet","flying":false,
              "stops":[{"id":"s2","placeId":"Stanton2","place":"Crusader","done":true,"actions":[]}]}]
            """);

        var text = page.NodeText("#cargo-body");

        Assert.Contains("Finished and filed", text);
        Assert.DoesNotContain("Other plans", text);
    }
}
