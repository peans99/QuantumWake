using System.Net;
using System.Text;
using System.Text.Json;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The MCP door (1.2.48), driven over HTTP the way Claude Code and Codex
/// drive it: shut until turned on, read-only until changes are allowed, and
/// shut to a web page whatever the switches say.
/// </summary>
[Collection("server")]
public class McpEndpointTests : IClassFixture<ServerUnderTest>, IAsyncLifetime
{
    private readonly ServerUnderTest _server;

    public McpEndpointTests(ServerUnderTest server) => _server = server;

    // Every test starts from the default: off, and no changes.
    public async Task InitializeAsync() => await _server.Posted("/api/mcp", new { enabled = false });

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<HttpResponseMessage> Rpc(string json, string? origin = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.ParseAdd("application/json");
        request.Headers.Accept.ParseAdd("text/event-stream");
        if (origin is not null) request.Headers.Add("Origin", origin);
        return await _server.Client.SendAsync(request);
    }

    private async Task<JsonElement> Result(string json)
    {
        var response = await Rpc(json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static string Call(string tool, string args = "{}") =>
        $$$"""{"jsonrpc":"2.0","id":7,"method":"tools/call","params":{"name":"{{{tool}}}","arguments":{{{args}}}}}""";

    private static string Text(JsonElement answer) =>
        answer.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;

    private static bool IsError(JsonElement answer) =>
        answer.GetProperty("result").GetProperty("isError").GetBoolean();

    [Fact]
    public async Task It_is_off_until_turned_on()
    {
        var response = await Rpc("""{"jsonrpc":"2.0","id":1,"method":"ping"}""");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("Settings", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("2025-06-18", "2025-06-18")]
    [InlineData("2025-03-26", "2025-03-26")]
    [InlineData("2099-01-01", "2025-11-25")]
    public async Task Initialize_answers_in_the_clients_revision_when_it_knows_it(string asked, string answered)
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var answer = await Result($$$"""
            {"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"{{{asked}}}","capabilities":{},
             "clientInfo":{"name":"test-client","version":"1"} } }
            """);

        var result = answer.GetProperty("result");
        Assert.Equal(answered, result.GetProperty("protocolVersion").GetString());
        Assert.Equal("quantum-wake", result.GetProperty("serverInfo").GetProperty("name").GetString());
        Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));

        var status = await _server.Get("/api/mcp");
        Assert.Equal("test-client", status.GetProperty("client").GetString());
    }

