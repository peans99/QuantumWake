using System.IO.Compression;
using System.Text.Json;
using Quantumwake.Core.Controls;
using Quantumwake.Server;
namespace Quantumwake.Tests;
public class StreamDeckTests
{
    [Theory]
    [InlineData("preset/Flight/flight_ready", "Flight", "Flight Ready", "flight")]
    [InlineData("preset/Flight/exit_seat", "Flight", "Exit Seat", "seat")]
    [InlineData("preset/Mining/lights", "Mining", "Lights", "lights")]
    [InlineData("preset/Mining/ping", "Mining", "Ping", "scan")]
    [InlineData("preset/Mining/scan_mode", "Mining", "Scan Mode", "scan")]
    [InlineData("preset/Power/pip_shd_max", "Power", "SHD MAX", "shield")]
    [InlineData("preset/Power/pip_eng_max", "Power", "ENG MAX", "engine")]
    [InlineData("preset/Power/pip_wpn_max", "Power", "WPN MAX", "weapons")]
    [InlineData("preset/Salvage/salvage_fire_fracture", "Salvage", "Fire Fracture", "salvage")]
    [InlineData("spaceship_power/v_engineering_assignment_weapons_max", "Power", "Weapons MAX", "weapons")]
    [InlineData("spaceship_power/v_engineering_assignment_shields_max", "Power", "Shields MAX", "shield")]
    public void Function_icons_are_not_confused_by_category_names(string id, string group, string label, string icon)
    {
        Assert.Equal(icon, StreamDeckProfiles.Appearance(id, group, label).Icon);
    }
    private static ControlCatalogue Catalogue => new([new("ship", "Ship", "FLIGHT")], [new("ship", "gear", "Landing Gear", "", "n", "", "", "", "", "")]);
    [Fact] public void Rebound_keys_replace_defaults_and_explicitly_cleared_keys_stay_unbound()
    {
        var rebound = new ControlProfile("default", null, [], [new("ship", "gear", ControlInput.Parse("kb1_lalt+g"), null, null)]);
        Assert.Equal("lalt+g", StreamDeckProfiles.Commands(Catalogue, rebound)[0].Key);
        var cleared = rebound with { Bindings = [new("ship", "gear", ControlInput.Parse("kb1_ "), null, null)] };
        Assert.NotNull(StreamDeckProfiles.Commands(Catalogue, cleared)[0].Problem);
        Assert.Equal("n", StreamDeckProfiles.Commands(Catalogue, null)[0].Key);
    }
    [Theory] [InlineData("15",5,3)] [InlineData("xl",8,4)]
    public void Every_page_fits_the_device_and_every_folder_points_to_a_page(string model, int columns, int rows)
    {
        var commands = Enumerable.Range(0,80).Select(n => new DeckCommand("ship/"+n, "Ship", "Action " + n, "n", "", null)).ToList();
        using var bytes = new MemoryStream(StreamDeckProfiles.Build(model, commands, "C:/SC/PTU/Bin64/StarCitizen.exe"));
        using var zip = new ZipArchive(bytes);
        var sent = new List<string>(); var folders = new List<string>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.Contains("/Profiles/") && e.FullName.EndsWith("/manifest.json")))
        {
            using var doc = JsonDocument.Parse(entry.Open());
            var actions = doc.RootElement.GetProperty("Controllers")[0].GetProperty("Actions");
            foreach(var action in actions.EnumerateObject())
            {
                var slot=action.Name.Split(',').Select(int.Parse).ToArray(); Assert.InRange(slot[0],0,columns-1); Assert.InRange(slot[1],0,rows-1);
                var value=action.Value; var kind=value.GetProperty("UUID").GetString();
                if(kind=="com.nlebel.starcitizen.command") sent.Add(value.GetProperty("Settings").GetProperty("label").GetString()!);
                if(kind=="com.elgato.streamdeck.profile.openchild") folders.Add(value.GetProperty("Settings").GetProperty("ProfileUUID").GetString()!);
            }
        }
        Assert.Equal(80,sent.Distinct().Count());
        foreach(var folder in folders) Assert.Contains(zip.Entries,e => e.FullName.EndsWith("/Profiles/"+folder+"/manifest.json"));
    }

    [Fact] public void Folder_back_and_more_keys_each_have_their_own_picture_inside_their_page()
    {
        var commands = Enumerable.Range(0, 40).Select(n => new DeckCommand("ship/" + n, "Ship", "Action " + n, "n", "", null)).ToList();
        using var zip = new ZipArchive(new MemoryStream(StreamDeckProfiles.Build("15", commands, null)));
        var used = new Dictionary<string, string>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.Contains("/Profiles/") && e.FullName.EndsWith("/manifest.json")))
        {
            var page = entry.FullName[..^"manifest.json".Length];
            using var doc = JsonDocument.Parse(entry.Open());
            foreach (var action in doc.RootElement.GetProperty("Controllers")[0].GetProperty("Actions").EnumerateObject())
            {
                var state = action.Value.GetProperty("States")[0];
                if (!state.TryGetProperty("Image", out var image)) continue;
                Assert.Contains(zip.Entries, e => e.FullName == page + image.GetString());
                used[action.Value.GetProperty("Name").GetString() == "Back" ? "back" : action.Value.GetProperty("Name").GetString() == "More" ? "more" : "folder"] = image.GetString()!;
                Assert.Equal("bottom", state.GetProperty("TitleAlignment").GetString());
            }
        }
        Assert.Equal("Images/flight.png", used["folder"]);
        Assert.Equal("Images/back.png", used["back"]);
        Assert.Equal("Images/next.png", used["more"]);
    }

    [Fact] public void Exported_commands_keep_their_bindings_and_receive_distinct_function_icons()
    {
        var commands = new[] {
            new DeckCommand("ship/gear", "Flight", "Landing Gear", "rctrl+n", "", null),
            new DeckCommand("ship/v_self_destruct", "Ship & Utilities", "Self Destruct", "backspace", "delayed_press_medium", null, HoldFor: 0.5),
            new DeckCommand("mining/mining_power_up", "Mining", "Laser Power", "mwheel_up", "", null),
        };
        Assert.Equal("gear", commands[0].Appearance.Icon);
        Assert.Equal("warning", commands[1].Appearance.Icon);
        Assert.Equal("mining", commands[2].Appearance.Icon);
        Assert.NotEqual(commands[0].Appearance.Color, commands[2].Appearance.Color);
        using var zip = new ZipArchive(new MemoryStream(StreamDeckProfiles.Build("15", commands, null)));
        var keys = new Dictionary<string, JsonElement>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.Contains("/Profiles/") && e.FullName.EndsWith("manifest.json")))
        {
            using var doc = JsonDocument.Parse(entry.Open());
            foreach (var action in doc.RootElement.GetProperty("Controllers")[0].GetProperty("Actions").EnumerateObject())
            {
                if (action.Value.GetProperty("UUID").GetString() != "com.nlebel.starcitizen.command") continue;
                var settings = action.Value.GetProperty("Settings");
                keys[settings.GetProperty("label").GetString()!] = settings.Clone();
            }
        }
        Assert.Equal("gear", keys["Landing Gear"].GetProperty("icon").GetString());
        Assert.Equal("rctrl+n", keys["Landing Gear"].GetProperty("keyOverride").GetString());
        Assert.Equal("warning", keys["Self Destruct"].GetProperty("icon").GetString());
        Assert.Equal(0, keys["Self Destruct"].GetProperty("holdMs").GetInt32());
        Assert.Equal("backspace", keys["Self Destruct"].GetProperty("keyOverride").GetString());
    }
    [Fact] public void Mining_wheel_presets_use_the_games_keyboard_field_but_respect_removal()
    {
        var action = new ActionInfo("spaceship_mining", "v_increase_mining_throttle", "Mining power", "", "maxis_z", "", "", "", "press", "");
        var catalogue = new ControlCatalogue([], [action]);
        var wheel = StreamDeckProfiles.Commands(catalogue,null).Single(c => c.Id == "preset/Mining/mining_power_up");
        Assert.Equal("mwheel_up", wheel.Key); Assert.Null(wheel.Problem);
        var profile = new ControlProfile("default",null,[],[new(action.ActionMap,action.Name,ControlInput.Parse("kb1_ "),null,null)]);
        Assert.NotNull(StreamDeckProfiles.Commands(catalogue,profile).Single(c => c.Id == wheel.Id).Problem);
    }
    [Fact] public void Unsupported_inputs_and_activation_modes_are_explained()
    {
        Assert.Null(StreamDeckProfiles.Key(ControlInput.Parse("js1_button2")));
        Assert.Null(StreamDeckProfiles.Key(ControlInput.Parse("kb1_unknown")));
        var catalogue = Catalogue with { Actions = [Catalogue.Actions[0] with { ActivationMode = "double_tap" }], Modes = Modes };
        Assert.NotNull(StreamDeckProfiles.Commands(catalogue,null)[0].Problem);
    }

    // The rules as defaultProfile.xml defines them on this install (Alpha 4.10).
    private static readonly Dictionary<string, ActivationRule> Modes = new()
    {
        ["double_tap"] = new(true, false, false, MultiTap: 2),
        ["delayed_press_medium"] = new(true, false, false, PressTriggerThreshold: 0.5),
        ["tap"] = new(false, false, true, ReleaseTriggerThreshold: 0.25),
        ["hold"] = new(true, false, true),
    };

    [Fact] public void Delayed_actions_say_how_long_to_hold_rather_than_firing_on_a_touch()
    {
        ActionInfo Act(string name, string mode, ActivationRule? inline = null) =>
            new("ship", name, name, "", "n", "", "", "", mode, "", inline);
        var catalogue = new ControlCatalogue([new("ship", "Ship", "FLIGHT")], [
            Act("destruct", "delayed_press_medium"),
            // Decoy: launches on a press, sets and launches the burst after 0.25 s.
            Act("decoy", "", new ActivationRule(true, true, true, HoldTriggerDelay: 0.25)),
            // Shields - Set to Max (Hold): nothing until 0.25 s.
            Act("shields", "", new ActivationRule(false, true, false, HoldTriggerDelay: 0.25)),
            Act("pip", "tap"),
            Act("boost", "hold"),
        ], Modes);
        var commands = StreamDeckProfiles.Commands(catalogue, null).Where(c => !c.Preset).ToDictionary(c => c.Id.Split('/')[1]);

        Assert.Equal((0.5, false), (commands["destruct"].HoldFor, commands["destruct"].TapOrHold));
        Assert.Equal((0.25, true), (commands["decoy"].HoldFor, commands["decoy"].TapOrHold));
        Assert.Equal((0.25, false), (commands["shields"].HoldFor, commands["shields"].TapOrHold));
        Assert.Equal(0, commands["pip"].HoldFor);
        Assert.Equal(0, commands["boost"].HoldFor);
        // Nothing is stretched: the finger still decides, as on the keyboard.
        Assert.All(commands.Values, c => Assert.Equal(0, c.Hold));
        Assert.All(commands.Values, c => Assert.Null(c.Problem));
    }

    [Fact] public void A_preset_keeps_the_hold_note_unless_it_already_holds_or_taps()
    {
        var destruct = new ActionInfo("spaceship_general", "v_self_destruct", "Self Destruct", "", "backspace", "", "", "", "delayed_press_medium", "");
        var catalogue = new ControlCatalogue([new("spaceship_general", "General", "FLIGHT")], [destruct], Modes);
        var preset = StreamDeckProfiles.Commands(catalogue, null).Single(c => c.Id == "preset/Ship & Utilities/self_destruct");
        Assert.Equal(0.5, preset.HoldFor);
    }

    /// <summary>
    /// The server ships the packed .streamDeckPlugin, not the source beside it,
    /// so an edit to keys.js that was never repacked would ship the old sender
    /// without a word. Line endings are compared loosely: git checks text out
    /// as CRLF here and the pack was made from LF.
    /// </summary>
    [Fact] public void The_packed_plugin_is_built_from_the_source_beside_it()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        var deck = Path.Combine(dir!.FullName, "integrations", "streamdeck");
        var source = Path.Combine(deck, "com.nlebel.starcitizen.sdPlugin");
        using var zip = ZipFile.OpenRead(Path.Combine(deck, "com.nlebel.starcitizen.streamDeckPlugin"));
        static byte[] Text(byte[] bytes) => System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes).Replace("\r\n", "\n"));

        var packed = zip.Entries.Where(e => e.Length > 0 && !e.FullName.Contains("node_modules")).ToDictionary(e => e.FullName.Replace('\\', '/'));
        var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "node_modules" + Path.DirectorySeparatorChar)).ToList();
        Assert.Equal(packed.Keys.Order(), files.Select(f => "com.nlebel.starcitizen.sdPlugin/" + Path.GetRelativePath(source, f).Replace('\\', '/')).Order());
        foreach (var file in files)
        {
            var entry = packed["com.nlebel.starcitizen.sdPlugin/" + Path.GetRelativePath(source, file).Replace('\\', '/')];
            using var stream = entry.Open(); using var copy = new MemoryStream(); stream.CopyTo(copy);
            var binary = file.EndsWith(".png", StringComparison.OrdinalIgnoreCase);
            Assert.True((binary ? File.ReadAllBytes(file) : Text(File.ReadAllBytes(file))).AsSpan().SequenceEqual(binary ? copy.ToArray() : Text(copy.ToArray())),
                $"{entry.FullName} differs from the source: repack the plugin (integrations/streamdeck/README.md).");
        }

        using var manifest = JsonDocument.Parse(packed["com.nlebel.starcitizen.sdPlugin/manifest.json"].Open());
        Assert.Equal(StreamDeckProfiles.PluginVersion, manifest.RootElement.GetProperty("Version").GetString());
        // Single-file builds serve the embedded renderer. Keeping only the
        // loose browser file current would make the two hosts show different keys.
        foreach (var (resource, file) in new[] { ("art", "bin/key-art.js"), ("appearance", "data/appearance.json") })
        {
            using var embedded = typeof(StreamDeckProfiles).Assembly.GetManifestResourceStream("streamdeck." + resource);
            Assert.NotNull(embedded);
            using var copy = new MemoryStream(); embedded.CopyTo(copy);
            Assert.Equal(Text(File.ReadAllBytes(Path.Combine(source, file))), Text(copy.ToArray()));
        }
    }
}
