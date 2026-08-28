namespace Quantumwake.WebTests;

/// <summary>
/// The Now page's party card. What it must never do is read as a roster: the
/// only thing behind it is a handful of toasts, and a tidy list of names is
/// exactly the shape a reader assumes is complete.
/// </summary>
public class NowPartyTests
{
    private const string Two =
        "party:[{handle:'D-Rud',moment:'connected',at:'2026-08-27T20:03:59Z'},"
        + "{handle:'Sylosis',moment:'left',at:'2026-08-27T19:41:02Z'}]";

    private static Page Now(string extra)
    {
        var page = new Page();
        page.Serve("/api/briefing", "{}");
        page.Serve("/api/trips", "[]");
        page.Do($"renderNow({{ connected:true, inGame:true, confidence:'None', recentEvents:[], {extra} }});");
        return page;
    }

    [Fact]
    public void It_names_everyone_the_party_channel_mentioned()
    {
        var page = Now(Two);

        Assert.False(page.Truth("__dom.node('#now-party-card').hidden"));
        Assert.Contains("D-Rud", page.NodeText("#now-party-list"));
        Assert.Contains("Sylosis", page.NodeText("#now-party-list"));
    }

    /// <summary>
    /// Connected and left are different facts and both are shown, so somebody
    /// who walked away is not left looking like somebody still aboard.
    /// </summary>
    [Fact]
    public void It_shows_the_last_thing_said_about_each_of_them()
    {
        var page = Now(Two);

        Assert.Contains("connected", page.NodeText("#now-party-list"));
        Assert.Contains("left", page.NodeText("#now-party-list"));
    }

    /// <summary>
    /// The caveat is permanent rather than shown when the list is short. A list
    /// of two is precisely when it would be mistaken for the whole party.
    /// </summary>
    [Fact]
    public void It_always_says_the_list_is_a_floor()
    {
        var note = Now(Two).NodeText("#now-party-note");

        Assert.Contains("floor, not a roster", note);
        Assert.Contains("never announced", note);
    }

    [Fact]
    public void It_says_so_once_the_party_has_disbanded()
    {
        Assert.Contains("since disbanded", Now($"{Two}, partyDisbanded:true").NodeText("#now-party-note"));
    }

    /// <summary>
    /// Flying alone and flying with a party that never produced a toast look
    /// identical from here, so the card leaves rather than printing a zero it
    /// cannot stand behind.
    /// </summary>
    [Fact]
    public void It_hides_itself_rather_than_showing_nobody()
    {
        Assert.True(Now("party:[]").Truth("__dom.node('#now-party-card').hidden"));
    }
}
