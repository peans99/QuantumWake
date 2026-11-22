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

    /// <summary>The areas a new install opens, posted to put back whatever a test narrowed.</summary>
    internal static readonly Dictionary<string, bool> DefaultAreas = McpArea.All.ToDictionary(a => a.Key, a => a.DefaultOn);

    // Every test starts from the default: off, no changes, the default areas.
    public async Task InitializeAsync() => await _server.Posted("/api/mcp", new { enabled = false, areas = DefaultAreas });

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

    private async Task<List<string>> ToolNames() =>
        [.. (await Result("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}"""))
            .GetProperty("result").GetProperty("tools").EnumerateArray().Select(t => t.GetProperty("name").GetString()!)];

    /// <summary>
    /// The table that sorts tools into areas is complete: a tool missing from
    /// it throws when the list is first built, and this builds it.
    /// </summary>
    [Fact]
    public void Every_tool_belongs_to_an_area_or_to_the_catalogue()
    {
        var tools = Quantumwake.Server.McpTools.All;

        Assert.All(tools.Where(t => t.Area is not null), t => Assert.Contains(McpArea.All, a => a.Key == t.Area));
        Assert.All(tools.Where(t => t.Writes), t => Assert.Equal(McpArea.Plans, t.Area));
        Assert.Contains(tools, t => t.Area is null);
    }

    [Fact]
    public async Task Closing_an_area_takes_its_tools_out_of_the_list_and_refuses_a_call()
    {
        await _server.Posted("/api/mcp", new { enabled = true, areas = new Dictionary<string, bool> { ["money"] = false } });

        var names = await ToolNames();
        Assert.DoesNotContain("my_ledger", names);
        Assert.DoesNotContain("my_spending", names);
        Assert.Contains("garage", names);
        Assert.Contains("find_places", names);

        var answer = await Result(Call("my_ledger"));
        Assert.True(IsError(answer));
        Assert.Contains("Money", Text(answer));
    }

    /// <summary>Other players' handles are not the pilot's to give: a yes to access does not include them.</summary>
    [Fact]
    public async Task The_crew_list_is_offered_only_once_its_own_switch_is_on()
    {
        await _server.Posted("/api/mcp", new { enabled = true });
        Assert.DoesNotContain("my_crew", await ToolNames());

        await _server.Posted("/api/mcp", new { areas = new Dictionary<string, bool> { ["crew"] = true } });
        Assert.Contains("my_crew", await ToolNames());
        var answer = await Result(Call("my_crew"));
        Assert.False(IsError(answer), Text(answer));
        Assert.Contains("floor", Text(answer));
    }

    [Fact]
    public async Task Closing_plans_closes_the_writing_tools_even_with_changes_allowed()
    {
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true, areas = new Dictionary<string, bool> { ["plans"] = false } });

        var names = await ToolNames();
        Assert.DoesNotContain("my_plans", names);
        Assert.DoesNotContain("create_checklist", names);

        var answer = await Result(Call("create_checklist", """{"title":"Through a closed area","tasks":["x"]}"""));
        Assert.True(IsError(answer));
    }

    /// <summary>Search crosses every source; what the pilot wrote goes out only with plans open.</summary>
    [Fact]
    public async Task Search_leaves_out_what_the_pilot_wrote_when_plans_are_closed()
    {
        var id = (await _server.Posted("/api/checklists", new { title = "Qzvx departure" })).GetProperty("id").GetString();
        try
        {
            await _server.Posted("/api/mcp", new { enabled = true });
            Assert.Contains("Qzvx departure", Text(await Result(Call("search", """{"query":"Qzvx"}"""))));

            await _server.Posted("/api/mcp", new { areas = new Dictionary<string, bool> { ["plans"] = false } });
            var text = Text(await Result(Call("search", """{"query":"Qzvx"}""")));
            Assert.DoesNotContain("Qzvx departure", text);
            Assert.DoesNotContain("things you wrote", text);
        }
        finally
        {
            await _server.Delete($"/api/checklists/{id}");
        }
    }

    [Fact]
    public async Task Settings_are_told_each_area_with_whether_it_is_open()
    {
        await _server.Posted("/api/mcp", new { enabled = true });

        var areas = (await _server.Get("/api/mcp")).GetProperty("areas").EnumerateArray().ToList();
        Assert.Equal(McpArea.All.Select(a => a.Key), areas.Select(a => a.GetProperty("key").GetString()));
        Assert.False(areas.Single(a => a.GetProperty("key").GetString() == "crew").GetProperty("open").GetBoolean());
        Assert.True(areas.Single(a => a.GetProperty("key").GetString() == "money").GetProperty("open").GetBoolean());
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

    public async Task InitializeAsync() =>
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = true, areas = McpEndpointTests.DefaultAreas });

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
    [InlineData("garage_builds")]
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

    /// <summary>A string field, or null when the server left it out, as it does every null.</summary>
    private static string? Field(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private async Task<JsonElement> Job(string? id) =>
        (await _server.Get("/api/jobs")).EnumerateArray().Single(j => j.GetProperty("id").GetString() == id);

    /// <summary>
    /// A run split between places (1.2.65), which a destination - one place for
    /// the whole list - could not say. The empty install has no price feed, so
    /// each store is kept as asked and the answer says the feed lists nothing there.
    /// </summary>
    [Fact]
    public async Task A_new_list_keeps_a_store_for_each_line()
    {
        var (error, text) = await Call("create_shopping_list", new
        {
            title = "MK5 kit",
            items = new object[]
            {
                new { name = "Arrowhead", needed = 1, store = "Orison" },
                new { name = "Concord", needed = 2, store = "HUR-L5" },
                new { name = "MedGel", needed = 4 },
            },
        });
        Assert.False(error, text);
        var made = JsonDocument.Parse(text).RootElement;
        var stores = made.GetProperty("stores").EnumerateArray().ToList();
        Assert.Equal(["Arrowhead", "Concord"], stores.Select(s => s.GetProperty("line").GetString()));
        Assert.All(stores, s => Assert.False(s.GetProperty("soldThere").GetBoolean()));

        var items = (await Job(made.GetProperty("id").GetString())).GetProperty("items").EnumerateArray().ToList();
        Assert.Equal("Orison", items[0].GetProperty("store").GetString());
        Assert.Equal("HUR-L5", items[1].GetProperty("store").GetString());
        Assert.Null(Field(items[2], "store"));

        // The chosen store is where the card says to buy it, with no price the feed does not have.
        Assert.Equal("HUR-L5", items[1].GetProperty("buyAt").GetString());
        Assert.False(items[1].TryGetProperty("buyPrice", out var price) && price.ValueKind == JsonValueKind.Number);

        await _server.Delete($"/api/jobs/{made.GetProperty("id").GetString()}");
    }

    [Fact]
    public async Task Choosing_a_store_sets_one_line_twice_alike_and_clears_it()
    {
        var id = (await _server.Posted("/api/jobs", new
        {
            title = "MK5 kit",
            kind = "list",
            items = new[] { new { name = "Arrowhead", needed = 1.0 }, new { name = "Concord", needed = 2.0 } },
        })).GetProperty("id").GetString();

        var (error, text) = await Call("choose_store", new { list_id = id, line = "concord", store = "HUR-L5" });
        Assert.False(error, text);
        await Call("choose_store", new { list_id = id, line = "Concord", store = "HUR-L5" });

        var items = (await Job(id)).GetProperty("items").EnumerateArray().ToList();
        Assert.Null(Field(items[0], "store"));
        Assert.Equal("HUR-L5", items[1].GetProperty("store").GetString());

        await Call("choose_store", new { list_id = id, line = "Concord" });
        Assert.Null(Field((await Job(id)).GetProperty("items")[1], "store"));

        var (missing, why) = await Call("choose_store", new { list_id = id, line = "Gatling", store = "HUR-L5" });
        Assert.True(missing);
        Assert.Contains("no line 'Gatling'", why);

        await _server.Delete($"/api/jobs/{id}");
    }

    /// <summary>
    /// The test install has no ship reference, which is also what a build
    /// saved for a ship since dropped from the game looks like: still listed,
    /// with no changes it can say.
    /// </summary>
    [Fact]
    public async Task A_saved_build_is_listed_even_when_its_ship_is_not_known()
    {
        var saved = await _server.Posted("/api/garage/builds", new
        {
            shipClass = "RSI_Hermes",
            name = "Hermes shields",
            note = "For cargo runs",
            swaps = new Dictionary<string, string?> { ["loadout.23"] = "SHLD_ASAS_S02_Shroud_SCItem" },
        });
        var id = saved.GetProperty("id").GetString();

        var (error, text) = await Call("garage_builds");
        Assert.False(error, text);
        var build = JsonDocument.Parse(text).RootElement.GetProperty("builds").EnumerateArray()
            .Single(b => b.GetProperty("id").GetString() == id);
        Assert.Equal("Hermes shields", build.GetProperty("name").GetString());
        Assert.Equal("For cargo runs", build.GetProperty("note").GetString());
        Assert.Equal("RSI_Hermes", build.GetProperty("ship").GetString());

        await _server.Delete($"/api/garage/builds/{id}");
    }

    [Theory]
    [InlineData("""{"ship":"RSI Hermes"}""", "needs a name")]
    [InlineData("""{"build_id":"nosuchid"}""", "No saved build with id 'nosuchid'")]
    [InlineData("""{"ship":"Nothing Like It","name":"x"}""", "No ship called 'Nothing Like It'")]
    public async Task Saving_a_build_says_what_is_missing(string args, string said)
    {
        var (error, text) = await Call("save_garage_build", JsonDocument.Parse(args).RootElement);

        Assert.True(error);
        Assert.Contains(said, text);
        Assert.Empty((await _server.Get("/api/garage/builds")).EnumerateArray());
    }

    [Fact]
    public async Task A_goal_is_set_refused_without_a_price_and_cleared()
    {
        var (error, text) = await Call("set_goal", new { name = "Drake Caterpillar", target = 4500000 });
        Assert.False(error, text);
        var goal = (await _server.Get("/api/earnings")).GetProperty("goal");
        Assert.Equal("Drake Caterpillar", goal.GetProperty("name").GetString());
        Assert.Equal(4500000m, goal.GetProperty("target").GetDecimal());

        var (refused, why) = await Call("set_goal", new { name = "Drake Caterpillar" });
        Assert.True(refused);
        Assert.Contains("price", why);
        Assert.Equal("Drake Caterpillar", (await _server.Get("/api/earnings")).GetProperty("goal").GetProperty("name").GetString());

        await Call("set_goal");
        var cleared = await _server.Get("/api/earnings");
        Assert.False(cleared.TryGetProperty("goal", out var left) && left.ValueKind == JsonValueKind.Object);
    }

    /// <summary>The list made with no destination could not be given one afterwards.</summary>
    [Fact]
    public async Task Choosing_a_store_with_no_line_points_the_whole_list()
    {
        var id = (await _server.Posted("/api/jobs", new
        {
            title = "MK5 kit",
            kind = "list",
            items = new[] { new { name = "Arrowhead", needed = 1.0 } },
        })).GetProperty("id").GetString();

        var (error, text) = await Call("choose_store", new { list_id = id, store = "Orison" });
        Assert.False(error, text);
        Assert.Equal("Orison", (await Job(id)).GetProperty("destination").GetString());

        await Call("choose_store", new { list_id = id });
        Assert.Null(Field(await Job(id), "destination"));

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

/// <summary>
/// The ready-made prompts (1.2.52): offered only where the areas they need are
/// open, and never naming a tool the assistant was not given.
/// </summary>
[Collection("server")]
public class McpPromptTests : IClassFixture<ServerUnderTest>, IAsyncLifetime
{
    private readonly ServerUnderTest _server;

    public McpPromptTests(ServerUnderTest server) => _server = server;

    public async Task InitializeAsync() =>
        await _server.Posted("/api/mcp", new { enabled = true, allowChanges = false, areas = McpEndpointTests.DefaultAreas });

    public async Task DisposeAsync() => await _server.Posted("/api/mcp", new { enabled = false, areas = McpEndpointTests.DefaultAreas });

    private async Task<System.Text.Json.JsonElement> Rpc(string method, object? parameters = null) =>
        await _server.Posted("/mcp", new { jsonrpc = "2.0", id = 4, method, @params = parameters ?? new { } });

    private async Task<List<string>> PromptNames() =>
        [.. (await Rpc("prompts/list")).GetProperty("result").GetProperty("prompts").EnumerateArray()
            .Select(p => p.GetProperty("name").GetString()!)];

    private async Task<string> Message(string name, object? arguments = null) =>
        (await Rpc("prompts/get", new { name, arguments = arguments ?? new { } }))
            .GetProperty("result").GetProperty("messages")[0].GetProperty("content").GetProperty("text").GetString()!;

    [Fact]
    public async Task Initialize_says_there_are_prompts()
    {
        var result = (await Rpc("initialize", new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "t", version = "1" } }))
            .GetProperty("result");

        Assert.True(result.GetProperty("capabilities").TryGetProperty("prompts", out _));
    }

    [Fact]
    public async Task All_three_are_offered_with_the_default_areas()
    {
        Assert.Equal(["plan_trade_run", "fit_my_ship", "debrief_last_session"], await PromptNames());
    }

    [Fact]
    public async Task A_prompt_whose_area_is_closed_is_not_offered_and_says_why_if_asked()
    {
        await _server.Posted("/api/mcp", new { areas = new Dictionary<string, bool> { ["ships"] = false } });

        Assert.DoesNotContain("fit_my_ship", await PromptNames());
        var error = (await Rpc("prompts/get", new { name = "fit_my_ship" })).GetProperty("error");
        Assert.Equal(-32602, error.GetProperty("code").GetInt32());
        Assert.Contains("Ships, gear and stashes", error.GetProperty("message").GetString());
    }

    [Fact]
    public async Task The_trade_run_takes_the_ship_and_budget_and_offers_to_save_only_with_changes_allowed()
    {
        var readOnly = await Message("plan_trade_run", new { ship = "Drake Caterpillar", budget = "250000" });
        Assert.Contains("Drake Caterpillar", readOnly);
        Assert.Contains("250000 aUEC", readOnly);
        Assert.DoesNotContain("create_flight_plan", readOnly);
        Assert.Contains("Changes are not allowed", readOnly);

        await _server.Posted("/api/mcp", new { allowChanges = true });
        Assert.Contains("create_flight_plan", await Message("plan_trade_run"));
    }

    [Fact]
    public async Task The_debrief_leaves_out_money_when_money_is_closed()
    {
        Assert.Contains("my_ledger", await Message("debrief_last_session"));

        await _server.Posted("/api/mcp", new { areas = new Dictionary<string, bool> { ["money"] = false } });
        var text = await Message("debrief_last_session");
        Assert.DoesNotContain("my_ledger", text);
        Assert.Contains("recent_sessions", text);
    }

    [Fact]
    public async Task Getting_a_prompt_is_in_the_call_log()
    {
        await Message("debrief_last_session");

        Assert.Equal("debrief_last_session", (await _server.Get("/api/mcp")).GetProperty("calls")[0].GetProperty("tool").GetString());
    }

    /// <summary>
    /// Every way the switches can stand, every prompt, with and without its
    /// arguments: a tool named in the message is one these switches offer.
    /// A prompt that sends the model to a tool it was not given reads as a
    /// fault, and the model tries the call regardless.
    /// </summary>
    [Fact]
    public void No_prompt_names_a_tool_the_switches_do_not_offer()
    {
        var keys = McpArea.All.Select(a => a.Key).ToArray();
        var names = Quantumwake.Server.McpTools.All.Select(t => t.Name).ToList();
        var checkedCount = 0;

        for (var mask = 0; mask < 1 << keys.Length; mask++)
            foreach (var changes in new[] { false, true })
            {
                var settings = new McpSettings(true, changes, null, [.. keys.Where((_, i) => (mask & (1 << i)) != 0)]);
                var open = Quantumwake.Server.McpTools.OpenUnder(settings).Select(t => t.Name).ToHashSet();

                foreach (var prompt in Quantumwake.Server.McpPrompts.OpenUnder(settings))
                    foreach (var args in new[]
                    {
                        new Dictionary<string, string>(),
                        prompt.Arguments.ToDictionary(a => a.Name, a => "Avenger Titan"),
                    })
                    {
                        var text = prompt.Build(args, settings);
                        foreach (var name in names.Where(n => System.Text.RegularExpressions.Regex.IsMatch(text, $@"{n}")))
                            Assert.True(open.Contains(name), $"{prompt.Name} names {name}, which is not offered under mask {mask}, changes {changes}");
                        checkedCount++;
                    }
            }

        Assert.True(checkedCount > 300);
    }
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

    [Fact]
    public void A_new_install_opens_every_area_but_the_crew_list()
    {
        var s = new McpSettingsStore(_root).Current;

        Assert.Null(s.Areas);
        Assert.Equal(["sessions", "money", "ships", "work", "plans"], s.OpenAreas);
        Assert.False(s.Allows(McpArea.Crew));
    }

    /// <summary>
    /// The crew list is a grant like changes: it does not outlive access.
    /// A narrowing is kept, because keeping a no is never the risk.
    /// </summary>
    [Fact]
    public void Turning_access_off_closes_the_crew_list_but_keeps_what_was_narrowed()
    {
        var store = new McpSettingsStore(_root);
        store.Set(enabled: true, areas: new Dictionary<string, bool> { ["crew"] = true, ["money"] = false });
        Assert.True(store.Current.Allows(McpArea.Crew));

        store.Set(enabled: false);
        store.Set(enabled: true);

        var s = new McpSettingsStore(_root).Current;
        Assert.False(s.Allows(McpArea.Crew));
        Assert.False(s.Allows(McpArea.Money));
        Assert.True(s.Allows(McpArea.Ships));
    }

    /// <summary>Stored as the list that is open, so an area a later version adds starts closed for anyone who chose.</summary>
    [Fact]
    public void An_area_the_pilot_never_saw_is_closed_once_they_have_chosen()
    {
        var store = new McpSettingsStore(_root);
        store.Set(enabled: true, areas: new Dictionary<string, bool> { ["money"] = false });

        Assert.False(store.Current.Allows("from-a-later-version"));
        Assert.DoesNotContain("money", store.Current.Areas!);
    }

    [Fact]
    public void Writes_need_plans_open()
    {
        var store = new McpSettingsStore(_root);
        store.Set(enabled: true, allowChanges: true, areas: new Dictionary<string, bool> { ["plans"] = false });

        Assert.True(store.Current.AllowChanges);
        Assert.False(store.Current.Writes);
    }
}
