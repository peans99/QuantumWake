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
        panel.Do("page = QwMfd.pageIds.indexOf('nav'); lastRows = ''; render();");

        panel.Press(8).Press(8);

        Assert.Empty(panel.Writes());
        Assert.Contains("NOT HERE", panel.Footer);
        Assert.Equal("NAV", panel.Title);

        // And the strip that would have carried a confirmation stays silent.
        Assert.True((bool)panel.Eval("__dom.node('#action').hidden")!);
    }

    [Fact]
    public void DoneOnActArmsFirstAndOnlyWritesOnTheSecondPress()
    {
        var panel = new Panel(Plan);
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");

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
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");
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
        Assert.Contains("PLAN CHANGED", panel.Footer);
        Assert.Equal("ready", panel.StripState);
    }

    /// <summary>Any other button stands a live confirmation down.</summary>
    [Fact]
    public void ReachingForAnotherPageCancelsAnArmedConfirmation()
    {
        var panel = new Panel(Plan);
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");
        panel.Press(8);
        Assert.Equal("armed", panel.StripState);

        panel.Press(1);
        Assert.Equal("NAV", panel.Title);
        panel.Press(3);

        Assert.Equal("ready", panel.StripState);
        panel.Press(8);
        Assert.Empty(panel.Writes());
    }

    [Fact]
    public void TheStripReportsWhatWasMarkedRatherThanLeavingThePilotGuessing()
    {
        var panel = new Panel(Plan);
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");
        // A server that accepts the toggle, so this is the saved path and not
        // the "check the dashboard" one.
        panel.Do("__fetch.routes['/api/trips/t1/stops/s1/actions/a1/toggle'] = { id: 't1' };");
        panel.Press(8).Press(8);

        Assert.Single(panel.Writes());
        Assert.Equal("saved", panel.StripState);
        Assert.Contains("Marked done: load · 32 SCU · Titanium", panel.Strip);
        Assert.Contains("MARKED DONE", panel.Footer);
    }

    /// <summary>
    /// A toggle sent twice puts the line back exactly where it started, so a
    /// second press while the first is still in flight must not become a write.
    /// </summary>
    [Fact]
    public void AWriteInFlightSwallowsAFurtherPress()
    {
        var panel = new Panel(Plan);
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");
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
        panel.Do("page = QwMfd.pageIds.indexOf('act'); lastRows = ''; render();");

        panel.Press(8).Press(8);

        Assert.Empty(panel.Writes());
        Assert.True((bool)panel.Eval("__dom.node('#action').hidden")!);
    }
}
