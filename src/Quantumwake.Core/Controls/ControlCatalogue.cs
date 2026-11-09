using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace Quantumwake.Core.Controls;

/// <summary>An action map from the catalogue: a group of actions with a label and a category.</summary>
/// <param name="Name">The game's id - <c>spaceship_targeting</c>.</param>
/// <param name="Label">In the game's words - "Vehicles - Targeting"; the id when the strings have none.</param>
/// <param name="Category">The keybinding screen's tab - "FLIGHT"; empty for a map with none.</param>
public sealed record ActionMapInfo(string Name, string Label, string Category);

/// <summary>
/// One action the game can bind, with its defaults per device kind and how
/// it fires.
/// </summary>
/// <param name="Label">In the game's words - "Eject"; the id when the strings have none.</param>
/// <param name="Description">The keybinding screen's help line, or empty.</param>
/// <param name="Keyboard">The default keyboard input, or empty; a single space in the file means "none, but bindable".</param>
/// <param name="ActivationMode">tap, hold, double_tap … or empty for press-and-release.</param>
/// <param name="OptionGroup">The curve group an axis belongs to - <c>flight_move_pitch</c> - or empty.</param>
/// <param name="Inline">
/// How the action fires when it names no mode but spells the rule out on
/// itself - "Shields - Set to Max (Hold)" carries <c>onHold=1
/// holdTriggerDelay=0.25</c> and no <c>activationMode</c> - or null.
/// </param>
/// <param name="KeyboardMode">A mode the keyboard default carries on its own, apart from the action's; empty for none.</param>
public sealed record ActionInfo(
    string ActionMap,
    string Name,
    string Label,
    string Description,
    string Keyboard,
    string Mouse,
    string Gamepad,
    string Joystick,
    string ActivationMode,
    string OptionGroup,
    ActivationRule? Inline = null,
    string KeyboardMode = "");

/// <summary>
/// What an activation mode asks of a key: when it fires, and how long the key
/// has to be down, or up, for that. Read from the <c>ActivationModes</c> block
/// at the top of <c>defaultProfile.xml</c>, where 18 are defined on this
/// install (Alpha 4.10).
/// </summary>
/// <param name="PressTriggerThreshold">Seconds the key must be held before a press counts - 0.5 for <c>delayed_press_medium</c>; -1 for none.</param>
/// <param name="ReleaseTriggerThreshold">Seconds within which the key must come back up - 0.25 for <c>tap</c>; -1 for none.</param>
/// <param name="HoldTriggerDelay">Seconds of holding before an <c>onHold</c> action fires; -1 for none.</param>
/// <param name="MultiTap">How many presses make one: 2 for <c>double_tap</c>.</param>
public sealed record ActivationRule(
    bool OnPress,
    bool OnHold,
    bool OnRelease,
    int MultiTap = 1,
    double PressTriggerThreshold = -1,
    double ReleaseTriggerThreshold = -1,
    double HoldTriggerDelay = -1)
{
    /// <summary>A plain press: fires as the key goes down.</summary>
    public static readonly ActivationRule Press = new(true, false, false);
}

