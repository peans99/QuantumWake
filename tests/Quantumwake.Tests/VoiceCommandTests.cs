using System.Xml.Linq;
using Quantumwake.Core.Controls;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Voice commands (1.2.0): the catalogue's firing rules, which key a command
/// presses and for how long, and what the copilot does with a heard phrase.
/// </summary>
/// <remarks>
/// The catalogue fixture is cut from this install's <c>defaultProfile.xml</c>
/// (Alpha 4.10) on 2026-11-07: three of its 18 activation modes, and the
/// actions the copilot presses in the shapes the file writes them.
/// </remarks>
public class VoiceCommandTests
{
    private const string Catalogue = """
        <profile version="1">
         <ActivationModes>
          <ActivationMode name="tap" onPress="0" onHold="0" onRelease="1" multiTap="1" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="0.25" releaseTriggerDelay="0" retriggerable="0"/>
          <ActivationMode name="press" onPress="1" onHold="0" onRelease="0" multiTap="1" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="-1" releaseTriggerDelay="0" retriggerable="0"/>
          <ActivationMode name="double_tap" onPress="1" onHold="0" onRelease="0" multiTap="2" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="-1" releaseTriggerDelay="0" retriggerable="0"/>
          <ActivationMode name="delayed_press_medium" onPress="1" onHold="0" onRelease="0" multiTap="1" multiTapBlock="1" pressTriggerThreshold="0.5" releaseTriggerThreshold="-1" releaseTriggerDelay="0" retriggerable="0"/>
         </ActivationModes>
         <actionmap name="seat_general">
          <action name="v_toggle_mining_mode" activationMode="press" keyboard="m"/>
          <action name="v_toggle_guns_mode" activationMode="press" keyboard=" " joystick=" "/>
         </actionmap>
         <actionmap name="spaceship_general">
          <action name="v_self_destruct" activationMode="delayed_press_medium" keyboard="backspace" joystick=" "><gamepad activationMode="delayed_press_medium" input=" "/></action>
          <action name="v_open_all_doors" activationMode="press" keyboard=" "/>
         </actionmap>
         <actionmap name="spaceship_movement">
          <action name="v_atc_request" activationMode="tap" keyboard="lalt+n"/>
         </actionmap>
         <actionmap name="spaceship_power">
          <action name="v_engineering_assignment_shields_max" onHold="1" holdTriggerDelay="0.25" keyboard="f7"/>
          <action name="v_engineering_assignment_shields_decrease" activationMode="tap" keyboard="f7+lalt"/>
         </actionmap>
         <actionmap name="spaceship_defensive">
          <action name="v_shield_raise_level_forward" activationMode="press" keyboard=" "/>
          <action name="v_shield_raise_level_back" activationMode="press" keyboard=" "/>
         </actionmap>
         <actionmap name="lights_controller">
          <action name="v_lights_stand_in" activationMode="press" keyboard="rctrl+np_1"/>
         </actionmap>
         <actionmap name="spaceship_mining">
          <action name="v_increase_mining_throttle" activationMode="press" keyboard="maxis_z"/>
          <action name="v_mining_use_consumable2" ActivationMode="press" keyboard="lalt+2"/>
         </actionmap>
         <actionmap name="prone">
          <action name="prone_rollleft"><keyboard activationMode="double_tap" input="q"/></action>
         </actionmap>
        </profile>
        """;

    private static readonly ControlCatalogue Game = ControlCatalogue.Parse(XDocument.Parse(Catalogue), new Dictionary<string, string>());

    private static ControlProfile Profile(string actionmaps) => ControlProfile.Parse(XDocument.Parse($"""
        <ActionMaps><ActionProfiles profileName="default" version="1">
         <options type="keyboard" instance="1"/>
         {actionmaps}
        </ActionProfiles></ActionMaps>
        """));

    private static VoiceCommand Command(string id) => VoiceCommands.Find(id)!;

    /* ---------- the catalogue's rules ---------- */

