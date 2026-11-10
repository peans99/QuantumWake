namespace Quantumwake.WebTests;

/// <summary>The debrief's Rocks worked section (1.2.23).</summary>
public class SessionRocksTests
{
    private const string Detail = """
        {
          "id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z","gameVersion":"4.9",
          "ships":[{"displayName":"ARGO MOLE","sorties":1}],
          "locations":[],"jumps":[],"contracts":[],"purchases":[],"trades":[],"partyNotes":[],"timeline":[],
          "spend":0,"income":0,"commoditySpend":0,"deaths":0
        }
        """;

    private const string RocksUrl = "/api/rocks?from=2026-08-20T20%3A00%3A00Z&to=2026-08-20T21%3A30%3A00Z";

    private static string Section(string? rocks)
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", Detail);
        if (rocks is not null) page.Serve(RocksUrl, rocks);
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300,
              primaryShip:'ARGO MOLE', lastLocation:'Daymar', jumps:0, contracts:0, deaths:0, incapacitations:0 }];
            await toggleSessionDebrief('s1');
            """);
        return page.Text("(__dom.node('#sessions-table tbody').querySelector('.session-rocks') || { textContent: '' }).textContent");
    }

    [Fact]
    public void Each_window_shows_how_it_went_and_the_scan_of_its_rock()
    {
        var text = Section("""
            {"rocks":[
              {"fracture":{"since":"2026-08-20T20:30:00Z","until":"2026-08-20T20:31:20Z","cracked":true,"filled":0.95,"inWindowSeconds":34,"overcharges":1},
               "scan":{"primary":"Quantainium","massKg":8400,"resistancePercent":22,"parts":[{"read":"QUANTAINIUM","mineral":"Quantainium","percent":34}]},
               "scannedAt":"2026-08-20T20:29:40Z"},
              {"fracture":{"since":"2026-08-20T20:40:00Z","until":"2026-08-20T20:40:30Z","cracked":false,"filled":0.4,"inWindowSeconds":5,"overcharges":0}}
            ]}
            """);

        Assert.Contains("Rocks worked", text);
        Assert.Contains("2 charge windows · 1 cracked · 1 let go · 1 overcharge", text);
        Assert.Contains("1m 20s on the laser · 34s in the window · 1 overcharge · Quantainium 34%, 8,400 kg, resistance 22%", text);
        Assert.Contains("Let go", text);
        Assert.Contains("bar 40% full", text);
        Assert.Contains("a refinery order never says which rocks went into it", text);
    }

    [Fact]
    public void A_session_with_no_rocks_has_no_section() =>
        Assert.Equal("", Section("""{"rocks":[]}"""));

    [Fact]
    public void A_server_without_the_record_has_no_section() =>
        Assert.Equal("", Section(null));
}
