using System.Xml.Linq;

namespace Quantumwake.Core.Controls;

/// <summary>
/// Writes a keybinding profile in the form the game imports: the export
/// document the keybinding screen and <c>pp_rebindkeys</c> read from
/// <c>user\client\0\controls\mappings\</c>.
/// </summary>
/// <remarks>
/// <para>
/// The live <c>actionmaps.xml</c> and an export are the same document apart
/// from the frame: the live file wraps its <c>ActionProfiles</c> in an
/// <c>ActionMaps</c> root, an export is one <c>ActionMaps</c> root carrying
/// the profile's attributes and a <c>CustomisationUIHeader</c> naming the
/// devices. So a kept copy of the live file becomes an import by moving the
/// attributes up and adding the header - nothing bound is touched.
/// </para>
/// <para>
/// Retargeting is the swap fix: when the sticks were re-enumerated and the
/// throttle that was <c>js2</c> is now <c>js3</c>, every <c>js2_</c> input and
/// the <c>js2</c> options line move to 3, and whatever was 3 moves to where
/// the pilot says. It is string work on the inputs, done through
/// <see cref="ControlInput"/> so a chord's modifiers move with it.
/// </para>
/// </remarks>
/// <summary>One change to what an action is bound to.</summary>
/// <param name="Input">The input as the game writes it - <c>js4_button7</c>; for a removal, the one to take off.</param>
/// <param name="Remove">True to take the binding off; the action then falls back to the game's default for that device kind.</param>
/// <param name="ActivationMode">A mode to write on the binding - <c>hold</c>, <c>double_tap</c> - or null for the action's own.</param>
public sealed record BindingChange(string ActionMap, string Action, string Input, bool Remove = false, string? ActivationMode = null);

/// <summary>A curve for one option group on one stick: the exponent, and whether the axis is inverted.</summary>
/// <param name="Exponent">1 is linear; above it the centre is flatter; null leaves the game's default.</param>
public sealed record CurveSetting(string Option, double? Exponent, bool Inverted);

