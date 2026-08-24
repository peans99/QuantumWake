namespace Quantumwake.WebTests;

/// <summary>A system map preserves its body's geometry; the cross-system view does not fake one.</summary>
public class MapModeTests
{
    [Fact]
    public void System_picker_defaults_to_the_current_system_and_explains_the_coordinate_limit()
    {
        var page = new Page();
        page.Do("""
            atlas = [
              { rawId: 'stan', name: 'Area18', system: 'Stanton', body: 'ArcCorp', kind: 'City', visits: 1 },
              { rawId: 'pyro', name: 'Ruin', system: 'Pyro', body: 'Pyro I', kind: 'Outpost', visits: 0 },
              { rawId: 'nyx', name: 'Levski', system: 'Nyx', body: 'Delamar', kind: 'City', visits: 0 }
            ];
            __dom.node('#map-mode').value = 'system';
            __dom.node('#map-system').value = '';
            syncMapModeControls();
            """);

        Assert.Equal("Stanton", page.Text("__dom.node('#map-system').value"));
        Assert.Equal(3, page.Count("__dom.node('#map-system').options.length"));
        Assert.Contains("relative orbit distances", page.NodeText("#map-mode-note"));
    }

    [Fact]
    public void System_mode_keeps_relative_orbit_distance_and_marks_missing_coordinates()
    {
        var page = new Page();
        page.Do("""
            bodyPositions = {
              stanton: { Near: { x: 10, y: 0 }, Far: { x: 100, y: 0 } },
              nyx: { 'Nyx III': { x: 100, y: 0 } }
            };
            __dom.node('#map-mode').value = 'system';
            const physical = bodyLayout('Stanton', ['Near', 'Far'], { x: 0, y: 0, orbit: 200 }, () => 10);
            const absent = bodyLayout('Nyx', ['Delamar'], { x: 0, y: 0, orbit: 200 }, () => 10);
            __physicalNear = physical.get('Near').x;
            __physicalFar = physical.get('Far').x;
            __delamarPositioned = absent.get('Delamar').positioned;
            __dom.node('#map-mode').value = 'network';
            const schematic = bodyLayout('Stanton', ['Near', 'Far'], { x: 0, y: 0, orbit: 200 }, () => 10);
            __schematicNear = schematic.get('Near').x;
            """);

        Assert.Equal(20, page.Number("__physicalNear"));
        Assert.Equal(200, page.Number("__physicalFar"));
        Assert.False(page.Truth("__delamarPositioned"));
        Assert.True(page.Number("__schematicNear") > page.Number("__physicalNear"));
    }

    [Fact]
    public void Jump_network_is_explicitly_schematic_and_has_no_place_marks()
    {
        var page = new Page();
        page.Do("""
            atlas = [
              { rawId: 'stan', name: 'Area18', system: 'Stanton', body: 'ArcCorp', kind: 'City', visits: 1 },
              { rawId: 'pyro', name: 'Ruin', system: 'Pyro', body: 'Pyro I', kind: 'Outpost', visits: 0 },
              { rawId: 'nyx', name: 'Levski', system: 'Nyx', body: 'Delamar', kind: 'City', visits: 0 }
            ];
            __dom.node('#map-mode').value = 'network';
            drawMap();
            """);

        Assert.Contains("JUMP NETWORK", page.NodeText("#starmap"));
        Assert.Contains("3 systems", page.NodeText("#map-count"));
        Assert.Equal(0, page.Count("nodeAt.size"));
    }

    [Fact]
    public void Player_location_remains_findable_when_viewing_another_system()
    {
        var page = new Page();
        page.Do("""
            atlas = [
              { rawId: 'stan', name: 'Area18', system: 'Stanton', body: 'ArcCorp', kind: 'City', visits: 1 },
              { rawId: 'pyro', name: 'Gaslight', system: 'Pyro', body: 'Pyro V', kind: 'RestStop', visits: 1 }
            ];
            hereId = 'pyro';
            performance = { now: () => 0 };
            __dom.node('#map-mode').value = 'system';
            __dom.node('#map-system').value = 'Stanton';
            syncMapModeControls();
            __beforeFocus = __dom.node('#map-here-label').textContent;
            __focused = focusHere();
            """);

        Assert.Equal("You · Gaslight", page.Text("__beforeFocus"));
        Assert.True(page.Truth("__focused"));
        Assert.Equal("system", page.Text("__dom.node('#map-mode').value"));
        Assert.Equal("Pyro", page.Text("__dom.node('#map-system').value"));
        Assert.Contains("YOU ARE HERE", page.NodeText("#starmap"));
    }

    [Fact]
    public void Work_layer_keeps_only_the_active_plan_stops_on_the_map()
    {
        var page = new Page();
        page.Do("""
            atlas = [
              { rawId: 'a18', name: 'Area18', system: 'Stanton', body: 'ArcCorp', kind: 'City', visits: 1 },
              { rawId: 'tressler', name: 'Port Tressler', system: 'Stanton', body: 'microTech', kind: 'Station', visits: 1 }
            ];
            trips = [{ tracked: true, stops: [{ placeId: 'tressler', place: 'Port Tressler', done: false }] }];
            __dom.node('#map-mode').value = 'system';
            __dom.node('#map-system').value = 'Stanton';
            selectMapFocus('plan');
            """);

        Assert.Equal(1, page.Count("nodeAt.size"));
        Assert.True(page.Truth("nodeAt.has('tressler')"));
        Assert.Equal("1 plan location shown", page.NodeText("#map-count"));
    }

    [Fact]
    public void Label_density_can_trade_detail_for_quiet()
    {
        var page = new Page();
        page.Do("__dom.node('#map-label-density').value = 'quiet';");

        Assert.Equal(8, page.Number("labelBudget()"));
    }

    [Fact]
    public void Commodity_freshness_is_labelled_without_claiming_place_age()
    {
        var page = new Page();
        page.Do("Date.now = () => Date.parse('2026-08-24T00:00:00Z');");

        Assert.Equal("fresh", page.Text("priceFreshness('2026-08-23T12:00:00Z').state"));
        Assert.Equal("aging", page.Text("priceFreshness('2026-08-18T00:00:00Z').state"));
        Assert.Equal("stale", page.Text("priceFreshness('2026-08-01T00:00:00Z').state"));
    }
}
