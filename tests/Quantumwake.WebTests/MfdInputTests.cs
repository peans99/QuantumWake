namespace Quantumwake.WebTests;

/// <summary>
/// What happens between a Cougar button and the pilot's flight plan.
/// </summary>
/// <remarks>
/// Both bugs here were reported from a real panel and neither could have been
/// caught by testing the page rules: the rules were right, and the input was
/// not asking them.
/// </remarks>
public class MfdInputTests
{
    private const string Plan =
        "{ tripId: 't1', stops: [ { id: 's1', place: 'Lorville', actions: ["
        + "{ id: 'a1', kind: 'load', quantity: 32, unit: 'SCU', text: 'Titanium', done: false },"
        + "{ id: 'a2', kind: 'refuel', text: 'Top up quantum', done: false } ] } ] }";

    /// <summary>
    /// Done is dimmed everywhere but Act, and dimming it was all that happened:
    /// two presses on Nav still marked a task off, with the confirmation drawn
    /// on a page the pilot was not looking at.
    /// </summary>
    [Fact]
    public void DoneDoesNothingOffTheActPageRatherThanQuietlyMarkingATask()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('nav');");

        panel.Press(8).Press(8);

        Assert.Empty(panel.Writes());
        Assert.Equal("Navigation", panel.Title);

