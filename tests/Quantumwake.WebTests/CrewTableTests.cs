namespace Quantumwake.WebTests;

/// <summary>
/// The Crew table, which has to keep four different facts apart.
/// </summary>
/// <remarks>
/// Joining and leaving are the party changing. Coming online and dropping are a
/// client, which a member who logs out and back in does all evening without
/// going anywhere. Showing one number for both would make somebody with a poor
/// connection look like somebody who walked off, which is the wrong thing to
/// tell a person about who they fly with.
/// </remarks>
public class CrewTableTests
{
    private const string Crew = """
        [{"handle":"Sylosis","sessions":6,"connected":12,"dropped":4,"ledParty":2,
          "joined":3,"left":4,"first":"2026-05-01T00:00:00+00:00","last":"2026-08-01T00:00:00+00:00"},
         {"handle":"drudz","sessions":2,"connected":0,"dropped":0,"ledParty":0,
          "joined":1,"left":2,"first":"2026-06-01T00:00:00+00:00","last":"2026-06-02T00:00:00+00:00"}]
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/crew?days=0", Crew);
        page.Do("__dom.node('#crew-period').value = '0'; await loadCrew();");
        return page;
    }

    [Fact]
    public void Joining_and_connecting_are_counted_apart()
    {
        var page = Loaded();
        var text = page.NodeText("#crew-table");

        Assert.Contains("Sylosis", text);
        Assert.Contains("drudz", text);

        // Sylosis: 6 sessions, 3 joins, 4 departures, 12 online, 4 drops, 2 led.
        var cells = page.Text("__dom.node('#crew-table').descendants()"
            + ".filter(n => n.tagName === 'tr')[0].children.map(c => c.textContent).join('|')");

        Assert.Equal("Sylosis|6|3|4|12|4|2", cells[..cells.LastIndexOf('|', cells.LastIndexOf('|') - 1)]);
    }

    /// <summary>
    /// Somebody the game only ever named as joining and leaving is real, and was
    /// invisible before the party channel's other two titles were read.
    /// </summary>
    [Fact]
    public void A_person_seen_only_joining_and_leaving_still_appears()
    {
        var page = Loaded();

        var cells = page.Text("__dom.node('#crew-table').descendants()"
            + ".filter(n => n.tagName === 'tr')[1].children.map(c => c.textContent).join('|')");

        Assert.StartsWith("drudz|2|1|2|0|0|", cells);
    }

    /// <summary>
    /// A zero here is nearly always "the game did not say", not "never", so it
    /// is left blank rather than reported as a score of nought.
    /// </summary>
    [Fact]
    public void A_count_the_logs_never_gave_is_blank_rather_than_zero()
    {
        var page = Loaded();

        var cells = page.Text("__dom.node('#crew-table').descendants()"
            + ".filter(n => n.tagName === 'tr')[1].children.map(c => c.textContent).join('|')");

        // drudz never took lead: the cell reads as a dash.
        Assert.Contains("—", cells);
    }

    /// <summary>
    /// The headline figure is joins, because that is the one that counts
    /// somebody who was not there a moment before.
    /// </summary>
    [Fact]
    public void The_summary_leads_with_the_party_changing_not_the_client()
    {
        var page = Loaded();
        var summary = page.NodeText("#crew-summary");

        Assert.Contains("Joined your party", summary);
        Assert.Contains("4", summary);
        Assert.Contains("Came online", summary);
    }
}
