namespace Quantumwake.WebTests;

/// <summary>
/// The Flight page should remain a useful preparation surface when cargo
/// advice is unavailable: a shared plan is independent of a ship's hold.
/// </summary>
public class FlightPlanDeckTests
{
    [Fact]
    public void Flight_brief_explains_how_to_start_when_no_plan_is_tracked()
    {
        var page = new Page();

        page.Do("trips = []; renderRouteFlightBrief();");

        Assert.Equal("No active plan", page.NodeText("#route-active-plan-status"));
        Assert.Contains("shopping list", page.NodeText("#route-active-plan-detail"));
        Assert.Equal("Start on map", page.NodeText("#routes-open-plan"));
    }

    [Fact]
    public void Flight_brief_names_the_next_stop_and_progress_of_the_tracked_run()
    {
        var page = new Page();

        page.Do("""
            trips = [{ id: 'run-1', title: 'Orison supplies', tracked: true, flying: true,
              stops: [
                { id: 'done', place: 'Port Olisar', done: true, actions: [] },
                { id: 'next', place: 'Orison', done: false, actions: [] }
              ]
            }];
            renderRouteFlightBrief();
            """);

        Assert.Equal("Run in progress", page.NodeText("#route-active-plan-status"));
        Assert.Contains("next: Orison", page.NodeText("#route-active-plan-detail"));
        Assert.Contains("1 of 2 stops complete", page.NodeText("#route-active-plan-detail"));
        Assert.Equal("Open active plan", page.NodeText("#routes-open-plan-card"));
    }
}
