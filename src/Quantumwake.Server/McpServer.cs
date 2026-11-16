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
/// Streamable HTTP at <c>/mcp</c> on the server the dashboard already runs,
/// answering each request with one JSON object and opening no event stream:
/// every tool here answers at once, so there is nothing to stream, and a GET
/// is refused with 405 as the transport allows. No session id either - the
/// transport makes it optional, and nothing here is kept between calls but
/// the call log.
/// </para>
/// <para>
/// Written by hand rather than on an SDK. The protocol surface used is four
/// methods - initialize, ping, tools/list, tools/call - and the app's rule
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
        app.MapGet("/api/mcp", (McpSettingsStore store, McpCallLog log) => Results.Ok(Describe(store, log)));
        app.MapPost("/api/mcp", (McpSwitch body, McpSettingsStore store, McpCallLog log) =>
        {
            store.Set(body.Enabled, body.AllowChanges);
            return Results.Ok(Describe(store, log));
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

    private static object Describe(McpSettingsStore store, McpCallLog log)
    {
        var s = store.Current;
        return new
        {
            s.Enabled,
            s.AllowChanges,
            s.ChangedAt,
            path = "/mcp",
            client = log.Client,
            tools = McpTools.All.Select(t => new { t.Name, t.Title, t.Writes }),
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
            var api = new McpApi(BaseOf(ctx));
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
                    ["capabilities"] = new JsonObject { ["tools"] = new JsonObject { ["listChanged"] = false } },
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
                var writes = store.Current.Writes;
                var tools = new JsonArray();
                foreach (var tool in McpTools.All.Where(t => writes || !t.Writes))
                    tools.Add(tool.Describe());
                return Result(id, new JsonObject { ["tools"] = tools });
            }

            case "tools/call":
                return await Call(id, parameters, store, log, api, token);

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
        if (tool.Writes && !store.Current.Writes)
        {
            log.Add(new McpCall(DateTimeOffset.UtcNow, name, "refused: changes are off", true, log.Client));
            return ToolResult(id, "The pilot has not allowed changes. Quantum Wake only lets an assistant make plans, "
                + "lists or checklists when 'Let it make plans and lists' is on in Settings → AI assistants.", isError: true);
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
public sealed record McpSwitch(bool? Enabled = null, bool? AllowChanges = null);

/// <summary>A tool's answer the assistant should read as a refusal or a miss, in words it can pass on.</summary>
public sealed class McpToolException(string message) : Exception(message);

/// <summary>The app's own API, reached over loopback by the tools.</summary>
public sealed class McpApi(string root)
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(60) };

    public async Task<JsonNode> Get(string path, CancellationToken token)
    {
        using var response = await Client.GetAsync(root + path, token);
        if (!response.IsSuccessStatusCode)
            throw new McpToolException($"The app answered {(int)response.StatusCode} to that.");
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(token)) ?? new JsonObject();
    }

    public async Task<JsonNode> Post(string path, object body, CancellationToken token)
    {
        using var response = await Client.PostAsJsonAsync(root + path, body, token);
        var text = await response.Content.ReadAsStringAsync(token);
        if (!response.IsSuccessStatusCode)
            throw new McpToolException($"The app refused that ({(int)response.StatusCode}): {McpEndpoints.Clip(text, 200)}");
        return text.Length == 0 ? new JsonObject() : JsonNode.Parse(text) ?? new JsonObject();
    }
}