    [Fact]
    public void The_catalogue_reads_its_activation_modes_and_the_rules_spelled_out_inline()
    {
        Assert.Equal(4, Game.Modes!.Count);
        Assert.Equal(0.5, Game.Modes["delayed_press_medium"].PressTriggerThreshold);
        Assert.Equal(0.25, Game.Modes["tap"].ReleaseTriggerThreshold);

        // Shields to max names no mode; the rule is on the action itself.
        var shields = Game.Find("spaceship_power", "v_engineering_assignment_shields_max")!;
        Assert.Equal("", shields.ActivationMode);
        Assert.True(shields.Inline!.OnHold);
        Assert.Equal(0.25, shields.Inline.HoldTriggerDelay);

        // ActivationMode with a capital A is still the mode.
        Assert.Equal("press", Game.Find("spaceship_mining", "v_mining_use_consumable2")!.ActivationMode);

        // A keyboard default kept in a child element, with its own mode.
        var roll = Game.Find("prone", "prone_rollleft")!;
        Assert.Equal("q", roll.Keyboard);
        Assert.Equal(2, Game.RuleFor(roll).MultiTap);
    }

    [Fact]
    public void Each_rule_is_pressed_for_as_long_as_the_game_asks()
    {
        // Self destruct counts only past 0.5 s held; a little over that.
        Assert.Equal((0.7, 1), Rounded(VoiceBinding.Timing(Game.Modes!["delayed_press_medium"])));
        // Shields to max fires after a 0.25 s hold.
        Assert.Equal((0.45, 1), Rounded(VoiceBinding.Timing(Game.Find("spaceship_power", "v_engineering_assignment_shields_max")!.Inline!)));
        // A tap must come back up inside 0.25 s, and a double tap is two of them.
        Assert.True(VoiceBinding.Timing(Game.Modes["tap"]).Hold < 0.15);
        Assert.Equal(2, VoiceBinding.Timing(Game.Modes["double_tap"]).Taps);
        // A command that holds on purpose holds as long as it says.
        Assert.Equal(3, VoiceBinding.Timing(ActivationRule.Press, 3).Hold);

        static (double, int) Rounded((double Hold, int Taps) t) => (Math.Round(t.Hold, 2), t.Taps);
    }

    /* ---------- which key ---------- */

    [Fact]
    public void A_command_presses_the_games_default_key_when_the_pilot_never_changed_it()
    {
        var plan = VoiceBinding.Plan(Command("atc"), Game, null);
        Assert.True(plan.CanPress);
        Assert.Equal(["lalt", "n"], plan.Keys);
        Assert.Equal("LAlt + N", plan.Shown);
        Assert.False(plan.FromProfile);
    }

    [Fact]
    public void A_chord_the_game_writes_modifier_last_is_pressed_modifier_first()
    {
        // Shields Decrease is f7+lalt in the file. F7 down alone first is
        // Shields Increase - the opposite of what was asked for.
        var plan = VoiceBinding.Plan(Command("shieldsdown"), Game, null);
        Assert.Equal(["lalt", "f7"], plan.Keys);
        Assert.Equal("LAlt + F7", plan.Shown);
    }

    [Fact]
    public void Spare_keys_go_only_to_commands_with_no_key_and_skip_every_key_in_use()
    {
        // rctrl+np_1 is a game default (the stand-in light) and the pilot has
        // rctrl+np_2 on something: the first free key is rctrl+np_3.
        var profile = Profile("""
            <actionmap name="spaceship_movement"><action name="v_atc_request"><rebind input="kb1_rctrl+np_2"/></action></actionmap>
            """);
        var spare = VoiceBinding.SpareKeys(VoiceCommands.All, Game, profile);
        Assert.Equal(["guns", "opendoors", "shieldfront", "shieldback"], spare.Select(k => k.Command.Id));
        Assert.Equal("kb1_rctrl+np_3", spare[0].Input);
        Assert.Equal("RCtrl + Numpad 3", spare[0].Shown);
        Assert.Equal(spare.Count, spare.Select(k => k.Input).Distinct().Count());
        // A key the app cannot press (the mouse wheel) is not "no key": it is left alone.
        Assert.DoesNotContain(spare, k => k.Command.Id == "powerup");

        // With RCtrl bound on its own, holding it would fire that too: only RAlt is used.
        var lone = Profile("""
            <actionmap name="spaceship_movement"><action name="v_atc_request"><rebind input="kb1_rctrl"/></action></actionmap>
            """);
        Assert.All(VoiceBinding.SpareKeys(VoiceCommands.All, Game, lone), k => Assert.StartsWith("kb1_ralt+", k.Input));

        // And a key it gave is then the command's own.
        var given = Profile("""
            <actionmap name="seat_general"><action name="v_toggle_guns_mode"><rebind input="kb1_rctrl+np_3"/></action></actionmap>
            """);
        Assert.True(VoiceBinding.Plan(Command("guns"), Game, given).CanPress);
        Assert.DoesNotContain(VoiceBinding.SpareKeys(VoiceCommands.All, Game, given), k => k.Command.Id == "guns");
    }

