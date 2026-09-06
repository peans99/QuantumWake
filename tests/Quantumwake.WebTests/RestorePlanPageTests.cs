namespace Quantumwake.WebTests;

/// <summary>
/// The restore preview: the one screen in the app that writes over work
/// somebody already did.
/// </summary>
/// <remarks>
/// The server decides what a restore would do and refuses to do anything else;
/// what these tests defend is the half the server cannot see. A tick box that
/// starts in the wrong state, or a choice sent as the opposite of what was
/// shown, overwrites a plan somebody wrote — and the page would look perfectly
/// correct while doing it.
/// </remarks>
public class RestorePlanPageTests
{
    private const string Plan = """
        {"hash":"abc123","ignored":2,
         "adds":1,"replaces":1,"conflicts":1,"deleted":1,"unchanged":1,
         "lines":[
          {"store":"jobs","id":"j1","key":"jobs:j1","label":"Buy armour","action":"add",
           "yours":null,"theirs":"2026-08-20T09:00:00+00:00","takenByDefault":true},
          {"store":"trips","id":"t1","key":"trips:t1","label":"Ore run","action":"replace",
           "yours":"2026-08-01T09:00:00+00:00","theirs":"2026-08-20T09:00:00+00:00","takenByDefault":true},
          {"store":"trips","id":"t2","key":"trips:t2","label":"Pyro scrap","action":"conflict",
           "yours":"2026-09-01T09:00:00+00:00","theirs":"2026-08-20T09:00:00+00:00","takenByDefault":false},
          {"store":"jobs","id":"j2","key":"jobs:j2","label":"Old list","action":"deleted",
           "yours":null,"theirs":"2026-08-20T09:00:00+00:00","takenByDefault":false},
          {"store":"notes","id":"n1","key":"notes:n1","label":"Good rocks","action":"same",
           "yours":"2026-08-20T09:00:00+00:00","theirs":"2026-08-20T09:00:00+00:00","takenByDefault":false}]}
        """;

    private static Page Planned(string plan = Plan)
    {
        var page = new Page();
        page.Serve("/api/backup/plan", plan);
        page.Do("await planRestore('{}');");
        return page;
    }

    [Fact]
    public void The_plan_is_shown_before_anything_is_written()
    {
        var page = Planned();

        Assert.False(page.Truth("__dom.node('#backup-plan').hidden"));
        Assert.DoesNotContain(page.Fetched(), u => u.Contains("/api/backup/restore"));
    }

    /// <summary>
    /// A line that is already identical is not a decision, and putting it in
    /// the table makes the reader check four rows to find the one that matters.
    /// </summary>
    [Fact]
    public void Records_that_already_match_are_not_listed()
    {
        var page = Planned();

        Assert.Equal(4, page.Count("__dom.node('#backup-plan-table').querySelectorAll('tr').length"));
        Assert.DoesNotContain("Good rocks", page.NodeText("#backup-plan-table"));
        Assert.Contains("already the same", page.NodeText("#backup-plan-summary"));
    }

    /// <summary>
    /// The defaults are the whole safety story: bring back what is missing,
    /// replace what is older here, and leave alone anything newer here or
    /// deleted on purpose.
    /// </summary>
    [Fact]
    public void The_ticks_start_where_the_server_said_they_should()
    {
        var page = Planned();
        var boxes = "__dom.node('#backup-plan-table').querySelectorAll('input')";

        Assert.True(page.Truth($"{boxes}[0].checked"));   // add
        Assert.True(page.Truth($"{boxes}[1].checked"));   // replace, yours older
        Assert.False(page.Truth($"{boxes}[2].checked"));  // conflict, yours newer
        Assert.False(page.Truth($"{boxes}[3].checked"));  // deleted on purpose
    }

