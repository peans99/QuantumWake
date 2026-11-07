using Quantumwake.Core.Controls;

namespace Quantumwake.Data;

/// <summary>
/// One thing the copilot can be told to do: the words, and the game action
/// they press.
/// </summary>
/// <param name="Id">Stable, for the settings file and the page - <c>mining</c>.</param>
/// <param name="Phrase">The words shown first, and the commonest way to say it.</param>
/// <param name="Also">Other ways to say the same thing; each is listened for.</param>
/// <param name="ActionMap">The action map the game files the action under.</param>
/// <param name="Action">The game's action id - <c>v_toggle_mining_mode</c>.</param>
/// <param name="Reply">What the copilot says once the key is pressed.</param>
/// <param name="Confirm">True when a spoken "confirm" has to follow before the key is pressed.</param>
/// <param name="HoldSeconds">
/// How long to hold the key when the action keeps working for as long as it is
/// held - a scan runs while its key is down - rather than firing once. Null for
/// an action that fires on its own rule.
/// </param>
public sealed record VoiceCommand(
    string Id,
    string Phrase,
    IReadOnlyList<string> Also,
    string ActionMap,
    string Action,
    string Reply,
    bool Confirm = false,
    double? HoldSeconds = null)
{
    /// <summary>Every way of saying it, the main phrase first.</summary>
    public IEnumerable<string> Phrases => Also.Prepend(Phrase);
}

/// <summary>
/// What the copilot listens for. A short, fixed list on purpose: a
/// recogniser told the dozen phrases it may hear is right far more often than
/// one taking dictation, and it needs no network.
/// </summary>
/// <remarks>
/// The actions are the ones named in <c>defaultProfile.xml</c> on this install
/// (Alpha 4.10). Mining and salvage mode are both on <c>m</c> by default; the
/// game picks by the ship you sit in, so both phrases press the same key there.
/// </remarks>
public static class VoiceCommands
{
    /// <summary>Wakes the copilot: commands are acted on until it is put to sleep.</summary>
    public const string WakePhrase = "quantum wake copilot on";

    /// <summary>Puts it to sleep; only the wake phrase is acted on after this.</summary>
    public const string SleepPhrase = "quantum wake copilot off";

    /// <summary>A shorter way to put it to sleep. Waking needs the full phrase, so a stray "on" cannot.</summary>
    public const string SleepShort = "copilot off";

    public const string ConfirmPhrase = "confirm";
    public const string CancelPhrase = "cancel";

    /// <summary>How long a command waiting on "confirm" waits.</summary>
    public static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(6);

    public static readonly IReadOnlyList<VoiceCommand> All =
    [
        new("guns", "gun mode", ["guns mode", "weapons mode"], "seat_general", "v_toggle_guns_mode", "Guns"),
        new("mining", "mining mode", [], "seat_general", "v_toggle_mining_mode", "Mining mode"),
        new("salvage", "salvage mode", [], "seat_general", "v_toggle_salvage_mode", "Salvage mode"),
        new("scanmode", "scan mode", ["scanning mode"], "seat_general", "v_toggle_scan_mode", "Scan mode"),
        new("quantum", "quantum mode", ["quantum drive mode"], "seat_general", "v_toggle_quantum_mode", "Quantum mode"),
        new("combat", "combat mode", ["S C M mode"], "spaceship_movement", "v_master_mode_set_scm", "Combat mode"),
        new("nav", "nav mode", ["navigation mode"], "spaceship_movement", "v_master_mode_set_nav", "Nav mode"),
        new("ping", "ping", ["scan", "radar ping"], "spaceship_radar", "v_invoke_ping", "Ping", HoldSeconds: 0.2),
        // Scanning runs while the key is held; three seconds is a first guess
        // at a scan that finishes on a ship close by, not a measured figure.
        new("scanobject", "scan target", ["scan object"], "spaceship_scanning", "v_scanning_trigger_scan", "Scanning", HoldSeconds: 3),
        // Mining, from the mining seat. The laser and its type are toggles the
        // game keeps the state of, so "fire laser" while it is firing stops
        // it: the phrases say what they usually mean, the key only flips.
        new("laser", "toggle laser", ["fire laser", "stop laser"], "spaceship_mining", "v_toggle_mining_laser_fire", "Laser"),
        new("lasertype", "switch laser", ["fracture mode", "extraction mode"], "spaceship_mining", "v_toggle_mining_laser_type", "Switching laser"),
        new("beam", "toggle beam", ["beam high", "beam low"], "spaceship_mining", "v_mining_use_permanent_modifier", "Beam"),
        new("module1", "module one", ["activate module one"], "spaceship_mining", "v_mining_use_consumable1", "Module one"),
        new("module2", "module two", ["activate module two"], "spaceship_mining", "v_mining_use_consumable2", "Module two"),
        new("module3", "module three", ["activate module three"], "spaceship_mining", "v_mining_use_consumable3", "Module three"),
        new("powerup", "more power", ["laser power up"], "spaceship_mining", "v_increase_mining_throttle", "More power"),
        new("powerdown", "less power", ["laser power down"], "spaceship_mining", "v_decrease_mining_throttle", "Less power"),
        // Throws the hold's ore away, so it waits for "confirm" like self destruct.
        new("jettison", "jettison cargo", ["dump cargo"], "spaceship_mining", "v_jettison_volatile_cargo", "Cargo jettisoned", Confirm: true),
        new("opendoors", "open all doors", ["open the doors", "open doors"], "spaceship_general", "v_open_all_doors", "Doors open"),
        new("closedoors", "close all doors", ["close the doors", "close doors"], "spaceship_general", "v_close_all_doors", "Doors closed"),
        new("shields", "power to shields", ["shields to max", "full power to shields"], "spaceship_power", "v_engineering_assignment_shields_max", "Shields to max"),
        new("atc", "request landing", ["request departure", "request takeoff"], "spaceship_movement", "v_atc_request", "Requesting"),
        new("selfdestruct", "self destruct", ["initiate self destruct"], "spaceship_general", "v_self_destruct", "Self destruct", Confirm: true),
        // The same key again stops the countdown, so stopping needs no confirm.
        new("abortdestruct", "abort self destruct", ["stop self destruct"], "spaceship_general", "v_self_destruct", "Self destruct stopped"),
    ];