    [Fact]
    public void The_pilots_own_keyboard_binding_wins_with_its_own_mode()
    {
        var profile = Profile("""
            <actionmap name="spaceship_movement"><action name="v_atc_request"><rebind input="kb1_rctrl+np_5" activationMode="double_tap"/></action></actionmap>
            """);
        var plan = VoiceBinding.Plan(Command("atc"), Game, profile);
        Assert.Equal(["rctrl", "np_5"], plan.Keys);
        Assert.Equal("RCtrl + Numpad 5", plan.Shown);
        Assert.Equal(2, plan.Taps);
        Assert.True(plan.FromProfile);
    }

    [Fact]
    public void A_cleared_key_or_one_on_a_stick_only_says_why_it_cannot_be_pressed()
    {
        var cleared = VoiceBinding.Plan(Command("mining"), Game, Profile("""
            <actionmap name="seat_general"><action name="v_toggle_mining_mode"><rebind input="kb1_ "/></action></actionmap>
            """));
        Assert.False(cleared.CanPress);
        Assert.Contains("cleared", cleared.Why);

        var stick = VoiceBinding.Plan(Command("guns"), Game, Profile("""
            <actionmap name="seat_general"><action name="v_toggle_guns_mode"><rebind input="js2_button10"/></action></actionmap>
            """));
        Assert.False(stick.CanPress);
        Assert.Contains("js2 button10", stick.Why);

        var wheel = VoiceBinding.Plan(Command("powerup"), Game, null);
        Assert.False(wheel.CanPress);
        Assert.Contains("mouse wheel", wheel.Why);

        var missing = VoiceBinding.Plan(Command("salvage"), Game, null);
        Assert.Contains("no v_toggle_salvage_mode action", missing.Why);
    }

    [Fact]
    public void Every_key_the_games_defaults_name_can_be_pressed_except_the_wheel_the_headset_and_pause()
    {
        // Every keyboard default on this install (Alpha 4.10), as --keys lists them.
        string[] used =
        [
            "lalt", "ralt", "a", "e", "d", "q", "space", "s", "w", "x", "up", "rctrl", "down", "right", "r", "left", "z", "lshift",
            "c", "1", "lctrl", "f", "b", "3", "2", "y", "tab", "t", "g", "f4", "4", "np_5", "n", "h", "backspace", "np_add", "mouse2",
            "mouse1", "m", "f1", "5", "v", "np_subtract", "i", "f11", "rshift", "rbracket", "o", "np_multiply", "np_divide", "np_6",
            "np_4", "np_3", "np_2", "np_1", "l", "j", "f9", "f2", "f10", "enter", "6", "u", "pgup", "pgdn", "p", "np_period", "np_9",
            "np_8", "np_7", "np_0", "mouse3", "lbracket", "k", "home", "f7", "f6", "f5", "escape", "end", "7", "0", "slash", "minus",
            "f8", "f3", "f12", "equals", "comma", "]", "9",
        ];
        Assert.DoesNotContain(used, k => !GameKeys.CanPress(k));
        Assert.False(GameKeys.CanPress("mwheel_up"));
        // The extended keys carry the flag that tells them from the numpad.
        Assert.True(GameKeys.Code("up")!.Value.Extended);
        Assert.False(GameKeys.Code("np_8")!.Value.Extended);
        Assert.Equal(GameKeys.Code("up")!.Value.Code, GameKeys.Code("np_8")!.Value.Code);
    }

    /* ---------- the copilot ---------- */

    private static readonly VoiceCommandSettings On = new(Enabled: true);
    private static readonly DateTimeOffset T0 = new(2026, 11, 7, 20, 0, 0, TimeSpan.Zero);

    private static HeardPhrase Said(string text, double seconds = 0, double confidence = 0.95) => new(text, confidence, T0.AddSeconds(seconds));

