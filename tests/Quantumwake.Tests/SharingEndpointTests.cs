using System.Net;
using System.Text;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The endpoints that move a file between two pilots.
/// </summary>
/// <remarks>
/// The document format and the reader are tested on their own. These are about
/// the wiring: that export is a POST and therefore behind the LAN rule, that a
/// refusal carries the sentence the page shows, that the file arrives named,
/// and that an import can be undone through the API rather than only in the
/// store underneath it.
/// </remarks>
[Collection("server")]
public class SharingEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public SharingEndpointTests(ServerUnderTest server) => _server = server;

    private static string Document(string handle = "nekron", string blueprint = "Omnisky IX") =>
        "{\"format\":\"quantumwake.export\",\"formatVersion\":1,\"contentVersion\":1,"
        + "\"exportedAt\":\"2026-08-24T12:00:00+00:00\","
        + "\"producer\":{\"app\":\"Quantum Wake\",\"version\":\"0.8.0\"},"
        + $"\"handle\":\"{handle}\",\"classes\":[\"blueprints\"],"
        + "\"blueprints\":{\"caveats\":[],\"rows\":"
        + $"[{{\"at\":\"2026-08-01T00:00:00+00:00\",\"name\":\"{blueprint}\"}}]}}}}";

    private Task<HttpResponseMessage> Import(string document, string name = "friend.json", bool force = false) =>
        _server.Post($"/api/imports{(force ? "?force=true" : "")}",
            new { document, sourceName = name });

    /* ---------- export ---------- */

    /// <summary>
    /// Asking for nothing has to be refused in words the page can show, not with
    /// an empty file that looks like a successful share of everything.
    /// </summary>
    [Fact]
    public async Task Export_refuses_a_choice_that_asks_for_nothing()
    {
        var response = await _server.Post("/api/export", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("at least one", await ServerUnderTest.Refusal(response));
    }

    /// <summary>
    /// Every class is off in the protocol even though the page's boxes start
    /// ticked, so a stale client or a typo shares nothing rather than everything.
    /// </summary>
    [Fact]
    public async Task A_body_naming_no_class_shares_nothing_rather_than_all_of_it()
    {
        var response = await _server.Post("/api/export", new { days = 30 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_export_arrives_as_a_named_file_stamped_by_this_build()
    {
        var response = await _server.Post("/api/export", new { blueprints = true });

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var name = response.Content.Headers.ContentDisposition?.FileName;
        Assert.NotNull(name);
        Assert.StartsWith("quantumwake-", name.Trim('"'));
        Assert.EndsWith(".json", name.Trim('"'));

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("quantumwake.export", document.GetProperty("format").GetString());
        Assert.Equal(1, document.GetProperty("formatVersion").GetInt32());
        Assert.Equal("Quantum Wake", document.GetProperty("producer").GetProperty("app").GetString());

        // Only what was asked for: the two classes not ticked are absent, not
        // present and empty.
        Assert.True(document.TryGetProperty("blueprints", out _));
        Assert.False(document.TryGetProperty("receipts", out _));
        Assert.False(document.TryGetProperty("authored", out _));
    }

    /// <summary>
    /// Nothing leaves without a click, and the preview is what the click follows.
    /// It answers counts, and must never answer rows.
    /// </summary>
    [Fact]
    public async Task The_preview_answers_counts_and_no_rows_at_all()
    {
        var preview = await _server.Get("/api/export/preview?receipts=true&blueprints=true&authored=true&days=7");

        Assert.Equal(7, preview.GetProperty("days").GetInt32());
        Assert.Equal(7, preview.GetProperty("defaultDays").GetInt32());
        Assert.True(preview.TryGetProperty("receipts", out var receipts));
        Assert.Equal(JsonValueKind.Number, receipts.ValueKind);

        Assert.False(preview.TryGetProperty("rows", out _));
    }

    /* ---------- import ---------- */

    [Fact]
    public async Task A_document_is_read_into_a_batch_that_says_where_it_came_from()
    {
        var response = await Import(Document("bob"), "bobs-share.json");
        Assert.True(response.IsSuccessStatusCode);

        var batch = JsonDocument.Parse(await response.Content.ReadAsStringAsync())
            .RootElement.GetProperty("batch");

        Assert.Equal("bob", batch.GetProperty("handle").GetString());
        Assert.Equal("bobs-share.json", batch.GetProperty("sourceName").GetString());
        Assert.Equal(1, batch.GetProperty("counts").GetProperty("blueprints").GetInt32());
        Assert.True(batch.GetProperty("readable").GetBoolean());

        // Contents are not on the list payload; a batch can hold 20,000 rows.
        var listed = await _server.Get("/api/imports");
        var first = listed.GetProperty("batches").EnumerateArray().First();
        Assert.False(first.TryGetProperty("blueprints", out _));
    }

    [Fact]
    public async Task Something_that_is_not_an_export_is_refused_by_name()
    {
        var response = await Import("{\"jobs\":[]}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("not a Quantum Wake export", await ServerUnderTest.Refusal(response));
    }

    [Fact]
    public async Task A_file_from_a_newer_build_is_refused_and_says_which()
    {
        var newer = Document().Replace("\"formatVersion\":1", "\"formatVersion\":99");
        var response = await Import(newer);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("newer Quantum Wake", await ServerUnderTest.Refusal(response));
    }

    /// <summary>
    /// The common way to arrive here twice is a double-clicked picker; the other
    /// is a deliberate re-import after a purge. Only the reader knows which, so
    /// the endpoint asks instead of choosing.
    /// </summary>
    [Fact]
    public async Task The_same_file_twice_is_a_question_and_force_is_the_answer()
    {
        var document = Document("kate");

        Assert.True((await Import(document)).IsSuccessStatusCode);

        var again = await Import(document);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        var conflict = JsonDocument.Parse(await again.Content.ReadAsStringAsync()).RootElement;
        Assert.True(conflict.GetProperty("duplicate").GetBoolean());
        Assert.Equal("kate", conflict.GetProperty("batch").GetProperty("handle").GetString());

        Assert.True((await Import(document, force: true)).IsSuccessStatusCode);

        var listed = await _server.Get("/api/imports");
        Assert.Equal(2, listed.GetProperty("batches").EnumerateArray()
            .Count(b => b.GetProperty("handle").GetString() == "kate"));
    }

    /// <summary>
    /// A file too big is stopped on its size before anything parses it, and the
    /// status has to be the one that means that rather than a generic refusal.
    /// </summary>
    [Fact]
    public async Task A_document_past_the_size_cap_is_refused_as_too_large()
    {
        var huge = Document(blueprint: new string('x', 9 * 1024 * 1024));
        var response = await Import(huge);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    /* ---------- taking it away again ---------- */

    [Fact]
    public async Task One_class_can_go_while_the_batch_and_its_fingerprint_stay()
    {
        var document = Document("removeme");
        var batch = JsonDocument.Parse(await (await Import(document)).Content.ReadAsStringAsync())
            .RootElement.GetProperty("batch");

        var id = batch.GetProperty("id").GetString();

        Assert.Equal(HttpStatusCode.OK, (await _server.Delete($"/api/imports/{id}/blueprints")).StatusCode);

        var listed = await _server.Get("/api/imports");
        var after = listed.GetProperty("batches").EnumerateArray()
            .Single(b => b.GetProperty("id").GetString() == id);

        Assert.Empty(after.GetProperty("classes").EnumerateArray());
        Assert.Equal(0, after.GetProperty("counts").GetProperty("blueprints").GetInt32());

        // Still recognised, so the same file coming round again is noticed.
        Assert.Equal(HttpStatusCode.Conflict, (await Import(document)).StatusCode);
    }

    [Fact]
    public async Task Hiding_is_a_different_thing_from_removing()
    {
        var id = JsonDocument.Parse(await (await Import(Document("hideme"))).Content.ReadAsStringAsync())
            .RootElement.GetProperty("batch").GetProperty("id").GetString();

        await _server.Posted($"/api/imports/{id}/hide");

        var listed = await _server.Get("/api/imports");
        var after = listed.GetProperty("batches").EnumerateArray()
            .Single(b => b.GetProperty("id").GetString() == id);

        Assert.True(after.GetProperty("hidden").GetBoolean());
        Assert.Equal(1, after.GetProperty("counts").GetProperty("blueprints").GetInt32());
    }

    [Theory]
    [InlineData("DELETE", "/api/imports/nosuchid")]
    [InlineData("DELETE", "/api/imports/nosuchid/blueprints")]
    [InlineData("POST", "/api/imports/nosuchid/hide")]
    public async Task A_batch_that_does_not_exist_is_not_found(string method, string url)
    {
        var response = method == "DELETE" ? await _server.Delete(url) : await _server.Post(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The promise the whole feature rests on: a purge takes the import away and
    /// leaves the reader's own work exactly where it was.
    /// </summary>
    [Fact]
    public async Task Clearing_every_import_does_not_touch_the_readers_own_work()
    {
        var mine = await _server.Posted("/api/jobs", new { title = "My own job", kind = "list" });
        var before = await File.ReadAllBytesAsync(Path.Combine(_server.DataDirectory, "jobs.json"));

        await Import(Document("someone"));
        await _server.Delete("/api/imports");

        var listed = await _server.Get("/api/imports");
        Assert.Empty(listed.GetProperty("batches").EnumerateArray());

        var after = await File.ReadAllBytesAsync(Path.Combine(_server.DataDirectory, "jobs.json"));
        Assert.Equal(before, after);

        var jobs = await _server.Get("/api/jobs");
        Assert.Contains(jobs.EnumerateArray(),
            j => j.GetProperty("id").GetString() == mine.GetProperty("id").GetString());
    }

    /* ---------- what a page sees ---------- */

    /// <summary>
    /// Imported rows are off unless asked for. Importing a friend's lists must
    /// not silently repopulate somebody's own pages.
    /// </summary>
    [Fact]
    public async Task Imported_rows_are_absent_until_a_page_asks_for_them()
    {
        var authored =
            "{\"format\":\"quantumwake.export\",\"formatVersion\":1,\"contentVersion\":1,"
            + "\"exportedAt\":\"2026-08-24T12:00:00+00:00\","
            + "\"producer\":{\"app\":\"Quantum Wake\",\"version\":\"0.8.0\"},"
            + "\"handle\":\"quiet\",\"classes\":[\"authored\"],"
            + "\"authored\":{\"checklists\":[],\"trips\":[],\"jobs\":[{\"id\":\"abc12345\","
            + "\"title\":\"Theirs\",\"kind\":\"list\",\"createdAt\":\"2026-08-01T00:00:00+00:00\","
            + "\"done\":false,\"items\":[]}]}}";

        Assert.True((await Import(authored, "quiet.json")).IsSuccessStatusCode);

        var mine = await _server.Get("/api/jobs");
        Assert.DoesNotContain(mine.EnumerateArray(), j => j.GetProperty("title").GetString() == "Theirs");

        var shared = await _server.Get("/api/jobs?imported=all");
        var theirs = shared.EnumerateArray().Single(j => j.GetProperty("title").GetString() == "Theirs");

        // Re-addressed, so pin and delete cannot reach a job of the reader's.
        Assert.StartsWith("imp:", theirs.GetProperty("id").GetString());
        Assert.Equal("quiet", theirs.GetProperty("imported").GetProperty("handle").GetString());
        Assert.False(theirs.GetProperty("pinned").GetBoolean());
    }

    /// <summary>
    /// The re-addressed id must not address anything. This is the defence that
    /// stops somebody else's file deleting the reader's own job.
    /// </summary>
    [Fact]
    public async Task An_imported_id_cannot_reach_a_job_of_the_readers()
    {
        var mine = await _server.Posted("/api/jobs", new { title = "Do not delete me", kind = "list" });
        var id = mine.GetProperty("id").GetString()!;

        // Exactly what re-importing your own export would produce.
        var response = await _server.Delete($"/api/jobs/imp:9f2c1ab3:{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var jobs = await _server.Get("/api/jobs");
        Assert.Contains(jobs.EnumerateArray(), j => j.GetProperty("id").GetString() == id);
    }
}
