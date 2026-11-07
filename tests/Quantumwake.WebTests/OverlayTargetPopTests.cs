namespace Quantumwake.WebTests;

/// <summary>
/// Pop the Target card (1.1.19): for pilots without the MFD frames, a lock
/// turns the overlay to Now with the Target card first, and puts it back.
/// </summary>
public class OverlayTargetPopTests
{
    private static Page Stepper()
    {
        var page = new Page();
        page.Do("""
            globalThis.t0 = 1000000;
            globalThis.mem = targetPopIdle();
            globalThis.step = (want, view, now) => { const r = targetPopStep(mem, { want, view, now }); mem = r.memory; return r; };
            """);
        return page;
    }

    [Fact]
    public void A_lock_turns_to_now_and_twenty_seconds_after_it_goes_back()
    {
        var page = Stepper();
        Assert.Equal("now", page.Text("step(true, 'map', t0).go"));
        Assert.True(page.Truth("step(true, 'now', t0 + 1500).go === null"));
        Assert.True(page.Truth("step(false, 'now', t0 + 10000).go === null"));
        Assert.True(page.Truth("step(false, 'now', t0 + 25000).go === null"));
        Assert.Equal("map", page.Text("step(false, 'now', t0 + 30000).go"));
        Assert.False(page.Truth("mem.popped"));
    }

    [Fact]
    public void A_lock_regained_inside_the_grace_keeps_the_card_up()
    {
        var page = Stepper();
        page.Do("step(true, 'map', t0); step(false, 'now', t0 + 5000); step(true, 'now', t0 + 15000);");
        Assert.True(page.Truth("step(false, 'now', t0 + 30000).go === null"));
        Assert.True(page.Truth("mem.popped"));
    }

    [Fact]
    public void A_pilot_who_left_now_meanwhile_is_not_taken_back()
    {
        var page = Stepper();
        page.Do("step(true, 'map', t0); step(true, 'market', t0 + 3000); step(false, 'market', t0 + 5000);");
        Assert.True(page.Truth("step(false, 'market', t0 + 30000).go === null"));
        Assert.False(page.Truth("mem.popped"));
    }

    [Fact]
    public void Already_on_now_it_only_moves_the_card()
    {
        var page = Stepper();
        Assert.True(page.Truth("step(true, 'now', t0).go === null"));
        Assert.True(page.Truth("mem.popped"));
        page.Do("step(false, 'now', t0 + 1000);");
        Assert.True(page.Truth("step(false, 'now', t0 + 30000).go === null"));
    }

    private const string Locked = """
        liveState = { available: true, enabled: true, status: { combat: { combat: true, target: { name: 'Vanduul Blade', stance: 'hostile', candidates: [] }, callouts: [] } } };
        """;

    [Theory]
    [InlineData("{ popTarget: true, cards: ['target'] }", true)]
    [InlineData("{ popTarget: false, cards: ['target'] }", false)]
    [InlineData("{ popTarget: true, cards: ['location'] }", false)]
    [InlineData("null", false)]
    public void The_card_goes_first_only_when_the_layout_wants_it(string layout, bool popped)
    {
        var page = new Page();
        page.Do($"overlayApplied = {layout}; {Locked} popTargetCard();");
        Assert.Equal(popped, page.Truth("__dom.node('#now-target-card').classList.contains('card-popped')"));
    }

    [Fact]
    public void Older_layouts_without_the_switch_pop()
    {
        var page = new Page();
        page.Do($"overlayApplied = {{ cards: ['target'] }}; {Locked} popTargetCard();");
        Assert.True(page.Truth("__dom.node('#now-target-card').classList.contains('card-popped')"));
    }
}