        Assert.True(panel.StripHidden);
    }

    [Fact]
    public void DoneOnActArmsFirstAndOnlyWritesOnTheSecondPress()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('act');");

        panel.Press(8);
        Assert.Empty(panel.Writes());
        Assert.Equal("armed", panel.StripState);
        Assert.Contains("Confirm: load · 32 SCU · Titanium?", panel.Strip);

        panel.Press(8);
        Assert.Equal(["POST /api/trips/t1/stops/s1/actions/a1/toggle"], panel.Writes());
    }

    /// <summary>
    /// The plan is re-read every five seconds. An index is not an identity, so
    /// a refresh between the two presses could put a different job under the
    /// cursor - and the second press confirmed whatever was there.
    /// </summary>
    [Fact]
    public void APlanThatMovesBetweenTheTwoPressesCancelsRatherThanConfirming()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('act');");
        panel.Press(8);
        Assert.Equal("armed", panel.StripState);

        // Titanium got loaded elsewhere, so the cursor now points at the refuel.
        panel.Do("__fetch.routes['/api/briefing'] = { tripId: 't1', stops: [ { id: 's1',"
            + " place: 'Lorville', actions: ["
            + "{ id: 'a1', kind: 'load', quantity: 32, unit: 'SCU', text: 'Titanium', done: true },"
            + "{ id: 'a2', kind: 'refuel', text: 'Top up quantum', done: false } ] } ] };");
        panel.Do("refreshBriefing();");

        panel.Press(8);

        Assert.Empty(panel.Writes());
        Assert.Equal("note", panel.StripState);
        Assert.Contains("The plan changed. Nothing was marked.", panel.Strip);
    }

    /// <summary>Any other button stands a live confirmation down.</summary>
    [Fact]
    public void ReachingForAnotherPageCancelsAnArmedConfirmation()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('act');");
        panel.Press(8);
        Assert.Equal("armed", panel.StripState);

        panel.Press(15);
        Assert.Equal("Operations", panel.Title);
        panel.Press(2);

        Assert.Equal("ready", panel.StripState);
        panel.Press(8);
        Assert.Empty(panel.Writes());
    }

    [Fact]
    public void TheStripReportsWhatWasMarkedRatherThanLeavingThePilotGuessing()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('act');");
        // A server that accepts the toggle, so this is the saved path and not
        // the "check the dashboard" one.
        panel.Do("__fetch.routes['/api/trips/t1/stops/s1/actions/a1/toggle'] = { id: 't1' };");
        panel.Press(8).Press(8);

        Assert.Single(panel.Writes());
        Assert.Equal("saved", panel.StripState);
        Assert.Contains("Marked done: load · 32 SCU · Titanium", panel.Strip);
    }

    /// <summary>
    /// A toggle sent twice puts the line back exactly where it started, so a
    /// second press while the first is still in flight must not become a write.
    /// </summary>
    [Fact]
    public void AWriteInFlightSwallowsAFurtherPress()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('act');");
        panel.Press(8);

        // A POST that never settles, so the guard is the only thing stopping a
        // second one.
        panel.Do("__fetch.routes['/api/trips/t1/stops/s1/actions/a1/toggle'] = undefined;"
            + "fetch = (url, options) => { __fetch.calls.push({ url, method: (options && options.method) || 'GET' });"
            + " return new Promise(() => {}); };");

        panel.Press(8).Press(8).Press(8);
        Assert.Single(panel.Writes());
    }

    [Fact]
    public void WithNoPlanTrackedDoneSaysSoAndWritesNothing()
    {
        var panel = new Panel("{ stops: [] }");
        panel.Do("navigate('act');");

        panel.Press(8).Press(8);

        Assert.Empty(panel.Writes());
        Assert.True(panel.StripHidden);
    }

    /// <summary>
    /// The header carries which frame this is and which Cougar drives it. The
    /// footer that used to carry it - and a readout of the press the pilot had
    /// just made with their own thumb - cost a reading on a 480 px panel.
    /// </summary>
    [Fact]
    public void TheHeaderNamesTheFrameAndItsCougar()
    {
        var panel = new Panel(Plan);
        Assert.Equal("LEFT · NAV MFD", panel.Text("__dom.node('#identity').textContent"));

        panel.Do("cougar = 1; usb = true; drawIdentity();");
        Assert.Equal("LEFT · NAV · MFD 1", panel.Text("__dom.node('#identity').textContent"));

        // A frame the USB reader cannot find says so beside the connection.
        panel.Do("usb = false; connection = 'LIVE LOG'; drawIdentity();");
        Assert.Equal("LIVE LOG · NO USB", panel.Text("__dom.node('#connection').textContent"));
    }

    [Fact]
    public void MissionBezelOpensTheRadarAndChecklistWithoutMarkingAnythingDone()
    {
        var panel = new Panel(Plan);
        panel.Do("navigate('task');");

        panel.Press(1);
        Assert.Equal("System map", panel.Title);
        Assert.Empty(panel.Writes());

        panel.Do("navigate('task');");
        panel.Press(2);
        Assert.Equal("Checklist", panel.Title);
        Assert.Empty(panel.Writes());
    }

    /// <summary>
    /// The cycling button is the one control whose caption is its own state, so
    /// the level has to reach the cap. A lamp icon that never changes would make
    /// four levels indistinguishable from the nudging pair they replaced.
    /// </summary>
    [Fact]
    public void TheBrightnessButtonShowsWhichOfTheFourLevelsItIsOn()
    {
        var panel = new Panel(Plan);
        panel.Do("bindings[6] = 'bright'; drawLabels();");
        Assert.Equal("BRT 4", panel.Text("face[6].text.textContent"));

        panel.Press(6);
        Assert.Equal("BRT 1", panel.Text("face[6].text.textContent"));
        Assert.Equal(.4, panel.Eval("brightness"));

        panel.Press(6).Press(6).Press(6);
        Assert.Equal("BRT 4", panel.Text("face[6].text.textContent"));

        // Nothing about brightness belongs in the pilot's plan.
        Assert.Empty(panel.Writes());
    }

    /// <summary>
    /// The rules being right is not the same as the buttons asking them: Done
    /// was dimmed and still acted, so a page rule that nothing presses is worth
    /// no more than the truncation it replaced.
    /// </summary>
    [Fact]
    public void DownPagesTheLedgerAndTheTitleSaysWhereYouAre()
    {
        var panel = new Panel(Plan);
        panel.Do("extra = { ledger: Array.from({length: 8}, (_, i) => "
                 + "({ kind: 'bought', what: 'item ' + i, amount: -i, at: 'then' })) };");
        panel.Do("navigate('ledger');");

        Assert.Contains("1-3 of 8", panel.Title);

        panel.Press(12);
        Assert.Contains("4-6 of 8", panel.Title);

        panel.Press(12);
        Assert.Contains("7-8 of 8", panel.Title);

        // The end is the end, and says so rather than silently doing nothing.
        panel.Press(12);
        Assert.Contains("7-8 of 8", panel.Title);
        Assert.Equal("End of the ledger", panel.Strip);

        panel.Press(14).Press(14);
        Assert.Contains("1-3 of 8", panel.Title);

        // Leaving and coming back starts at the newest, not where you had read to.
        panel.Press(12);
        panel.Do("navigate('nav'); navigate('ledger');");
        Assert.Contains("1-3 of 8", panel.Title);

        Assert.Empty(panel.Writes());
    }

    /// <summary>
    /// The radar sweep turns about the star, and is pinned here because the
    /// thing that broke it is invisible in a diff and nearly invisible on the
    /// frame.
    /// </summary>
    /// <remarks>
    /// It was rotated by CSS with <c>transform-origin: 50% 50%</c>. This plot's
    /// viewBox starts at -1.18, the percentage landed on user-space
    /// (1.18, 1.18) - a whole radius off centre - and the sweep orbited that
    /// point instead of turning about the star, spending most of its travel
    /// outside the viewport and coming back as a clipped stub adrift past the
    /// outer ring. It still looked like a deliberate mark, which is why it went
    /// unnoticed. SMIL names the centre in user units and cannot be read two
    /// ways; asserting on that is asserting on the pivot itself.
    /// </remarks>
    [Fact]
    public void TheRadarSweepTurnsAboutTheCentreOfThePlot()
    {
        var panel = new Panel("{stops:[{placeId:'STAN_HUR_L1'}]}");

        // The plot draws nothing without somewhere to put, so the panel gets
        // the same atlas the view-model tests use.
        panel.Do("atlas = {positions:{stanton:{Hurston:{x:12850457093,y:0},Crusader:{x:0,y:19151568440},"
            + "microTech:{x:-43443771120,y:0},ArcCorp:{x:0,y:-28917482763}}},"
            + "nodes:[{rawId:'RR_MIC_LEO',name:'Port Tressler',body:'microTech'},"
            + "{rawId:'STAN_HUR_L1',name:'Everus Harbor',body:'Hurston'}]};");
        panel.Do("state = {connected:true, inGame:true, locationSystem:'Stanton', locationBody:'ArcCorp'};");
        panel.Do("navigate('map');");

        var sweep = "__dom.node('#map-plot').children.find(c => c.getAttribute('class') === 'radar-sweep')";
        Assert.True((bool)panel.Eval($"!!{sweep}")!, "the plot draws a sweep");

        var spin = $"{sweep}.children.find(c => c.tagName === 'animatetransform')";
        Assert.Equal("rotate", panel.Text($"{spin}.getAttribute('type')"));
        Assert.Equal("0 0 0", panel.Text($"{spin}.getAttribute('from')"));
        Assert.Equal("360 0 0", panel.Text($"{spin}.getAttribute('to')"));
        Assert.Equal("indefinite", panel.Text($"{spin}.getAttribute('repeatCount')"));

        // Every part of it starts at the star: the leading edge and each sector
        // of the trail. A shape that did not would be adrift however it turned.
        Assert.Equal("0", panel.Text($"{sweep}.children.find(c => c.getAttribute('class') === 'sweep-edge').getAttribute('x1')"));
        Assert.Equal("0", panel.Text($"{sweep}.children.find(c => c.getAttribute('class') === 'sweep-edge').getAttribute('y1')"));

        var trail = $"{sweep}.children.filter(c => c.getAttribute('class') === 'sweep-trail')";
        Assert.Equal(5, Convert.ToInt32(panel.Eval($"{trail}.length")));
        Assert.True((bool)panel.Eval($"{trail}.every(c => c.getAttribute('d').startsWith('M 0 0 L '))")!,
            "every sector of the trail is anchored at the star");

        // Faintest furthest behind: a trail that did not fade is a fan.
        Assert.True((bool)panel.Eval(
            $"{trail}.map(c => Number(c.getAttribute('opacity'))).every((v, i, all) => i === 0 || v < all[i - 1])")!,
            "the trail fades behind the leading edge");
    }
}
