namespace Quantumwake.WebTests;

/// <summary>
/// Lighting up the places the game says have a facility.
/// </summary>
/// <remarks>
/// Kept apart from the service badges, which are UEX's account of where you can
/// actually trade today. This is the star map's own list, matched on the
/// readable name because the game's data carries no map id to join on.
/// </remarks>
public class MapAmenityTests
{
    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/map/amenities", """
            [{"place":"Area18","amenities":["Hospital","Buy Armor","Refinery"]},
             {"place":"Lorville","amenities":["Buy Armor"]},
             {"place":"Checkmate","amenities":["Refinery"]}]
            """);
        page.Do("await loadMapAmenities();");
        return page;
    }

    /// <summary>
    /// Rarest first: a facility two places have is worth searching for, and one
    /// that every place has narrows nothing.
    /// </summary>
    [Fact]
    public void The_rarest_facilities_are_offered_first()
    {
        var options = Loaded().Text(
            "__dom.node('#map-amenity').options.map(o => o.textContent).join('|')");

        // The stub does not load the markup, so the standing "Any facility"
        // entry is not here; what matters is the order of what gets added.
        Assert.StartsWith("Hospital (1)", options);
        Assert.Contains("Buy Armor (2)", options);
    }

    [Fact]
    public void Choosing_one_lights_the_places_that_have_it()
    {
        var page = Loaded();

        Assert.True(page.Truth("hasAmenity({ name: 'Checkmate' }, 'Refinery')"));
        Assert.False(page.Truth("hasAmenity({ name: 'Lorville' }, 'Refinery')"));
    }

    /// <summary>
    /// The join is on a name a human reads, so it must not care about case.
    /// </summary>
    [Fact]
    public void The_name_match_ignores_case()
    {
        Assert.True(Loaded().Truth("hasAmenity({ name: 'AREA18' }, 'Hospital')"));
    }
}
