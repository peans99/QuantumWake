using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// The app as an MCP server (1.2.48): Claude, Codex or any client of the Model
/// Context Protocol can ask it things, and - only with a second yes - make
/// plans in it.
/// </summary>
/// <remarks>
/// <para>
/// Streamable HTTP at <c>/mcp</c> on the server the dashboard already runs
/// (and, since 1.2.53, stdio through <see cref="McpStdioBridge"/>, which
/// relays here),
/// answering each request with one JSON object and opening no event stream:
/// every tool here answers at once, so there is nothing to stream, and a GET
/// is refused with 405 as the transport allows. No session id either - the
/// transport makes it optional, and nothing here is kept between calls but
/// the call log.
/// </para>
/// <para>
/// Written by hand rather than on an SDK. The protocol surface used is six
/// methods - initialize, ping, tools/list, tools/call, and since 1.2.52
/// prompts/list and prompts/get - and the app's rule
/// for a dependency is that it earns its place; the SDK's is a moving target
/// still in preview when this was written, and this is a page of code.
/// </para>
/// <para>
/// Every tool is a call onto the app's own API over loopback, then trimmed.
/// That keeps one answer to every question: the assistant reads exactly what
/// the dashboard draws, never a second projection that drifts from it. And the
/// guards in front of that API stand in front of this too. A POST from another
/// machine is refused by <see cref="LanGuard"/>, so a dashboard opened to a
/// tablet does not open the assistant door with it; a POST a web page makes in
/// the pilot's browser declares a foreign Origin and is refused by
/// <see cref="OriginGuard"/>. What remains is software on this PC, which
/// already has the files.
/// </para>
/// </remarks>
public static class McpEndpoints
{
    /// <summary>The newest protocol revision this answers to, offered when a client asks for one it does not know.</summary>
    public const string Latest = "2025-11-25";

    /// <summary>Revisions whose shape these four methods have not changed; a client asking for one gets it back.</summary>
    public static readonly string[] Supported = ["2025-11-25", "2025-06-18", "2025-03-26"];

    private const string Instructions =
        "Quantum Wake reads one Star Citizen pilot's own game logs on their PC, plus the installed game's files, "
        + "and - only where they turned them on - UEX community prices and a community dataset. Answers describe "
        + "this pilot and this install, not the game in general. Prices are UEX's last reports, not live. "
        + "Before planning, use find_places for the exact place names the app knows. Tools that change "
        + "things exist only when the pilot allowed changes in Settings; never say a plan was made unless "
        + "the tool returned it.";

