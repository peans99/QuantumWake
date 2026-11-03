namespace Quantumwake.WebTests;

/// <summary>
/// Settings → What was read: every place the app reads from, what it found
/// there, and where it is kept (0.20.36).
/// </summary>
/// <remarks>
/// The pieces lived on five pages before this. The tests pin what the one
/// page has to say: a verdict per source that does not pretend, the counts
/// behind it, the folder entry by entry, and the signals table that moved
/// here from General.
/// </remarks>
public class DataPageTests
{
    private const string Data = """
        {"takenAt":"2026-11-01T21:00:00Z",
         "logs":{"found":true,"channel":"LIVE","root":"C:\\Games\\StarCitizen\\LIVE","hasGameLog":true,
                 "files":{"onDisk":282,"current":281,"changed":1,"unread":0,"gone":3,"bytes":741000000},
                 "sessions":{"total":284,"counted":43,"first":"2026-05-01T18:00:00Z","last":"2026-11-01T03:15:50Z",
                             "builds":[{"build":"4.10.2","sessions":200},{"build":"4.9.1","sessions":84}]},
                 "lastScan":{"startedAt":"2026-11-01T20:00:00Z","finishedAt":"2026-11-01T20:00:04Z","files":282,"parsed":1,"forced":false},
                 "wipe":{"at":"2026-05-15T00:00:00Z","patch":"4.9","scope":"Everything","hidden":241}},
         "gameFiles":{"status":{"state":"ready","problem":null,"startedAt":"2026-11-01T19:59:00Z","finishedAt":"2026-11-01T19:59:31Z","seconds":31.2,"counts":{}},
                      "root":"C:\\Games\\StarCitizen\\LIVE",
                      "names":{"loaded":true,"items":26531,"vehicles":312,"places":529},
                      "catalogues":{"items":26531,"commodities":203,"vehicles":312,"places":529,"recipes":160,"deposits":1200,"paints":900,"cargoHulls":149,"crates":12,"wikeloTrades":72,"weapons":344,"armour":2469}},
         "community":{"enabled":true,"fetchedAt":"2026-11-01T20:26:13Z","dump":"4.10.2-LIVE.12881860","commodities":203,"ships":299,"items":5000,"blueprints":160},
         "uex":{"enabled":false,"prices":0,"hasCredentials":false,
                "feeds":[{"key":"rentals","title":"Rental desks","enabled":false,"fetchedAt":null}]},
         "screens":{"canRead":true,"watching":true,"readings":21,"latest":"2026-10-28T20:00:00Z",
                    "byKind":[{"kind":"Reputation","count":21,"last":"2026-10-28T20:00:00Z"}],"reputation":21,"clipboards":2,"pinned":1},
         "yours":{"lists":4,"listsOpen":2,"checklists":3,"plans":2,"plansFlying":1,"mapNotes":5,"shardNotes":0,"builds":1,"kits":0,"imports":0,"keybindingVersions":6,"itemLabels":true},
         "folder":{"path":"C:\\Users\\pilot\\AppData\\Local\\Quantumwake","relocated":false,"bytes":512000000,
                   "entries":[{"name":"ocr","kind":"folder","bytes":400000000,"modified":"2026-10-30T00:00:00Z","files":4},
                              {"name":"sessions.db","kind":"file","bytes":90000000,"modified":"2026-11-01T20:00:04Z","files":1},
                              {"name":"jobs.json","kind":"file","bytes":12000,"modified":"2026-10-31T00:00:00Z","files":1}]}}
        """;

    private static Page Loaded(string data = Data)
    {
        var page = new Page();
        page.Serve("/api/data", data);
        page.Serve("/api/signals", """[{"name":"Item bought","group":"Money","total":354,"sessions":40,"lastSeen":"2026-10-29T16:15:00Z","note":null}]""");
        page.Do("await loadDataPage();");
        return page;
    }

    [Fact]
    public void Each_source_gets_a_card_with_its_verdict_and_counts()
    {
        var page = Loaded();
        var text = page.NodeText("#data-sections");

        Assert.Equal(6, page.Count("__dom.node('#data-sections').byClass('data-card').length"));
        Assert.Contains("284 sessions read from 281 log files", text);
        Assert.Contains("1 changed since it was read", text);
        Assert.Contains("3 read, since deleted by the game", text);
        Assert.Contains("4.10.2 ×200, 4.9.1 ×84", text);
        // The harness formats dates its own way, so the date itself is not pinned.
        Assert.Contains("241 before your wipe of", text);
        Assert.Contains("are kept but not counted", text);
        Assert.Contains("Read in 31.2 s", text);
        Assert.Contains("26,531", text);
        Assert.Contains("Dump 4.10.2-LIVE.12881860", text);
        Assert.Contains("Off. Prices need UEX, in Settings.", text);
        Assert.Contains("Watching your screenshot folder", text);
        Assert.Contains("Rep app", text);
        Assert.Contains("4 · 2 open", text);
        Assert.Contains("6", text);
    }

    [Fact]
    public void The_folder_is_listed_entry_by_entry_largest_first()
    {
        var page = Loaded();

        Assert.Contains("C:\\Users\\pilot\\AppData\\Local\\Quantumwake · 488 MB", page.NodeText("#data-folder"));
        var rows = page.NodeText("#data-folder-table tbody");
        Assert.Contains("ocr", rows);
        Assert.Contains("4 files", rows);
        Assert.Contains("381 MB", rows);
        Assert.Contains("12 KB", rows);
        Assert.Equal(3, page.Count("__dom.node('#data-folder-table tbody').querySelectorAll('tr').length"));
    }

    /// <summary>The signals table lives here now; it still fills from the same answer.</summary>
    [Fact]
    public void The_signals_table_fills_on_this_page()
    {
        var page = Loaded();

        var rows = page.Text("__dom.node('#signals-table').querySelector('tbody').textContent");
        Assert.Contains("Item bought", rows);
        Assert.Contains("354", rows);
    }

    [Fact]
    public void A_missing_install_and_an_unreadable_store_are_said_not_dressed_up()
    {
        var page = Loaded(Data
            .Replace("\"found\":true", "\"found\":false")
            .Replace("\"state\":\"ready\"", "\"state\":\"noinstall\"")
            .Replace("\"problem\":null", "\"problem\":\"No Star Citizen install was found, so nothing could be read from one.\""));
        var text = page.NodeText("#data-sections");

        Assert.Contains("No Star Citizen install was found, so no log has been read.", text);
        Assert.Contains("so nothing could be read from one.", text);

        var dark = new Page();
        dark.Do("__fetch.unreachable.push('/api/data'); await loadDataPage();");
        Assert.Contains("could not be read", dark.NodeText("#data-status"));
    }
}