    public static VoiceCommand? Find(string id) => All.FirstOrDefault(c => c.Id == id);

    /// <summary>The command a heard phrase is, or null when it is none of them.</summary>
    public static VoiceCommand? ForPhrase(string phrase) =>
        All.FirstOrDefault(c => c.Phrases.Any(p => string.Equals(p, phrase, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Everything the recogniser is told to listen for.</summary>
    public static IReadOnlyList<string> Grammar() =>
        [WakePhrase, SleepPhrase, SleepShort, ConfirmPhrase, CancelPhrase, .. All.SelectMany(c => c.Phrases).Distinct(StringComparer.OrdinalIgnoreCase)];
}

/// <summary>
/// How a command will be pressed: the keys, how long, how many times - or
/// why it cannot be.
/// </summary>
/// <param name="Keys">The game's key names, modifiers first - <c>lalt</c>, <c>n</c>; empty when it cannot be pressed.</param>
/// <param name="HoldSeconds">How long the last key stays down.</param>
/// <param name="Taps">1, or 2 for an action that fires on a double tap.</param>
/// <param name="Shown">The keys the way the keybinding screen shows them - "LAlt + N".</param>
/// <param name="FromProfile">True when the key is one the pilot bound, false for the game's default.</param>
/// <param name="Why">Why it cannot be pressed, as a sentence the page can show; null when it can.</param>
public sealed record KeyPlan(
    IReadOnlyList<string> Keys,
    double HoldSeconds,
    int Taps,
    string Shown,
    bool FromProfile,
    string? Why = null)
{
    public bool CanPress => Why is null && Keys.Count > 0;

    public static KeyPlan Not(string why, string shown = "") => new([], 0, 0, shown, false, why);
}

/// <summary>
/// Works out which key the game expects for an action on this install, and how
/// to press it so the game counts it.
/// </summary>
public static class VoiceBinding
{
    /// <summary>How long a plain press is held: long enough to be seen, short enough to stay a tap (the shortest tap window is 0.15 s).</summary>
    public const double TapSeconds = 0.06;

    /// <summary>Added to a hold threshold, so a frame late on either side still counts.</summary>
    public const double HoldMargin = 0.2;

    /// <summary>
    /// The plan for a command: the pilot's own keyboard binding when the
    /// profile has one, else the game's default unless the pilot cleared it.
    /// </summary>
    public static KeyPlan Plan(VoiceCommand command, ControlCatalogue catalogue, ControlProfile? profile)
    {
        if (catalogue.IsEmpty)
            return KeyPlan.Not("The game's keybinding list has not been read yet.");
        var action = catalogue.Find(command.ActionMap, command.Action);
        if (action is null)
            return KeyPlan.Not($"This version of the game has no {command.Action} action.");

        var mine = (profile?.Bindings ?? [])
            .Where(b => b.Action == command.Action && (b.ActionMap == command.ActionMap || b.ActionMap == action.ActionMap))
            .ToList();
        var keyboard = mine.Where(b => DeviceOf(b.Input) == "kb").ToList();

        ControlInput input;
        string? bindingMode = null;
        var fromProfile = false;
        if (keyboard.FirstOrDefault(b => b.Input.Kind != InputKind.None) is { } bound)
        {
            input = bound.Input;
            bindingMode = bound.ActivationMode;
            fromProfile = true;
        }
        else if (keyboard.Count > 0)
        {
            return KeyPlan.Not("Its keyboard key was cleared in your keybindings, and voice can only press keys." + StickNote(mine));
        }
        else if (action.Keyboard.Length > 0)
        {
            input = ControlInput.Parse(action.Keyboard);
        }
        else
        {
            return KeyPlan.Not("It has no keyboard key, and voice can only press keys." + StickNote(mine));
        }

        var keys = input.Modifiers.Select(m => m.Name.Length > 0 ? m.Name : m.Raw).Append(input.Name).ToList();
        var shown = string.Join(" + ", keys.Select(GameKeys.Label));
        if (keys.FirstOrDefault(k => !GameKeys.CanPress(k)) is { } odd)
            return KeyPlan.Not($"It is on {GameKeys.Label(odd)}, which the app cannot press.", shown);

        var rule = catalogue.RuleFor(action, bindingMode);
        var (hold, taps) = Timing(rule, command.HoldSeconds);
        return new KeyPlan(keys, hold, taps, shown, fromProfile);
    }

    /// <summary>
    /// How long to hold, and how many times, for a rule: past a press
    /// threshold (self destruct waits 0.5 s), past a hold delay (shields to
    /// max, 0.25 s), twice for a double tap, else a quick tap that stays inside
    /// the shortest release window.
    /// </summary>
    public static (double Hold, int Taps) Timing(ActivationRule rule, double? holdFor = null)
    {
        var taps = rule.MultiTap >= 2 ? 2 : 1;
        if (holdFor is { } h) return (h, taps);
        if (rule.PressTriggerThreshold > 0) return (rule.PressTriggerThreshold + HoldMargin, taps);
        if (rule.OnHold && rule.HoldTriggerDelay > 0) return (rule.HoldTriggerDelay + HoldMargin, taps);
        return (TapSeconds, taps);
    }

    private static string StickNote(IReadOnlyList<ControlBinding> mine) =>
        mine.FirstOrDefault(b => DeviceOf(b.Input) == "js" && b.Input.Kind != InputKind.None) is { } stick
            ? $" You have it on {stick.Input.Raw.Replace("_", " ")}."
            : "";

    /// <summary>
    /// The device a binding is on. A chord carries the prefix once, on its
    /// first part - <c>kb1_rctrl+np_5</c> - so the key itself reads as
    /// having none.
    /// </summary>
    private static string DeviceOf(ControlInput input) =>
        input.Device.Length > 0 ? input.Device : input.Modifiers.FirstOrDefault()?.Device ?? "";
}

/// <summary>
/// The game's key names and the keyboard scan codes they are, by position on
/// the board rather than by letter: the game binds the physical key, so on an
/// AZERTY board its <c>a</c> is the key printed Q, and pressing by scan code
/// presses the key the game means whatever the layout says.
/// </summary>
public static class GameKeys
{
    /// <summary>A key: its set-1 scan code, and whether it is one of the extended keys that need the E0 prefix.</summary>
    public readonly record struct ScanCode(ushort Code, bool Extended);

    private static readonly Dictionary<string, ScanCode> Codes = Build();

    /// <summary>The mouse buttons the game names, which can be pressed as well.</summary>
    private static readonly HashSet<string> MouseButtons = new(StringComparer.OrdinalIgnoreCase) { "mouse1", "mouse2", "mouse3", "mouse4", "mouse5" };

    public static ScanCode? Code(string key) => Codes.TryGetValue(key, out var code) ? code : null;

    public static bool IsMouse(string key) => MouseButtons.Contains(key);

    public static bool CanPress(string key) => Codes.ContainsKey(key) || IsMouse(key);

    /// <summary>A key the way the keybinding screen writes it: "LAlt", "F7", "Numpad 5".</summary>
    public static string Label(string key) => key.ToLowerInvariant() switch
    {
        "lalt" => "LAlt", "ralt" => "RAlt", "lctrl" => "LCtrl", "rctrl" => "RCtrl", "lshift" => "LShift", "rshift" => "RShift",
        "space" => "Space", "tab" => "Tab", "enter" => "Enter", "backspace" => "Backspace", "escape" => "Esc",
        "pgup" => "Page Up", "pgdn" => "Page Down", "home" => "Home", "end" => "End", "insert" => "Insert", "delete" => "Delete",
        "up" => "Up", "down" => "Down", "left" => "Left", "right" => "Right",
        "mouse1" => "Left mouse", "mouse2" => "Right mouse", "mouse3" => "Middle mouse", "mouse4" => "Mouse 4", "mouse5" => "Mouse 5",
        "maxis_z" or "mwheel_up" or "mwheel_down" => "the mouse wheel",
        var k when k.StartsWith("np_") => "Numpad " + k[3..] switch
        {
            "add" => "+", "subtract" => "-", "multiply" => "*", "divide" => "/", "period" => ".", "enter" => "Enter", var d => d,
        },
        var k when k.Length == 1 => k.ToUpperInvariant(),
        var k when k.Length is 2 or 3 && k[0] == 'f' && char.IsDigit(k[1]) => k.ToUpperInvariant(),
        _ => key,
    };

    private static Dictionary<string, ScanCode> Build()
    {
        var codes = new Dictionary<string, ScanCode>(StringComparer.OrdinalIgnoreCase);
        void Row(ushort start, string keys)
        {
            foreach (var k in keys) codes[k.ToString()] = new ScanCode(start++, false);
        }
        Row(0x02, "123456789");
        codes["0"] = new(0x0B, false);
        Row(0x10, "qwertyuiop");
        Row(0x1E, "asdfghjkl");
        Row(0x2C, "zxcvbnm");
        for (var i = 0; i < 10; i++) codes[$"f{i + 1}"] = new((ushort)(0x3B + i), false);
        codes["f11"] = new(0x57, false);
        codes["f12"] = new(0x58, false);

        (string Name, ushort Code, bool Extended)[] named =
        [
            ("escape", 0x01, false), ("minus", 0x0C, false), ("equals", 0x0D, false), ("backspace", 0x0E, false),
            ("tab", 0x0F, false), ("lbracket", 0x1A, false), ("rbracket", 0x1B, false), ("enter", 0x1C, false),
            ("lctrl", 0x1D, false), ("semicolon", 0x27, false), ("apostrophe", 0x28, false), ("grave", 0x29, false),
            ("lshift", 0x2A, false), ("backslash", 0x2B, false), ("comma", 0x33, false), ("period", 0x34, false),
            ("slash", 0x35, false), ("rshift", 0x36, false), ("np_multiply", 0x37, false), ("lalt", 0x38, false),
            ("space", 0x39, false), ("capslock", 0x3A, false), ("scrolllock", 0x46, false),
            ("np_7", 0x47, false), ("np_8", 0x48, false), ("np_9", 0x49, false), ("np_subtract", 0x4A, false),
            ("np_4", 0x4B, false), ("np_5", 0x4C, false), ("np_6", 0x4D, false), ("np_add", 0x4E, false),
            ("np_1", 0x4F, false), ("np_2", 0x50, false), ("np_3", 0x51, false), ("np_0", 0x52, false), ("np_period", 0x53, false),
            // The extended keys share a code with a numpad or left-hand key and
            // are told apart only by the E0 prefix; sent without it, Right Alt
            // arrives as Left Alt and the Up arrow as Numpad 8.
            ("np_enter", 0x1C, true), ("rctrl", 0x1D, true), ("np_divide", 0x35, true), ("ralt", 0x38, true),
            ("home", 0x47, true), ("up", 0x48, true), ("pgup", 0x49, true), ("left", 0x4B, true), ("right", 0x4D, true),
            ("end", 0x4F, true), ("down", 0x50, true), ("pgdn", 0x51, true), ("insert", 0x52, true), ("delete", 0x53, true),
        ];
        foreach (var (name, code, extended) in named) codes[name] = new(code, extended);
        // One default on this install writes the bracket itself rather than its name.
        codes["["] = codes["lbracket"];
        codes["]"] = codes["rbracket"];
        return codes;
    }
}
