namespace Quantumwake.WebTests;

/// <summary>
/// Toasts raised off the live stream.
/// </summary>
/// <remarks>
/// The stream pushes the whole Now snapshot once a second, so "what is new" is
/// a diff the client has to do for itself. Two failures matter and neither is
/// visible in a diff: replaying the backlog, which greets every page refresh
/// with an hour of history, and toasting on every frame, which stacks the same
/// contract up 60 times a minute. In the overlay both land on top of the game.
/// </remarks>
public class LiveToastTests
{
    private const string Payout =
        "{at:'2026-05-03T18:06:45Z',kind:'payout',text:'Paid 50,250 aUEC',"
        + "detail:'Opportunity for Independent Cargo Hauler'}";

    private const string Arrival =
        "{at:'2026-05-03T18:04:00Z',kind:'location',text:'Arrived',detail:'Port Tressler'}";

    private const string Differs =
        "{at:'2026-05-03T18:05:30Z',kind:'screen-differs',text:'Ship: Argo MOLE',"
        + "detail:'the logs last saw you in a Drake Cutlass Black'}";

    private static Page Live()
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        return page;
    }

    private static void Frame(Page page, string events) =>
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[{events}] }});");

    /// <summary>
    /// The stream opens with up to 40 entries of history. Toasting what the
    /// first frame carries would replay the session on every refresh, and the
    /// overlay reloads whenever its layout is saved.
    /// </summary>
    [Fact]
    public void The_first_frame_is_history_and_is_not_toasted()
    {
        var page = Live();
        Frame(page, Payout);

        Assert.Equal(0, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }

    [Fact]
    public void Something_that_arrives_after_the_first_frame_is_toasted()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page, $"{Payout},{Arrival}");

        Assert.Equal(1, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
        Assert.Contains("50,250", page.NodeText("#toasts"));
        Assert.Contains("Opportunity for Independent Cargo Hauler", page.NodeText("#toasts"));
    }

    /// <summary>
    /// A frame lands every second whether or not anything happened. Re-toasting
    /// an unchanged head is the difference between a notifier and a nuisance.
    /// </summary>
    [Fact]
    public void An_unchanged_frame_toasts_nothing_more()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page, $"{Payout},{Arrival}");
        Frame(page, $"{Payout},{Arrival}");
        Frame(page, $"{Payout},{Arrival}");

        Assert.Equal(1, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }

    /// <summary>
    /// Arrivals, medbeds and quantum jumps all reach the feed and none of them
    /// is worth interrupting somebody for. A notifier that fires on everything
    /// gets ignored, which costs the two events that mattered.
    /// </summary>
    /// <summary>
    /// A session can open with nothing in its timeline - the dashboard left on
    /// the menu, or the overlay reloaded before anything happened - and an
    /// empty frame is still a frame. Anchoring only on a non-empty one meant
    /// the first thing that ever happened became the anchor and was swallowed,
    /// which for a screenshot is the one disagreement worth interrupting for.
    /// </summary>
    [Fact]
    public void The_first_thing_to_happen_in_an_empty_session_is_toasted()
    {
        var page = Live();
        Frame(page, "");
        Frame(page, Differs);

        Assert.Equal(1, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
        Assert.Contains("Argo MOLE", page.NodeText("#toasts"));
    }

    /// <summary>
    /// The empty frame says there is no backlog, so what follows is news rather
    /// than history - but only once. The frame after it is the ordinary case
    /// again and must not re-toast what it still carries.
    /// </summary>
    [Fact]
    public void An_empty_session_still_toasts_each_thing_once()
    {
        var page = Live();
        Frame(page, "");
        Frame(page, Differs);
        Frame(page, Differs);
        Frame(page, $"{Payout},{Differs}");

        Assert.Equal(2, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }

    [Fact]
    public void Ordinary_feed_entries_are_not_toasted()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page, $"{{at:'2026-05-03T18:05:00Z',kind:'location',text:'Arrived',detail:'Everus Harbor'}},{Arrival}");

        Assert.Equal(0, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }

    [Fact]
    public void A_completed_contract_is_toasted()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page,
            "{at:'2026-05-03T18:06:45Z',kind:'contract-done',text:'Contract completed',"
            + $"detail:'Junior Rank - Direct Medium Cargo Haul'}},{Arrival}");

        Assert.Equal(1, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
        Assert.Contains("Junior Rank", page.NodeText("#toasts"));
    }

    /// <summary>
    /// Several things can land between two frames - a completion and its payout
    /// arrive 0.2 s apart, which is inside one push - and both are shown, in
    /// the order they happened rather than the newest-first order they arrive.
    /// </summary>
    [Fact]
    public void Everything_since_the_last_frame_is_shown_oldest_first()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page,
            $"{Payout},"
            + "{at:'2026-05-03T18:06:45Z',kind:'contract-done',text:'Contract completed',"
            + $"detail:'Cargo run'}},{Arrival}");

        Assert.Equal(2, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
        Assert.Equal("Contract completed", page.Text("__dom.node('#toasts').querySelectorAll('.toast')[0].querySelectorAll('.toast-text')[0].textContent"));
        Assert.Contains("50,250", page.Text("__dom.node('#toasts').querySelectorAll('.toast')[1].textContent"));
    }

    /// <summary>
    /// Money is coloured apart from everything else, the same green the ledger
    /// uses, so a payout is recognisable without being read.
    /// </summary>
    [Fact]
    public void A_payout_toast_is_marked_as_money()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page, $"{Payout},{Arrival}");

        Assert.True(page.Truth("__dom.node('#toasts').querySelectorAll('.toast')[0].classList.contains('payout')"));
    }

    /// <summary>
    /// StarStrings writes its additions inside the game's own markup and the
    /// log keeps the tags, so an unfiltered toast reads "Rookie |
    /// &lt;EM3&gt;DIRECT&lt;/EM3&gt; Extra Small Haul". The bracket tags stay -
    /// they are the research the mod exists for.
    /// </summary>
    [Fact]
    public void The_mods_markup_does_not_reach_the_toast()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page,
            "{at:'2026-05-03T18:06:45Z',kind:'contract-done',text:'Contract completed',"
            + $"detail:'Rookie | <EM3>DIRECT</EM3> Small Haul <EM4>[BP]*</EM4>'}},{Arrival}");

        var text = page.NodeText("#toasts");

        Assert.DoesNotContain("EM3", text);
        Assert.DoesNotContain("EM4", text);
        Assert.Contains("Rookie | DIRECT Small Haul [BP]*", text);
    }

    /// <summary>
    /// Clicking dismisses. In the overlay a toast sits over the game, and
    /// waiting out a nine-second fade is not an option mid-fight.
    /// </summary>
    [Fact]
    public void A_toast_can_be_dismissed()
    {
        var page = Live();
        Frame(page, Arrival);
        Frame(page, $"{Payout},{Arrival}");
        page.Do("__dom.node('#toasts').querySelectorAll('.toast')[0].click();");

        Assert.Equal(0, page.Count("__dom.node('#toasts').querySelectorAll('.toast').length"));
    }
}