    [Fact]
    public void Asleep_it_acts_on_nothing_but_the_wake_phrase()
    {
        var copilot = new VoiceCopilot();
        Assert.Null(copilot.Hear(Said("mining mode"), On).Press);
        Assert.Equal("asleep", copilot.State(On, T0));

        var wake = copilot.Hear(Said(VoiceCommands.WakePhrase, 1), On);
        Assert.Equal("Copilot on", wake.Say);
        Assert.Equal("mining", copilot.Hear(Said("Mining Mode", 2), On).Press?.Id);

        copilot.Hear(Said(VoiceCommands.SleepShort, 3), On);
        Assert.Null(copilot.Hear(Said("mining mode", 4), On).Press);
    }

    [Fact]
    public void A_doubtful_match_is_not_acted_on_and_waking_needs_more_certainty_than_a_command()
    {
        var copilot = new VoiceCopilot();
        // 0.75 clears the 0.7 bar for a command but not the 0.8 one for waking.
        Assert.Null(copilot.Hear(Said(VoiceCommands.WakePhrase, 0, 0.75), On).Say);
        Assert.False(copilot.Awake);
        copilot.Hear(Said(VoiceCommands.WakePhrase, 1, 0.9), On);
        Assert.Null(copilot.Hear(Said("mining mode", 2, 0.5), On).Press);
        Assert.NotNull(copilot.Hear(Said("mining mode", 3, 0.75), On).Press);
    }

    [Fact]
    public void Self_destruct_waits_for_confirm_and_anything_else_stands_it_down()
    {
        var copilot = new VoiceCopilot();
        copilot.Hear(Said(VoiceCommands.WakePhrase), On);

        var asked = copilot.Hear(Said("self destruct", 1), On);
        Assert.Null(asked.Press);
        Assert.Equal("Confirm self destruct?", asked.Say);
        Assert.Equal("confirming", copilot.State(On, T0.AddSeconds(2)));
        Assert.Equal("selfdestruct", copilot.Hear(Said("confirm", 3), On).Press?.Id);

        copilot.Hear(Said("self destruct", 10), On);
        var cancelled = copilot.Hear(Said("cancel", 11), On);
        Assert.Null(cancelled.Press);
        Assert.Equal("Cancelled", cancelled.Say);
        Assert.Null(copilot.Hear(Said("confirm", 12), On).Press);

        // Too late: the window is six seconds.
        copilot.Hear(Said("self destruct", 20), On);
        Assert.Null(copilot.Hear(Said("confirm", 27), On).Press);

        // Jettison waits the same way.
        Assert.Null(copilot.Hear(Said("jettison cargo", 30), On).Press);
        Assert.Equal("jettison", copilot.Hear(Said("confirm", 31), On).Press?.Id);
    }

    [Fact]
    public void Push_to_talk_counts_while_held_and_a_beat_after_letting_go()
    {
        var settings = On with { Trigger = VoiceTrigger.Button };
        var copilot = new VoiceCopilot();
        // The wake phrase does nothing with push-to-talk only.
        copilot.Hear(Said(VoiceCommands.WakePhrase), settings);
        Assert.Null(copilot.Hear(Said("mining mode", 1), settings).Press);

        copilot.Button(true, T0.AddSeconds(2));
        Assert.Equal("listening", copilot.State(settings, T0.AddSeconds(2)));
        Assert.NotNull(copilot.Hear(Said("mining mode", 2.5), settings).Press);
        copilot.Button(false, T0.AddSeconds(3));
        // The recogniser hands the phrase over a moment after the words end.
        Assert.NotNull(copilot.Hear(Said("mining mode", 4), settings).Press);
        Assert.Null(copilot.Hear(Said("mining mode", 5), settings).Press);
    }

    [Fact]
    public void With_push_to_talk_a_confirm_said_with_the_button_up_cancels_instead_of_firing()
    {
        var settings = On with { Trigger = VoiceTrigger.Button };
        var copilot = new VoiceCopilot();
        copilot.Button(true, T0);
        Assert.Equal("Confirm self destruct?", copilot.Hear(Said("self destruct", 0.5), settings).Say);
        copilot.Button(false, T0.AddSeconds(1));

        // Four seconds later, inside the confirm window but well past the
        // button's grace: someone on comms says it.
        var stray = copilot.Hear(Said("confirm", 5), settings);
        Assert.Null(stray.Press);
        Assert.Equal("Cancelled", stray.Say);

        // Held, it confirms.
        copilot.Button(true, T0.AddSeconds(10));
        copilot.Hear(Said("self destruct", 10.5), settings);
        Assert.Equal("selfdestruct", copilot.Hear(Said("confirm", 11), settings).Press?.Id);
    }

