using System.Net;

namespace Quantumwake.Tests;

/// <summary>
/// The emporium endpoints over a server with no game install.
/// </summary>
/// <remarks>
/// The reader itself is proven against the real archive (docs/wikelo.md
/// records what it found); the harness here has no Data.p4k, which is exactly
/// the state a fresh install is in before the game data has been read. What is
/// pinned down is that the page is told so rather than shown zero trades, and
/// that a trade the file does not list cannot become a job.
/// </remarks>
[Collection("server")]
public class WikeloEndpointTests : IClassFixture<ServerUnderTest>
{
    private readonly ServerUnderTest _server;

    public WikeloEndpointTests(ServerUnderTest server) => _server = server;

    [Fact]
    public async Task With_no_game_files_the_emporium_says_it_is_unavailable_rather_than_empty()
    {
        var got = await _server.Get("/api/wikelo");

        Assert.False(got.GetProperty("available").GetBoolean());
        Assert.Equal(0, got.GetProperty("trades").GetArrayLength());
        Assert.Equal(0, got.GetProperty("standings").GetArrayLength());
    }

    [Fact]
    public async Task Tracking_a_trade_the_file_does_not_list_is_refused_and_makes_no_job()
    {
        var before = (await _server.Get("/api/jobs")).GetArrayLength();

        var response = await _server.Client.PostAsync("/api/wikelo/TheCollector_Vehicle_Small_Fortune/track", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("installed game does not list", await response.Content.ReadAsStringAsync());
        Assert.Equal(before, (await _server.Get("/api/jobs")).GetArrayLength());
    }
}
