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
/// <param name="ChangedAt">When a switch last moved.</param>
/// <param name="Areas">
/// The parts of the pilot's own data an assistant may read (1.2.51), by
/// <see cref="McpArea.Key"/>; null until the pilot first changes one, which
/// means <see cref="McpArea.DefaultOn"/>. Kept as the list that is on rather
/// than the list that is off, so an area added in a later version arrives off
/// for anyone who has already made choices - nothing here guesses a yes.
/// </param>
public sealed record McpSettings(
    bool Enabled = false,
    bool AllowChanges = false,
    DateTimeOffset? ChangedAt = null,
    IReadOnlyList<string>? Areas = null)
{
    /// <summary>
    /// Whether writing tools are offered and run now: both switches on, and
    /// plans readable - an assistant cannot be trusted to add to lists it is
    /// not allowed to see.
    /// </summary>
    public bool Writes => Enabled && AllowChanges && Allows(McpArea.Plans);

    /// <summary>Whether an area of the pilot's data is open to an assistant.</summary>
    public bool Allows(string area) =>
        Areas is null
            ? McpArea.All.Any(a => a.Key == area && a.DefaultOn)
            : Areas.Contains(area, StringComparer.Ordinal);

    /// <summary>The areas open now, in the order Settings lists them.</summary>
    public IReadOnlyList<string> OpenAreas => [.. McpArea.All.Where(a => Allows(a.Key)).Select(a => a.Key)];

    /// <summary>
    /// What turning access off leaves: no changes, and none of the areas that
    /// start off. Those are grants made for an occasion, the same as changes,
    /// and turning access back on later must not quietly restore them. Areas
    /// the pilot narrowed stay narrowed - keeping a "no" is never the risk.
    /// </summary>
    internal McpSettings Closed() => this with
    {
        Enabled = false,
        AllowChanges = false,
        Areas = Areas is null ? null : [.. Areas.Where(k => McpArea.All.Any(a => a.Key == k && a.DefaultOn))],
    };
}

/// <summary>One part of the pilot's own data an assistant can be kept from.</summary>
/// <param name="DefaultOn">
/// Whether a yes to access includes it. Only the crew list does not: it is
/// other players' handles, which are not the pilot's to hand to a service.
/// </param>
public sealed record McpArea(string Key, string Title, string Description, bool DefaultOn = true)
{
    public const string Sessions = "sessions";
    public const string Money = "money";
    public const string Ships = "ships";
    public const string Work = "work";
    public const string Plans = "plans";
    public const string Crew = "crew";

    /// <summary>
    /// Every area, in Settings order. The game's own catalogue - items, ships,
    /// places, missions, prices, routes - is in none of them: it is not the
    /// pilot's, and an assistant with access always reads it.
    /// </summary>
    public static readonly IReadOnlyList<McpArea> All =
    [
        new(Sessions, "Sessions and history",
            "Where you are and what you fly now, your play sessions, the logbook, deaths and med beds, and how often you have been to each place."),
        new(Money, "Money",
            "Your ledger, what you have spent at shops and what you have earned."),
        new(Ships, "Ships, gear and stashes",
            "The ships you have flown, their fits in the Garage, what you carry, and which stashes hold what."),
        new(Work, "Contracts, reputation and events",
            "Your contracts and how they ended, your standing with each organisation, event points and refinery orders."),
        new(Plans, "Plans and lists",
            "Your flight plans, shopping lists and checklists. Making or changing them also needs the switch below."),
        new(Crew, "People you have played with",
            "Other players' handles from the party channel, and the ships you shared. Their names rather than yours, so off until you turn it on.",
            DefaultOn: false),
    ];
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
    /// Moves either switch, or areas; anything left null stays as it was.
    /// Turning access off takes the changes switch and the off-by-default
    /// areas with it (<see cref="McpSettings.Closed"/>), so turning access back
    /// on later never silently restores what was granted for another occasion.
    /// </summary>
    /// <param name="areas">Areas to open (true) or close (false) by key; an unknown key is ignored.</param>
    public McpSettings Set(bool? enabled = null, bool? allowChanges = null, IReadOnlyDictionary<string, bool>? areas = null)
    {
        lock (_gate)
        {
            var next = _current;
            if (areas is { Count: > 0 })
            {
                var open = next.OpenAreas.ToHashSet(StringComparer.Ordinal);
                foreach (var (key, wanted) in areas)
                {
                    if (wanted) open.Add(key);
                    else open.Remove(key);
                }
                next = next with { Areas = [.. McpArea.All.Select(a => a.Key).Where(open.Contains)] };
            }

            next = enabled ?? next.Enabled
                ? next with { Enabled = true, AllowChanges = allowChanges ?? next.AllowChanges }
                : next.Closed();
            _current = next with { ChangedAt = DateTimeOffset.UtcNow };
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
                _current = read.Enabled ? read : read.Closed();
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
