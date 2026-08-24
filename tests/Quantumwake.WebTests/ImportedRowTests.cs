namespace Quantumwake.WebTests;

/// <summary>
/// Somebody else's rows, drawn beside the reader's own.
/// </summary>
/// <remarks>
/// The hazard this exists to prevent: ids are minted per install with eight hex
/// characters and no namespace, so a file exported from this machine and read
/// back carries ids identical to the reader's own by construction, not by bad
/// luck. Every control on a job card builds its URL out of one. Without both
/// defences - the server's prefix and this page drawing no mutating control at
/// all - clicking Delete on a stranger's card would delete your own job.
/// </remarks>
public class ImportedRowTests
{
    /// <summary>
    /// The collision, arranged exactly: the same id on a local job and an
    /// imported one, which is what re-importing your own export produces.
    /// </summary>
    private const string Colliding = """
        [
          {"id":"4e7b21aa","title":"My own build","kind":"craft","createdAt":"2026-08-01T00:00:00+00:00",
           "done":false,"pinned":false,"items":[],"haveCount":0,"totalCount":0},
          {"id":"imp:9f2c1ab3:4e7b21aa","title":"Bob's build","kind":"craft",
           "createdAt":"2026-08-01T00:00:00+00:00","done":false,"pinned":false,
           "items":[{"name":"Agricium","needed":4,"unit":"SCU","have":true,"where":["Port Tressler"],
                     "wornNow":false,"buyPrice":null,"buyAt":null}],
           "haveCount":1,"totalCount":1,
           "imported":{"batchId":"9f2c1ab3","handle":"bob","importedAt":"2026-08-24T18:00:00+00:00"}}
        ]
        """;

    private static Page WithJobs(string json)
    {
        var page = new Page();
        page.Serve("/api/jobs", json);
        page.Serve("/api/trips", "[]");
        page.Do("await loadJobList();");
        return page;
    }

    [Fact]
    public void An_imported_job_says_whose_it_is()
    {
        var page = WithJobs(Colliding);
        var text = page.NodeText("#blueprint-jobs");

        Assert.Contains("Bob's build", text);
        Assert.Contains("from bob", text);
    }

    /// <summary>
    /// The one that matters. Nothing on an imported card may issue a request
    /// that could reach a job of the reader's, and the ids here are equal, so
    /// the only safe number of such requests is zero.
    /// </summary>
    [Fact]
    public void No_control_on_an_imported_card_can_touch_a_job_of_your_own()
    {
        var page = WithJobs(Colliding);

        // Press everything the imported card offers.
        page.Do("""
            __imported = __dom.node('#blueprint-jobs').byClass('from-a-file')
              .filter(n => n.tagName === 'article')[0];
            for (const button of __imported.descendants().filter(n => n.tagName === 'button')) {
              button.fire('click');
            }
            """);

        // Nothing may address the bare id, which is also the reader's own job.
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/api/jobs/4e7b21aa"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("DELETE"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/toggle"));
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("/pin"));
    }

    [Fact]
    public void Your_own_card_keeps_every_control_it_had()
    {
        var page = WithJobs(Colliding);
        page.Do("""
            __mine = __dom.node('#blueprint-jobs').descendants()
              .filter(n => n.tagName === 'article' && !n.classList.contains('from-a-file'))[0];
            __mine.descendants().filter(n => n.tagName === 'button'
              && n.textContent === 'Delete')[0].fire('click');
            """);

        Assert.Contains("DELETE /api/jobs/4e7b21aa", page.Fetched());
    }

    /// <summary>
    /// The only route by which somebody else's data becomes yours, and it goes
    /// through the ordinary authoring endpoint so the copy is minted a fresh
    /// local id with the normal checks.
    /// </summary>
    [Fact]
    public void Copying_makes_your_own_list_through_the_ordinary_door()
    {
        var page = WithJobs(Colliding);
        page.Serve("/api/jobs", Colliding);
        page.Do("""
            __imported = __dom.node('#blueprint-jobs').byClass('from-a-file')
              .filter(n => n.tagName === 'article')[0];
            __imported.descendants().filter(n => n.tagName === 'button'
              && n.textContent === 'Copy to my lists')[0].fire('click');
            """);

        Assert.Contains("POST /api/jobs", page.Fetched());

        var sent = page.BodyOf("/api/jobs");
        Assert.Contains("Bob's build", sent);
        Assert.Contains("copied from bob", sent);
        Assert.Contains("Agricium", sent);

        // The copy carries no id of theirs; the server mints one.
        Assert.DoesNotContain("4e7b21aa", sent);
    }

    /// <summary>
    /// Importing a friend's lists must not silently repopulate your own pages,
    /// so the request goes out exactly as it did before this existed.
    /// </summary>
    [Fact]
    public void Nothing_shared_is_asked_for_until_it_is_switched_on()
    {
        var page = WithJobs("[]");

        Assert.Contains("GET /api/jobs", page.Fetched());
        Assert.DoesNotContain(page.Fetched(), url => url.Contains("imported="));
    }

    [Fact]
    public void Switching_it_on_asks_for_the_batch_that_was_chosen()
    {
        var page = WithJobs("[]");
        page.Serve("/api/jobs?imported=9f2c1ab3", "[]");
        page.Serve("/api/checklists?imported=9f2c1ab3", "[]");
        page.Serve("/api/trips?imported=9f2c1ab3", "[]");
        page.Do("setShowImported('9f2c1ab3');");

        Assert.Contains(page.Fetched(), url => url.Contains("/api/jobs?imported=9f2c1ab3"));
    }

    /// <summary>
    /// Now is the one card where being wrong is loudest, and the overlay draws
    /// from it too.
    /// </summary>
    [Fact]
    public void An_imported_list_never_becomes_the_one_pinned_to_now()
    {
        var page = new Page();
        page.Serve("/api/checklists", """
            [{"id":"imp:9f2c1ab3:c1","title":"Bob's departure","createdAt":"2026-08-01T00:00:00+00:00",
              "pinned":true,"items":[],
              "imported":{"batchId":"9f2c1ab3","handle":"bob","importedAt":"2026-08-24T18:00:00+00:00"}}]
            """);
        page.Do("await loadChecklists();");

        Assert.True(page.Truth("__dom.node('#now-checklist-card').hidden"));
    }
}
