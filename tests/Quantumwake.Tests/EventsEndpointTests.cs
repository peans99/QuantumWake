using System.Net;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The events endpoint over a server with no game install.
/// </summary>
/// <remarks>
/// The tiers and points come from the archive, which the harness does not
/// have - the state a fresh install is in before the game data is read. What
/// is pinned down is that the page is told so, rather than shown a patch with
/// no events in it, and that a choice to track can only name an event the
/// installed game lists.
/// </remarks>
[Collection("server")]
public class EventsEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public EventsEndpointTests(ServerUnderTest server) => _server = server;

    [Fact]
    public async Task With_no_game_files_events_say_they_are_unavailable_rather_than_empty()
    {
        var got = await _server.Get("/api/events");

        Assert.False(got.GetProperty("available").GetBoolean());
        Assert.Equal(0, got.GetProperty("events").GetArrayLength());
        Assert.Equal(0, got.GetProperty("open").GetArrayLength());
    }

    /// <summary>
    /// A typo or a retired event would pin the Now card to nothing, so an id
    /// the installed game does not list is refused and changes nothing.
    /// </summary>
    [Fact]
    public async Task Tracking_an_event_the_game_does_not_list_is_refused()
    {
        var response = await _server.Client.PostAsync("/api/events/track?id=Iasi_ScenarioProgress", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("installed game does not list", await response.Content.ReadAsStringAsync());
        Assert.True(Untracked(await _server.Get("/api/events")));
    }

    [Fact]
    public async Task Clearing_the_choice_always_works()
    {
        var cleared = await _server.Posted("/api/events/track");

        Assert.True(Untracked(cleared));
    }

    // The server leaves nulls out of its JSON, so no choice is a missing field
    // as often as a null one; the page reads both as "follow play".
    private static bool Untracked(System.Text.Json.JsonElement body) =>
        !body.TryGetProperty("tracked", out var tracked) || tracked.ValueKind == System.Text.Json.JsonValueKind.Null;
}

/// <summary>The tracked event survives a restart, and clearing it does too.</summary>
public class EventTrackStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"qw-track-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void A_choice_is_kept_across_a_restart_and_so_is_clearing_it()
    {
        new EventTrackStore(_directory).Save(" Iasi_ScenarioProgress ");
        Assert.Equal("Iasi_ScenarioProgress", new EventTrackStore(_directory).Tracked);

        new EventTrackStore(_directory).Save("");
        Assert.Null(new EventTrackStore(_directory).Tracked);
    }

    [Fact]
    public void No_file_is_no_choice()
    {
        Assert.Null(new EventTrackStore(_directory).Tracked);
    }
}