    /// <summary>
    /// "Conflict" is the code's word and the wrong one here. Nothing has gone
    /// wrong — the reader is being asked whether to overwrite their own newer
    /// work, and the row has to say that.
    /// </summary>
    [Fact]
    public void Each_row_says_why_it_is_on_the_list()
    {
        var text = Planned().NodeText("#backup-plan-table");

        Assert.Contains("not on this machine", text);
        Assert.Contains("yours is older", text);
        Assert.Contains("yours is newer", text);
        Assert.Contains("you deleted this", text);
        Assert.DoesNotContain("conflict", text);
    }

    /// <summary>
    /// "What happened to my pins" is a question somebody will ask of a screen
    /// that just rewrote their jobs, so it is answered before they ask.
    /// </summary>
    [Fact]
    public void It_says_what_it_is_leaving_alone()
    {
        Assert.Contains("pinned or tracked", Planned().NodeText("#backup-plan-summary"));
    }

    [Fact]
    public void Approving_the_plan_untouched_sends_no_exceptions()
    {
        var page = Planned();
        page.Serve("/api/backup/restore", """{"restored":2,"skipped":2,"ignored":2}""");
        page.Do("await applyRestore();");

        var call = Assert.Single(page.Fetched(), u => u.Contains("/api/backup/restore"));

        Assert.Contains("hash=abc123", call);
        Assert.DoesNotContain("take=", call);
        Assert.DoesNotContain("leave=", call);
    }

    /// <summary>
    /// Turning a line around has to reach the server as an exception. Sent the
    /// wrong way it silently does the opposite of what the reader ticked.
    /// </summary>
    [Fact]
    public void Turning_a_line_around_travels_as_an_exception()
    {
        var page = Planned();
        page.Serve("/api/backup/restore", """{"restored":1,"skipped":1,"ignored":2}""");

        var boxes = "__dom.node('#backup-plan-table').querySelectorAll('input')";
        page.Do($"{boxes}[0].checked = false; {boxes}[2].checked = true; await applyRestore();");

        var call = Assert.Single(page.Fetched(), u => u.Contains("/api/backup/restore"));

        Assert.Contains("leave=jobs%3Aj1", call);
        Assert.Contains("take=trips%3At2", call);
    }

    /// <summary>
    /// The hash is what binds the preview to the restore. Without it the server
    /// cannot tell that the file changed underneath, and the promise the
    /// preview made stops being one.
    /// </summary>
    [Fact]
    public void The_restore_quotes_back_the_file_it_previewed()
    {
        var page = Planned();
        page.Serve("/api/backup/restore", """{"restored":2,"skipped":0,"ignored":0}""");
        page.Do("await applyRestore();");

        Assert.Contains("hash=abc123", Assert.Single(page.Fetched(), u => u.Contains("restore")));
    }

    [Fact]
    public void A_file_that_cannot_be_read_says_so_and_shows_no_plan()
    {
        var page = new Page();
        page.Fail("/api/backup/plan", 400, """{"problem":"That is a shared export, not a backup."}""");
        page.Do("await planRestore('{}');");

        Assert.Contains("shared export", page.NodeText("#backup-status"));
        Assert.True(page.Truth("__dom.node('#backup-plan').hidden"));
    }

    /// <summary>
    /// A file that matches what is already here must not offer a Restore button
    /// that would do nothing when pressed.
    /// </summary>
    [Fact]
    public void A_plan_with_nothing_to_do_says_so()
    {
        var page = Planned("""
            {"hash":"abc123","ignored":0,"adds":0,"replaces":0,"conflicts":0,"deleted":0,"unchanged":1,
             "lines":[{"store":"notes","id":"n1","key":"notes:n1","label":"Good rocks","action":"same",
              "yours":null,"theirs":null,"takenByDefault":false}]}
            """);

        Assert.Contains("Nothing to do", page.NodeText("#backup-plan-summary"));
        Assert.True(page.Truth("__dom.node('#backup-apply').disabled"));
    }

    [Fact]
    public void Cancelling_forgets_the_file()
    {
        var page = Planned();
        page.Do("closeRestore();");

        Assert.True(page.Truth("__dom.node('#backup-plan').hidden"));
        Assert.True(page.Truth("restoreFile === null"));
    }
}
