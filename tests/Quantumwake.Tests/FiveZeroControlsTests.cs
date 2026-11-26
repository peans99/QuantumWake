using System.Xml.Linq;
using Quantumwake.Core.Controls;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// What the 5.0 PTU (5.0.202.46035) changed in the controls, against LIVE
/// 4.10.201: nine door and port-lock actions moved from <c>spaceship_general</c>
/// and <c>vehicle_general</c> to <c>seat_general</c>, and eleven actions went
/// from <c>press</c> to <c>tap</c> - the nine operator-mode toggles, the door
/// toggle and the port-lock toggle. Nothing else moved or changed its mode.
/// </summary>
public class FiveZeroControlsTests
{
    // Groups and modes as the PTU's defaultProfile.xml writes them (tap fires
    // on a release inside a quarter of a second); the keys are placeholders
    // except M, which is the game's own for the industrial cycle.
    private static ControlCatalogue Ptu => ControlCatalogue.Parse(XDocument.Parse("""
        <profile version="1">
         <ActivationModes>
          <ActivationMode name="press" onPress="1" onHold="0" onRelease="0" multiTap="1" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="-1" releaseTriggerDelay="0" retriggerable="0"/>
          <ActivationMode name="tap" onPress="0" onHold="0" onRelease="1" multiTap="1" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="0.25" releaseTriggerDelay="0" retriggerable="0"/>
         </ActivationModes>
         <actionmap name="seat_general">
          <action name="v_cycle_industrial_mode" activationMode="tap" keyboard="m"/>
          <action name="v_toggle_mining_mode" activationMode="tap" keyboard="ralt+np_1"/>
          <action name="v_toggle_salvage_mode" activationMode="tap" keyboard="ralt+np_2"/>
          <action name="v_toggle_scan_mode" activationMode="tap" keyboard="v"/>
          <action name="v_toggle_quantum_mode" activationMode="tap" keyboard="b"/>
          <action name="v_toggle_all_doors" activationMode="tap" keyboard=" "/>
          <action name="v_open_all_doors" activationMode="press" keyboard=" "/>
         </actionmap>
        </profile>
        """), new Dictionary<string, string>());

    [Fact]
    public void A_door_key_the_pilot_bound_under_the_new_group_is_the_one_voice_presses()
    {
        // The voice command still names spaceship_general, which is right for
        // LIVE; the catalogue finds the action by name and the profile binding
        // is matched under either group.
        var profile = new ControlProfile("default", null, [],
            [new("seat_general", "v_open_all_doors", ControlInput.Parse("kb1_ralt+np_7"), null, null)]);

        var plan = VoiceBinding.Plan(VoiceCommands.Find("opendoors")!, Ptu, profile);

        Assert.True(plan.CanPress);
        Assert.True(plan.FromProfile);
        Assert.Equal(["ralt", "np_7"], plan.Keys);
    }

    [Fact]
    public void A_voice_press_of_a_tap_action_lets_go_inside_the_tap_window()
    {
        var plan = VoiceBinding.Plan(VoiceCommands.Find("industrial")!, Ptu, null);

        Assert.True(plan.CanPress);
        Assert.True(plan.HoldSeconds < 0.25);
    }

    [Theory]
    [InlineData("Flight/quantum_mode")]
    [InlineData("Mining/mining_mode")]
    [InlineData("Mining/next_mode")]
    [InlineData("Mining/scan_mode")]
    [InlineData("Salvage/salvage_mode")]
    [InlineData("Salvage/next_mode")]
    [InlineData("Salvage/scan_mode")]
    [InlineData("Scan/scan_mode")]
    public void The_mode_keys_send_a_tap_so_a_slow_finger_does_not_miss_the_window(string preset)
    {
        // The deck mirrors the finger. On 5.0 a mode toggle fires only on a
        // release within 0.25 s, so a press held a moment longer did nothing;
        // a fixed tap fires on both LIVE's press and 5.0's tap.
        var commands = StreamDeckProfiles.Commands(Ptu, null);

        Assert.Equal(true, commands.Single(c => c.Id == "preset/" + preset).Tap);
    }
}