/// <summary>
/// The keybinding catalogue: every action the game can bind, from
/// <c>Data\Libs\Config\defaultProfile.xml</c>, labelled from the game's own
/// strings.
/// </summary>
/// <remarks>
/// 50 action maps and 1,106 actions on this install (Alpha 4.10); 770 of the
/// 837 distinct labels resolve through <c>global.ini</c>, and the rest read
/// as their id, which is still what the profile calls them.
/// </remarks>
/// <param name="Modes">The named activation modes, by name; null in a catalogue cached before they were read.</param>
public sealed record ControlCatalogue(
    IReadOnlyList<ActionMapInfo> ActionMaps,
    IReadOnlyList<ActionInfo> Actions,
    IReadOnlyDictionary<string, ActivationRule>? Modes = null)
{
    public static readonly ControlCatalogue Empty = new([], []);

    [JsonIgnore]
    public bool IsEmpty => Actions.Count == 0;

    /// <summary>The action, or null when the profile binds something the catalogue does not list.</summary>
    public ActionInfo? Find(string actionMap, string action) =>
        Actions.FirstOrDefault(a => a.ActionMap == actionMap && a.Name == action)
        ?? Actions.FirstOrDefault(a => a.Name == action);

    /// <summary>
    /// The rule an action fires by on the keyboard: a mode the binding names
    /// (a profile can override it per binding), else the keyboard default's
    /// own, else the action's, else what the action spells out inline, else a
    /// plain press - which is what the game does with an action that says
    /// nothing.
    /// </summary>
    public ActivationRule RuleFor(ActionInfo action, string? bindingMode = null)
    {
        foreach (var name in new[] { bindingMode, action.KeyboardMode, action.ActivationMode })
            if (!string.IsNullOrEmpty(name) && Modes?.GetValueOrDefault(name) is { } rule)
                return rule;
        return action.Inline ?? ActivationRule.Press;
    }

    public static ControlCatalogue Parse(byte[] data, IReadOnlyDictionary<string, string> text) =>
        Parse(Quantumwake.Core.GameData.CryXml.Parse(data), text);

    public static ControlCatalogue Parse(XDocument document, IReadOnlyDictionary<string, string> text)
    {
        var root = document.Root ?? throw new InvalidDataException("No root element.");
        string Word(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return "";
            var bare = key.StartsWith('@') ? key[1..] : key;
            // The strings file keeps a platform variant under "key,P" - the
            // PC wording - and for a third of the actions only that one:
            // ui_CIMissileMode has no plain line, ui_CIMissileMode,P does.
            if (text.TryGetValue(bare, out var value) && value.Length > 0) return value;
            if (text.TryGetValue(bare + ",P", out var pc) && pc.Length > 0) return pc;
            return bare;
        }

        var modes = new Dictionary<string, ActivationRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var mode in root.Element("ActivationModes")?.Elements() ?? [])
            if ((string?)mode.Attribute("name") is { Length: > 0 } modeName)
                modes[modeName] = RuleOf(mode) ?? ActivationRule.Press;

        var maps = new List<ActionMapInfo>();
        var actions = new List<ActionInfo>();
        foreach (var map in root.Elements("actionmap"))
        {
            var mapName = (string?)map.Attribute("name") ?? "";
            if (mapName.Length == 0) continue;
            // A category with no string - ui_CCSeatGeneral has none on this
            // install - is no category, not a raw key on a tab.
            var categoryKey = (string?)map.Attribute("UICategory");
            var category = Word(categoryKey);
            if (category == (categoryKey ?? "").TrimStart('@')) category = "";
            maps.Add(new ActionMapInfo(mapName, Word((string?)map.Attribute("UILabel")) is { Length: > 0 } l ? l : mapName, category));

            foreach (var action in map.Elements("action"))
            {
                var name = (string?)action.Attribute("name") ?? "";
                if (name.Length == 0) continue;
                // Five actions keep their keyboard default in a child element
                // rather than the attribute - prone rolls as
                // <keyboard activationMode="double_tap" input="q"/>, the text
                // fields as a list of <inputdata>; read as the attribute, the
                // key would show as unbound.
                var keyboard = ((string?)action.Attribute("keyboard") ?? "").Trim();
                var keyboardMode = "";
                if (action.Element("keyboard") is { } child)
                {
                    if (keyboard.Length == 0)
                        keyboard = ((string?)child.Attribute("input")
                            ?? (string?)child.Elements("inputdata").FirstOrDefault()?.Attribute("input") ?? "").Trim();
                    keyboardMode = Attr(child, "activationMode") ?? "";
                }
                actions.Add(new ActionInfo(
                    mapName, name,
                    Word((string?)action.Attribute("UILabel")) is { Length: > 0 } label ? label : name,
                    Word((string?)action.Attribute("UIDescription")),
                    keyboard,
                    ((string?)action.Attribute("mouse") ?? "").Trim(),
                    ((string?)action.Attribute("gamepad") ?? "").Trim(),
                    ((string?)action.Attribute("joystick") ?? "").Trim(),
                    Attr(action, "activationMode") ?? "",
                    (string?)action.Attribute("optionGroup") ?? "",
                    RuleOf(action),
                    keyboardMode));
            }
        }

        return new ControlCatalogue(maps, actions, modes);
    }

    /// <summary>
    /// The attribute in any case: the file spells it <c>activationMode</c> on
    /// 731 actions, <c>ActivationMode</c> on 21 and <c>activationmode</c> on
    /// one, and reading only the first left 22 actions with no mode at all.
    /// </summary>
    private static string? Attr(XElement element, string name) =>
        element.Attributes().FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))?.Value;

    /// <summary>The firing rule an element spells out, or null when it names none of its parts.</summary>
    private static ActivationRule? RuleOf(XElement element)
    {
        string? A(string n) => Attr(element, n);
        if (A("onPress") is null && A("onHold") is null && A("onRelease") is null) return null;
        static double Num(string? v) => double.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : -1;
        return new ActivationRule(
            A("onPress") == "1", A("onHold") == "1", A("onRelease") == "1",
            int.TryParse(A("multiTap"), out var taps) && taps > 0 ? taps : 1,
            Num(A("pressTriggerThreshold")), Num(A("releaseTriggerThreshold")), Num(A("holdTriggerDelay")));
    }
}
