using System.IO.Compression;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Quantumwake.Core.Controls;
using Quantumwake.Core.Logging;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <param name="Hold">Milliseconds the plugin keeps the key down at least, whatever the finger does.</param>
/// <param name="HoldFor">Seconds the game wants the key held before the action fires; 0 when a touch does it.</param>
/// <param name="TapOrHold">A tap fires the action and holding for <paramref name="HoldFor"/> fires its hold version.</param>
public sealed record DeckCommand(string Id, string Group, string Label, string Key, string Activation, string? Problem, bool Preset = false, int Hold = 0, bool? Tap = null,
    double HoldFor = 0, bool TapOrHold = false)
{
    public DeckAppearance Appearance => StreamDeckProfiles.Appearance(Id, Group, Label);
}
public sealed record DeckAppearance(string Icon, string Color);
public sealed record DeckExport(string Model, string[] Actions, bool Defaults = false);

public static class StreamDeckProfiles
{
    /// <summary>
    /// The bundled plugin's manifest version: profiles name it, and Open in
    /// Stream Deck installs the bundled copy when an older one is in place.
    /// A test holds it to the packed manifest.
    /// </summary>
    public const string PluginVersion = "1.1.3.0";

    private sealed record AppearanceRule(string Icon, string? Color, string[] Match);
    private sealed record DeckPalette(DeckAppearance Fallback, AppearanceRule[] Groups, AppearanceRule[] Actions);
    private static readonly DeckPalette Palette = JsonSerializer.Deserialize<DeckPalette>(Resource("appearance"),
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    private static bool Matches(string text, AppearanceRule rule) => rule.Match.Any(word => text.Contains(word, StringComparison.OrdinalIgnoreCase));
    public static DeckAppearance GroupAppearance(string group)
    {
        var rule = Palette.Groups.FirstOrDefault(rule => Matches(group, rule));
        return rule is null ? Palette.Fallback : new DeckAppearance(rule.Icon, rule.Color!);
    }
    public static DeckAppearance Appearance(string id, string group, string label)
    {
        var baseLook = GroupAppearance(group);
        // A preset id contains its category. Matching that would give Flight
        // commands a lamp ("light" inside "flight"), and every Mining key a pick.
        var text = id.Split('/')[^1] + " " + label.Replace('\n', ' ');
        var action = Palette.Actions.FirstOrDefault(rule => Matches(text, rule));
        return action is null ? baseLook : new DeckAppearance(action.Icon, action.Color ?? baseLook.Color);
    }

    public static string FolderTitle(string text)
    {
        var words = text.Replace("Vehicles - ", "").Replace("Flight - ", "").Split(' ');
        var lines = new List<string>(); var line = "";
        foreach (var word in words) { if (line.Length > 0 && line.Length + word.Length + 1 > 12) { lines.Add(line); line = word; } else line += (line.Length > 0 ? " " : "") + word; }
        if (line.Length > 0) lines.Add(line);
        return string.Join('\n', lines.Take(3));
    }

    // Keep this list aligned with the scan-code sender in integrations/streamdeck/bin/keys.js.
    private static readonly HashSet<string> Keys = new(("escape minus equals backspace tab lbracket rbracket enter lctrl semicolon apostrophe grave lshift backslash comma period slash rshift np_multiply lalt space capslock numlock scrolllock np_subtract np_add np_period np_enter rctrl np_divide ralt home up pgup left right end down pgdn insert delete mouse1 mouse2 mouse3 mwheel_up mwheel_down " + string.Join(' ', "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()) + " " + string.Join(' ', Enumerable.Range(1, 24).Select(n => $"f{n}")) + " " + string.Join(' ', Enumerable.Range(0, 10).Select(n => $"np_{n}"))).Split(' '), StringComparer.OrdinalIgnoreCase);

    public static string? Key(ControlInput input)
    {
        if (input.Kind == InputKind.None || input.Device is not ("" or "kb" or "mo")) return null;
        var parts = input.Modifiers.Select(Key).Append(Keys.Contains(input.Name) ? input.Name.ToLowerInvariant() : null).ToArray();
        return parts.Any(p => p is null) ? null : string.Join('+', parts);
    }

    public static List<DeckCommand> Commands(ControlCatalogue catalogue, ControlProfile? profile)
    {
        var commands = catalogue.Actions.Select(action =>
        {
            var overrides = profile?.Bindings.Where(b => b.ActionMap == action.ActionMap && b.Action == action.Name).ToList() ?? [];
            string? Resolve(string device, string fallback)
            {
                var replacements = overrides.Where(b => (b.Input.Device.Length > 0 ? b.Input.Device : b.Input.Modifiers.FirstOrDefault(m => m.Device.Length > 0)?.Device) == device).ToList();
                return replacements.Count > 0 ? replacements.Select(b => Key(b.Input)).FirstOrDefault(k => k is not null)
                    : Key(ControlInput.Parse(fallback));
            }
            var key = Resolve("kb", action.Keyboard) ?? Resolve("mo", action.Mouse);
            var binding = overrides.FirstOrDefault(b => Key(b.Input) == key);
            var activation = binding?.ActivationMode ?? action.ActivationMode;
            // The game's own rule for the mode, as voice commands read it, rather
            // than a list of mode names: the game defines 18, and 260 actions name
            // none and spell their rule out inline.
            var rule = catalogue.RuleFor(action, binding?.ActivationMode);
            // The plugin mirrors the finger, as the keyboard does, so a mode that
            // waits for a held key keeps waiting. That is deliberate: self destruct's
            // half second is its safety catch, and Decoy fires one thing on a tap and
            // another after 0.25 s. Stretching every touch to the threshold would make
            // a brushed button fire the first and take the tap away from the second,
            // so the page says how long to hold instead (1.2.9).
            var holdFor = rule.PressTriggerThreshold > 0 ? rule.PressTriggerThreshold
                : rule.OnHold && rule.HoldTriggerDelay > 0 ? rule.HoldTriggerDelay : 0;
            var tapOrHold = rule.PressTriggerThreshold <= 0 && holdFor > 0 && (rule.OnPress || rule.OnRelease);
            var problem = key is null ? "No supported keyboard or mouse binding. Bind this action in Star Citizen, then refresh."
                : binding?.MultiTap > 1 || rule.MultiTap > 1 ? "Multi-tap bindings are not supported by the Stream Deck sender." : null;
            return new DeckCommand(action.ActionMap + "/" + action.Name,
                catalogue.ActionMaps.FirstOrDefault(m => m.Name == action.ActionMap)?.Label ?? action.ActionMap,
                action.Label, key ?? "", activation, problem, HoldFor: holdFor, TapOrHold: tapOrHold);
        }).ToList();
        var presets = JsonSerializer.Deserialize<List<PresetCommand>>(Resource("presets"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        foreach (var preset in presets)
        {
            var action = commands.FirstOrDefault(c => c.Id == preset.Action);
            if (preset.WheelDirection != 0 && action is not null)
            {
                var parts = preset.Action.Split('/');
                var original = catalogue.Find(parts[0], parts[1]);
                var replacement = profile?.Bindings.FirstOrDefault(b => b.ActionMap == parts[0] && b.Action == parts[1] && (b.Input.Raw.StartsWith("mo", StringComparison.OrdinalIgnoreCase) || b.Input.Raw.StartsWith("kb", StringComparison.OrdinalIgnoreCase)));
                var axis = replacement?.Input ?? ControlInput.Parse(string.IsNullOrWhiteSpace(original?.Mouse) ? original?.Keyboard ?? "" : original.Mouse);
                if (axis.Name == "maxis_z" && axis.Modifiers.All(m => Key(m) is not null))
                    action = action with { Key = string.Join('+', axis.Modifiers.Select(m => Key(m)).Append(preset.WheelDirection > 0 ? "mwheel_up" : "mwheel_down")), Problem = null, Activation = "press", HoldFor = 0, TapOrHold = false };
            }
            // A preset that taps, or already holds past the threshold, fires the same
            // way whatever the finger does, so there is nothing left to tell.
            var settled = action is not null && (preset.Tap || preset.Hold >= action.HoldFor * 1000);
            commands.Add(action is null ? new DeckCommand("preset/" + preset.Id, preset.Group, preset.Label, "", "", "This command could not be matched to a supported game action.", true)
                : action with { Id = "preset/" + preset.Id, Group = preset.Group, Label = preset.Label, Preset = true, Hold = preset.Hold, Tap = preset.Tap,
                    HoldFor = settled ? 0 : action.HoldFor, TapOrHold = !settled && action.TapOrHold });
        }
        return commands;
    }
    private sealed record PresetCommand(string Id, string Action, string Group, string Label, int Hold, bool Tap, int WheelDirection = 0);

    private static ControlProfile? Profile(GameInstall? install)
    {
        var path = install is null ? null : ControlsWatchService.ProfilePath(install);
        if (path is null || !File.Exists(path)) return null;
        // A partially written profile must fail the request rather than silently export defaults.
        return ControlProfile.Parse(File.ReadAllBytes(path));
    }

    public static void Map(WebApplication app, GameInstall? install)
    {
        app.MapGet("/api/streamdeck/art.js", () => Results.Bytes(Resource("art"), "text/javascript; charset=utf-8"));
        app.MapGet("/api/streamdeck", (LogLibrary lib, bool? defaults) =>
        {
            try
            {
                var profile = defaults == true ? null : Profile(install);
                var commands = Commands(lib.GameCommodities.Controls.Catalogue, profile);
                return Results.Ok(new { ready = !lib.GameCommodities.Controls.Catalogue.IsEmpty, profileFound = profile is not null,
                    commands, groups = commands.Select(c => c.Group).Distinct().Select(name => new { name, title = FolderTitle(name), appearance = GroupAppearance(name) }) });
            }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or InvalidDataException or System.Xml.XmlException)
            { return Results.BadRequest(new { message = "The current keybindings could not be read. Try again after the game finishes saving them." }); }
        });
        app.MapPost("/api/streamdeck/profile", (DeckExport request, LogLibrary lib) =>
        {
            try
            {
                var commands = Commands(lib.GameCommodities.Controls.Catalogue, request.Defaults ? null : Profile(install));
                var ids = request.Actions?.ToHashSet(StringComparer.Ordinal) ?? [];
                var selected = commands.Where(c => ids.Contains(c.Id)).ToList();
                if (ids.Count == 0 || selected.Count != ids.Count || selected.Any(c => c.Problem is not null))
                    return Results.BadRequest(new { message = "Select commands with supported bindings. Refresh if your bindings have changed." });
                return Results.File(Build(request.Model, selected, install is null ? null : Path.Combine(install.RootPath, "Bin64", "StarCitizen.exe")),
                    "application/octet-stream", $"QuantumWake-StarCitizen-{request.Model}.streamDeckProfile");
            }
            catch (ArgumentException) { return Results.BadRequest(new { message = "Choose a 15-key or XL Stream Deck." }); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or InvalidDataException or System.Xml.XmlException)
            { return Results.BadRequest(new { message = "The current keybindings could not be read. Refresh and try again." }); }
        });
        app.MapPost("/api/streamdeck/open", (DeckExport request, LogLibrary lib) =>
        {
            if (!OperatingSystem.IsWindows()) return Results.BadRequest(new { message = "Opening Stream Deck is available on Windows only. Download the profile instead." });
            try
            {
                var commands = Commands(lib.GameCommodities.Controls.Catalogue, request.Defaults ? null : Profile(install));
                var ids = request.Actions?.ToHashSet(StringComparer.Ordinal) ?? [];
                var selected = commands.Where(c => ids.Contains(c.Id)).ToList();
                if (ids.Count == 0 || selected.Count != ids.Count || selected.Any(c => c.Problem is not null))
                    return Results.BadRequest(new { message = "Select commands with supported bindings. Refresh if your bindings have changed." });
                var bytes = Build(request.Model, selected, install is null ? null : Path.Combine(install.RootPath, "Bin64", "StarCitizen.exe"));
                var manifest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elgato", "StreamDeck", "Plugins", "com.nlebel.starcitizen.sdPlugin", "manifest.json");
                var pluginReady = false;
                if (File.Exists(manifest))
                {
                    try { using var doc = JsonDocument.Parse(File.ReadAllBytes(manifest)); pluginReady = Version.TryParse(doc.RootElement.GetProperty("Version").GetString(), out var version) && version >= new Version(PluginVersion); }
                    catch (Exception e) when (e is JsonException or KeyNotFoundException) { }
                }
                var folder = Path.Combine(Path.GetTempPath(), "QuantumWake-StreamDeck"); Directory.CreateDirectory(folder);
                if (!pluginReady)
                {
                    var installer = Path.Combine(folder, "com.nlebel.starcitizen.streamDeckPlugin"); File.WriteAllBytes(installer, Resource("plugin"));
                    Process.Start(new ProcessStartInfo(installer) { UseShellExecute = true });
                    return Results.Ok(new { message = "Plugin installer opened in Stream Deck. Finish installing it, then click Open in Stream Deck again to import your profile." });
                }
                var file = Path.Combine(folder, "QuantumWake-" + Guid.NewGuid().ToString("N") + ".streamDeckProfile");
                File.WriteAllBytes(file, bytes);
                Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
                return Results.Ok(new { message = "Profile sent to Stream Deck. Complete its import prompt to choose your device." });
            }
            catch (ArgumentException) { return Results.BadRequest(new { message = "Choose a 15-key or XL Stream Deck." }); }
            catch (Exception e) when (e is UnauthorizedAccessException or IOException or InvalidDataException or System.Xml.XmlException or System.ComponentModel.Win32Exception)
            { return Results.BadRequest(new { message = "Stream Deck could not open the profile. Install Stream Deck and its companion plugin, or use Download profile." }); }
        });
        app.MapGet("/api/streamdeck/plugin", () => Results.File(Resource("plugin"), "application/octet-stream", "com.nlebel.starcitizen.streamDeckPlugin"));
    }

    private static byte[] Resource(string name)
    {
        using var stream = typeof(StreamDeckProfiles).Assembly.GetManifestResourceStream("streamdeck." + name)!;
        using var memory = new MemoryStream(); stream.CopyTo(memory); return memory.ToArray();
    }

    public static byte[] Build(string model, IReadOnlyList<DeckCommand> commands, string? executable)
    {
        var (columns, rows, device) = model switch { "15" => (5, 3, "20GAA9902"), "xl" => (8, 4, "20GAT9901"), _ => throw new ArgumentException("model") };
        var capacity = columns * rows;
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            var identity = SHA256.HashData(Encoding.UTF8.GetBytes("QuantumWake-StarCitizen-" + model));
            var root = new Guid(identity.AsSpan(0, 16)).ToString("D").ToUpperInvariant() + ".sdProfile/";
            string Id() => Guid.NewGuid().ToString("D").ToUpperInvariant();
            void Bytes(string path, byte[] bytes) { using var s = zip.CreateEntry(root + path).Open(); s.Write(bytes); }
            void Json(string path, object value) => Bytes(path, JsonSerializer.SerializeToUtf8Bytes(value));
            object Command(DeckCommand c) => new { ActionID = Id(), LinkedTitle = true, Name = "SC Command",
                Plugin = new { Name = "Star Citizen Commands", UUID = "com.nlebel.starcitizen", Version = PluginVersion },
                Settings = new { command = "flight_ready", keyOverride = c.Key, label = c.Label, holdMs = c.Hold, tap = c.Tap ?? (c.Activation == "tap"), color = c.Appearance.Color, icon = c.Appearance.Icon },
                State = 0, States = new[] { new { ShowTitle = false } }, UUID = "com.nlebel.starcitizen.command" };
            // The art carries no words: the first folder.png had "Ship & Utilities"
            // drawn into it, and every folder's own title landed on top of that,
            // two labels over each other on every folder key (1.2.10). The emblem
            // fills the top half, so the title sits below it.
            object Folder(string child, string title, bool back = false, bool more = false)
            {
                var shown = FolderTitle(title);
                var appearance = GroupAppearance(title);
                return new { ActionID = Id(), LinkedTitle = true, Name = title,
                    Settings = back ? (object)new { } : new { ProfileUUID = child }, State = 0,
                    States = new[] { new { Image = back ? "Images/back.png" : more ? "Images/next.png" : $"Images/{appearance.Icon}.png", ShowTitle = true, Title = shown,
                        TitleColor = "#ffffff", FontSize = shown.Count(ch => ch == '\n') >= 2 ? 10 : shown.Contains('\n') ? 12 : 14, FontStyle = "Bold", TitleAlignment = "bottom" } },
                    UUID = back ? "com.elgato.streamdeck.profile.backtoparent" : "com.elgato.streamdeck.profile.openchild" };
            }
            void Page(string id, string title, List<object> items, bool back)
            {
                var actions = new Dictionary<string, object>(); var at = 0;
                string Slot(int n) => $"{n % columns},{n / columns}";
                if (back) actions[Slot(at++)] = Folder("", "Back", true);
                var take = capacity - at;
                if (items.Count > take) take--;
                foreach (var item in items.Take(take)) actions[Slot(at++)] = item;
                if (items.Count > take) { var next = Id(); actions[Slot(capacity - 1)] = Folder(next, "More", more: true); Page(next, title, items.Skip(take).ToList(), true); }
                Json($"Profiles/{id}/manifest.json", new { Controllers = new[] { new { Actions = actions, Type = "Keypad" } }, Icon = "", Name = title });
                foreach (var image in new[] { "folder", "back", "next" })
                    Bytes($"Profiles/{id}/Images/{image}.png", Resource(image));
                foreach (var icon in Palette.Groups.Select(g => g.Icon).Append(Palette.Fallback.Icon).Distinct())
                    Bytes($"Profiles/{id}/Images/{icon}.png", Resource("icon." + icon));
            }
            var home = Id(); var template = Id(); var folders = new List<object>();
            foreach (var group in commands.GroupBy(c => c.Group)) { var page = Id(); Page(page, group.Key, group.Select(Command).ToList(), true); folders.Add(Folder(page, group.Key)); }
            Page(home, "Star Citizen", folders, false); Page(template, "", [], false);
            Json("manifest.json", new { AppIdentifier = executable ?? "", Device = new { Model = device, UUID = "" }, Name = "Quantum Wake · Star Citizen", Pages = new { Current = home, Default = template, Pages = new[] { home } }, Version = "3.0" });
        }
        return memory.ToArray();
    }
}
