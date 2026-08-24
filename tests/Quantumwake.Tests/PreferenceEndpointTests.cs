using System.Net;
using System.Text.Json;

namespace Quantumwake.Tests;

/// <summary>
/// The endpoints that change what the app counts or what it is allowed to do.
/// </summary>
/// <remarks>
/// These are the settings-shaped writes, and they matter more than their size
/// suggests: the wipe line decides how much of somebody's history is counted at
/// all, and the two preferences decide whether the app goes out to the internet
/// unattended. A route that read its argument wrongly would turn a refusal into
/// consent, and nothing underneath would notice.
/// </remarks>
[Collection("server")]
public class PreferenceEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public PreferenceEndpointTests(ServerUnderTest server) => _server = server;

    /// <summary>
    /// A wipe is not destructive - it moves the line history is counted from -
    /// so the endpoint has to hand back what it decided rather than leaving the
    /// page to assume it landed.
    /// </summary>
    [Fact]
    public async Task Setting_the_wipe_line_answers_with_what_it_set()
    {
        var set = await _server.Posted("/api/wipe", new
        {
            at = "2026-05-15T00:00:00+00:00",
            patch = "Alpha 4.8",
            covers = new[] { "money", "inventory" },
        });

        Assert.Equal("Alpha 4.8", set.GetProperty("patch").GetString());

        var read = await _server.Get("/api/wipe");
        Assert.Equal("Alpha 4.8", read.GetProperty("patch").GetString());
    }

    /// <summary>
    /// Moving the date back has to bring the whole history straight back with
    /// it, since a wipe never destroyed anything to begin with.
    /// </summary>
    [Fact]
    public async Task A_wipe_line_can_be_moved_back_again()
    {
        await _server.Posted("/api/wipe", new
        {
            at = "2026-05-15T00:00:00+00:00",
            patch = "Alpha 4.8",
            covers = new[] { "money" },
        });

        var cleared = await _server.Posted("/api/wipe", new
        {
            at = (string?)null,
            patch = (string?)null,
            covers = Array.Empty<string>(),
        });

        // Nothing is counted out any more, and the endpoint says so rather than
        // leaving the previous line in place with no complaint.
        Assert.NotEqual("Alpha 4.8", Patch(cleared));
        Assert.NotEqual("Alpha 4.8", Patch(await _server.Get("/api/wipe")));
    }

    /// <summary>
    /// Both of these are answers to "may the app go out on its own". The wrong
    /// reading of the flag turns a refusal into standing permission, which is
    /// the promise the README makes on the project's behalf.
    /// </summary>
    [Theory]
    [InlineData("/api/updates/answer")]
    [InlineData("/api/uex/auto/answer")]
    public async Task A_standing_permission_is_recorded_as_given_and_as_refused(string url)
    {
        var refused = await _server.Posted($"{url}?automatic=false");
        Assert.False(Automatic(refused));

        var given = await _server.Posted($"{url}?automatic=true");
        Assert.True(Automatic(given));

        // And asked-once means asked: a refusal is a recorded answer, not silence.
        var again = await _server.Posted($"{url}?automatic=false");
        Assert.False(Automatic(again));
        Assert.True(Asked(again));
    }

    private static string? Patch(JsonElement wipe) =>
        wipe.TryGetProperty("patch", out var value) && value.ValueKind is JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool Automatic(JsonElement answer) =>
        answer.TryGetProperty("automatic", out var value) && value.GetBoolean();

    private static bool Asked(JsonElement answer) =>
        answer.TryGetProperty("asked", out var value) && value.GetBoolean();

    /// <summary>
    /// Collecting the same thing twice sums it rather than making a second line,
    /// which is the store's rule and the reason an imported row must never be
    /// collected into a list somebody already had.
    /// </summary>
    [Fact]
    public async Task Collecting_the_same_thing_twice_sums_it_into_one_line()
    {
        await _server.Posted("/api/jobs/collect", new { name = "Hadanite", needed = 4.0, unit = "SCU" });
        var second = await _server.Posted("/api/jobs/collect", new { name = "hadanite", needed = 3.0, unit = "SCU" });

        var jobs = await _server.Get("/api/jobs");
        var list = jobs.EnumerateArray()
            .Single(j => j.GetProperty("id").GetString() == second.GetProperty("id").GetString());

        var line = list.GetProperty("items").EnumerateArray()
            .Single(i => string.Equals(i.GetProperty("name").GetString(), "Hadanite",
                StringComparison.OrdinalIgnoreCase));

        Assert.Equal(7.0, line.GetProperty("needed").GetDouble());
    }

    /// <summary>
    /// A destination is a plan rather than a fact, so it can be set and cleared;
    /// clearing it is sending nothing, which must not be read as a place called
    /// nothing.
    /// </summary>
    [Fact]
    public async Task A_jobs_destination_can_be_set_and_taken_away_again()
    {
        var job = await _server.Posted("/api/jobs", new { title = "Shopping", kind = "list" });
        var id = job.GetProperty("id").GetString();

        await _server.Posted($"/api/jobs/{id}/destination",
            new { place = "Port Tressler", placeId = "RR_MIC_LEO" });

        var withPlace = (await _server.Get("/api/jobs")).EnumerateArray()
            .Single(j => j.GetProperty("id").GetString() == id);
        Assert.Equal("Port Tressler", withPlace.GetProperty("destination").GetString());
        Assert.Equal("RR_MIC_LEO", withPlace.GetProperty("destinationId").GetString());

        await _server.Posted($"/api/jobs/{id}/destination", new { place = (string?)null, placeId = (string?)null });

        var cleared = (await _server.Get("/api/jobs")).EnumerateArray()
            .Single(j => j.GetProperty("id").GetString() == id);
        // Absent rather than an empty string: WhenWritingNull drops it, and a
        // place called "" would be a destination the map would try to find.
        Assert.False(cleared.TryGetProperty("destination", out var destination)
            && destination.ValueKind is not JsonValueKind.Null);
    }

    /// <summary>
    /// The version the About page and the footer read, and the number an export
    /// stamps itself with. Reflection rather than a constant, so it can never
    /// disagree with the assembly actually running.
    /// </summary>
    [Fact]
    public async Task The_server_reports_the_version_it_is_actually_running()
    {
        var version = await _server.Get("/api/version");

        var reported = version.GetProperty("version").GetString();
        Assert.NotNull(reported);
        Assert.NotEqual("0.0.0", reported);

        var exported = await _server.Post("/api/export", new { blueprints = true });
        var document = JsonDocument.Parse(await exported.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(reported, document.GetProperty("producer").GetProperty("version").GetString());
    }

    /// <summary>
    /// A body the endpoint cannot make sense of must not become a 500. The page
    /// shows a refusal; an unhandled exception is a blank screen.
    /// </summary>
    [Fact]
    public async Task A_malformed_body_is_refused_rather_than_thrown()
    {
        var response = await _server.Client.PostAsync("/api/jobs",
            new StringContent("{ this is not json", System.Text.Encoding.UTF8, "application/json"));

        Assert.True((int)response.StatusCode < 500,
            $"a broken body answered {(int)response.StatusCode}");
    }
}
