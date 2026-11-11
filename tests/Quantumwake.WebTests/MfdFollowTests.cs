using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// Follow the action (1.1.17): one MFD turns to Target in a fight and to
/// Charge at a rock, then goes back to the pilot's page once it is over.
/// Since 1.2.28 an empty Target goes back a few seconds after the lock, on
/// any frame that saw it, and spoken lines are put up over the page.
/// </summary>
public class MfdFollowTests
{
    private static Engine Engine()
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        // A frame left alone for a minute, and a step helper that keeps memory.
        // A wanted Target is a lock unless a test says otherwise (a missile).
        engine.Execute("""
            var t0 = 1000000;
            var live = (status) => ({ available: true, enabled: true, status });
            var memory = QwMfd.followIdle();
            var previous = null;
            var step = (want, screen, now, lastPress, locked) => {
              const r = QwMfd.followStep(memory, { want, screen, now, lastPress: lastPress ?? t0 - 60000,
                locked: locked ?? want === 'target', previous, home: 'task' });
              memory = r.memory;
              return r.go;
            };
            var heard = (...lines) => lines.map(([s, text]) => ({ at: new Date(t0 + s * 1000).toISOString(), text }));
            var calls = QwMfd.calloutIdle();
            var hear = (list, now) => { calls = QwMfd.calloutStep(calls, { heard: list, now }); return calls; };
            """);
        return engine;
    }

    private static string? Want(Engine e, string status, string now = "t0") =>
        e.Evaluate($"QwMfd.followWant(live({status}), {now})") is var v && v.IsNull() ? null : v.AsString();

    private static bool True(Engine e, string script) => e.Evaluate(script).AsBoolean();

    private static void Hear(Engine e, string list, string now) => e.Evaluate($"hear({list}, {now})");

    [Fact]
    public void A_lock_or_a_missile_wants_the_target_page()
    {
        var e = Engine();
        Assert.Equal("target", Want(e, "{ combat: { target: { name: 'Vanduul Blade' }, callouts: [] } }"));
        Assert.Equal("target", Want(e, "{ combat: { callouts: [{ kind: 'missile', at: new Date(t0 - 3000).toISOString() }] } }"));
        // A missile called a minute ago is over.
        Assert.Null(Want(e, "{ combat: { callouts: [{ kind: 'missile', at: new Date(t0 - 60000).toISOString() }] } }"));
    }

    [Fact]
    public void A_hostile_locked_a_while_ago_no_longer_holds_an_empty_target()
    {
        // The half minute after a hostile lock used to keep NOTHING LOCKED up
        // on the frame; with no lock and no missile there is nothing to show.
        var e = Engine();
        Assert.Null(Want(e, "{ combat: { combat: true, target: null, callouts: [] } }"));
    }

    [Fact]
    public void A_charging_rock_wants_charge_and_a_fight_beats_it()
    {
        var e = Engine();
        Assert.Equal("charge", Want(e, "{ fracture: { active: true } }"));
        Assert.Equal("target", Want(e, "{ fracture: { active: true }, combat: { target: {}, callouts: [] } }"));
        Assert.Null(Want(e, "{ fracture: { active: false } }"));
    }

    [Fact]
    public void Nothing_is_wanted_with_reading_off()
    {
        var e = Engine();
        Assert.True(True(e, "QwMfd.followWant({ available: true, enabled: false, status: { combat: { target: {} } } }, t0) === null"));
        Assert.True(True(e, "QwMfd.followWant(undefined, t0) === null"));
    }

    [Fact]
    public void It_switches_then_goes_back_six_seconds_after_the_lock()
    {
        var e = Engine();
        Assert.Equal("target", e.Evaluate("step('target', 'nav', t0)").AsString());
        Assert.True(True(e, "step('target', 'target', t0 + 5000) === null"));
        // The lock drops: nothing for the first six seconds.
        Assert.True(True(e, "step(null, 'target', t0 + 10000) === null"));
        Assert.True(True(e, "step(null, 'target', t0 + 15000) === null"));
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 16000)").AsString());
        Assert.True(True(e, "memory.auto === null"));
    }

    [Fact]
    public void A_lock_regained_inside_the_grace_keeps_the_page()
    {
        var e = Engine();
        e.Evaluate("step('target', 'nav', t0)");
        e.Evaluate("step(null, 'target', t0 + 10000)");
        e.Evaluate("step('target', 'target', t0 + 14000)");
        // Lost again at 20 s: the six seconds start over from there.
        Assert.True(True(e, "step(null, 'target', t0 + 20000) === null"));
        Assert.True(True(e, "step(null, 'target', t0 + 25000) === null"));
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 26000)").AsString());
    }

    [Fact]
    public void A_press_on_the_target_page_still_goes_back_when_the_lock_does()
    {
        // Any press in the fight used to cancel the trip back, and the frame
        // sat on NOTHING LOCKED until someone moved it.
        var e = Engine();
        e.Evaluate("step('target', 'nav', t0)");
        e.Evaluate("step('target', 'target', t0 + 3000, t0 + 2000)");
        Assert.True(True(e, "step(null, 'target', t0 + 10000, t0 + 2000) === null"));
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 16000, t0 + 2000)").AsString());
    }

    [Fact]
    public void Leaving_the_page_yourself_makes_it_yours()
    {
        var e = Engine();
        e.Evaluate("step('target', 'nav', t0)");
        // The pilot pressed to a page of their own at t0 + 2 s.
        Assert.True(True(e, "step(null, 'map', t0 + 30000, t0 + 2000) === null"));
        Assert.True(True(e, "step(null, 'map', t0 + 60000, t0 + 2000) === null"));
        Assert.True(True(e, "memory.auto === null && memory.back === null"));
    }

    [Fact]
    public void It_does_not_move_the_page_under_a_pressing_thumb()
    {
        var e = Engine();
        Assert.True(True(e, "step('target', 'nav', t0, t0 - 2000) === null"));
        Assert.Equal("target", e.Evaluate("step('target', 'nav', t0 + 4000, t0 - 2000)").AsString());
        // Nor on the way back: a press a moment ago holds the return.
        e.Evaluate("step(null, 'target', t0 + 5000, t0 - 60000)");
        Assert.True(True(e, "step(null, 'target', t0 + 12000, t0 + 10000) === null"));
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 15000, t0 + 10000)").AsString());
    }

    [Fact]
    public void A_missile_with_nothing_locked_goes_back_once_it_is_over()
    {
        var e = Engine();
        Assert.Equal("target", e.Evaluate("step('target', 'nav', t0, undefined, false)").AsString());
        Assert.True(True(e, "step('target', 'target', t0 + 10000, undefined, false) === null"));
        Assert.True(True(e, "step(null, 'target', t0 + 15000, undefined, false) === null"));
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 21000, undefined, false)").AsString());
    }

    [Fact]
    public void A_target_opened_by_hand_goes_back_to_where_the_pilot_was_once_its_lock_is_gone()
    {
        // The frame that does not follow the action: never sent anywhere by
        // the fight, but an empty Target is no more use on it.
        var e = Engine();
        e.Evaluate("previous = 'map'");
        Assert.True(True(e, "step(null, 'target', t0, undefined, true) === null"));
        Assert.True(True(e, "step(null, 'target', t0 + 3000, undefined, false) === null"));
        Assert.Equal("map", e.Evaluate("step(null, 'target', t0 + 9000, undefined, false)").AsString());
    }

    [Fact]
    public void With_no_page_to_go_back_to_it_goes_to_the_frames_own()
    {
        // Opened on Target at start-up: there is no page before it.
        var e = Engine();
        e.Evaluate("step(null, 'target', t0, undefined, true)");
        e.Evaluate("step(null, 'target', t0 + 1000, undefined, false)");
        Assert.Equal("task", e.Evaluate("step(null, 'target', t0 + 7000, undefined, false)").AsString());
    }

    [Fact]
    public void A_target_opened_by_hand_onto_nothing_is_left_alone()
    {
        // The pilot is looking at it; there has been no fight on it to end.
        var e = Engine();
        e.Evaluate("previous = 'map'");
        Assert.True(True(e, "step(null, 'target', t0, undefined, false) === null"));
        Assert.True(True(e, "step(null, 'target', t0 + 60000, undefined, false) === null"));
    }

    [Fact]
    public void The_callouts_page_counts_as_the_fight()
    {
        var e = Engine();
        Assert.True(True(e, "step('target', 'calls', t0) === null"));
    }

    [Fact]
    public void A_fight_at_the_rock_goes_to_target_then_charge_then_home()
    {
        var e = Engine();
        Assert.Equal("charge", e.Evaluate("step('charge', 'task', t0)").AsString());
        Assert.Equal("target", e.Evaluate("step('target', 'charge', t0 + 5000)").AsString());
        Assert.Equal("charge", e.Evaluate("step('charge', 'target', t0 + 60000)").AsString());
        Assert.True(True(e, "memory.back === 'task'"));
        e.Evaluate("step(null, 'charge', t0 + 70000)");
        // A rock between bursts keeps its twenty seconds.
        Assert.True(True(e, "step(null, 'charge', t0 + 85000) === null"));
        Assert.Equal("task", e.Evaluate("step(null, 'charge', t0 + 90000)").AsString());
    }

    /// <summary>
    /// The pages read live in the moment take the strip, so their readings
    /// stay in sight; every other page takes the full card.
    /// </summary>
    [Fact]
    public void Live_pages_take_the_strip_and_the_rest_the_card()
    {
        var e = Engine();
        Assert.True(True(e, "['target', 'calls', 'charge'].every(QwMfd.calloutStrip)"));
        Assert.False(True(e, "['nav', 'task', 'map', 'home', 'ship'].some(QwMfd.calloutStrip)"));
    }

    [Fact]
    public void What_was_said_before_the_frame_opened_is_not_shown()
    {
        var e = Engine();
        Hear(e, "heard([-30, 'Target identified: Vanduul Blade'])", "t0");
        Assert.False(True(e, "QwMfd.calloutShowing(calls, t0)"));
    }

    [Fact]
    public void A_new_line_is_shown_for_six_seconds_then_the_page_is_back()
    {
        var e = Engine();
        Hear(e, "heard([-30, 'Copilot online'])", "t0");
        Hear(e, "heard([2, 'Hostile target identified: Vanduul Blade'], [-30, 'Copilot online'])", "t0 + 2500");
        Assert.True(True(e, "QwMfd.calloutShowing(calls, t0 + 2500)"));
        Assert.Equal("Hostile target identified: Vanduul Blade", e.Evaluate("calls.lines[0].text").AsString());
        Assert.Equal(1, (int)e.Evaluate("calls.lines.length").AsNumber());
        // Asked again with nothing new: still up, and still only the one line.
        Hear(e, "heard([2, 'Hostile target identified: Vanduul Blade'], [-30, 'Copilot online'])", "t0 + 4000");
        Assert.True(True(e, "QwMfd.calloutShowing(calls, t0 + 8000)"));
        Assert.False(True(e, "QwMfd.calloutShowing(calls, t0 + 8600)"));
    }

    [Fact]
    public void Lines_close_together_stack_newest_first()
    {
        var e = Engine();
        Hear(e, "[]", "t0");
        Hear(e, "heard([1, 'Target identified: Gladius'])", "t0 + 1500");
        Hear(e, "heard([3, 'Missile inbound'], [1, 'Target identified: Gladius'])", "t0 + 3000");
        Assert.Equal("Missile inbound", e.Evaluate("calls.lines[0].text").AsString());
        Assert.Equal("Target identified: Gladius", e.Evaluate("calls.lines[1].text").AsString());
        // The newer line keeps the card up for its own six seconds.
        Assert.True(True(e, "QwMfd.calloutShowing(calls, t0 + 8500)"));
    }

    [Fact]
    public void A_line_after_the_card_went_down_starts_a_fresh_card()
    {
        var e = Engine();
        Hear(e, "[]", "t0");
        Hear(e, "heard([1, 'Rock cracked'])", "t0 + 1500");
        Hear(e, "heard([20, 'Three Riccite'], [1, 'Rock cracked'])", "t0 + 20500");
        Assert.Equal(1, (int)e.Evaluate("calls.lines.length").AsNumber());
        Assert.Equal("Three Riccite", e.Evaluate("calls.lines[0].text").AsString());
    }
}
