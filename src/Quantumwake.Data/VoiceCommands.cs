using Quantumwake.Core.Controls;

namespace Quantumwake.Data;

/// <summary>
/// One thing the copilot can be told to do: the words, and the game action
/// they press.
/// </summary>
/// <param name="Id">Stable, for the settings file and the page - <c>mining</c>.</param>
/// <param name="Phrase">The words shown first, and the commonest way to say it.</param>
/// <param name="Also">
/// Other ways to say the same thing, as <see cref="PhrasePattern"/>s - one
/// pattern can stand for hundreds of wordings.
/// </param>
/// <param name="ActionMap">The action map the game files the action under.</param>
/// <param name="Action">The game's action id - <c>v_toggle_mining_mode</c>.</param>
/// <param name="Reply">What the copilot says once the key is pressed.</param>
/// <param name="Confirm">True when a spoken "confirm" has to follow before the key is pressed.</param>
/// <param name="Prompt">What the copilot asks when the command waits on "confirm" - "Confirm self destruct?" - or null for none.</param>
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
    double? HoldSeconds = null,
    string? Prompt = null)
{
    /// <summary>Every pattern it is listened for, the main phrase first.</summary>
    public IEnumerable<string> Phrases => Also.Prepend(Phrase);

    /// <summary>A few of the wordings it understands, for the page; the main phrase is not among them.</summary>
    public IReadOnlyList<string> Examples(int count = 4) =>
        [.. Phrases.Skip(1).SelectMany(p => PhrasePattern.Parse(p).Examples(2))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(e => !string.Equals(e, Phrase, StringComparison.OrdinalIgnoreCase))
            .Take(count)];
}

