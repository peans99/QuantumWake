namespace Quantumwake.WebTests;

/// <summary>The route page makes a report's limits visible before its profit.</summary>
public class RouteReliabilityTests
{
    [Fact]
    public void A_route_names_price_age_demand_and_a_fallback_buyer()
    {
        var page = new Page();
        page.Serve("/api/routes?scu=64&capital=10000&from=&ranking=reliable&freshOnly=false&evidence=reported", """
            [{"commodity":"Beryl","buyAt":"Shubin","buyPrice":100,"sellAt":"TDD","sellPrice":150,
              "marginPerScu":50,"units":18,"profit":900,"outlay":1800,"limitedBy":"demand",
              "desiredUnits":64,"buyStockScu":80,"sellDemandScu":18,"buyAvailability":"enough","sellAvailability":"limited","availability":"reported-partial","mapReady":false,"buySeenAt":"2026-08-23T10:00:00Z",
              "sellSeenAt":"2026-08-23T11:00:00Z","freshness":"fresh",
              "fallbackSells":[{"terminal":"Area18 TDD","sellPrice":142,"demandScu":64,"seenAt":"2026-08-23T10:00:00Z","freshness":"fresh"}]}]
            """);

        page.Do("__dom.node('#routes-ship').value = '64'; __dom.node('#routes-capital').value = '10000'; await loadRoutes();");

        var text = page.NodeText("#routes-table tbody");
        Assert.Contains("Fresh reports", text);
        Assert.Contains("Reported partial · 18 / 64 SCU", text);
        Assert.Contains("buy stock 80 SCU (enough)", text);
        Assert.Contains("sell demand 18 SCU (limited)", text);
        Assert.Contains("Fallback: Area18 TDD", text);
        Assert.Contains("Text plan", text);
        Assert.Contains("demand", text);
    }

    [Fact]
    public void Fresh_only_is_part_of_the_route_query()
    {
        var page = new Page();
        page.Do("__dom.node('#routes-fresh-only').checked = true; await loadRoutes();");

        Assert.Contains(page.Fetched(), url => url.Contains("freshOnly=true"));
        Assert.Contains(page.Fetched(), url => url.Contains("ranking=reliable"));
        Assert.Contains(page.Fetched(), url => url.Contains("evidence=reported"));
    }

    [Fact]
    public void Unknown_capacity_is_named_an_estimate_and_can_be_included()
    {
        var page = new Page();
        page.Serve("/api/routes?scu=64&capital=0&from=&ranking=reliable&freshOnly=false&evidence=any", """
            [{"commodity":"Beryl","buyAt":"Levski","buyPrice":100,"sellAt":"HUR-L1","sellPrice":150,
              "marginPerScu":50,"units":64,"profit":3200,"outlay":6400,"limitedBy":"hold","desiredUnits":64,
              "buyAvailability":"unknown","sellAvailability":"enough","availability":"capacity-unknown","sellDemandScu":85,
              "freshness":"fresh","mapReady":true,"fallbackSells":[]}]
            """);

        page.Do("__dom.node('#routes-evidence').value = 'any'; __dom.node('#routes-ship').value = '64'; await loadRoutes();");

        var text = page.NodeText("#routes-table tbody");
        Assert.Contains("Capacity unknown · projected 64 SCU", text);
        Assert.Contains("buy stock unknown", text);
        Assert.Contains("~3,200", text);
        Assert.Contains("Plan", text);
    }
}
