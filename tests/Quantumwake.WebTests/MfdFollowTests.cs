using Jint;

namespace Quantumwake.WebTests;

/// <summary>
/// Follow the action (1.1.17): one MFD turns to Target in a fight and to
/// Charge at a rock, then goes back to the pilot's page once it is over.
/// </summary>
public class MfdFollowTests
{
    private static Engine Engine()
    {
        var engine = new Engine();
        engine.Execute("var window = globalThis;");
        engine.Execute(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "mfd-core.js")));
        // A frame left alone for a minute, and a step helper that keeps memory.
        engine.Execute("""
            var t0 = 1000000;
            var live = (status) => ({ available: true, enabled: true, status });
            var memory = QwMfd.followIdle();
            var step = (want, screen, now, lastPress) => {
              const r = QwMfd.followStep(memory, { want, screen, now, lastPress: lastPress ?? t0 - 60000 });
              memory = r.memory;
              return r.go;
            };
            """);
        return engine;
    }

    private static string? Want(Engine e, string status, string now = "t0") =>
        e.Evaluate($"QwMfd.followWant(live({status}), {now})") is var v && v.IsNull() ? null : v.AsString();

    [Fact]
    public void A_lock_a_hostile_nearby_or_a_missile_wants_the_target_page()
    {
        var e = Engine();
        Assert.Equal("target", Want(e, "{ combat: { target: { name: 'Vanduul Blade' }, callouts: [] } }"));
        Assert.Equal("target", Want(e, "{ combat: { combat: true, target: null, callouts: [] } }"));
        Assert.Equal("target", Want(e, "{ combat: { callouts: [{ kind: 'missile', at: new Date(t0 - 3000).toISOString() }] } }"));
        // A missile called a minute ago is over.
        Assert.Null(Want(e, "{ combat: { callouts: [{ kind: 'missile', at: new Date(t0 - 60000).toISOString() }] } }"));
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
        Assert.True(e.Evaluate("QwMfd.followWant({ available: true, enabled: false, status: { combat: { target: {} } } }, t0) === null").AsBoolean());
        Assert.True(e.Evaluate("QwMfd.followWant(undefined, t0) === null").AsBoolean());
    }

    [Fact]
    public void It_switches_then_goes_back_twenty_seconds_after_the_fight()
    {
        var e = Engine();
        Assert.Equal("target", e.Evaluate("step('target', 'nav', t0)").AsString());
        Assert.True(e.Evaluate("step('target', 'target', t0 + 5000) === null").AsBoolean());
        // The lock drops: nothing for the first twenty seconds.
        Assert.True(e.Evaluate("step(null, 'target', t0 + 10000) === null").AsBoolean());
        Assert.True(e.Evaluate("step(null, 'target', t0 + 25000) === null").AsBoolean());
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 30000)").AsString());
        Assert.True(e.Evaluate("memory.auto === null").AsBoolean());
    }

    [Fact]
    public void A_lock_regained_inside_the_grace_keeps_the_page()
    {
        var e = Engine();
        e.Evaluate("step('target', 'nav', t0)");
        e.Evaluate("step(null, 'target', t0 + 10000)");
        e.Evaluate("step('target', 'target', t0 + 20000)");
        // Lost again at 35 s: the twenty seconds start over from there.
        Assert.True(e.Evaluate("step(null, 'target', t0 + 35000) === null").AsBoolean());
        Assert.True(e.Evaluate("step(null, 'target', t0 + 50000) === null").AsBoolean());
        Assert.Equal("nav", e.Evaluate("step(null, 'target', t0 + 55000)").AsString());
    }

    [Fact]
    public void A_press_after_the_switch_makes_the_page_the_pilots()
    {
        var e = Engine();
        e.Evaluate("step('target', 'nav', t0)");
        // The pilot pressed to a page of their own at t0 + 2 s.
        Assert.True(e.Evaluate("step(null, 'map', t0 + 30000, t0 + 2000) === null").AsBoolean());
        Assert.True(e.Evaluate("memory.auto === null && memory.back === null").AsBoolean());
    }

    [Fact]
    public void It_does_not_move_the_page_under_a_pressing_thumb()
    {
        var e = Engine();
        Assert.True(e.Evaluate("step('target', 'nav', t0, t0 - 2000) === null").AsBoolean());
        Assert.Equal("target", e.Evaluate("step('target', 'nav', t0 + 4000, t0 - 2000)").AsString());
    }

    [Fact]
    public void The_callouts_page_counts_as_the_fight()
    {
        var e = Engine();
        Assert.True(e.Evaluate("step('target', 'calls', t0) === null").AsBoolean());
    }

    [Fact]
    public void A_fight_at_the_rock_goes_to_target_then_charge_then_home()
    {
        var e = Engine();
        Assert.Equal("charge", e.Evaluate("step('charge', 'task', t0)").AsString());
        Assert.Equal("target", e.Evaluate("step('target', 'charge', t0 + 5000)").AsString());
        Assert.Equal("charge", e.Evaluate("step('charge', 'target', t0 + 60000)").AsString());
        Assert.True(e.Evaluate("memory.back === 'task'").AsBoolean());
        e.Evaluate("step(null, 'charge', t0 + 70000)");
        Assert.Equal("task", e.Evaluate("step(null, 'charge', t0 + 90000)").AsString());
    }
}