public static class ControlsExport
{
    /// <summary>
    /// The export document for a profile, live or already exported.
    /// </summary>
    /// <param name="source">The profile as the game wrote it.</param>
    /// <param name="name">The profile name the game will list it under; also the header label.</param>
    /// <param name="retarget">Old joystick instance to new, for the sticks that moved; others keep their number.</param>
    public static XDocument Build(XDocument source, string name, IReadOnlyDictionary<int, int>? retarget = null)
    {
        var root = source.Root ?? throw new InvalidDataException("No root element.");
        var profile = root.Name.LocalName == "ActionMaps" && root.Element("ActionProfiles") is { } inner ? inner : root;

        var export = new XElement("ActionMaps",
            new XAttribute("version", (string?)profile.Attribute("version") ?? "1"),
            new XAttribute("optionsVersion", (string?)profile.Attribute("optionsVersion") ?? "2"),
            new XAttribute("rebindVersion", (string?)profile.Attribute("rebindVersion") ?? "2"),
            new XAttribute("profileName", name));

        var moved = retarget ?? new Dictionary<int, int>();
        int Instance(int n) => moved.TryGetValue(n, out var to) ? to : n;

        // The header lists the devices the profile binds, keyboard and mouse
        // included, which the game wants even when it can read them off the
        // options below.
        var devices = new XElement("devices");
        var seen = new HashSet<(string, int)>();
        // Keyboard and mouse first, as the game writes them, whether or not
        // the profile carries an options line for either.
        foreach (var type in new[] { "keyboard", "mouse" })
            if (seen.Add((type, 1))) devices.Add(new XElement(type, new XAttribute("instance", 1)));
        foreach (var options in profile.Elements("options"))
        {
            var type = (string?)options.Attribute("type") ?? "";
            if (!int.TryParse((string?)options.Attribute("instance"), out var instance)) continue;
            if (type == "joystick") instance = Instance(instance);
            if (seen.Add((type, instance))) devices.Add(new XElement(type, new XAttribute("instance", instance)));
        }
        export.Add(new XElement("CustomisationUIHeader",
            new XAttribute("label", name), new XAttribute("description", ""), new XAttribute("image", ""),
            devices, new XElement("categories")));

        foreach (var child in profile.Elements())
        {
            if (child.Name.LocalName == "CustomisationUIHeader") continue;
            var copy = new XElement(child);
            if (copy.Name.LocalName == "options" && (string?)copy.Attribute("type") == "joystick"
                && int.TryParse((string?)copy.Attribute("instance"), out var instance))
                copy.SetAttributeValue("instance", Instance(instance));
            if (copy.Name.LocalName == "actionmap" && moved.Count > 0)
                foreach (var rebind in copy.Descendants("rebind"))
                    if ((string?)rebind.Attribute("input") is { } input)
                        rebind.SetAttributeValue("input", Retarget(input, moved));
            export.Add(copy);
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", null), export);
    }

    /// <summary>
    /// The live frame for a document: what <c>actionmaps.xml</c> has to look
    /// like for the game to read it at start. A kept copy of the live file
    /// is already that; an export brought back from the mappings folder is
    /// the same profile under the other root, and is re-framed - the header
    /// dropped, the attributes moved down, the name set back to "default",
    /// which is the only profile the game reads there.
    /// </summary>
    public static XDocument ToLive(XDocument source)
    {
        var root = source.Root ?? throw new InvalidDataException("No root element.");
        if (root.Name.LocalName == "ActionMaps" && root.Element("ActionProfiles") is not null)
            return new XDocument(source);

        var profile = new XElement("ActionProfiles",
            new XAttribute("version", (string?)root.Attribute("version") ?? "1"),
            new XAttribute("optionsVersion", (string?)root.Attribute("optionsVersion") ?? "2"),
            new XAttribute("rebindVersion", (string?)root.Attribute("rebindVersion") ?? "2"),
            new XAttribute("profileName", "default"));
        foreach (var child in root.Elements())
            if (child.Name.LocalName != "CustomisationUIHeader") profile.Add(new XElement(child));
        return new XDocument(new XElement("ActionMaps", profile));
    }

    /// <summary>
    /// The profile with one stick's axis settings changed: the curve per
    /// option group on its <c>options</c> line, the dead zone per axis on its
    /// <c>deviceoptions</c> block. Everything else is left as the game wrote
    /// it. A setting at its default - exponent 1, not inverted, dead zone 0 -
    /// is removed rather than written, which is how the game writes a
    /// setting put back.
    /// </summary>
    /// <param name="instance">The stick's number.</param>
    /// <param name="product">Its product name, for a deviceoptions block that has to be made.</param>
    /// <param name="guid">Its product GUID, likewise.</param>
    public static XDocument ApplyAxes(
        XDocument source, int instance, string? product, string? guid,
        IReadOnlyList<CurveSetting> curves, IReadOnlyDictionary<string, double> deadzones)
    {
        var document = new XDocument(source);
        var root = document.Root ?? throw new InvalidDataException("No root element.");
        var profile = root.Name.LocalName == "ActionMaps" && root.Element("ActionProfiles") is { } inner ? inner : root;
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        var options = profile.Elements("options").FirstOrDefault(o =>
            (string?)o.Attribute("type") == "joystick" && (string?)o.Attribute("instance") == instance.ToString(culture));
        if (options is null)
        {
            options = new XElement("options", new XAttribute("type", "joystick"), new XAttribute("instance", instance));
            if (product is not null) options.SetAttributeValue("Product", guid is not null ? $"{product}  {guid}" : product);
            // After the last options line, where the game keeps them.
            var last = profile.Elements("options").LastOrDefault();
            if (last is not null) last.AddAfterSelf(options); else profile.AddFirst(options);
        }
        foreach (var curve in curves)
        {
            if (string.IsNullOrWhiteSpace(curve.Option)) continue;
            var line = options.Element(curve.Option);
            var plain = (curve.Exponent is null || Math.Abs(curve.Exponent.Value - 1) < 0.0005) && !curve.Inverted;
            if (plain) { line?.Remove(); continue; }
            if (line is null) { line = new XElement(curve.Option); options.Add(line); }
            if (curve.Exponent is { } e && Math.Abs(e - 1) >= 0.0005) line.SetAttributeValue("exponent", e.ToString("0.###", culture));
            else line.SetAttributeValue("exponent", null);
            line.SetAttributeValue("invert", curve.Inverted ? "1" : null);
        }

        if (deadzones.Count > 0 && product is not null)
        {
            var block = profile.Elements("deviceoptions").FirstOrDefault(d =>
                ((string?)d.Attribute("name") ?? "").StartsWith(product, StringComparison.OrdinalIgnoreCase));
            if (block is null)
            {
                block = new XElement("deviceoptions", new XAttribute("name", guid is not null ? $"{product}  {guid}" : product));
                profile.AddFirst(block);
            }
            foreach (var (axis, value) in deadzones)
            {
                var option = block.Elements("option").FirstOrDefault(o => string.Equals((string?)o.Attribute("input"), axis, StringComparison.OrdinalIgnoreCase));
                if (value <= 0) { option?.Remove(); continue; }
                if (option is null) { option = new XElement("option", new XAttribute("input", axis)); block.Add(option); }
                option.SetAttributeValue("deadzone", value.ToString("0.####", culture));
            }
            if (!block.Elements("option").Any()) block.Remove();
        }

        return document;
    }

    /// <summary>
    /// The profile with bindings changed. Setting an input on an action
    /// replaces whatever that action had on the same device, and an exact
    /// input belongs to one action only. Chords are separate inputs, so a
    /// button can still be used as a modifier. Removing one takes that
    /// rebind off, which is the game's default back, not a cleared default.
    /// An action map or action the profile has no line for yet is made.
    /// </summary>
    public static XDocument ApplyBindings(XDocument source, IReadOnlyList<BindingChange> changes)
    {
        var document = new XDocument(source);
        var root = document.Root ?? throw new InvalidDataException("No root element.");
        var profile = root.Name.LocalName == "ActionMaps" && root.Element("ActionProfiles") is { } inner ? inner : root;

        foreach (var change in changes)
        {
            if (string.IsNullOrWhiteSpace(change.ActionMap) || string.IsNullOrWhiteSpace(change.Action) || string.IsNullOrWhiteSpace(change.Input)) continue;
            var input = ControlInput.Parse(change.Input);
            var map = profile.Elements("actionmap").FirstOrDefault(m => (string?)m.Attribute("name") == change.ActionMap);
            if (map is null)
            {
                if (change.Remove) continue;
                map = new XElement("actionmap", new XAttribute("name", change.ActionMap));
                profile.Add(map);
            }
            var action = map.Elements("action").FirstOrDefault(a => (string?)a.Attribute("name") == change.Action);
            if (action is null)
            {
                if (change.Remove) continue;
                action = new XElement("action", new XAttribute("name", change.Action));
                map.Add(action);
            }

            if (change.Remove)
            {
                foreach (var rebind in action.Elements("rebind").Where(r => string.Equals((string?)r.Attribute("input"), change.Input, StringComparison.OrdinalIgnoreCase)).ToList())
                    rebind.Remove();
            }
            else
            {
                // The game can retain duplicate direct inputs across action
                // maps, but which one fires is context-dependent and a
                // mapping editor has no honest way to present that as one
                // button. Move this exact input to the chosen action instead.
                foreach (var other in profile.Elements("actionmap")
                    .SelectMany(m => m.Elements("action"))
                    .Where(a => a != action).ToList())
                {
                    foreach (var rebind in other.Elements("rebind")
                        .Where(r => string.Equals((string?)r.Attribute("input"), change.Input, StringComparison.OrdinalIgnoreCase)).ToList())
                        rebind.Remove();
                    if (!other.HasElements) other.Remove();
                }

                // One binding per device on an action: the old one on this
                // device goes, one on a keyboard or another stick stays.
                foreach (var rebind in action.Elements("rebind").Where(r => ControlInput.Parse((string?)r.Attribute("input") ?? "").DeviceKey == input.DeviceKey).ToList())
                    rebind.Remove();
                var made = new XElement("rebind", new XAttribute("input", change.Input));
                if (!string.IsNullOrWhiteSpace(change.ActivationMode)) made.SetAttributeValue("activationMode", change.ActivationMode);
                action.Add(made);
            }

            // An action with nothing left is no line at all, as the game writes it.
            if (!action.HasElements) action.Remove();
            if (!map.HasElements) map.Remove();
        }

        return document;
    }

    /// <summary>One input string with its joystick instances moved; anything not a joystick is left alone.</summary>
    public static string Retarget(string input, IReadOnlyDictionary<int, int> moved)
    {
        // Each part of a chord on its own: a segment has no modifiers of its
        // own, so its raw text is the part and nothing more.
        return string.Join("+", input.Split('+').Select(segment =>
        {
            var part = ControlInput.Parse(segment);
            return part.IsJoystick && moved.TryGetValue(part.Instance, out var to) ? part.OnInstance(to) : segment;
        }));
    }

    /// <summary>
    /// A file name the game accepts: letters, digits, dash and underscore;
    /// the profile is then <c>pp_rebindkeys &lt;name&gt;</c> at the console.
    /// </summary>
    /// <summary>
    /// The filename the game will list and <c>pp_rebindkeys</c> will load.
    /// </summary>
    /// <remarks>
    /// The game's own export writes <c>layout_&lt;name&gt;_exported.xml</c> - the one
    /// file in this install's mappings folder is <c>layout_nick_exported.xml</c> - and
    /// the console command is given that filename, not a bare name. A profile written
    /// under any other name sits in the folder and is never listed, which fails
    /// silently: the app reported it had written one and printed a command that finds
    /// nothing. A name that already carries the prefix or the suffix keeps them rather
    /// than growing a second pair.
    /// </remarks>
    public static string MappingFileName(string name)
    {
        var safe = SafeName(name);
        if (safe.StartsWith("layout_", StringComparison.OrdinalIgnoreCase)) safe = safe["layout_".Length..];
        if (safe.EndsWith("_exported", StringComparison.OrdinalIgnoreCase)) safe = safe[..^"_exported".Length];
        safe = safe.Trim('_');
        if (safe.Length == 0) safe = "quantumwake";
        return $"layout_{safe}_exported.xml";
    }

    public static string SafeName(string name)
    {
        var chars = name.Trim().Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        var safe = new string(chars).Trim('_');
        return safe.Length > 0 ? safe : "quantumwake";
    }
}