    [Fact]
    public void A_command_switched_off_is_heard_but_not_pressed()
    {
        var settings = On with { Off = ["jettison"] };
        var copilot = new VoiceCopilot();
        copilot.Hear(Said(VoiceCommands.WakePhrase), settings);
        var heard = copilot.Hear(Said("jettison cargo", 1), settings);
        Assert.Null(heard.Press);
        Assert.Contains("switched off", heard.Outcome);
    }

    [Fact]
    public void Every_wording_belongs_to_one_command_only()
    {
        // Each pattern spelled out in full and matched against every command:
        // a wording two commands both claim would press whichever came first.
        Assert.Equal(VoiceCommands.All.Count, VoiceCommands.All.Select(c => c.Id).Distinct().Count());
        var control = new[] { VoiceCommands.WakePhrase, VoiceCommands.SleepPhrase, VoiceCommands.SleepShort, VoiceCommands.ConfirmPhrase, VoiceCommands.CancelPhrase };
        var patterns = VoiceCommands.All.SelectMany(c => c.Phrases.Select(p => (c.Id, Pattern: PhrasePattern.Parse(p)))).ToList();
        var clashes = new List<string>();
        var wordings = 0;
        foreach (var (id, pattern) in patterns)
        {
            foreach (var wording in pattern.Expand())
            {
                wordings++;
                var claimed = patterns.Where(x => x.Id != id && x.Pattern.Matches(wording)).Select(x => x.Id).Distinct().ToList();
                if (claimed.Count > 0) clashes.Add($"'{wording}' ({id}) is also {string.Join(", ", claimed)}");
                if (control.Contains(wording)) clashes.Add($"'{wording}' ({id}) is a control word");
                if (clashes.Count > 20) break;
            }
        }
        Assert.True(clashes.Count == 0, string.Join(Environment.NewLine, clashes));
        Assert.True(wordings > 5_000, $"only {wordings} wordings");
    }

    [Theory]
    [InlineData("power to shields", "shields")]
    [InlineData("full power to shield", "shields")]
    [InlineData("full power to the shields", "shields")]
    [InlineData("max shields", "shields")]
    [InlineData("shields up", "shieldsup")]
    [InlineData("increase shields", "shieldsup")]
    [InlineData("lower shields", "shieldsdown")]
    [InlineData("full shield back", "shieldback")]
    [InlineData("increase shield back", "shieldback")]
    [InlineData("boost the rear shields", "shieldback")]
    [InlineData("shields to the right", "shieldright")]
    [InlineData("shield starboard", "shieldright")]
    [InlineData("divert shields to port", "shieldleft")]
    [InlineData("full power to the front shields", "shieldfront")]
    [InlineData("Copilot, full power to the front shields, please.", "shieldfront")]
    [InlineData("balance shields", "shieldreset")]
    [InlineData("shields off", "shieldsoff")]
    [InlineData("turn on the shields", "shieldson")]
    [InlineData("gear down", "geardown")]
    [InlineData("lower the landing gear", "geardown")]
    [InlineData("switch to mining", "mining")]
    [InlineData("go to S C M mode", "combat")]
    [InlineData("request takeoff", "atc")]
    [InlineData("open the doors please", "opendoors")]
    [InlineData("dump the cargo", "jettison")]
    [InlineData("cancel self destruct", "abortdestruct")]
    public void The_ways_a_pilot_says_it_are_heard(string said, string id) =>
        Assert.Equal(id, VoiceCommands.ForPhrase(said)?.Id);

    [Theory]
    [InlineData("shields")]
    [InlineData("power")]
    [InlineData("copilot")]
    [InlineData("please")]
    [InlineData("shields to the side")]
    [InlineData("confirm please")]
    public void Words_that_are_no_command_are_not_one(string said) =>
        Assert.Null(VoiceCommands.ForPhrase(said));

    [Fact]
    public void The_recogniser_gets_manners_on_commands_but_never_on_confirm()
    {
        var grammar = VoiceCommands.Grammar();
        Assert.Contains(VoiceCommands.ConfirmPhrase, grammar);
        Assert.DoesNotContain(grammar, g => g.Contains("confirm") && g.Contains('['));
        Assert.Contains("[copilot] power to shields [please]", grammar);
        Assert.All(grammar, g => PhrasePattern.Parse(g));
    }

