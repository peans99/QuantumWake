namespace Quantumwake.WebTests;

/// <summary>
/// The ships you and somebody else were both aboard.
/// </summary>
/// <remarks>
/// The number here is boardings, and it would be very easy to read as time
/// flown together. Nothing records how long anybody stayed, a channel opens on
/// boarding rather than on flying, and a parked ship looks the same as a
/// crossing - so the section has to say that rather than let the count imply it.
/// </remarks>
public class CrewShipTests
{
    private const string Crew = """
        [{"handle":"Sylosis","sessions":4,"connected":6,"dropped":2,"ledParty":1,
          "joined":2,"left":1,"first":"2026-05-01T00:00:00+00:00","last":"2026-08-01T00:00:00+00:00"}]
        """;

    private const string Ships = """
        [{"handle":"DeathStrokeo1","ship":"RSI Ursa Medivac","owner":"DeathStrokeo1","times":10,
          "first":"2026-05-01T00:00:00+00:00","last":"2026-08-01T00:00:00+00:00"},
         {"handle":"Sylosis","ship":"Tumbril Cyclone MT","owner":"nekron","times":2,
          "first":"2026-05-10T00:00:00+00:00","last":"2026-05-10T00:00:00+00:00"}]
        """;

    private static Page Loaded(string ships = Ships)
    {
        var page = new Page();
        page.Serve("/api/crew?days=0", Crew);
        page.Serve("/api/crew/ships?days=0", ships);
        page.Do("__dom.node('#crew-period').value = '0'; await loadCrew();");
        return page;
    }

    [Fact]
    public void Each_shared_ship_names_the_pilot_the_vehicle_and_whose_it_was()
    {
        var text = Loaded().NodeText("#crew-ships");

        Assert.Contains("Ships you have shared", text);
        Assert.Contains("DeathStrokeo1", text);
        Assert.Contains("RSI Ursa Medivac", text);
        Assert.Contains("Tumbril Cyclone MT", text);
    }

    /// <summary>
    /// Crewing for somebody is a different evening from having them aboard
    /// yours, and the table has to say which.
    /// </summary>
    [Fact]
    public void Whose_ship_it_was_is_stated_rather_than_left_to_the_reader()
    {
        var page = Loaded();

        var rows = page.Text("__dom.node('#crew-ships').descendants()"
            + ".filter(n => n.tagName === 'tr').map(r => r.children.map(c => c.textContent).join('|')).join(';')");

        Assert.Contains("DeathStrokeo1|RSI Ursa Medivac|theirs|10", rows);
        Assert.Contains("Sylosis|Tumbril Cyclone MT|yours|2", rows);
    }

    /// <summary>
    /// The count is boardings. A caption that let it read as hours would be the
    /// app claiming something the logs cannot support.
    /// </summary>
    [Fact]
    public void The_section_says_the_count_is_boardings_and_not_time()
    {
        var text = Loaded().NodeText("#crew-ships");

        Assert.Contains("Boardings", text);
        Assert.Contains("not hours", text);
        Assert.Contains("parked ship", text);
    }

    [Fact]
    public void With_nobody_shared_the_section_is_absent_rather_than_empty()
    {
        Assert.Equal("", Loaded("[]").NodeText("#crew-ships"));
    }

    /// <summary>
    /// The ships come from their own route, so a page that cannot reach it still
    /// draws the crew table it already had.
    /// </summary>
    [Fact]
    public void A_failed_ships_fetch_leaves_the_rest_of_the_page_standing()
    {
        var page = new Page();
        page.Serve("/api/crew?days=0", Crew);
        page.Do("__dom.node('#crew-period').value = '0'; await loadCrew();");

        Assert.Contains("Sylosis", page.NodeText("#crew-table"));
        Assert.Equal("", page.NodeText("#crew-ships"));
    }
}
