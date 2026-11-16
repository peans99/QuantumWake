using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Quantumwake.Server;

/// <summary>
/// <c>QuantumWake.exe --mcp-stdio</c> (1.2.53): the app as a local stdio MCP
/// server, for clients that only start one - Claude Desktop is the one asked
/// for - by relaying each message to the running app's <c>/mcp</c>.
/// </summary>
/// <remarks>
/// <para>
/// A relay rather than a second server. The copy a client starts this way
/// is a fresh process with nothing scanned; answering from it would mean
/// parsing every log again for each conversation, and holding a second view
/// of the pilot's plans that the dashboard does not see. Relayed, there is one
/// app, one set of switches and one call log, and Settings shows what Claude
/// Desktop asked beside what Claude Code did.
/// </para>
/// <para>
/// It never starts the app. A chat client launches its servers when it opens,
/// and Quantum Wake appearing in the tray because Claude was opened would be
/// the app doing something the pilot did not ask for. When nothing answers,
/// each request gets an error saying to start it.
/// </para>
/// <para>
/// The stdio transport is one JSON-RPC message per line on stdin and stdout,
/// with stdout holding nothing else. Diagnostics go to stderr, which the
/// clients keep as the server's log. A GUI-subsystem exe has no console, but
/// a client that starts it with pipes hands it the pipes, and those are what
/// <see cref="Console.OpenStandardInput()"/> opens.
/// </para>
/// </remarks>
public static class McpStdioBridge
{
    public const string Flag = "--mcp-stdio";

    /// <summary>The port the app answers on unless started with <c>--Port</c>.</summary>
    public const int DefaultPort = 31337;

    public static bool Asked(IEnumerable<string> args) =>
        args.Any(a => a.Equals(Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// <c>--Port 31395</c> and <c>--Port=31395</c>, the two spellings the
    /// configuration binder accepts; anything else is the server's default.
    /// </summary>
    public static int PortFromArguments(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            var a = args[i];
            if (a.Equals("--Port", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count
                && int.TryParse(args[i + 1], out var next))
                return next;
            if (a.StartsWith("--Port=", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(a["--Port=".Length..], out var inline))
                return inline;
        }
        return DefaultPort;
    }

    /// <summary>Runs on the process's own stdin and stdout until stdin closes.</summary>
    public static async Task<int> RunAsync(string[] args)
    {
        var utf8 = new UTF8Encoding(false);
        using var input = new StreamReader(Console.OpenStandardInput(), utf8);
        await using var output = new StreamWriter(Console.OpenStandardOutput(), utf8) { NewLine = "\n", AutoFlush = true };
        await using var log = new StreamWriter(Console.OpenStandardError(), utf8) { NewLine = "\n", AutoFlush = true };
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };

        var port = PortFromArguments(args);
        return await Relay(input, output, log, client, new Uri($"http://127.0.0.1:{port}/mcp"), CancellationToken.None);
    }

    /// <summary>The relay itself, on any reader and writer, so it is tested without a process.</summary>
    public static async Task<int> Relay(TextReader input, TextWriter output, TextWriter log, HttpClient client, Uri endpoint, CancellationToken token)
    {
        await log.WriteLineAsync($"Quantum Wake MCP bridge: relaying stdio to {endpoint}");

        // One at a time and in order. JSON-RPC lets answers come back out of
        // order, but every tool here answers in well under the client's
        // timeout, and a serial relay cannot interleave two lines on stdout.
        while (await input.ReadLineAsync(token) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (await Forward(line, client, endpoint, log, token) is { } answer)
                await output.WriteLineAsync(answer);
        }
        return 0;
    }

    private static async Task<string?> Forward(string line, HttpClient client, Uri endpoint, TextWriter log, CancellationToken token)
    {
        JsonNode? message;
        try
        {
            message = JsonNode.Parse(line);
        }
        catch (JsonException)
        {
            return Error(null, -32700, "The line is not JSON.").ToJsonString();
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(line, Encoding.UTF8, "application/json"),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, token);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            await log.WriteLineAsync($"Quantum Wake did not answer at {endpoint}: {e.Message}");
            return Errors(message, -32000,
                $"Quantum Wake is not running on port {endpoint.Port}. Start it, then ask again.");
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(token);

            // A notification, or a response from the client: nothing goes back.
            if (response.StatusCode == HttpStatusCode.Accepted)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                // The app's own sentence when it has one - "AI assistants are
                // off in Quantum Wake. Turn them on in Settings" - so the client
                // shows the pilot what to do rather than a status code.
                var reason = Reason(body) ?? $"Quantum Wake answered {(int)response.StatusCode}.";
                await log.WriteLineAsync(reason);
                return Errors(message, -32000, reason);
            }

            // Compact, so an answer is exactly one line whatever the server wrote.
            try
            {
                return JsonNode.Parse(body)?.ToJsonString();
            }
            catch (JsonException)
            {
                return Errors(message, -32603, "Quantum Wake's answer was not JSON.");
            }
        }
    }

    private static string? Reason(string body)
    {
        try
        {
            return JsonNode.Parse(body) is JsonObject o && o["error"] is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>An error for each request in the message that expects an answer; null when none does.</summary>
    private static string? Errors(JsonNode? message, int code, string text)
    {
        var requests = message is JsonArray batch ? batch.OfType<JsonObject>() : message is JsonObject one ? [one] : [];
        var answers = requests
            .Where(r => r.ContainsKey("id") && r.ContainsKey("method"))
            .Select(r => (JsonNode)Error(r["id"]?.DeepClone(), code, text))
            .ToList();
        return answers.Count == 0 ? null
            : message is JsonArray ? new JsonArray([.. answers]).ToJsonString()
            : answers[0].ToJsonString();
    }

    private static JsonObject Error(JsonNode? id, int code, string text) =>
        new() { ["jsonrpc"] = "2.0", ["id"] = id, ["error"] = new JsonObject { ["code"] = code, ["message"] = text } };

    /// <summary>
    /// What a client's configuration should start, for Settings to show: this
    /// executable with the flag, or under <c>dotnet</c> the entry assembly.
    /// </summary>
    /// <remarks>
    /// A Store copy is named by its App Execution Alias under
    /// <c>%LOCALAPPDATA%\Microsoft\WindowsApps</c>, not by its own path: the
    /// package folder's name carries the version, so a path copied today stops
    /// working at the next Store update, and the alias does not.
    /// </remarks>
    public static (string Command, string[] Args) Launch(int port)
    {
        var tail = port == DefaultPort ? new[] { Flag } : [Flag, "--Port", port.ToString(System.Globalization.CultureInfo.InvariantCulture)];

        if (Core.StoreInstall.Is)
            return (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft", "WindowsApps", "QuantumWake.exe"), tail);

        var process = Environment.ProcessPath ?? "QuantumWake.exe";
        if (Path.GetFileNameWithoutExtension(process).Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && Assembly.GetEntryAssembly()?.Location is { Length: > 0 } entry)
            return (process, [entry, .. tail]);
        return (process, tail);
    }
}
