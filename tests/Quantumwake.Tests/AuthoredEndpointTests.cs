using System.Net;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The endpoints behind the pilot's own work: jobs, checklists and flight plans.
/// </summary>
/// <remarks>
/// The stores these sit on are covered elsewhere. What is covered here is the
/// wiring: that a route reaches the store it names, that a missing id is a 404
/// rather than a 500 or a silent success, and that the one-at-a-time rules the
/// stores enforce survive being driven over HTTP.
/// </remarks>
[Collection("server")]
public class AuthoredEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public AuthoredEndpointTests(ServerUnderTest server) => _server = server;

    private static string Id(JsonElement element) => element.GetProperty("id").GetString()!;

    [Fact]
    public async Task A_job_can_be_made_read_back_and_deleted()
    {
        var made = await _server.Posted("/api/jobs", new
        {
            title = "Craft an Omnisky",
            kind = "craft",
            items = new[] { new { name = "Agricium", needed = 4.0, unit = "SCU" } },
        });

        var id = Id(made);

        var listed = await _server.Get("/api/jobs");
        Assert.Contains(listed.EnumerateArray(), j => j.GetProperty("id").GetString() == id);

        Assert.Equal(HttpStatusCode.OK, (await _server.Delete($"/api/jobs/{id}")).StatusCode);

        var after = await _server.Get("/api/jobs");
        Assert.DoesNotContain(after.EnumerateArray(), j => j.GetProperty("id").GetString() == id);
    }

    /// <summary>
    /// A 404 rather than a 500 or a quiet 200: the page turns these straight
    /// into "that is gone" and would otherwise report success for nothing.
    /// </summary>
    [Theory]
    [InlineData("DELETE", "/api/jobs/nosuchid")]
    [InlineData("POST", "/api/jobs/nosuchid/toggle")]
    [InlineData("POST", "/api/jobs/nosuchid/pin")]
    [InlineData("DELETE", "/api/checklists/nosuchid")]
    [InlineData("POST", "/api/checklists/nosuchid/pin")]
    [InlineData("DELETE", "/api/trips/nosuchid")]
    public async Task An_id_that_does_not_exist_is_not_found(string method, string url)
    {
        var response = method == "DELETE"
            ? await _server.Delete(url)
            : await _server.Post(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Only one thing is on Now at a time, and the rule lives in the store. This
    /// says it still holds when the pinning is done through the API, which is
    /// the only way anybody does it.
    /// </summary>
    [Fact]
    public async Task Pinning_a_second_checklist_unpins_the_first()
    {
        var first = Id(await _server.Posted("/api/checklists", new { title = "Pyro departure" }));
        var second = Id(await _server.Posted("/api/checklists", new { title = "Mining run" }));

        await _server.Posted($"/api/checklists/{first}/pin");
        await _server.Posted($"/api/checklists/{second}/pin");

        var lists = await _server.Get("/api/checklists");
        var pinned = lists.EnumerateArray()
            .Where(l => l.GetProperty("pinned").GetBoolean())
            .Select(Id)
            .ToList();

        Assert.Equal([second], pinned);
    }

    /// <summary>
    /// The store caps a title and refuses an empty one. Driving it over HTTP
    /// proves the request record hands the value across rather than dropping it
    /// somewhere between the body and the store.
    /// </summary>
    [Fact]
    public async Task A_title_is_cleaned_on_the_way_in_rather_than_stored_raw()
    {
        var long_ = new string('x', 400);
        var made = await _server.Posted("/api/checklists", new { title = long_ });

        Assert.True(made.GetProperty("title").GetString()!.Length <= 240);

        var blank = await _server.Posted("/api/checklists", new { title = "   " });
        Assert.False(string.IsNullOrWhiteSpace(blank.GetProperty("title").GetString()));
    }

    [Fact]
    public async Task A_checklist_item_can_be_added_ticked_and_removed()
    {
        var list = Id(await _server.Posted("/api/checklists", new { title = "Departure" }));

        var withItem = await _server.Posted($"/api/checklists/{list}/items", new
        {
            text = "Refuel",
            attachments = new[] { new { kind = "location", label = "Port Tressler", target = "Port Tressler" } },
        });

        var item = Id(withItem.GetProperty("items").EnumerateArray().Single());

        var toggled = await _server.Post($"/api/checklists/{list}/items/{item}/toggle");
        Assert.Equal(HttpStatusCode.OK, toggled.StatusCode);

        var after = await _server.Get("/api/checklists");
        var stored = after.EnumerateArray().Single(l => Id(l) == list);
        Assert.True(stored.GetProperty("items").EnumerateArray().Single().GetProperty("done").GetBoolean());

        Assert.Equal(HttpStatusCode.OK,
            (await _server.Delete($"/api/checklists/{list}/items/{item}")).StatusCode);
    }

    /// <summary>
    /// An item on a checklist that is not there must not be created out of thin
    /// air on a list id nobody has.
    /// </summary>
    [Fact]
    public async Task Adding_an_item_to_a_list_that_does_not_exist_is_not_found()
    {
        var response = await _server.Post("/api/checklists/nosuchid/items", new { text = "Refuel" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_trip_keeps_the_stops_it_was_given_in_the_order_given()
    {
        var made = await _server.Posted("/api/trips", new
        {
            title = "Supply run",
            stops = new[]
            {
                new { placeId = "RR_MIC_LEO", place = "Port Tressler", note = "Buy Agricium" },
                new { placeId = "Stanton1_Lorville", place = "Lorville", note = "Sell it" },
            },
        });

        var stops = made.GetProperty("stops").EnumerateArray()
            .Select(s => s.GetProperty("place").GetString())
            .ToList();

        Assert.Equal(["Port Tressler", "Lorville"], stops);

        // Next is the first stop not yet crossed off - what the Now card reads.
        Assert.Equal("Port Tressler", made.GetProperty("next").GetProperty("place").GetString());
    }
}