    /// <summary>
    /// Web casing, and no escaping beyond what JSON needs: the tool's answer is
    /// read by a model, and "Hauling run · 6 contracts" is noise where
    /// "Hauling run · 6 contracts" is the name the pilot sees. Nothing here is
    /// written into HTML, which is the only reason the default escapes them.
    /// </summary>
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Map(WebApplication app)
    {
        // The switches and the call log, for Settings. Reads are allowed from
        // the LAN like every read; the switches are POSTs, which are not.
        app.MapGet("/api/mcp", (HttpContext ctx, McpSettingsStore store, McpCallLog log) => Results.Ok(Describe(ctx, store, log)));
        app.MapPost("/api/mcp", (HttpContext ctx, McpSwitch body, McpSettingsStore store, McpCallLog log) =>
        {
            store.Set(body.Enabled, body.AllowChanges, body.Areas);
            return Results.Ok(Describe(ctx, store, log));
        });

        app.MapPost("/mcp", Handle);

        // No event stream is offered: every answer here is immediate.
        app.MapGet("/mcp", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });
        app.MapDelete("/mcp", (HttpContext ctx) =>
        {
            ctx.Response.Headers.Allow = "POST";
            return Results.StatusCode(StatusCodes.Status405MethodNotAllowed);
        });
    }

    private static object Describe(HttpContext ctx, McpSettingsStore store, McpCallLog log)
    {
        var s = store.Current;
        var (command, args) = McpStdioBridge.Launch(ctx.Connection.LocalPort is > 0 and var p ? p : McpStdioBridge.DefaultPort);
        return new
        {
            s.Enabled,
            s.AllowChanges,
            s.ChangedAt,
            path = "/mcp",
            // What a stdio-only client such as Claude Desktop starts.
            stdio = new { command, args, packaged = Core.StoreInstall.Is },
            client = log.Client,
            areas = McpArea.All.Select(a => new
            {
                a.Key,
                a.Title,
                a.Description,
                a.DefaultOn,
                open = s.Allows(a.Key),
                tools = McpTools.All.Where(t => t.Area == a.Key && !t.Writes).Select(t => t.Name),
            }),
            tools = McpTools.All.Select(t => new { t.Name, t.Title, t.Writes, t.Area, open = t.OpenUnder(s) }),
            prompts = McpPrompts.All.Select(p => new { p.Name, p.Title, open = p.OpenUnder(s) }),
            calls = log.Recent(),
        };
    }

    private static async Task Handle(HttpContext ctx, McpSettingsStore store, McpCallLog log)
    {
        if (!store.Current.Enabled)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            await ctx.Response.WriteAsJsonAsync(new
            {
                error = "AI assistants are off in Quantum Wake. Turn them on in Settings → AI assistants.",
            });
            return;
        }

        JsonDocument document;
        try
        {
            document = await JsonDocument.ParseAsync(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
        }
        catch (JsonException)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ctx.Response.WriteAsJsonAsync(Error(null, -32700, "The body is not JSON."));
            return;
        }

        using (document)
        {
            // One reading of the switches per request, so a batch is answered
            // under the same agreement from its first message to its last.
            var api = new McpApi(BaseOf(ctx), store.Current);
            var root = document.RootElement;

            // A batch, from a client on the 2025-03-26 revision: answered as
            // one, each request in turn. Later revisions send one at a time.
            if (root.ValueKind == JsonValueKind.Array)
            {
                var answers = new JsonArray();
                foreach (var message in root.EnumerateArray())
                    if (await Answer(message, store, log, api, ctx.RequestAborted) is { } answer)
                        answers.Add(answer);
                await Reply(ctx, answers.Count == 0 ? null : answers);
                return;
            }

            await Reply(ctx, await Answer(root, store, log, api, ctx.RequestAborted));
        }
    }

    /// <summary>202 with no body for a notification or a response, as the transport says; else the answer.</summary>
    private static async Task Reply(HttpContext ctx, JsonNode? answer)
    {
        if (answer is null)
        {
            ctx.Response.StatusCode = StatusCodes.Status202Accepted;
            return;
        }

        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(answer.ToJsonString(Web), Encoding.UTF8, ctx.RequestAborted);
    }

    /// <summary>
    /// The address the app's own API answers on: the address and port this
    /// request came in on, when that is loopback - which every allowed MCP
    /// call is - else IPv4 loopback on the same port.
    /// </summary>
    private static string BaseOf(HttpContext ctx)
    {
        var local = ctx.Connection.LocalIpAddress;
        var host = local is not null && IPAddress.IsLoopback(local)
            ? local.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? "[::1]" : "127.0.0.1"
            : "127.0.0.1";
        return $"http://{host}:{ctx.Connection.LocalPort}";
    }

    private static async Task<JsonNode?> Answer(JsonElement message, McpSettingsStore store, McpCallLog log, McpApi api, CancellationToken token)
    {
        if (message.ValueKind != JsonValueKind.Object)
            return Error(null, -32600, "A message has to be a JSON object.");

        // A notification has no id, and a response has no method: neither is answered.
        var hasId = message.TryGetProperty("id", out var idElement);
        if (!message.TryGetProperty("method", out var methodElement) || methodElement.ValueKind != JsonValueKind.String)
            return null;
        if (!hasId) return null;

        var id = JsonNode.Parse(idElement.GetRawText());
        var method = methodElement.GetString()!;
        message.TryGetProperty("params", out var parameters);

        switch (method)
        {
            case "initialize":
            {
                var asked = parameters.ValueKind == JsonValueKind.Object
                    && parameters.TryGetProperty("protocolVersion", out var v) && v.ValueKind == JsonValueKind.String
                    ? v.GetString() : null;
                if (parameters.ValueKind == JsonValueKind.Object
                    && parameters.TryGetProperty("clientInfo", out var info) && info.ValueKind == JsonValueKind.Object
                    && info.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                    log.Client = Clip(name.GetString(), 60);

                log.Add(new McpCall(DateTimeOffset.UtcNow, "connect", "ok", false, log.Client));
                return Result(id, new JsonObject
                {
                    ["protocolVersion"] = asked is not null && Supported.Contains(asked) ? asked : Latest,
                    ["capabilities"] = new JsonObject
                    {
                        ["tools"] = new JsonObject { ["listChanged"] = false },
                        ["prompts"] = new JsonObject { ["listChanged"] = false },
                    },
                    ["serverInfo"] = new JsonObject
                    {
                        ["name"] = "quantum-wake",
                        ["title"] = "Quantum Wake",
                        ["version"] = typeof(McpEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
                    },
                    ["instructions"] = Instructions,
                });
            }

            case "ping":
                return Result(id, new JsonObject());

            case "tools/list":
            {
                var tools = new JsonArray();
                foreach (var tool in McpTools.OpenUnder(api.Settings))
                    tools.Add(tool.Describe());
                return Result(id, new JsonObject { ["tools"] = tools });
            }

            case "tools/call":
                return await Call(id, parameters, store, log, api, token);

            case "prompts/list":
            {
                var prompts = new JsonArray();
                foreach (var prompt in McpPrompts.OpenUnder(api.Settings))
                    prompts.Add(prompt.Describe());
                return Result(id, new JsonObject { ["prompts"] = prompts });
            }

            case "prompts/get":
                return Prompt(id, parameters, log, api);

            default:
                return Error(id, -32601, $"Quantum Wake does not answer '{Clip(method, 60)}'.");
        }
    }

    private static async Task<JsonNode> Call(JsonNode? id, JsonElement parameters, McpSettingsStore store, McpCallLog log, McpApi api, CancellationToken token)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return Error(id, -32602, "tools/call needs a tool name.");

        var name = nameElement.GetString()!;
        var tool = McpTools.All.FirstOrDefault(t => t.Name == name);
        if (tool is null)
            return Error(id, -32602, $"There is no tool called '{Clip(name, 60)}'.");

        var args = parameters.TryGetProperty("arguments", out var a) && a.ValueKind == JsonValueKind.Object ? a : default;

        // Checked at the call, not only at the listing: a client that cached
        // the list from before the switch went off still cannot write.
        if (tool.Area is { } area && !api.Settings.Allows(area))
        {
            var title = McpArea.All.FirstOrDefault(a => a.Key == area)?.Title ?? area;
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, $"refused: {title.ToLowerInvariant()} is off", tool.Writes, log.Client));
            return ToolResult(id, $"The pilot keeps '{title}' from assistants in Quantum Wake (Settings → AI assistants), "
                + "so this tool is not available. Say so rather than guessing the answer.", isError: true);
        }

        if (tool.Writes && !api.Settings.Writes)
        {
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, "refused: changes are off", true, log.Client));
            return ToolResult(id, "The pilot has not allowed changes. Quantum Wake only lets an assistant make, pin, "
                + "tick or track plans, lists and checklists when 'Let it make plans and lists' is on in Settings → AI assistants.", isError: true);
        }

        try
        {
            var answer = await tool.Run(args, api, token);
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, "ok", tool.Writes, log.Client));
            return ToolResult(id, answer.ToJsonString(Web), isError: false);
        }
        catch (McpToolException e)
        {
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, Clip(e.Message, 80) ?? "failed", tool.Writes, log.Client));
            return ToolResult(id, e.Message, isError: true);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, "failed", tool.Writes, log.Client));
            return ToolResult(id, $"Quantum Wake could not answer that just now: {e.Message}", isError: true);
        }
    }

    /// <summary>
    /// A prompt's message, written against the switches as they are now. A
    /// closed or unknown one is a protocol error, as the specification has it,
    /// worded so a client that shows the message shows the reason.
    /// </summary>
    private static JsonNode Prompt(JsonNode? id, JsonElement parameters, McpCallLog log, McpApi api)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || !parameters.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            return Error(id, -32602, "prompts/get needs a prompt name.");

        var name = nameElement.GetString()!;
        var prompt = McpPrompts.All.FirstOrDefault(p => p.Name == name);
        if (prompt is null)
            return Error(id, -32602, $"There is no prompt called '{Clip(name, 60)}'.");
        if (!prompt.OpenUnder(api.Settings))
        {
            var closed = McpArea.All.Where(a => prompt.Needs.Contains(a.Key) && !api.Settings.Allows(a.Key)).Select(a => a.Title);
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, "refused: area off", false, log.Client));
            return Error(id, -32602, $"'{prompt.Title}' needs {string.Join(" and ", closed)}, which the pilot keeps from assistants.");
        }

        // Arguments are text by the specification; anything else, or blank, is left out.
        var args = new Dictionary<string, string>(StringComparer.Ordinal);
        if (parameters.TryGetProperty("arguments", out var given) && given.ValueKind == JsonValueKind.Object)
            foreach (var arg in prompt.Arguments)
                if (given.TryGetProperty(arg.Name, out var v) && v.ValueKind == JsonValueKind.String
                    && v.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
                    args[arg.Name] = Clip(s.Trim(), 120)!;

        if (prompt.Arguments.FirstOrDefault(x => x.Required && !args.ContainsKey(x.Name)) is { } missing)
            return Error(id, -32602, $"'{prompt.Title}' needs '{missing.Name}'.");

        log.Add(new McpCall(DateTimeOffset.UtcNow, name, "ok", false, log.Client));
        return Result(id, new JsonObject
        {
            ["description"] = prompt.Description,
            ["messages"] = new JsonArray(new JsonObject
            {
                ["role"] = "user",
                ["content"] = new JsonObject { ["type"] = "text", ["text"] = prompt.Build(args, api.Settings) },
            }),
        });
    }

    private static JsonObject ToolResult(JsonNode? id, string text, bool isError) => Result(id, new JsonObject
    {
        ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = text }),
        ["isError"] = isError,
    });

    private static JsonObject Result(JsonNode? id, JsonNode result) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["result"] = result };

    private static JsonObject Error(JsonNode? id, int code, string message) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = message } };

    internal static string? Clip(string? text, int max) =>
        text is null ? null : text.Length <= max ? text : text[..max];
}

