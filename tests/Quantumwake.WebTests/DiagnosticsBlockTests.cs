namespace Quantumwake.WebTests;

/// <summary>
/// The Settings block that turns "it does not work for me" into something
/// answerable.
/// </summary>
/// <remarks>
/// The block asks a pilot to send a file about their own play, so it follows
/// the rule the export block follows: what would go is on screen before there
/// is anything to send. Saving builds the file locally and hands it to the
/// browser - nothing is posted anywhere, which is the whole reason the promise
/// above the button can be made at all.
/// </remarks>
public class DiagnosticsBlockTests
{
    private const string Report = """
        {"producer":{"name":"Quantum Wake","version":"0.8.30"},
         "takenAt":"2026-08-27T18:00:00Z",
         "install":{"found":true,"channel":"LIVE","hasGameLog":true,"backups":158},
         "library":{"sessions":158,"counted":50,"first":"2026-05-01T00:00:00Z",
                    "last":"2026-08-27T13:47:08Z",
                    "builds":[{"build":"12519617","sessions":2},{"build":"12344265","sessions":41}]},
         "parser":{"unread":3,"samples":false,"tags":[{"tag":"SomeTag","count":3,"sample":""}]},
         "views":{"ships":22,"places":67,"destinations":31,"contracts":9,
                  "purchases":120,"trades":39,"fleet":18,"loadout":18,"stash":7},
         "data":{"community":true,"communityDump":"4.10.0-LIVE.12519617","uex":false,"uexKeysStored":false},
         "wipe":{"at":"2026-05-15T00:00:00Z","patch":"Alpha 4.8","scope":"Everything","hidden":0}}
        """;

    private const string Clean = """
        {"producer":{"name":"Quantum Wake","version":"0.8.30"},
         "takenAt":"2026-08-27T18:00:00Z",
         "install":{"found":true,"channel":"LIVE","hasGameLog":true,"backups":158},
         "library":{"sessions":158,"counted":158,"first":null,"last":null,
                    "builds":[{"build":"12519617","sessions":2}]},
         "parser":{"unread":0,"samples":false,"tags":[]},
         "views":{"ships":22,"places":67,"destinations":31,"contracts":9,
                  "purchases":120,"trades":39,"fleet":18,"loadout":18,"stash":7},
         "data":{"community":true,"communityDump":null,"uex":false,"uexKeysStored":false},
         "wipe":{"at":"2026-05-15T00:00:00Z","patch":"Alpha 4.8","scope":"Everything","hidden":0}}
        """;

    private static Page Rendered(string report = Report)
    {
        var page = new Page();
        page.Serve("/api/diagnostics?samples=false", report);
        page.Do("await renderDiagnostics();");
        return page;
    }

    /// <summary>
    /// The size of the problem, before the file exists: how much was read and
    /// how much of it defeated the parser.
    /// </summary>
    [Fact]
    public void The_preview_says_what_the_report_would_carry()
    {
        var line = Rendered().NodeText("#diag-preview");

        Assert.Contains("158 sessions", line);
        Assert.Contains("2 game builds", line);
        Assert.Contains("3 unreadable lines", line);
        Assert.Contains("1 tag", line);
    }

    /// <summary>
    /// One of a thing is one, not one of them. A report is read by whoever is
    /// being asked for help, and "1 sessions" reads as a bug in the thing they
    /// were about to report a bug in.
    /// </summary>
    [Fact]
    public void One_session_is_not_called_sessions()
    {
        const string single = """
            {"producer":{"name":"Quantum Wake","version":"0.8.30"},
             "takenAt":"2026-08-27T18:00:00Z",
             "install":{"found":true,"channel":"LIVE","hasGameLog":true,"backups":1},
             "library":{"sessions":1,"counted":1,"first":null,"last":null,
                        "builds":[{"build":"12599999","sessions":1}]},
             "parser":{"unread":6,"samples":false,"tags":[{"tag":"A","count":6,"sample":""}]},
             "views":{"ships":0,"places":0,"destinations":0,"contracts":0,"purchases":0,
                      "trades":0,"fleet":null,"loadout":0,"stash":0},
             "data":{"community":false,"communityDump":null,"uex":false,"uexKeysStored":false},
             "wipe":{"at":"2026-05-15T00:00:00Z","patch":"Alpha 4.8","scope":"Everything","hidden":0}}
            """;

        var line = Rendered(single).NodeText("#diag-preview");

        Assert.Contains("1 session ", line);
        Assert.Contains("1 game build ", line);
        Assert.DoesNotContain("1 sessions", line);
    }

    /// <summary>
    /// A parser that read everything says so plainly. "0 unreadable lines" is a
    /// number to squint at; the sentence is not.
    /// </summary>
    [Fact]
    public void Nothing_unreadable_is_said_in_words()
    {
        var line = Rendered(Clean).NodeText("#diag-preview");

        Assert.Contains("nothing unreadable", line);
    }