    [Fact]
    public async Task A_notification_is_accepted_without_an_answer()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var response = await Rpc("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    /// <summary>No event stream is offered, which the transport answers with 405.</summary>
    [Fact]
    public async Task A_get_is_refused_with_405()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await _server.Client.GetAsync("/mcp")).StatusCode);
    }

    [Fact]
    public async Task Read_only_lists_no_tool_that_changes_anything()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var tools = (await Result("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""))
            .GetProperty("result").GetProperty("tools").EnumerateArray().ToList();

        Assert.Contains(tools, t => t.GetProperty("name").GetString() == "find_places");
        Assert.DoesNotContain(tools, t => t.GetProperty("name").GetString()!.StartsWith("create_", StringComparison.Ordinal));
        Assert.All(tools, t => Assert.True(t.GetProperty("annotations").GetProperty("readOnlyHint").GetBoolean()));
    }

    /// <summary>Checked at the call: a client that listed tools before the switch went off still cannot write.</summary>
    [Fact]
    public async Task A_writing_tool_is_refused_while_changes_are_off()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var answer = await Result(Call("create_checklist", """{"title":"Sneaky","tasks":["x"]}"""));

        Assert.True(IsError(answer));
        Assert.Contains("not allowed changes", Text(answer));
        var lists = await _server.Get("/api/checklists");
        Assert.DoesNotContain(lists.EnumerateArray(), l => l.GetProperty("title").GetString() == "Sneaky");
    }

    [Fact]
    public async Task With_changes_allowed_a_checklist_is_made_with_its_tasks()
    {
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true });

        var answer = await Result(Call("create_checklist", """{"title":"Pyro departure","tasks":["Refuel","Bring tractor beam"]}"""));

        Assert.False(IsError(answer), Text(answer));
        var made = JsonDocument.Parse(Text(answer)).RootElement;
        Assert.Equal(2, made.GetProperty("tasks").GetInt32());

        var list = (await _server.Get("/api/checklists")).EnumerateArray()
            .Single(l => l.GetProperty("id").GetString() == made.GetProperty("id").GetString());
        Assert.Equal(["Refuel", "Bring tractor beam"], list.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()));
        await _server.Delete($"/api/checklists/{made.GetProperty("id").GetString()}");
    }

    [Fact]
    public async Task A_shopping_list_from_an_assistant_says_where_it_came_from()
    {
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true });

        var answer = await Result(Call("create_shopping_list", """{"title":"Hermes fit","items":[{"name":"JS-400","needed":2}]}"""));
        var id = JsonDocument.Parse(Text(answer)).RootElement.GetProperty("id").GetString();

        var job = (await _server.Get("/api/jobs")).EnumerateArray().Single(j => j.GetProperty("id").GetString() == id);
        Assert.Equal("an AI assistant", job.GetProperty("source").GetString());
        await _server.Delete($"/api/jobs/{id}");
    }

    [Fact]
    public async Task A_missing_argument_comes_back_as_a_tool_error_the_model_can_read()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var answer = await Result(Call("find_places"));

        Assert.True(IsError(answer));
        Assert.Contains("'query' is needed", Text(answer));
    }

    [Fact]
    public async Task An_unknown_tool_or_method_is_a_protocol_error()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var tool = await Result(Call("delete_everything"));
        Assert.Equal(-32602, tool.GetProperty("error").GetProperty("code").GetInt32());

        var method = await Result("""{"jsonrpc":"2.0","id":3,"method":"resources/list"}""");
        Assert.Equal(-32601, method.GetProperty("error").GetProperty("code").GetInt32());
    }

    /// <summary>A page in the pilot's browser cannot use the assistant door, whatever the switches say.</summary>
    [Fact]
    public async Task A_request_made_for_another_website_is_refused()
    {
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true });

        var response = await Rpc(Call("create_checklist", """{"title":"From a web page","tasks":["x"]}"""), origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Each_call_is_shown_in_the_log_with_whether_it_wrote()
    {
        await _server.Posted("/api/mcp", new { enabled = true });
        await Result(Call("find_places", """{"query":"Lorville"}"""));

        var calls = (await _server.Get("/api/mcp")).GetProperty("calls");
        var latest = calls[0];
        Assert.Equal("find_places", latest.GetProperty("tool").GetString());
        Assert.Equal("ok", latest.GetProperty("outcome").GetString());
        Assert.False(latest.GetProperty("wrote").GetBoolean());
    }
}

/// <summary>
/// The second set of tools (1.2.50), against the test server's empty install:
/// every reading tool answers rather than failing on data that is not there,
/// and the acting ones set a state, so asking twice changes nothing more.
/// </summary>
[Collection("server")]
public class McpPilotToolTests : IClassFixture<ServerUnderTest>, IAsyncLifetime
{
    private readonly ServerUnderTest _server;

    public McpPilotToolTests(ServerUnderTest server) => _server = server;

