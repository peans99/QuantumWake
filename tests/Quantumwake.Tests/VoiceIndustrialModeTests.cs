using System.Xml.Linq;
using Quantumwake.Core.Controls;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// "Next mode" (1.2.25): the 4.10.3 PTU (4.10.200, surveyed 2026-11-10) takes
/// M off mining and salvage mode and puts it on <c>v_cycle_industrial_mode</c>.
/// </summary>
public class VoiceIndustrialModeTests
{
    // The three actions as the PTU's catalogue has them, and as LIVE 4.10.2's.
    private static ControlCatalogue Catalogue(bool ptu) => ControlCatalogue.Parse(XDocument.Parse($"""
        <profile version="1">
         <ActivationModes>
          <ActivationMode name="press" onPress="1" onHold="0" onRelease="0" multiTap="1" multiTapBlock="1" pressTriggerThreshold="-1" releaseTriggerThreshold="-1" releaseTriggerDelay="0" retriggerable="0"/>
         </ActivationModes>
         <actionmap name="seat_general">
          <action name="v_toggle_mining_mode" activationMode="press" keyboard="{(ptu ? " " : "m")}"/>
          <action name="v_toggle_salvage_mode" activationMode="press" keyboard="{(ptu ? " " : "m")}"/>
          {(ptu ? """<action name="v_cycle_industrial_mode" activationMode="press" keyboard="m"/>""" : "")}
         </actionmap>
        </profile>
        """), new Dictionary<string, string>());

    [Theory]
    [InlineData("next mode")]
    [InlineData("next industrial mode")]
    [InlineData("cycle industrial modes")]
    [InlineData("switch operator mode")]
    [InlineData("copilot cycle mode please")]
    public void The_ways_of_saying_it_are_the_one_command(string said) =>
        Assert.Equal("industrial", VoiceCommands.ForPhrase(said)?.Id);

    [Fact]
    public void Mining_and_salvage_mode_are_still_themselves()
    {
        Assert.Equal("mining", VoiceCommands.ForPhrase("mining mode")?.Id);
        Assert.Equal("salvage", VoiceCommands.ForPhrase("switch to salvage")?.Id);
    }

    [Fact]
    public void On_the_ptu_it_presses_m_and_mining_mode_asks_for_a_spare_key()
    {
        var game = Catalogue(ptu: true);

        var next = VoiceBinding.Plan(VoiceCommands.Find("industrial")!, game, null);
        Assert.True(next.CanPress);
        Assert.Equal(["m"], next.Keys);

        var mining = VoiceBinding.Plan(VoiceCommands.Find("mining")!, game, null);
        Assert.False(mining.CanPress);
        Assert.True(mining.NeedsKey);
    }

    [Fact]
    public void On_live_it_says_the_game_has_no_such_action_and_presses_nothing()
    {
        var next = VoiceBinding.Plan(VoiceCommands.Find("industrial")!, Catalogue(ptu: false), null);

        Assert.False(next.CanPress);
        Assert.Contains("no v_cycle_industrial_mode action", next.Why);
        Assert.True(VoiceBinding.Plan(VoiceCommands.Find("mining")!, Catalogue(ptu: false), null).CanPress);
    }
}