/// <summary>Body of POST /api/mcp; a switch left out stays as it was.</summary>
public sealed record McpSwitch(bool? Enabled = null, bool? AllowChanges = null, Dictionary<string, bool>? Areas = null);

/// <summary>A tool's answer the assistant should read as a refusal or a miss, in words it can pass on.</summary>
public sealed class McpToolException(string message) : Exception(message);

/// <summary>The app's own API, reached over loopback by the tools.</summary>
public sealed class McpApi(string root, McpSettings settings)
{
    /// <summary>The switches as they stood when the request came in.</summary>
    public McpSettings Settings { get; } = settings;

    /// <summary>Whether an area of the pilot's data may go into an answer.</summary>
    public bool Allows(string area) => Settings.Allows(area);

    /// <summary>The sentence a refusal carries - the API answers {"message"} or {"trouble"} - else the body as it came.</summary>
    private static string Refusal(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            foreach (var key in new[] { "message", "trouble", "error", "detail", "title" })
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String)
                    return v.GetString()!;
        }
        catch (JsonException) { }
        return body;
    }

    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<JsonNode> Get(string path, CancellationToken token)
    {
        using var response = await Client.GetAsync(root + path, token);
        if (!response.IsSuccessStatusCode)
            throw new McpToolException($"The app answered {(int)response.StatusCode} to that.");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(token)) ?? new JsonObject();
    }

    public Task<JsonNode> Post(string path, object? body, CancellationToken token) =>
        Send(HttpMethod.Post, path, body, token);

    /// <summary>For the routes that change a record in place, such as a saved build (1.2.65).</summary>
    public Task<JsonNode> Put(string path, object? body, CancellationToken token) =>
        Send(HttpMethod.Put, path, body, token);

    private async Task<JsonNode> Send(HttpMethod method, string path, object? body, CancellationToken token)
    {
        using var request = new HttpRequestMessage(method, root + path) { Content = JsonContent.Create(body) };
        using var response = await Client.SendAsync(request, token);
        var text = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new McpToolException($"The app refused that: {McpEndpoints.Clip(Refusal(text), 300)}");
        return text.Length == 0 ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
    }
}