    [Fact]
    public void A_pattern_reads_its_brackets_and_refuses_empty_choices()
    {
        var p = PhrasePattern.Parse("(full|max) power to [the] (shield|shields)");
        Assert.Equal(8, p.Expand().Count);
        Assert.True(p.Matches("Max power to shield"));
        Assert.False(p.Matches("power to shield"));
        Assert.Equal(["max power to shield", "full power to the shields"], p.Examples(2));
        Assert.Throws<FormatException>(() => PhrasePattern.Parse("(full|) power"));
        Assert.Throws<FormatException>(() => PhrasePattern.Parse("[the shields"));
        Assert.Throws<FormatException>(() => PhrasePattern.Parse("(a]"));
    }

    [Fact]
    public void Abort_presses_at_once_only_after_a_self_destruct_confirmed_here()
    {
        var copilot = new VoiceCopilot();
        copilot.Hear(Said(VoiceCommands.WakePhrase), On);

        // No countdown this copilot knows of: the key would start one, so it asks.
        var cold = copilot.Hear(Said("abort self destruct", 1), On);
        Assert.Null(cold.Press);
        Assert.Equal("Confirm abort self destruct?", cold.Say);
        Assert.Equal("abortdestruct", copilot.Hear(Said("confirm", 2), On).Press?.Id);

        // After a confirmed self destruct, abort is immediate.
        copilot.Hear(Said("self destruct", 10), On);
        Assert.Equal("selfdestruct", copilot.Hear(Said("confirm", 11), On).Press?.Id);
        Assert.Equal("abortdestruct", copilot.Hear(Said("stop self destruct", 20), On).Press?.Id);

        // And once stopped, a second abort asks again.
        Assert.Null(copilot.Hear(Said("abort self destruct", 30), On).Press);
    }

    [Fact]
    public void Abort_while_self_destruct_waits_on_confirm_is_a_no()
    {
        var copilot = new VoiceCopilot();
        copilot.Hear(Said(VoiceCommands.WakePhrase), On);
        copilot.Hear(Said("self destruct", 1), On);
        var no = copilot.Hear(Said("cancel self destruct", 2), On);
        Assert.Null(no.Press);
        Assert.Equal(VoiceCommands.SayCancelled, no.Say);
        Assert.Null(copilot.Pending);
    }

    [Fact]
    public void Shields_off_waits_on_confirm()
    {
        var copilot = new VoiceCopilot();
        copilot.Hear(Said(VoiceCommands.WakePhrase), On);
        Assert.Equal("Confirm shields off?", copilot.Hear(Said("shields off", 1), On).Say);
        Assert.Equal("shieldson", copilot.Hear(Said("shields on", 10), On).Press?.Id);
    }

    [Fact]
    public void Every_sentence_the_copilot_says_is_in_bellas_recorded_pack()
    {
        // The pack the app embeds, read from the repository: a reply added to
        // VoiceCommands without its clip would be said in the Windows voice,
        // a different copilot halfway through a session.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props"))) dir = dir.Parent;
        Assert.NotNull(dir);
        using var manifest = File.OpenRead(Path.Combine(dir!.FullName, "src", "Quantumwake.Ocr", "Voices", "af_bella", "manifest.json"));
        var pack = VoicePack.Load(manifest)!;

        var missing = VoiceCommands.Spoken().Where(s => pack.Clips(s) is not { Count: 1 }).ToList();
        Assert.True(missing.Count == 0, "Not recorded as one clip: " + string.Join(", ", missing));
        Assert.Equal("Confirm self destruct?", VoiceCommands.Find("selfdestruct")!.Prompt);
    }

    [Fact]
    public void The_store_keeps_its_answer_and_drops_what_this_build_does_not_know()
    {
        var folder = Directory.CreateTempSubdirectory("qw-voice-").FullName;
        try
        {
            var store = new VoiceCommandStore(folder);
            Assert.False(store.Current.Enabled);
            store.Change(s => s with { Enabled = true, Trigger = "shout", Off = ["mining", "nonsense"], MinConfidence = 2 });
            var again = new VoiceCommandStore(folder).Current;
            Assert.True(again.Enabled);
            Assert.Equal(VoiceTrigger.Phrase, again.Trigger);
            Assert.Equal(["mining"], again.Off);
            Assert.Equal(0.95, again.MinConfidence);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
