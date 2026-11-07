using Quantumwake.Core.Events;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// 1.1.14: every sound its own switch, the HUD's missile warning, territory
/// banner and mission health, and the game's mission toasts said aloud.
/// </summary>
public sealed class CalloutSwitchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-callouts-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset T0 = new(2026, 11, 6, 3, 55, 0, TimeSpan.Zero);

    public CalloutSwitchTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    private static ScreenTextLine Line(string text, double left, double top, double height = 30) => new(text, left, top, height);

    // ---- the switches ----

    [Theory]
    [InlineData("online")]
    [InlineData("identified")]
    [InlineData("likely")]
    [InlineData("hostile")]
    [InlineData("critical")]
    [InlineData("disabled")]
    [InlineData("window")]
    [InlineData("overcharge")]
    [InlineData("cracked")]
    [InlineData("ping")]
    [InlineData("missile")]
    [InlineData("territory")]
    [InlineData("objective-health")]
    [InlineData("objective")]
    [InlineData("objective-done")]
    [InlineData("objective-failed")]
    [InlineData("contract-done")]
    [InlineData("contract-failed")]
    public void Every_kind_a_watch_says_has_a_row(string kind) => Assert.NotNull(Callouts.Find(kind));

    [Fact]
    public void A_muted_kind_is_not_said_and_the_rest_of_its_group_is()
    {
        var store = new LiveScreenStore(_root);
        store.SetVoice(true);
        store.SetCallout("missile", false);

        Assert.False(store.Current.Speaks("missile"));
        Assert.True(store.Current.Speaks("hostile"));
        Assert.True(store.Current.Speaks("ping"));

        store.SetCallout("missile", true);
        Assert.True(store.Current.Speaks("missile"));
        Assert.Null(store.Current.Muted);
    }

    [Fact]
    public void A_group_switch_and_the_voice_still_rule_over_a_kind()
    {
        var store = new LiveScreenStore(_root);
        store.SetVoice(true, missions: false);
        Assert.False(store.Current.Speaks("contract-done"));
        Assert.False(store.Current.Speaks("objective-health"));
        Assert.True(store.Current.Speaks("missile"));

        store.SetVoice(false);
        Assert.False(store.Current.Speaks("missile"));
        Assert.False(store.Current.VoiceMissions);
    }

    [Fact]
    public void Muted_sounds_survive_a_restart_and_an_unknown_kind_is_refused()
    {
        var store = new LiveScreenStore(_root);
        store.Answer(true);
        store.SetVoice(true);
        store.SetCallout("territory", false);
        Assert.Null(store.SetCallout("no-such-sound", false));

        var again = new LiveScreenStore(_root).Current;
        Assert.Equal(["territory"], again.Muted);
        Assert.False(again.Speaks("territory"));
        Assert.True(again.Speaks("missile"));
    }

    [Fact]
    public void An_answer_saved_before_1_1_14_says_everything()
    {
        File.WriteAllText(Path.Combine(_root, "live-screen.json"),
            """{"Asked":true,"Enabled":true,"Voice":true,"VoiceCombat":true,"VoiceMining":false}""");
        var live = new LiveScreenStore(_root).Current;

        Assert.True(live.VoiceMissions);
        Assert.Null(live.Muted);
        Assert.True(live.Speaks("contract-done"));
        Assert.False(live.Speaks("ping"));
    }

    // ---- the HUD's text ----

    [Fact]
    public void The_missile_warning_is_read_where_it_moved_on_the_recording()
    {
        // b_024 and b_051 at 3440x1440: the warning sat 100 px apart.
        Assert.True(HudText.Missile([Line("INBOUND MISILE", 1487, 413, 43)], 3440, 1440));
        Assert.True(HudText.Missile([Line("INBOUND MISSILE 62", 1617, 311, 46)], 3440, 1440));
        Assert.True(HudText.Missile([Line("INBLNDISILE", 1490, 410, 40)], 3440, 1440));
    }

    [Fact]
    public void Chat_about_missiles_is_not_a_warning()
    {
        // b_044's global chat, left of the band.
        Assert.False(HudText.Missile([Line("tell me how im on foat and getting a missle lock warning", 546, 791, 22)], 3440, 1440));
        Assert.False(HudText.Missile([Line("SCM", 1080, 544), Line("GUN", 1078, 574)], 3440, 1440));
    }

    [Fact]
    public void The_territory_banner_is_read_at_the_top_and_nowhere_else()
    {
        Assert.True(HudText.HostileTerritory([Line("HOSTILE TERRITORY", 1601, 122, 18)], 3440, 1440));
        Assert.True(HudText.HostileTerritory([Line("H0STILE TERRITORY 2684", 1595, 114, 33)], 3440, 1440));
        Assert.False(HudText.HostileTerritory([Line("HOSTILE TERRITORY", 300, 700, 18)], 3440, 1440));
    }

    [Fact]
    public void Mission_health_is_read_on_its_own_row_or_beside_its_label()
    {
        // a_000: the figure above the label's middle; a_036: on the same line;
        // b_014: an escort's hull.
        var defend = HudText.Health([Line("Defend Tranguity", 2582, 179, 45), Line("24%", 2886, 232, 20), Line("Ship Health:", 2605, 250, 35)], 3440, 1440);
        Assert.Equal(24, defend["objective"]);

        var relay = HudText.Health([Line("Power Relay Health 100%", 2579, 174, 63)], 3440, 1440);
        Assert.Equal(100, relay["objective"]);

        var escort = HudText.Health([Line("Escort Gabe", 2585, 196, 28), Line("100%", 2875, 232, 22), Line("Ship Hull Integrity", 2603, 246, 41)], 3440, 1440);
        Assert.Equal(100, escort["escort"]);
    }

    [Fact]
    public void A_figure_without_its_percent_sign_is_not_taken()
    {
        // a_002 read 24% as "248".
        var read = HudText.Health([Line("248", 2887, 234, 18), Line("Ship Health:", 2602, 250, 34)], 3440, 1440);
        Assert.Empty(read);
    }

    // ---- what the HUD watch says ----

    [Fact]
    public void A_missile_is_said_once_while_it_shows_and_again_after_it_has_gone()
    {
        var hud = new HudWatch();
        Assert.Single(hud.SeeMissile(true, T0));
        Assert.Empty(hud.SeeMissile(true, T0.AddSeconds(1)));
        Assert.Empty(hud.SeeMissile(false, T0.AddSeconds(2)));
        Assert.Empty(hud.SeeMissile(true, T0.AddSeconds(3)));     // a frame missed, the same missile
        Assert.Empty(hud.SeeMissile(false, T0.AddSeconds(4)));
        Assert.Empty(hud.SeeMissile(false, T0.AddSeconds(7)));
        var again = Assert.Single(hud.SeeMissile(true, T0.AddSeconds(28)));
        Assert.Equal("Missile inbound", again.Text);
        Assert.Equal("missile", again.Kind);
    }

    [Fact]
    public void Hostile_territory_is_said_on_the_way_in_not_on_every_read()
    {
        var hud = new HudWatch();
        var none = new Dictionary<string, int>();
        Assert.Single(hud.SeeWhole(true, none, T0));
        Assert.Empty(hud.SeeWhole(true, none, T0.AddSeconds(3)));
        Assert.Empty(hud.SeeWhole(false, none, T0.AddSeconds(6)));   // the engine missed it
        Assert.Empty(hud.SeeWhole(true, none, T0.AddSeconds(9)));
        Assert.True(hud.Hostile(T0.AddSeconds(60)));
        Assert.Equal("Entering hostile territory", Assert.Single(hud.SeeWhole(true, none, T0.AddMinutes(5))).Text);
    }

    [Fact]
    public void A_bar_already_low_is_said_on_the_second_read()
    {
        // a_000 and a_001: Defend Tranquility's ship at 24% from the first frame.
        var hud = new HudWatch();
        Assert.Empty(hud.SeeWhole(false, new Dictionary<string, int> { ["objective"] = 24 }, T0));
        var said = Assert.Single(hud.SeeWhole(false, new Dictionary<string, int> { ["objective"] = 24 }, T0.AddSeconds(3)));
        Assert.Equal("Objective health below 25 percent", said.Text);
        Assert.Empty(hud.SeeWhole(false, new Dictionary<string, int> { ["objective"] = 22 }, T0.AddSeconds(6)));
    }

    [Fact]
    public void A_bar_falling_fast_is_said_as_it_falls()
    {
        // a_047 to a_053: a power relay went 100, 83, 66, 50, 16 in six seconds.
        var hud = new HudWatch();
        var said = new List<CombatCallout>();
        var at = T0;
        foreach (var percent in new[] { 100, 83, 66, 40, 16 })
            said.AddRange(hud.SeeWhole(false, new Dictionary<string, int> { ["objective"] = percent }, at = at.AddSeconds(2)));

        Assert.Equal(["Objective health below 50 percent", "Objective health below 25 percent"], said.Select(c => c.Text));
    }

    [Fact]
    public void One_misread_figure_on_a_full_bar_is_not_said()
    {
        var hud = new HudWatch();
        var escort = (int p) => new Dictionary<string, int> { ["escort"] = p };
        Assert.Empty(hud.SeeWhole(false, escort(100), T0));
        Assert.Empty(hud.SeeWhole(false, escort(10), T0.AddSeconds(3)));
        Assert.Empty(hud.SeeWhole(false, escort(100), T0.AddSeconds(6)));
        Assert.Empty(hud.SeeWhole(false, escort(45), T0.AddSeconds(9)));
        Assert.Equal("Escort hull below 50 percent", Assert.Single(hud.SeeWhole(false, escort(44), T0.AddSeconds(12))).Text);
    }

    // ---- the ship's HUD coming on ----

    [Fact]
    public void The_flight_hud_is_two_of_its_words_and_one_is_not_enough()
    {
        // b_051's cockpit; a shop on foot lists guns.
        Assert.True(HudText.FlightHud([Line("SCM", 1080, 544), Line("GUN", 1078, 574), Line("87% H-FUEL", 901, 908)]));
        Assert.True(HudText.FlightHud([Line("ESP", 1030, 720), Line("CPLD", 1030, 760)]));
        Assert.False(HudText.FlightHud([Line("GUNS", 400, 300), Line("GUN RACK", 400, 340)]));
        Assert.False(HudText.FlightHud([Line("ESPRESSO", 400, 300), Line("SCMP", 400, 340)]));
    }

    [Fact]
    public void System_online_is_said_when_the_hud_appears_and_not_while_it_stays()
    {
        var hud = new HudWatch();
        var none = new Dictionary<string, int>();
        var said = Assert.Single(hud.SeeWhole(false, none, T0, flight: true));
        Assert.Equal("Quantum Wake system online", said.Text);
        Assert.Equal("online", said.Kind);

        Assert.Empty(hud.SeeWhole(false, none, T0.AddSeconds(2), flight: true));
        Assert.Empty(hud.SeeWhole(false, none, T0.AddSeconds(30), flight: false));   // looking round the cabin
        Assert.Empty(hud.SeeWhole(false, none, T0.AddSeconds(60), flight: true));
        // Ten minutes in mobiGlas without leaving the seat is the same flight (1.1.18).
        Assert.Empty(hud.SeeWhole(false, none, T0.AddMinutes(11), flight: true));
        Assert.Single(hud.SeeWhole(false, none, T0.AddMinutes(30), flight: true));   // the next ship
    }

    [Fact]
    public void The_ship_sound_needs_only_the_voice_and_its_own_row()
    {
        var store = new LiveScreenStore(_root);
        store.SetVoice(true, combat: false, mining: false, missions: false);
        Assert.True(store.Current.Speaks("online"));
        store.SetCallout("online", false);
        Assert.False(store.Current.Speaks("online"));
    }

    // ---- mission toasts ----

    [Fact]
    public void A_fresh_mission_toast_is_said_once()
    {
        var watch = new MissionWatch();
        var toast = new NotificationEvent(T0, "New Objective: Defend Tranquility: ", "31", "dd8f2c9e-a274-4112-953d-c843742c63d8");

        var said = watch.See(toast, T0.AddSeconds(1));
        Assert.NotNull(said);
        Assert.Equal("New objective", said!.Text);
        Assert.Equal("objective", said.Kind);

        // The same toast logged again as it is shown and faded.
        Assert.Null(watch.See(toast, T0.AddSeconds(2)));
        Assert.Null(watch.See(toast with { Timestamp = T0.AddSeconds(4) }, T0.AddSeconds(4)));
    }

    [Theory]
    [InlineData("Objective Complete: Go to QV Extraction Station", "objective-done", "Objective complete")]
    [InlineData("Objective Failed: Escort Gabe", "objective-failed", "Objective failed")]
    [InlineData("Contract Complete: Tactical Strike Group Needed", "contract-done", "Contract complete")]
    [InlineData("Contract Failed: Tactical Strike Group Needed", "contract-failed", "Contract failed")]
    public void Each_ending_has_its_own_sound(string text, string kind, string spoken)
    {
        var said = new MissionWatch().See(new NotificationEvent(T0, text, "7", null), T0);
        Assert.Equal(kind, said?.Kind);
        Assert.Equal(spoken, said?.Text);
    }

    [Fact]
    public void The_log_read_at_start_is_not_said_again()
    {
        var watch = new MissionWatch();
        Assert.Null(watch.See(new NotificationEvent(T0, "Contract Complete: Tactical Strike Group Needed", "40", null), T0.AddMinutes(30)));
    }

    [Fact]
    public void Other_toasts_say_nothing()
    {
        var watch = new MissionWatch();
        Assert.Null(watch.See(new NotificationEvent(T0, "Entering Armistice Zone - Combat Prohibited: ", "3", null), T0));
        Assert.Null(watch.See(new NotificationEvent(T0, "Contract Accepted: Tactical Strike Group Needed", "4", null), T0));
    }
}
