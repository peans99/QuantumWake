using System.Xml.Linq;
using Quantumwake.Core.Controls;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The keybinding backups, the diff between two of them, and the export
/// the game can import back - the swap fix included.
/// </summary>
public sealed class ControlsStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"qw-controls-{Guid.NewGuid():N}");
    private readonly ControlsStore _store;
    private readonly string _profile;

    private const string Live = """
        <ActionMaps>
         <ActionProfiles version="1" optionsVersion="2" rebindVersion="2" profileName="default">
          <deviceoptions name="Throttle - HOTAS Warthog  {0404044F-0000-0000-0000-504944564944}">
           <option input="z" deadzone="0.0495"/>
          </deviceoptions>
          <options type="keyboard" instance="1" Product="Keyboard  {6F1D2B61-D5A0-11CF-BFC7-444553540000}"/>
          <options type="joystick" instance="2" Product="Throttle - HOTAS Warthog  {0404044F-0000-0000-0000-504944564944}">
           <flight_strafe_forward exponent="2"/>
          </options>
          <options type="joystick" instance="3" Product="T-Pendular-Rudder  {B68F044F-0000-0000-0000-504944564944}"/>
          <modifiers />
          <actionmap name="seat_general">
           <action name="v_eject"><rebind input="js2_button7"/></action>
          </actionmap>
          <actionmap name="spaceship_movement">
           <action name="v_afterburner"><rebind input="js2_button5+js2_button11" activationMode="hold"/><rebind input="kb1_lshift"/></action>
           <action name="v_yaw"><rebind input="js3_x"/></action>
          </actionmap>
         </ActionProfiles>
        </ActionMaps>
        """;

    public ControlsStoreTests()
    {
        Directory.CreateDirectory(_root);
        _store = new ControlsStore(Path.Combine(_root, "backups"));
        _profile = Path.Combine(_root, "actionmaps.xml");
        File.WriteAllText(_profile, Live);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void A_snapshot_keeps_the_file_once_and_not_again_until_it_changes()
    {
        var first = _store.Snapshot(_profile)!.Value;
        Assert.True(first.Taken);
        Assert.Equal(8, first.Backup.Hash.Length);

        var again = _store.Snapshot(_profile)!.Value;
        Assert.False(again.Taken);
        Assert.Equal(first.Backup.Id, again.Backup.Id);
        Assert.Single(_store.Backups());

        File.WriteAllText(_profile, Live.Replace("js2_button7", "js2_button8"));
        var changed = _store.Snapshot(_profile)!.Value;
        Assert.True(changed.Taken);
        Assert.NotEqual(first.Backup.Hash, changed.Backup.Hash);

        var backups = _store.Backups();
        Assert.Equal(2, backups.Count);
        Assert.Equal(changed.Backup.Id, backups[0].Id);
        Assert.Contains("js2_button8", System.Text.Encoding.UTF8.GetString(_store.Read(backups[0].Id)!));
        Assert.Null(_store.Read("../actionmaps"));
    }

    [Fact]
    public void A_missing_or_empty_profile_is_not_a_backup()
    {
        Assert.Null(_store.Snapshot(Path.Combine(_root, "nope.xml")));
        File.WriteAllText(_profile, "");
        Assert.Null(_store.Snapshot(_profile));
        Assert.Empty(_store.Backups());
    }

    /// <summary>
    /// A curve is per option group on the stick's options line, a dead zone
    /// per axis on its deviceoptions block; a setting back at its default is
    /// removed, the way the game writes one put back.
    /// </summary>
    [Fact]
    public void Axis_settings_are_written_where_the_game_keeps_them_and_defaults_are_removed()
    {
        var changed = ControlsExport.ApplyAxes(XDocument.Parse(Live), 2, "Throttle - HOTAS Warthog", "{0404044F-0000-0000-0000-504944564944}",
            [new CurveSetting("flight_strafe_forward", 1, false), new CurveSetting("flight_throttle_abs", 1.75, true)],
            new Dictionary<string, double> { ["z"] = 0.12, ["x"] = 0 });
        var throttle = ControlProfile.Parse(changed).Devices.Single(d => d.Key == "js2");

        Assert.DoesNotContain(throttle.Curves, c => c.Option == "flight_strafe_forward");
        var abs = Assert.Single(throttle.Curves);
        Assert.Equal("flight_throttle_abs", abs.Option);
        Assert.Equal(1.75, abs.Exponent);
        Assert.True(abs.Inverted);
        Assert.Equal(0.12, throttle.Deadzones["z"], 6);
        Assert.False(throttle.Deadzones.ContainsKey("x"));

        // A stick the profile has no options line for gets one, with its product.
        var pedals = ControlsExport.ApplyAxes(XDocument.Parse(Live), 5, "T-Rudder", "{B679044F-0000-0000-0000-504944564944}",
            [new CurveSetting("flight_move_yaw", 0.5, false)], new Dictionary<string, double> { ["x"] = 0.02 });
        var js5 = ControlProfile.Parse(pedals).Devices.Single(d => d.Key == "js5");
        Assert.Equal("T-Rudder", js5.Product);
        Assert.Equal(0.5, Assert.Single(js5.Curves).Exponent);
        Assert.Equal(0.02, js5.Deadzones["x"], 6);
    }

    /// <summary>
    /// Setting a control on an action replaces that action's binding on the
    /// same stick and leaves its keyboard one; taking one off falls back to
    /// the game's own; an action with nothing left is no line at all.
    /// </summary>
    [Fact]
    public void Binding_changes_replace_per_device_and_remove_cleanly()
    {
        var changed = ControlsExport.ApplyBindings(XDocument.Parse(Live),
        [
            new BindingChange("spaceship_movement", "v_afterburner", "js2_button3"),
            new BindingChange("seat_general", "v_eject", "js2_button7", Remove: true),
            new BindingChange("spaceship_targeting", "v_target_cycle_all_fwd", "js3_button1", ActivationMode: "hold"),
        ]);
        var profile = ControlProfile.Parse(changed);

        var afterburner = profile.Bindings.Where(b => b.Action == "v_afterburner").Select(b => b.Input.Raw).Order().ToList();
        Assert.Equal(["js2_button3", "kb1_lshift"], afterburner);
        Assert.DoesNotContain(profile.Bindings, b => b.Action == "v_eject");
        Assert.Null(changed.Root!.Element("ActionProfiles")!.Elements("actionmap").FirstOrDefault(m => (string?)m.Attribute("name") == "seat_general"));
        var cycle = Assert.Single(profile.Bindings, b => b.Action == "v_target_cycle_all_fwd");
        Assert.Equal("js3_button1", cycle.Input.Raw);
        Assert.Equal("hold", cycle.ActivationMode);
        // The rest untouched.
        Assert.Equal("js3_x", Assert.Single(profile.Bindings, b => b.Action == "v_yaw").Input.Raw);
    }

    [Fact]
    public void Binding_an_exact_input_moves_it_off_the_other_action()
    {
        var changed = ControlsExport.ApplyBindings(XDocument.Parse(Live),
        [
            new BindingChange("spaceship_targeting", "v_target_cycle_all_fwd", "js2_button7"),
        ]);
        var profile = ControlProfile.Parse(changed);

        Assert.DoesNotContain(profile.Bindings, b => b.Action == "v_eject");
        var cycle = Assert.Single(profile.Bindings, b => b.Action == "v_target_cycle_all_fwd");
        Assert.Equal("js2_button7", cycle.Input.Raw);
        Assert.Contains(profile.Bindings, b => b.Input.Raw == "js2_button5+js2_button11");
    }

    [Fact]
    public void The_diff_names_what_moved_what_went_and_what_arrived()
    {
        var older = ControlProfile.Parse(XDocument.Parse(Live));
        var newer = ControlProfile.Parse(XDocument.Parse(Live
            .Replace("js2_button7", "js2_button8")
            .Replace("<action name=\"v_yaw\"><rebind input=\"js3_x\"/></action>", "<action name=\"v_pitch\"><rebind input=\"js3_y\"/></action>")));

        var changes = ControlsDiff.Between(older, newer);

        Assert.Equal(3, changes.Count);
        var eject = changes.Single(c => c.Action == "v_eject");
        Assert.Equal(["js2_button7"], eject.Before);
        Assert.Equal(["js2_button8"], eject.After);
        Assert.Empty(changes.Single(c => c.Action == "v_yaw").After);
        Assert.Empty(changes.Single(c => c.Action == "v_pitch").Before);
        // The afterburner is bound the same way in both; a chord and a key.
        Assert.DoesNotContain(changes, c => c.Action == "v_afterburner");
        Assert.Empty(ControlsDiff.Between(older, older));
    }

    /// <summary>
    /// A file brought back is kept once however often it is added, while
    /// the watch keeps an older version again when the game goes back to
    /// it - the list is a history of what the game had.
    /// </summary>
    [Fact]
    public void A_file_brought_back_is_kept_once_and_the_watch_keeps_history()
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(Live);
        var first = _store.Keep(bytes, DateTimeOffset.UtcNow);
        Assert.True(first.Taken);
        Assert.False(_store.Keep(bytes, DateTimeOffset.UtcNow).Taken);

        var other = System.Text.Encoding.UTF8.GetBytes(Live.Replace("js2_button7", "js2_button8"));
        Assert.True(_store.Keep(other, DateTimeOffset.UtcNow).Taken);
        // The same content as an older version, added as a file: not again.
        Assert.False(_store.Keep(bytes, DateTimeOffset.UtcNow).Taken);
        // But seen on disk by the watch, it is the game going back: kept again.
        Assert.True(_store.Keep(bytes, DateTimeOffset.UtcNow, againstLatestOnly: true).Taken);
        Assert.Equal(3, _store.Backups().Count);
    }

    /// <summary>
    /// An export from the mappings folder, restored, has to become the live
    /// frame the game reads at start; a copy of the live file already is.
    /// </summary>
    [Fact]
    public void An_export_is_reframed_as_the_live_profile_and_a_live_copy_is_left_alone()
    {
        var export = ControlsExport.Build(XDocument.Parse(Live), "nick");
        var live = ControlsExport.ToLive(export);

        Assert.Equal("ActionMaps", live.Root!.Name.LocalName);
        var profile = live.Root.Element("ActionProfiles")!;
        Assert.Equal("default", (string?)profile.Attribute("profileName"));
        Assert.Equal("2", (string?)profile.Attribute("rebindVersion"));
        Assert.Null(profile.Element("CustomisationUIHeader"));
        Assert.Equal(2, profile.Elements("actionmap").Count());
        Assert.Equal(4, ControlProfile.Parse(live).Bindings.Count);

        var already = ControlsExport.ToLive(XDocument.Parse(Live));
        Assert.Equal(XDocument.Parse(Live).ToString(), already.ToString());
    }

    [Fact]
    public void The_export_is_the_live_profile_in_the_games_import_frame()
    {
        var export = ControlsExport.Build(XDocument.Parse(Live), "restored");
        var root = export.Root!;

        Assert.Equal("ActionMaps", root.Name.LocalName);
        Assert.Equal("restored", (string?)root.Attribute("profileName"));
        Assert.Equal("2", (string?)root.Attribute("rebindVersion"));
        var header = root.Element("CustomisationUIHeader")!;
        Assert.Equal("restored", (string?)header.Attribute("label"));
        Assert.Equal(["keyboard 1", "mouse 1", "joystick 2", "joystick 3"],
            header.Element("devices")!.Elements().Select(e => $"{e.Name.LocalName} {(string?)e.Attribute("instance")}"));
        Assert.Null(root.Element("ActionProfiles"));
        Assert.Equal(2, root.Elements("actionmap").Count());
        Assert.NotNull(root.Element("deviceoptions"));

        // And it reads back as the same profile.
        var again = ControlProfile.Parse(export);
        Assert.Equal("restored", again.Name);
        Assert.Equal(4, again.Bindings.Count);
        Assert.Equal("Throttle - HOTAS Warthog", again.Joysticks.First().Product);
    }

    /// <summary>
    /// The sticks were re-enumerated: the throttle that was js2 is js3 now
    /// and the pedals js2. Every input and options line follows its device,
    /// a chord's two halves together, and the keyboard is left alone.
    /// </summary>
    [Fact]
    public void Retargeting_moves_every_binding_with_its_device()
    {
        var export = ControlsExport.Build(XDocument.Parse(Live), "swapped", new Dictionary<int, int> { [2] = 3, [3] = 2 });
        var profile = ControlProfile.Parse(export);

        Assert.Equal("Throttle - HOTAS Warthog", profile.Devices.Single(d => d.Key == "js3").Product);
        Assert.Equal("T-Pendular-Rudder", profile.Devices.Single(d => d.Key == "js2").Product);
        Assert.Equal("js3_button7", profile.Bindings.Single(b => b.Action == "v_eject").Input.Raw);
        Assert.Equal("js3_button5+js3_button11", profile.Bindings.First(b => b.Action == "v_afterburner").Input.Raw);
        Assert.Equal("kb1_lshift", profile.Bindings.Last(b => b.Action == "v_afterburner").Input.Raw);
        Assert.Equal("js2_x", profile.Bindings.Single(b => b.Action == "v_yaw").Input.Raw);
        Assert.Equal(["joystick 2", "joystick 3"],
            export.Root!.Element("CustomisationUIHeader")!.Element("devices")!.Elements("joystick").Select(e => $"joystick {(string?)e.Attribute("instance")}").Order());
    }

    [Fact]
    public void A_cleared_default_moves_too_and_the_file_name_is_made_safe()
    {
        Assert.Equal("js5_ ", ControlsExport.Retarget("js1_ ", new Dictionary<int, int> { [1] = 5 }));
        Assert.Equal("js1_button2", ControlsExport.Retarget("js1_button2", new Dictionary<int, int> { [2] = 1 }));
        Assert.Equal("nick_s_layout", ControlsExport.SafeName("nick's layout"));
        Assert.Equal("quantumwake", ControlsExport.SafeName("  "));
    }
}
