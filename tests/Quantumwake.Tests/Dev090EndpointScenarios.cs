using System.Net;
using System.Text;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The 0.9 authored features driven through the same HTTP routes the page uses.
/// </summary>
/// <remarks>
/// The stores have their own focused tests. These scenarios protect the joins
/// between a page-sized request and its result: a backup's plan hash must come
/// back to the same bytes, a kit's missing line must become a real job, and a
/// mining haul must move through every endpoint in the same order the page
/// offers them.
/// </remarks>
[Collection("server")]
public class Dev090EndpointScenarios : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public Dev090EndpointScenarios(ServerUnderTest server) => _server = server;

    private static string Id(JsonElement value) => value.GetProperty("id").GetString()!;

    /// <summary>
    /// A backup only earns its name if it can bring back actual authored work,
    /// using the preview hash the page was shown before it wrote anything.
    /// </summary>
    [Fact]
    public async Task A_backup_round_trip_returns_a_deleted_job()
    {
        var made = await _server.Posted("/api/jobs", new
        {
            title = "0.9 backup round trip",
            kind = "list",
            items = new[] { new { name = "MedPen", needed = 4.0, unit = "units" } },
        });
        var id = Id(made);

        var backup = await _server.Client.GetStringAsync("/api/backup");
        Assert.Equal(HttpStatusCode.OK, (await _server.Delete($"/api/jobs/{id}")).StatusCode);

        using var planResponse = await _server.Client.PostAsync("/api/backup/plan", Text(backup));
        Assert.Equal(HttpStatusCode.OK, planResponse.StatusCode);

        using var planDocument = JsonDocument.Parse(await planResponse.Content.ReadAsStringAsync());
        var plan = planDocument.RootElement;
        var hash = plan.GetProperty("hash").GetString()!;

        Assert.Contains(plan.GetProperty("lines").EnumerateArray(), line =>
            line.GetProperty("id").GetString() == id
            && line.GetProperty("action").GetString() == "Deleted");

        // Deletion is deliberate, so restoring this particular record requires
        // the explicit exception the preview offered rather than a default
        // restore quietly undoing the pilot's choice.
        var take = Uri.EscapeDataString($"jobs:{id}");
        using var restored = await _server.Client.PostAsync($"/api/backup/restore?hash={hash}&take={take}", Text(backup));
        Assert.Equal(HttpStatusCode.OK, restored.StatusCode);

        var jobs = await _server.Get("/api/jobs");
        Assert.Contains(jobs.EnumerateArray(), job => job.GetProperty("id").GetString() == id);
    }

    /// <summary>
    /// Whitespace does not change a parsed backup, but it does change the file
    /// the preview approved. The restore must refuse it rather than treating a
    /// preview of one file as consent to write another.
    /// </summary>
    [Fact]
    public async Task A_backup_file_changed_after_preview_is_refused()
    {
        var backup = await _server.Client.GetStringAsync("/api/backup");

        using var planResponse = await _server.Client.PostAsync("/api/backup/plan", Text(backup));
        using var planDocument = JsonDocument.Parse(await planResponse.Content.ReadAsStringAsync());
        var hash = planDocument.RootElement.GetProperty("hash").GetString()!;

        using var restored = await _server.Client.PostAsync($"/api/backup/restore?hash={hash}", Text(backup + " "));

        Assert.Equal(HttpStatusCode.BadRequest, restored.StatusCode);
        Assert.Contains("not the file", await ServerUnderTest.Refusal(restored));
    }

    /// <summary>
    /// The useful end-to-end answer from a kit is a shopping job, and it must
    /// preserve the requested quantity instead of merely proving a kit exists.
    /// </summary>
    [Fact]
    public async Task A_missing_kit_item_becomes_a_shopping_job()
    {
        var kit = await _server.Posted("/api/kits", new
        {
            name = "0.9 bounty kit",
            items = new[] { new { name = "Railgun", quantity = 2, optional = false } },
        });
        var kitId = Id(kit);

        var prepared = await _server.Get($"/api/kits/{kitId}/prepare");
        var line = Assert.Single(prepared.GetProperty("lines").EnumerateArray());
        Assert.Equal("Missing", line.GetProperty("holding").GetString());

        var shopping = await _server.Posted($"/api/kits/{kitId}/shopping");
        var jobId = shopping.GetProperty("job").GetString()!;
        Assert.Equal(1, shopping.GetProperty("items").GetInt32());

        var jobs = await _server.Get("/api/jobs");
        var item = jobs.EnumerateArray()
            .Single(job => job.GetProperty("id").GetString() == jobId)
            .GetProperty("items").EnumerateArray().Single();

        Assert.Equal("Railgun", item.GetProperty("name").GetString());
        Assert.Equal(2, item.GetProperty("needed").GetDouble());
    }

    /// <summary>
    /// The stage endpoints are deliberately one-way. This covers the sequence
    /// the page offers and verifies that a finished haul no longer looks like
    /// something waiting to be refined.
    /// </summary>
    [Fact]
    public async Task A_haul_moves_from_extracted_to_sold_through_the_api()
    {
        var haul = await _server.Posted("/api/mining/log", new
        {
            place = "Daymar",
            resource = "Quantainium",
            scu = 32.0,
        });
        var id = Id(haul);

        Assert.Equal(HttpStatusCode.OK, (await _server.Post($"/api/mining/log/{id}/submit", new
        {
            place = "ArcCorp 141",
            method = "Dinyx Solventation",
            cost = 4800,
            expectedAt = "2026-09-10T12:00:00+00:00",
        })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _server.Post($"/api/mining/log/{id}/collect?yield=24")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _server.Post($"/api/mining/log/{id}/sell?revenue=180000")).StatusCode);

        var rows = await _server.Get("/api/mining/log");
        var sold = rows.EnumerateArray().Single(run => run.GetProperty("id").GetString() == id);

        Assert.Equal("Sold", sold.GetProperty("stage").GetString());
        Assert.Equal(180_000, sold.GetProperty("revenue").GetDecimal());
        Assert.Equal(24, sold.GetProperty("refinery").GetProperty("yield").GetDouble());
    }

    private static StringContent Text(string value) => new(value, Encoding.UTF8, "application/json");
}
