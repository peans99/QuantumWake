namespace Quantumwake.Tests;

/// <summary>
/// The events endpoint over a server with no game install.
/// </summary>
/// <remarks>
/// The tiers and points come from the archive, which the harness does not
/// have - the state a fresh install is in before the game data is read. What
/// is pinned down is that the page is told so, rather than shown a patch with
/// no events in it.
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
}