    /// <summary>
    /// The file is built in the page from what the endpoint returned. Nothing is
    /// posted: the block promises the report goes nowhere until the pilot sends
    /// it, and a POST here would make that untrue.
    /// </summary>
    [Fact]
    public void Saving_writes_a_file_and_sends_nothing_anywhere()
    {
        var page = Rendered();
        page.Do("__dom.node('#diag-save').click();");

        Assert.Equal(1, page.Count("__downloads.length"));
        Assert.Contains("quantumwake-report-2026-08-27.json", page.Text("__downloads[0].name"));
        Assert.DoesNotContain(page.Fetched(), url => url.StartsWith("POST"));
    }

    /// <summary>
    /// Read it, then send it - so the status says so rather than just
    /// announcing success.
    /// </summary>
    [Fact]
    public void The_pilot_is_told_to_read_it_before_sending()
    {
        var page = Rendered();
        page.Do("__dom.node('#diag-save').click();");

        var status = page.NodeText("#diag-status");
        Assert.Contains("Read it", status);
    }

    /// <summary>
    /// A report built while a scan was still running would describe a moment
    /// other than the one being reported, so the click asks again rather than
    /// saving whatever the preview held.
    /// </summary>
    [Fact]
    public void Saving_asks_for_the_report_again_rather_than_reusing_the_preview()
    {
        var page = Rendered();
        page.Do("__dom.node('#diag-save').click();");

        Assert.Equal(2, page.Fetched().Count(url => url.StartsWith("GET /api/diagnostics")));
    }

    /// <summary>
    /// Example lines are off unless asked for, and asking is a separate act.
    /// </summary>
    /// <remarks>
    /// A sample exists only because a format changed, and a changed format can
    /// write a name in a shape the scrubber has never seen - proved by a log
    /// whose login line was reshaped, which came through still naming its pilot.
    /// Everything else in the report is safe by construction; this is the one
    /// part that is not, so it is its own yes.
    /// </remarks>
    [Fact]
    public void Example_lines_are_not_asked_for_by_default()
    {
        var page = Rendered();

        Assert.False(page.Truth("__dom.node('#diag-samples').checked === true"));
        Assert.Contains(page.Fetched(), url => url.Contains("samples=false"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("samples=true"));
    }

    /// <summary>
    /// And the page says what it cannot promise about them, in the words
    /// somebody deciding would need.
    /// </summary>
    /// <remarks>
    /// Read from the markup rather than through the stub, which seeds ids and
    /// states but carries no text. Static copy is exactly where a promise gets
    /// quietly softened, so it is worth pinning even crudely.
    /// </remarks>
    [Fact]
    public void The_page_says_what_an_example_line_cannot_promise()
    {
        var markup = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

        var block = markup[markup.IndexOf("diag-samples-copy", StringComparison.Ordinal)..];
        block = block[..block.IndexOf("</p>", StringComparison.Ordinal)];

        Assert.Contains("raw lines from your log", block);
        Assert.Contains("changed its format", block);
        Assert.Contains("Read them before you", block);
    }

    /// <summary>
    /// Ticking the box asks again with samples, so the preview describes what
    /// would actually go rather than what would have gone a moment ago.
    /// </summary>
    [Fact]
    public void Ticking_the_box_asks_for_them()
    {
        var page = Rendered();
        page.Serve("/api/diagnostics?samples=true", """
            {"producer":{"name":"Quantum Wake","version":"0.8.30"},
             "takenAt":"2026-08-27T18:00:00Z",
             "install":{"found":true,"channel":"LIVE","hasGameLog":true,"backups":158},
             "library":{"sessions":158,"counted":50,"first":null,"last":null,"builds":[]},
             "parser":{"unread":3,"samples":true,
                       "tags":[{"tag":"SomeTag","count":3,"sample":"<SomeTag> raw text"}]},
             "views":{"ships":1,"places":1,"destinations":1,"contracts":1,"purchases":1,
                      "trades":1,"fleet":1,"loadout":1,"stash":1},
             "data":{"community":false,"communityDump":null,"uex":false,"uexKeysStored":false},
             "wipe":{"at":"2026-05-15T00:00:00Z","patch":"Alpha 4.8","scope":"Everything","hidden":0}}
            """);

        page.Do("__dom.node('#diag-samples').checked = true; __dom.node('#diag-samples').fire('change');");

        Assert.Contains(page.Fetched(), url => url.Contains("samples=true"));
        Assert.Contains("with an example of each", page.NodeText("#diag-preview"));
    }

    /// <summary>
    /// A server that cannot answer leaves no half-written file and says what
    /// went wrong.
    /// </summary>
    [Fact]
    public void A_report_that_cannot_be_built_saves_nothing()
    {
        var page = Rendered();
        page.Do("""
            __fetch.unreachable.push('/api/diagnostics?samples=false');
            __dom.node('#diag-save').click();
            """);

        Assert.Equal(0, page.Count("__downloads.length"));
        Assert.Contains("could not be built", page.NodeText("#diag-status"));
        Assert.False(page.Truth("__dom.node('#diag-save').disabled"));
    }
}
