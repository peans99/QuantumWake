namespace Quantumwake.WebTests;

/// <summary>Personal Fleet controls stay in the browser and never rewrite the flight log.</summary>
public class FleetFeaturesTests
{
    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/fleet/hangar", """
            {"available":true,"ships":[
              {"name":"Drake Corsair","className":"DRAK_Corsair","sorties":4,"lastFlown":"2026-09-11T00:00:00Z","hours":2,"beam":30,"length":53,"height":25,"icon":true,"kind":"Spaceship"},
              {"name":"Greycat PTV","className":"GRIN_PTV","sorties":1,"lastFlown":"2026-08-01T00:00:00Z","hours":0.2,"beam":2.6,"length":4,"height":2.5,"icon":true,"kind":"Ground"}]}
            """);
        page.Do("""
            excludedShips = new Set(['Greycat PTV']);
            favouriteShips = new Set(['Drake Corsair']);
            hangarComparison = new Set();
            shipPictureStyle = 'tint';
            libraryStats = { fleetSize: 2, fleetHistory: [], ships: [
              {name:'Drake Corsair', className:'DRAK_Corsair', sorties:4, estimatedTime:'02:00:00', lastFlown:new Date().toISOString(), reference:{isSpaceship:true}},
              {name:'Greycat PTV', className:'GRIN_PTV', sorties:1, estimatedTime:'00:12:00', lastFlown:'2026-08-01T00:00:00Z', reference:{isSpaceship:false}}
            ]};
            renderFleet(libraryStats);
            """);
        return page;
    }

    [Fact]
    public void Fleet_marks_favourites_recent_flights_and_ships_excluded_from_the_hangar()
    {
        var page = Loaded();

        var ships = page.NodeText("#fleet-ships");
        var vehicles = page.NodeText("#fleet-vehicles");
        Assert.Contains("favourite", ships);
        Assert.Contains("flown this week", ships);
        Assert.Contains("out of Hangar", vehicles);
        Assert.True(page.Truth("__dom.node('#fleet-vehicles').byClass('ship-compare')[0].disabled"));

        page.Do("__dom.node('#fleet-filter').value = 'favourites'; renderFleetShips();");
        Assert.Contains("Corsair", page.NodeText("#fleet-ships"));
        Assert.DoesNotContain("PTV", page.NodeText("#fleet-vehicles"));
    }

    [Fact]
    public void Fleet_icons_open_a_compact_flight_and_size_preview()
    {
        var page = Loaded();
        page.Do("await hangarPreviewFor(libraryStats.ships[0]); await __dom.node('#fleet-ships').byClass('ship-outline')[0].fire('click');");

        var preview = page.NodeText("#fleet-ships");
        Assert.Contains("4 sorties", preview);
        Assert.Contains("53 × 30 × 25 m", preview);
        Assert.Contains("installed vehicle table", preview);
    }

    [Fact]
    public void Two_cards_can_open_their_to_scale_comparison_in_hangar()
    {
        var page = Loaded();
        page.Do("""
            excludedShips = new Set(); renderFleet(libraryStats);
            __dom.node('#fleet-ships').byClass('ship-compare')[0].fire('click');
            __dom.node('#fleet-vehicles').byClass('ship-compare')[0].fire('click');
            await __dom.node('#fleet-compare').byClass('fleet-compare-open')[0].fire('click');
            await loadHangar();
            """);

        Assert.Equal(2, Convert.ToInt32(page.Eval("hangarComparison.size")));
        Assert.Equal("scale", page.Text("__dom.node('#hangar-mode').value"));
        Assert.Contains("Comparing: Drake Corsair · Greycat PTV", page.NodeText("#hangar-scale"));
    }
}