    public async Task InitializeAsync() => await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true });

    public async Task DisposeAsync() => await _server.Posted("/api/mcp", new { enabled = false });

    private async Task<(bool Error, string Text)> Call(string tool, object? args = null)
    {
        var body = new { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name = tool, arguments = args ?? new { } } };
        var answer = await _server.Posted("/mcp", body);
        var result = answer.GetProperty("result");
        return (result.GetProperty("isError").GetBoolean(), result.GetProperty("content")[0].GetProperty("text").GetString()!);
    }

    [Theory]
    [InlineData("my_spending")]
    [InlineData("my_ledger")]
    [InlineData("my_earnings")]
    [InlineData("garage")]
    [InlineData("my_loadout")]
    [InlineData("my_contracts")]
    [InlineData("my_reputation")]
    [InlineData("events")]
    [InlineData("wikelo")]
    [InlineData("logbook")]
    [InlineData("refinery")]
    [InlineData("deaths_and_medical")]
    public async Task A_reading_tool_answers_on_an_install_with_nothing_in_it(string tool)
    {
        var (error, text) = await Call(tool);

        Assert.False(error, text);
        Assert.StartsWith("{", text);
    }

    [Fact]
    public async Task Pinning_twice_leaves_it_pinned_and_unpinning_takes_it_off()
    {
        var id = (await _server.Posted("/api/checklists", new { title = "Departure" })).GetProperty("id").GetString();

        await Call("pin_to_now", new { what = "checklist", id });
        await Call("pin_to_now", new { what = "checklist", id });
        Assert.True(Checklist(await _server.Get("/api/checklists"), id!).GetProperty("pinned").GetBoolean());

        await Call("pin_to_now", new { what = "checklist", id, pinned = false });
        Assert.False(Checklist(await _server.Get("/api/checklists"), id!).GetProperty("pinned").GetBoolean());
        await _server.Delete($"/api/checklists/{id}");
    }

    [Fact]
    public async Task Ticking_a_line_twice_leaves_it_ticked()
    {
        var id = (await _server.Posted("/api/jobs", new
        {
            title = "Medical run",
            kind = "list",
            items = new[] { new { name = "MedGel", needed = 4.0 } },
        })).GetProperty("id").GetString();

        var (error, text) = await Call("tick", new { what = "shopping_line", list_id = id, line = "medgel" });
        Assert.False(error, text);
        await Call("tick", new { what = "shopping_line", list_id = id, line = "MedGel" });

        var line = (await _server.Get("/api/jobs")).EnumerateArray().Single(j => j.GetProperty("id").GetString() == id)
            .GetProperty("items")[0];
        Assert.True(line.GetProperty("got").GetBoolean());
        await _server.Delete($"/api/jobs/{id}");
    }

    [Fact]
    public async Task Tracking_a_plan_twice_does_not_untrack_it()
    {
        var plan = await _server.Posted("/api/trips", new { title = "Run", stops = new[] { new { place = "Lorville" } } });
        var id = plan.GetProperty("id").GetString();
        await _server.Posted($"/api/trips/{id}/track");   // the app tracks a new plan; take that off first

        await Call("track_flight_plan", new { plan_id = id });
        await Call("track_flight_plan", new { plan_id = id });

        var trip = (await _server.Get("/api/trips")).EnumerateArray().Single(x => x.GetProperty("id").GetString() == id);
        Assert.True(trip.GetProperty("tracked").GetBoolean());
        await _server.Delete($"/api/trips/{id}");
    }

    /// <summary>The app's own reason, as a sentence: the model passes it on, and raw JSON reads as a fault.</summary>
    [Fact]
    public async Task A_refusal_comes_back_as_the_apps_own_sentence()
    {
        var (error, text) = await Call("plan_haul_run");

        Assert.True(error);
        Assert.DoesNotContain("{", text);
    }

    private static System.Text.Json.JsonElement Checklist(System.Text.Json.JsonElement lists, string id) =>
        lists.EnumerateArray().Single(l => l.GetProperty("id").GetString() == id);
}

/// <summary>The switches themselves.</summary>
public sealed class McpSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qw-mcp-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    [Fact]
    public void A_new_install_is_off_and_read_only()
    {
        var s = new McpSettingsStore(_root).Current;
        Assert.False(s.Enabled);
        Assert.False(s.AllowChanges);
        Assert.False(s.Writes);
    }

    /// <summary>Turning access off takes changes with it, so a later yes to access is not a yes to writing.</summary>
    [Fact]
    public void Turning_access_off_takes_the_changes_switch_with_it()
    {
        var store = new McpSettingsStore(_root);
        store.Set(enabled: true, allowChanges: true);
        Assert.True(store.Current.Writes);

        store.Set(enabled: false);
        store.Set(enabled: true);

        Assert.False(new McpSettingsStore(_root).Current.AllowChanges);
    }

    [Fact]
    public void Changes_cannot_be_allowed_with_access_off()
    {
        var store = new McpSettingsStore(_root);
        store.Set(allowChanges: true);
        Assert.False(store.Current.AllowChanges);

        File.WriteAllText(Path.Combine(_root, "mcp.json"), """{"Enabled":false,"AllowChanges":true}""");
        Assert.False(new McpSettingsStore(_root).Current.AllowChanges);
    }
}
