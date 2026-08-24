namespace Quantumwake.WebTests;

/// <summary>A history row opens the evidence the session already carries.</summary>
public class SessionDebriefTests
{
    private const string Detail = """
        {
          "id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z","gameVersion":"4.9",
          "ships":[{"displayName":"RSI Zeus","sorties":2}],
          "locations":[
            {"at":"2026-08-20T20:05:00Z","rawId":"RR_MIC_LEO","displayName":"Port Tressler","system":"Stanton","body":"microTech"},
            {"at":"2026-08-20T20:45:00Z","rawId":"RR_HUR_L1","displayName":"HUR-L1","system":"Stanton"}
          ],
          "jumps":[
            {"at":"2026-08-20T20:20:00Z","toId":"party","toName":"PartyMemberMarker_12345"},
            {"at":"2026-08-20T20:30:00Z","toId":"RR_HUR_L1","toName":"HUR-L1"}
          ],
          "contracts":[{"displayName":"Supply run","outcome":"Completed","steps":2,"stepsDone":2}],
          "timeline":[{"at":"2026-08-20T21:10:00Z","kind":"ship","text":"Left RSI Zeus","detail":"~30 min"}],
          "purchases":[{"at":"2026-08-20T20:10:00Z","item":"MedPen","total":500,"quantity":2,"confirmed":true}],
          "trades":[{"at":"2026-08-20T21:00:00Z","amount":4000,"quantity":8,"isSell":true}],
          "partyNotes":[{"at":"2026-08-20T20:20:00Z","handle":"D-Rud","moment":"Connected"}],
          "spend":500,"income":4000,"commoditySpend":0,"deaths":0
        }
        """;

    [Fact]
    public void A_session_row_expands_into_route_economy_contract_and_ship_evidence()
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", Detail);

        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300,
              primaryShip:'RSI Zeus', lastLocation:'HUR-L1', jumps:1, contracts:1, deaths:0, incapacitations:0 }];
            await toggleSessionDebrief('s1');
            """);

        var text = page.NodeText("#sessions-table tbody");
        Assert.Contains("RSI Zeus · 2 sorties", text);
        Assert.Contains("Port Tressler", text);
        Assert.Contains("HUR-L1", text);
        Assert.Contains("Cargo sold · 8 SCU", text);
        Assert.Contains("Supply run", text);
        Assert.Contains("2 / 2 steps", text);
        Assert.Contains("1 named", text);
        Assert.Contains("Crew observed*", text);
        Assert.Contains("Cargo amounts are kiosk requests", text);
        Assert.DoesNotContain("PartyMemberMarker", text);
    }

    [Fact]
    public void Repeating_a_session_creates_an_ordered_flight_plan()
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", Detail);
        page.Serve("/api/trips", "[]");

        page.Do("await toggleSessionDebrief('s1'); await repeatSessionRoute(sessionDetails.get('s1'));");

        var body = page.BodyOf("/api/trips");
        Assert.Contains("Repeat", body);
        Assert.True(body.IndexOf("Port Tressler", StringComparison.Ordinal)
                    < body.IndexOf("HUR-L1", StringComparison.Ordinal));
        Assert.Contains("RR_MIC_LEO", body);
        Assert.Contains("RR_HUR_L1", body);
        Assert.DoesNotContain("PartyMemberMarker", body);
    }
}
