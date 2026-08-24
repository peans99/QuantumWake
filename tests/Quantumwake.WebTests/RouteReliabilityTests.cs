namespace Quantumwake.WebTests;

/// <summary>The route page makes a report's limits visible before its profit.</summary>
public class RouteReliabilityTests
{
    [Fact]
    public void A_route_names_price_age_demand_and_a_fallback_buyer()
    {
        var page = new Page();
        page.Serve("/api/routes?scu=64&capital=10000&from=&ranking=reliable&freshOnly=false", """
            [{"commodity":"Beryl","buyAt":"Shubin","buyPrice":100,"sellAt":"TDD","sellPrice":150,
              "marginPerScu":50,"units":18,"profit":900,"outlay":1800,"limitedBy":"demand",
              "buyStockScu":80,"sellDemandScu":18,"buySeenAt":"2026-08-23T10:00:00Z",
              "sellSeenAt":"2026-08-23T11:00:00Z","freshness":"fresh",
              "fallbackSells":[{"terminal":"Area18 TDD","sellPrice":142,"demandScu":64,"seenAt":"2026-08-23T10:00:00Z","freshness":"fresh"}]}]
            """);

        page.Do("__dom.node('#routes-ship').value = '64'; __dom.node('#routes-capital').value = '10000'; await loadRoutes();");

        var text = page.NodeText("#routes-table tbody");
        Assert.Contains("Fresh reports", text);
        Assert.Contains("stock 80 SCU", text);
        Assert.Contains("demand 18 SCU", text);
        Assert.Contains("Fallback: Area18 TDD", text);
        Assert.Contains("demand", text);
    }

    [Fact]
    public void Fresh_only_is_part_of_the_route_query()
    {
        var page = new Page();
        page.Do("__dom.node('#routes-fresh-only').checked = true; await loadRoutes();");

        Assert.Contains(page.Fetched(), url => url.Contains("freshOnly=true"));
        Assert.Contains(page.Fetched(), url => url.Contains("ranking=reliable"));
    }
}
