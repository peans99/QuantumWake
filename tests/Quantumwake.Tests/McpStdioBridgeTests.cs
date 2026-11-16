using System.Text.Json;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The stdio relay Claude Desktop starts (1.2.53), on in-memory readers
/// against the test server: one line in, one line out, nothing for a
/// notification, and a sentence the client can show when the app is shut or
/// not running.
/// </summary>
[Collection("server")]
public class McpStdioBridgeTests : IClassFixture<ServerUnderTest>, IAsyncLifetime
{
    private readonly ServerUnderTest _server;

    public McpStdioBridgeTests(ServerUnderTest server) => _server = server;

    public async Task InitializeAsync() =>
        await _server.Posted("/api/mcp", new { enabled = true, areas = McpEndpointTests.DefaultAreas });

    public async Task DisposeAsync() => await _server.Posted("/api/mcp", new { enabled = false });

    private static async Task<(List<JsonElement> Out, string Log)> Relay(Uri endpoint, params string[] lines)
    {
        var output = new StringWriter { NewLine = "\n" };
        var log = new StringWriter();
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        var code = await McpStdioBridge.Relay(new StringReader(string.Join("\n", lines) + "\n"), output, log, client, endpoint, CancellationToken.None);

        Assert.Equal(0, code);
        var written = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return ([.. written.Select(l => JsonDocument.Parse(l).RootElement.Clone())], log.ToString());
    }

    private Uri Mcp => new(_server.Client.BaseAddress!, "/mcp");

    [Fact]
    public async Task Each_request_gets_one_line_back_and_a_notification_gets_none()
    {
        var (answers, log) = await Relay(Mcp,
            """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"claude-ai","version":"1"}}}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""",
            "",
            """{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");

        Assert.Equal(2, answers.Count);
        Assert.Equal(1, answers[0].GetProperty("id").GetInt32());
        Assert.Equal("quantum-wake", answers[0].GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.Contains(answers[1].GetProperty("result").GetProperty("tools").EnumerateArray(), t => t.GetProperty("name").GetString() == "find_places");
        Assert.Contains("relaying stdio", log);

        // Relayed, the client is in the app's own call log like any other.
        Assert.Equal("claude-ai", (await _server.Get("/api/mcp")).GetProperty("client").GetString());
    }

    [Fact]
    public async Task With_access_off_the_client_is_told_where_to_turn_it_on()
    {
        await _server.Posted("/api/mcp", new { enabled = false });

        var (answers, _) = await Relay(Mcp, """{"jsonrpc":"2.0","id":"a","method":"ping"}""");

        var error = Assert.Single(answers).GetProperty("error");
        Assert.Equal("a", answers[0].GetProperty("id").GetString());
        Assert.Contains("Settings", error.GetProperty("message").GetString());
    }

    /// <summary>Port 1 refuses: the app is not running, and the bridge does not start it.</summary>
    [Fact]
    public async Task With_nothing_running_each_request_says_to_start_the_app()
    {
        var (answers, log) = await Relay(new Uri("http://127.0.0.1:1/mcp"),
            """{"jsonrpc":"2.0","id":5,"method":"tools/list"}""",
            """{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        var answer = Assert.Single(answers);
        Assert.Equal(5, answer.GetProperty("id").GetInt32());
        Assert.Contains("not running on port 1", answer.GetProperty("error").GetProperty("message").GetString());
        Assert.Contains("did not answer", log);
    }

    [Fact]
    public async Task A_line_that_is_not_JSON_is_a_parse_error()
    {
        var (answers, _) = await Relay(Mcp, "{not json");

        Assert.Equal(-32700, Assert.Single(answers).GetProperty("error").GetProperty("code").GetInt32());
    }

    [Theory]
    [InlineData(new[] { "--mcp-stdio" }, 31337)]
    [InlineData(new[] { "--mcp-stdio", "--Port", "31395" }, 31395)]
    [InlineData(new[] { "--Port=31400", "--mcp-stdio" }, 31400)]
    public void The_port_is_read_as_the_server_reads_it(string[] args, int port)
    {
        Assert.True(McpStdioBridge.Asked(args));
        Assert.Equal(port, McpStdioBridge.PortFromArguments(args));
    }

    [Fact]
    public void The_launch_line_names_the_port_only_when_it_is_not_the_default()
    {
        Assert.Equal(["--mcp-stdio"], McpStdioBridge.Launch(31337).Args[^1..]);
        Assert.Equal(["--mcp-stdio", "--Port", "31395"], McpStdioBridge.Launch(31395).Args[^3..]);
    }
}