/// <summary>
/// What the copilot listens for. A closed vocabulary on purpose: a
/// recogniser told the phrases it may hear is right far more often than one
/// taking dictation, and it needs no network. Since 1.2.5 each command is
/// patterns rather than a sentence or two, so the usual ways of saying a
/// thing all work, and any command may open with "copilot" and end with
/// "please".
/// </summary>
/// <remarks>
/// The actions are the ones named in <c>defaultProfile.xml</c> on this install
/// (Alpha 4.10). Mining and salvage mode are both on <c>m</c> by default up to
/// 4.10.2; the game picks by the ship you sit in, so both phrases press the
/// same key there. The 4.10.3 PTU (4.10.200, 2026-11-10) takes <c>m</c> off
/// both and gives it to <c>v_cycle_industrial_mode</c>, which steps through
/// the operator modes: "next mode" presses that, and on a build without the
/// action it says so rather than pressing anything.
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

    // The words a command is built from, written once so every command that
    // names shields hears them the same way (1.2.5).
    private const string Shield = "(shield|shields)";
    private const string Switch = "[switch to|go to|enter|set|engage]";
    private const string FaceVerb = "[raise|boost|increase|reinforce|full|max|maximum|more|divert|angle|shift|focus|move]";

    /// <summary>
    /// Raising one shield face, said either way round: "boost the rear
    /// shields", "full power to the front shields", "shields to the right",
    /// "full shield back". Up and down are left out of the directions on
    /// purpose - "shields up" and "lower shields" are power, not a face.
    /// </summary>
    private static VoiceCommand Face(string id, string phrase, string direction, string action, string reply) =>
        new(id, phrase,
            [$"{FaceVerb} [power to] [the] {direction} {Shield}", $"{FaceVerb} {Shield} [power] [to] [the] {direction}"],
            "spaceship_defensive", action, reply);

    public static readonly IReadOnlyList<VoiceCommand> All =
    [
        new("guns", "gun mode", [$"{Switch} (gun|guns|gunnery|weapon|weapons) mode", "weapons hot"], "seat_general", "v_toggle_guns_mode", "Guns"),
        new("mining", "mining mode", [$"{Switch} mining mode", "switch to mining"], "seat_general", "v_toggle_mining_mode", "Mining mode"),
        new("salvage", "salvage mode", [$"{Switch} salvage mode", "switch to salvage"], "seat_general", "v_toggle_salvage_mode", "Salvage mode"),
        new("industrial", "next mode", ["next [industrial|operator] mode", "(cycle|change|switch) [the] (industrial|operator) (mode|modes)", "cycle (mode|modes)", "industrial mode"], "seat_general", "v_cycle_industrial_mode", "Next mode"),
        new("scanmode", "scan mode", [$"{Switch} (scan|scanning) mode", "switch to scanning"], "seat_general", "v_toggle_scan_mode", "Scan mode"),
        new("quantum", "quantum mode", [$"{Switch} quantum [drive] mode", "switch to quantum"], "seat_general", "v_toggle_quantum_mode", "Quantum mode"),
        new("combat", "combat mode", [$"{Switch} (combat|S C M) mode", "switch to (combat|S C M)", "S C M"], "spaceship_movement", "v_master_mode_set_scm", "Combat mode"),
        new("nav", "nav mode", [$"{Switch} (nav|navigation) mode", "switch to (nav|navigation)"], "spaceship_movement", "v_master_mode_set_nav", "Nav mode"),
        new("ping", "ping", ["[radar] ping", "send [a|out a] [radar] ping", "scan [the] area", "scan"], "spaceship_radar", "v_invoke_ping", "Ping", HoldSeconds: 0.2),
        // Scanning runs while the key is held; three seconds is a first guess
        // at a scan that finishes on a ship close by, not a measured figure.
        new("scanobject", "scan target", ["scan [the|that] (target|object|contact|ship)"], "spaceship_scanning", "v_scanning_trigger_scan", "Scanning", HoldSeconds: 3),
        // Mining, from the mining seat. The laser and its type are toggles the
        // game keeps the state of, so "fire laser" while it is firing stops
        // it: the phrases say what they usually mean, the key only flips.
        new("laser", "toggle laser", ["(toggle|fire|stop|start) [the] [mining] laser", "[mining] laser (on|off)"], "spaceship_mining", "v_toggle_mining_laser_fire", "Laser"),
        new("lasertype", "switch laser", ["switch [the] laser [type|mode]", "(fracture|fracturing|extraction|extract) mode", "switch to (fracture|fracturing|extraction)"], "spaceship_mining", "v_toggle_mining_laser_type", "Switching laser"),
        new("beam", "toggle beam", ["(toggle|switch) [the] beam", "beam (high|low)"], "spaceship_mining", "v_mining_use_permanent_modifier", "Beam switched"),
        new("module1", "module one", ["[activate|use|fire|trigger] [mining] module one", "first module"], "spaceship_mining", "v_mining_use_consumable1", "Module one"),
        new("module2", "module two", ["[activate|use|fire|trigger] [mining] module two", "second module"], "spaceship_mining", "v_mining_use_consumable2", "Module two"),
        new("module3", "module three", ["[activate|use|fire|trigger] [mining] module three", "third module"], "spaceship_mining", "v_mining_use_consumable3", "Module three"),
        new("powerup", "more power", ["(more|increase|raise) [laser|mining] power", "(laser|mining) power up", "increase [the] [laser|mining] throttle"], "spaceship_mining", "v_increase_mining_throttle", "More power"),
        new("powerdown", "less power", ["(less|decrease|reduce) [laser|mining] power", "(laser|mining) power down", "decrease [the] [laser|mining] throttle"], "spaceship_mining", "v_decrease_mining_throttle", "Less power"),
        // Throws the hold's ore away, so it waits for "confirm" like self destruct.
        new("jettison", "jettison cargo", ["(jettison|dump|eject|drop) [the|all|all the|our] cargo"], "spaceship_mining", "v_jettison_volatile_cargo", "Cargo jettisoned", Confirm: true, Prompt: "Confirm jettison?"),
        new("opendoors", "open all doors", ["open [all] [the] doors", "open all the doors"], "spaceship_general", "v_open_all_doors", "Doors open"),
        new("closedoors", "close all doors", ["(close|shut) [all] [the] doors", "(close|shut) all the doors"], "spaceship_general", "v_close_all_doors", "Doors closed"),
        new("atc", "request landing", ["request (landing|departure|takeoff|take off|clearance|landing clearance|permission to land)", "(call|contact|hail) [the] tower"], "spaceship_movement", "v_atc_request", "Requesting clearance"),
        // Landing gear: deploy and retract are separate actions with no key
        // by default, so they need the spare keys; the toggle is on N.
        new("geardown", "gear down", ["[landing] gear down", "(deploy|lower|drop) [the] [landing] gear"], "spaceship_movement", "v_deploy_landing_system", "Gear down"),
        new("gearup", "gear up", ["[landing] gear up", "(retract|raise) [the] [landing] gear"], "spaceship_movement", "v_retract_landing_system", "Gear up"),
        new("geartoggle", "toggle landing gear", ["toggle [the] [landing] gear"], "spaceship_movement", "v_toggle_landing_system", "Landing gear"),

        // Power to shields: the engineering assignment, F7 and LAlt+F7 by
        // default - a tap moves a pip, a hold goes to the end.
        new("shields", "power to shields", [$"[full|max|maximum|all] power to [the] {Shield}", $"{Shield} to (max|maximum|full)", $"(max|maximum|full|maximize) [the] {Shield}"], "spaceship_power", "v_engineering_assignment_shields_max", "Shields to max"),
        new("shieldsup", "increase shields", [$"(increase|raise|boost) [the] {Shield} [power]", $"more power to [the] {Shield}", $"more {Shield}", $"{Shield} (up|power up)"], "spaceship_power", "v_engineering_assignment_shields_increase", "Shields up"),
        new("shieldsdown", "decrease shields", [$"(decrease|lower|reduce) [the] {Shield} [power]", $"less power to [the] {Shield}", $"less {Shield}", $"{Shield} (down|power down)"], "spaceship_power", "v_engineering_assignment_shields_decrease", "Shields down"),
        new("shieldsmin", "minimum shields", [$"(min|minimum|minimize) [the] {Shield}", $"{Shield} to (min|minimum)", $"minimum power to [the] {Shield}"], "spaceship_power", "v_engineering_assignment_shields_min", "Shields to minimum"),
        new("weapons", "power to weapons", ["[full|max|maximum|all] power to [the] (weapon|weapons|guns)", "(weapon|weapons) to (max|maximum|full)"], "spaceship_power", "v_engineering_assignment_weapons_max", "Weapons to max"),
        new("weaponsup", "more power to weapons", ["more power to [the] (weapon|weapons|guns)", "(increase|boost) [the] (weapon|weapons) power"], "spaceship_power", "v_engineering_assignment_weapons_increase", "Weapons up"),
        new("weaponsdown", "less power to weapons", ["less power to [the] (weapon|weapons|guns)", "(decrease|reduce|lower) [the] (weapon|weapons) power"], "spaceship_power", "v_engineering_assignment_weapons_decrease", "Weapons down"),
        new("engines", "power to engines", ["[full|max|maximum|all] power to [the] (engine|engines|thrusters)", "(engine|engines) to (max|maximum|full)"], "spaceship_power", "v_engineering_assignment_engine_max", "Engines to max"),
        new("enginesup", "more power to engines", ["more power to [the] (engine|engines|thrusters)", "(increase|boost) [the] (engine|engines) power"], "spaceship_power", "v_engineering_assignment_engine_increase", "Engines up"),
        new("enginesdown", "less power to engines", ["less power to [the] (engine|engines|thrusters)", "(decrease|reduce|lower) [the] (engine|engines) power"], "spaceship_power", "v_engineering_assignment_engine_decrease", "Engines down"),
        new("powerreset", "balance power", ["(reset|balance|equalize) [the] power", "reset [the] [power] assignments", "power (reset|balanced)"], "spaceship_power", "v_engineering_assignment_reset", "Power balanced"),

        // Shield faces. In the controls file and unbound by default; whether a
        // given ship still has faces to raise is the game's to say, not ours.
        Face("shieldfront", "front shields", "(front|forward|fore|nose|frontal)", "v_shield_raise_level_forward", "Front shields"),
        Face("shieldback", "rear shields", "(back|rear|aft|stern)", "v_shield_raise_level_back", "Rear shields"),
        Face("shieldleft", "left shields", "(left|port)", "v_shield_raise_level_left", "Left shields"),
        Face("shieldright", "right shields", "(right|starboard)", "v_shield_raise_level_right", "Right shields"),
        Face("shieldtop", "top shields", "(top|dorsal|upper)", "v_shield_raise_level_up", "Top shields"),
        Face("shieldbottom", "bottom shields", "(bottom|belly|ventral|underside)", "v_shield_raise_level_down", "Bottom shields"),
        new("shieldreset", "balance shields", [$"(reset|balance|equalize|center|centre|level) [the] {Shield} [faces|levels]", $"even out [the] {Shield}", $"{Shield} (reset|balanced|even)"], "spaceship_defensive", "v_shield_reset_level", "Shields balanced"),

        // Shield power itself. On and off are a sound apart, so off waits on
        // "confirm": a misheard "on" must not drop them in a fight.
        new("shieldson", "shields on", [$"{Shield} (on|online)", $"turn [the] {Shield} on", $"turn on [the] {Shield}", $"(enable|activate) [the] {Shield}"], "spaceship_power", "v_power_set_shields_on", "Shields on"),
        new("shieldsoff", "shields off", [$"{Shield} (off|offline)", $"turn [the] {Shield} off", $"turn off [the] {Shield}", $"(disable|deactivate|kill|drop) [the] {Shield}"], "spaceship_power", "v_power_set_shields_off", "Shields off", Confirm: true, Prompt: "Confirm shields off?"),
        new("shieldstoggle", "toggle shields", [$"toggle [the] {Shield}"], "spaceship_power", "v_power_toggle_shields", "Shields toggled"),

        new("selfdestruct", "self destruct", ["[initiate|start|begin|activate|engage] self destruct [sequence]"], "spaceship_general", "v_self_destruct", "Self destruct", Confirm: true, Prompt: "Confirm self destruct?"),
        // The same key again stops the countdown - and starts one when none is
        // running. So it presses at once only shortly after a self destruct the
        // copilot confirmed itself, and asks for "confirm" otherwise (1.2.5).
        new("abortdestruct", "abort self destruct", ["(abort|stop|cancel|halt) [the] self destruct [sequence]"], "spaceship_general", "v_self_destruct", "Self destruct stopped", Prompt: "Confirm abort self destruct?"),
    ];

    public const string SayAwake = "Copilot on";
    public const string SayAsleep = "Copilot off";
    public const string SayCancelled = "Cancelled";
    public const string SayCannotPress = "Can't press that";
    public const string SayNotInFront = "Game not in front";

    /// <summary>
    /// Every sentence the copilot can say. The recorded voice pack has to
    /// cover each one - a test holds it to that - or the reply falls back to
    /// the Windows voice mid-session and sounds like a different copilot.
    /// </summary>
    public static IReadOnlyList<string> Spoken() =>
    [
        SayAwake, SayAsleep, SayCancelled, SayCannotPress, SayNotInFront,
        .. All.Select(c => c.Reply),
        .. All.Where(c => c.Confirm || c.Prompt is not null).Select(c => c.Prompt ?? $"Confirm {c.Phrase}?"),
    ];

    public static VoiceCommand? Find(string id) => All.FirstOrDefault(c => c.Id == id);

    private static readonly Lazy<(VoiceCommand Command, PhrasePattern Pattern)[]> Compiled =
        new(() => [.. All.SelectMany(c => c.Phrases.Select(p => (c, PhrasePattern.Parse(p))))]);

    /// <summary>
    /// The command a heard phrase is, or null when it is none of them. A
    /// leading "copilot" and a trailing "please" are manners, not words of
    /// the command.
    /// </summary>
    public static VoiceCommand? ForPhrase(string phrase)
    {
        var text = WithoutManners(phrase);
        return text.Length == 0 ? null : Compiled.Value.FirstOrDefault(x => x.Pattern.Matches(text)).Command;
    }

    /// <summary>The heard words with a leading "copilot" and a trailing "please" taken off.</summary>
    public static string WithoutManners(string phrase)
    {
        var text = PhrasePattern.Normalise(phrase);
        if (text.StartsWith("copilot ", StringComparison.Ordinal)) text = text["copilot ".Length..];
        if (text.EndsWith(" please", StringComparison.Ordinal)) text = text[..^" please".Length];
        return text;
    }

    /// <summary>
    /// Every pattern the recogniser is told to listen for. The wake, sleep,
    /// confirm and cancel words stay bare: they are compared word for word,
    /// and "confirm" is the one word that must never be heard loosely.
    /// </summary>
    public static IReadOnlyList<string> Grammar() =>
        [WakePhrase, SleepPhrase, SleepShort, ConfirmPhrase, CancelPhrase,
            .. All.SelectMany(c => c.Phrases).Distinct(StringComparer.OrdinalIgnoreCase).Select(p => $"[copilot] {p} [please]")];
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
/// <param name="NeedsKey">True when the reason is that it has no keyboard key at all - the case a spare key fixes.</param>
public sealed record KeyPlan(
    IReadOnlyList<string> Keys,
    double HoldSeconds,
    int Taps,
    string Shown,
    bool FromProfile,
    string? Why = null,
    bool NeedsKey = false)
{
    public bool CanPress => Why is null && Keys.Count > 0;

    public static KeyPlan Not(string why, string shown = "", bool needsKey = false) => new([], 0, 0, shown, false, why, needsKey);
}

