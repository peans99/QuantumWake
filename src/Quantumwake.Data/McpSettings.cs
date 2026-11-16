using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// Whether an AI assistant - Claude, Codex, anything that speaks the Model
/// Context Protocol - may use this app, and how far.
/// </summary>
/// <param name="Enabled">
/// Whether <c>/mcp</c> answers at all. Off until the pilot turns it on: an
/// assistant reading the pilot's logs, fleet and plans is a different thing to
/// agree to from the pilot reading them, and nothing here guesses that yes.
/// </param>
/// <param name="AllowChanges">
/// Whether the assistant may also write: make a flight plan, a shopping list,
/// a checklist. A second switch under the first, off on its own, because
/// "look at my things" and "change my things" are two agreements - and a
/// change made by an assistant is a change in the pilot's own data, which no
/// rescan puts back.
/// </param>
/// <param name="ChangedAt">When either switch last moved.</param>
public sealed record McpSettings(bool Enabled = false, bool AllowChanges = false, DateTimeOffset? ChangedAt = null)
{
    /// <summary>Whether writing tools are offered and run now: both switches on.</summary>
    public bool Writes => Enabled && AllowChanges;
}

/// <summary>Remembers the assistant switches, in their own file.</summary>
/// <remarks>
/// Apart from every other store on purpose, the rule every agreement here
/// follows: turning on screen reading, voice commands or a price feed never
/// turns this on, and this never turns on any of them.
/// </remarks>
public sealed class McpSettingsStore
{
    private readonly string _path;
    private readonly Lock _gate = new();
    private McpSettings _current = new();

    public McpSettingsStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? Core.AppPaths.Root, "mcp.json");
        Load();
    }

    public McpSettings Current
    {
        get { lock (_gate) return _current; }
    }

    /// <summary>
    /// Moves either switch; one left null stays as it was. Turning access off
    /// takes the changes switch with it, so turning access back on later never
    /// silently restores write access that was granted for another occasion.
    /// </summary>
    public McpSettings Set(bool? enabled = null, bool? allowChanges = null)
    {
        lock (_gate)
        {
            var on = enabled ?? _current.Enabled;
            _current = new McpSettings(
                on,
                on && (allowChanges ?? _current.AllowChanges),
                DateTimeOffset.UtcNow);
            Save();
            return _current;
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(_current));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // Unsaved means off again next start, which is the safe way round.
        }
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path) && JsonSerializer.Deserialize<McpSettings>(File.ReadAllText(_path)) is { } read)
                _current = read with { AllowChanges = read.Enabled && read.AllowChanges };
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            _current = new McpSettings();
        }
    }
}

/// <summary>One thing an assistant asked of the app, for the Settings page.</summary>
/// <param name="Tool">The tool's name, or the protocol method when it was not a tool call.</param>
/// <param name="Outcome">"ok", "refused" or the error, in a few words.</param>
/// <param name="Client">What the assistant called itself at initialize, when it said.</param>
public sealed record McpCall(DateTimeOffset At, string Tool, string Outcome, bool Wrote, string? Client = null);

/// <summary>
/// The last assistant calls, in memory only, so the pilot can see what was
/// asked of their data. Not written down: it is a window, not a record, the
/// same way the voice page keeps what it heard.
/// </summary>
public sealed class McpCallLog
{
    private const int Keep = 40;
    private readonly Lock _gate = new();
    private readonly LinkedList<McpCall> _calls = new();

    /// <summary>What the last assistant to connect called itself - "claude-code", "codex-mcp-client" - for the page.</summary>
    public string? Client { get; set; }

    public void Add(McpCall call)
    {
        lock (_gate)
        {
            _calls.AddFirst(call);
            while (_calls.Count > Keep) _calls.RemoveLast();
        }
    }

    public IReadOnlyList<McpCall> Recent()
    {
        lock (_gate) return [.. _calls];
    }
}
