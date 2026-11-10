namespace Quantumwake.WebTests;

/// <summary>
/// The debrief's Fights section (1.2.21): what the screen reader kept of each
/// fight, beside what the log wrote in the same minutes.
/// </summary>
public class SessionFightsTests
{
    private const string Detail = """
        {
          "id":"s1","startedAt":"2026-08-20T20:00:00Z","endedAt":"2026-08-20T21:30:00Z","gameVersion":"4.9",
          "ships":[{"displayName":"Anvil F7C-M Super Hornet Mk II","sorties":1}],
          "locations":[],"jumps":[],"contracts":[],"purchases":[],"trades":[],"partyNotes":[],
          "timeline":[
            {"at":"2026-08-20T20:31:00Z","kind":"death","text":"Killed by Vanduul Blade","detail":"Ship destruction"},
            {"at":"2026-08-20T20:34:00Z","kind":"contract-done","text":"Contract completed","detail":"Defend Tranquility"},
            {"at":"2026-08-20T21:20:00Z","kind":"kill","text":"Killed a thief","detail":"later, not in the fight"}
          ],
          "spend":0,"income":0,"commoditySpend":0,"deaths":1
        }
        """;

    private const string CombatUrl = "/api/combat?from=2026-08-20T20%3A00%3A00Z&to=2026-08-20T21%3A30%3A00Z";

    private const string Fights = """
        {"keeping":true,"any":true,"fights":[{
          "start":"2026-08-20T20:29:40Z","end":"2026-08-20T20:33:00Z",
          "locks":[
            {"since":"2026-08-20T20:30:00Z","until":"2026-08-20T20:30:40Z","name":"Vanduul Blade","class":"VNCL_Blade",
             "faction":"Vanduul","stance":"hostile","closestKm":0.62,"topSpeedMs":261,"hull":"critical","stopped":true,"reads":30},
            {"since":"2026-08-20T20:31:30Z","until":"2026-08-20T20:33:00Z","likely":"Vanduul Glaive","chance":0.83,
             "class":"VNCL_Glaive","stance":"unread","reads":12}
          ],
          "said":[{"at":"2026-08-20T20:32:00Z","kind":"missile","text":"Missile inbound"}],
          "named":1,"likely":1,"hostile":1,"stopped":1,"missiles":1,"lowestObjective":25
        }]}
        """;

    private static Page Opened(string? combat)
    {
        var page = new Page();
        page.Serve("/api/sessions/s1", Detail);
        if (combat is not null) page.Serve(CombatUrl, combat);
        page.Do("""
            allSessions = [{ id:'s1', startedAt:'2026-08-20T20:00:00Z', inGame:5100, menu:300,
              primaryShip:'Anvil F7C-M Super Hornet Mk II', lastLocation:'HUR-L1', jumps:0, contracts:1, deaths:1, incapacitations:0 }];
            await toggleSessionDebrief('s1');
            """);
        return page;
    }

    [Fact]
    public void A_fight_lists_its_locks_its_counts_and_what_the_log_wrote_meanwhile()
    {
        // The section alone: the debrief's highlights list the whole timeline too.
        var text = Opened(Fights).Text("__dom.node('#sessions-table tbody').querySelector('.session-fights').textContent");

        Assert.Contains("Fights read off the screen", text);
        Assert.Contains("2 locks · 1 named · 1 likely · 1 hostile · 1 stopped · 1 missile warning · objective below 25%", text);
        Assert.Contains("Vanduul Blade", text);
        Assert.Contains("hostile · Vanduul · closest 0.62 km · up to 261 m/s · hologram critical · stopped after moving", text);
        Assert.Contains("likely Vanduul Glaive 83%", text);
        Assert.Contains("Killed by Vanduul Blade", text);
        Assert.Contains("Contract completed", text);
        Assert.DoesNotContain("Killed a thief", text);   // fifty minutes after, not this fight
        Assert.Contains("A likely hull is the signature’s guess", text);
    }

    [Fact]
    public void A_record_never_turned_on_adds_nothing_to_the_debrief()
    {
        var text = Opened("""{"keeping":false,"any":false,"fights":[]}""").NodeText("#sessions-table tbody");
        Assert.DoesNotContain("Fights read off the screen", text);
    }

    [Fact]
    public void A_record_on_with_nothing_locked_says_so()
    {
        var text = Opened("""{"keeping":true,"any":true,"fights":[]}""").NodeText("#sessions-table tbody");
        Assert.Contains("No ship was locked in this session while the screen was being read.", text);
    }

    [Fact]
    public void A_server_without_the_record_still_opens_the_debrief()
    {
        var text = Opened(null).NodeText("#sessions-table tbody");
        Assert.Contains("debrief", text);
        Assert.DoesNotContain("Fights read off the screen", text);
    }

    [Fact]
    public void The_switch_posts_on_its_own_and_shows_only_with_reading_on()
    {
        var page = new Page();
        page.Serve("/api/live/fights", """{"available":true,"enabled":true,"keepFights":true,"status":{"state":"reading"}}""");
        page.Do("""
            liveState = { available:true, enabled:true, keepFights:false, status:{ state:'reading' } };
            renderLiveScreen();
            """);
        Assert.Equal(0, page.Count("__dom.node('#live-fights-row').hidden ? 1 : 0"));

        page.Do("""
            const box = __dom.node('#live-fights');
            box.checked = true;
            await box.fire('change');
            """);
        Assert.Contains("POST /api/live/fights", page.Fetched());
        Assert.Equal(1, page.Count("liveState.keepFights === true ? 1 : 0"));

        page.Do("""
            liveState = { available:true, enabled:false, keepFights:true, status:{ state:'off' } };
            renderLiveScreen();
            """);
        Assert.Equal(1, page.Count("__dom.node('#live-fights-row').hidden ? 1 : 0"));
    }
}