/// <summary>A keyboard key the app proposes for a command that has none, in the profile's own spelling - <c>kb1_rctrl+np_1</c>.</summary>
public sealed record SpareKey(VoiceCommand Command, string Input, string Shown);

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
            return KeyPlan.Not("Its keyboard key was cleared in your keybindings, and voice can only press keys." + StickNote(mine), needsKey: true);
        }
        else if (action.Keyboard.Length > 0)
        {
            input = ControlInput.Parse(action.Keyboard);
        }
        else
        {
            return KeyPlan.Not("It has no keyboard key, and voice can only press keys." + StickNote(mine), needsKey: true);
        }

        // The game writes some chords modifier last - Shields Decrease is
        // f7+lalt - and pressing them in that order put F7 down alone first,
        // which is Shields Increase. Modifiers go down first, whatever the
        // spelling (1.2.5).
        var keys = input.Modifiers.Select(m => m.Name.Length > 0 ? m.Name : m.Raw).Append(input.Name)
            .OrderBy(k => GameKeys.IsModifier(k) ? 0 : 1).ToList();
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

    /// <summary>
    /// Keys the app offers for commands that have none: a right-hand modifier
    /// with a numpad key or a function key, which the game leaves free by
    /// default. Any key already used - by the pilot's profile or by any of the
    /// game's own defaults, for any action - is skipped, because binding it
    /// would take it off that action. A modifier bound on its own anywhere is
    /// not used at all, since holding it would fire that action too.
    /// </summary>
    public static IReadOnlyList<SpareKey> SpareKeys(IEnumerable<VoiceCommand> commands, ControlCatalogue catalogue, ControlProfile? profile)
    {
        if (catalogue.IsEmpty) return [];
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lone = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Note(ControlInput input)
        {
            var parts = input.Modifiers.Select(m => m.Name).Append(input.Name).Where(k => k.Length > 0).ToList();
            if (parts.Count == 0) return;
            used.Add(Chord(parts));
            if (parts.Count == 1) lone.Add(parts[0]);
        }
        foreach (var action in catalogue.Actions.Where(a => a.Keyboard.Length > 0)) Note(ControlInput.Parse(action.Keyboard));
        foreach (var b in (profile?.Bindings ?? []).Where(b => DeviceOf(b.Input) == "kb" && b.Input.Kind != InputKind.None)) Note(b.Input);

        string[] tails = ["np_1", "np_2", "np_3", "np_4", "np_5", "np_6", "np_7", "np_8", "np_9", "np_0",
            "f1", "f2", "f3", "f4", "f5", "f6", "f7", "f8", "f9", "f10", "f11", "f12"];
        var pool = new[] { "rctrl", "ralt" }.Where(m => !lone.Contains(m))
            .SelectMany(m => tails.Select(t => new[] { m, t }))
            .Where(c => c.All(GameKeys.CanPress) && !used.Contains(Chord(c)))
            .GetEnumerator();

        var spare = new List<SpareKey>();
        var given = new HashSet<(string, string)>();
        foreach (var command in commands)
        {
            if (!given.Add((command.ActionMap, command.Action))) continue;
            if (!Plan(command, catalogue, profile).NeedsKey) continue;
            if (!pool.MoveNext()) break;
            var chord = pool.Current;
            spare.Add(new SpareKey(command, $"kb1_{string.Join('+', chord)}", string.Join(" + ", chord.Select(GameKeys.Label))));
        }
        return spare;
    }

    private static string Chord(IEnumerable<string> keys) =>
        string.Join('+', keys.Select(k => k.ToLowerInvariant()).OrderBy(k => k, StringComparer.Ordinal));

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

    /// <summary>True for a key that is held with another rather than pressed for itself.</summary>
    public static bool IsModifier(string key) => key.ToLowerInvariant() is "lalt" or "ralt" or "lctrl" or "rctrl" or "lshift" or "rshift";

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
