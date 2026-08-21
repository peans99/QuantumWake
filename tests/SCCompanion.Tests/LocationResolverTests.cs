using SCCompanion.Core.Locations;

namespace SCCompanion.Tests;

/// <summary>
/// Location id resolution. Every id below was observed in the real backfill.
/// </summary>
public class LocationResolverTests
{
    [Theory]
    [InlineData("Stanton4_NewBabbage", "New Babbage", "Stanton", "microTech", LocationKind.City)]
    [InlineData("Stanton1_Lorville", "Lorville", "Stanton", "Hurston", LocationKind.City)]
    [InlineData("Stanton2_Orison", "Orison", "Stanton", "Crusader", LocationKind.City)]
    [InlineData("Stanton3_Area18", "Area18", "Stanton", "ArcCorp", LocationKind.City)]
    public void Resolves_landing_zones(
        string id, string name, string system, string body, LocationKind kind)
    {
        var location = LocationResolver.Resolve(id);

        Assert.True(location.IsResolved);
        Assert.Equal(name, location.DisplayName);
        Assert.Equal(system, location.System);
        Assert.Equal(body, location.Body);
        Assert.Equal(kind, location.Kind);
    }

    /// <summary>Moon designators matter: 4b is Clio, 3b is Wala.</summary>
    [Theory]
    [InlineData("Stanton4b_RayariHydro_Cantwell", "Clio")]
    [InlineData("Stanton4b_RayariHydro_McGarth", "Clio")]
    [InlineData("Stanton2a_RayariHydro_HickesResearch", "Cellin")]
    public void Resolves_moon_facilities(string id, string body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(body, location.Body);
        Assert.Equal(LocationKind.Research, location.Kind);
        Assert.StartsWith("Rayari", location.DisplayName);
    }

    [Fact]
    public void Resolves_facility_on_a_moon_to_its_named_site()
    {
        var location = LocationResolver.Resolve("Stanton3b_ArcCorp_Area061");

        Assert.Equal("Stanton", location.System);
        Assert.Equal("Wala", location.Body);
        Assert.Equal("Area 061", location.DisplayName);
    }

    /// <summary>
    /// LEO stations are real places with real names, not generic rest stops.
    /// The localisation table files them under Stanton4_Transfer and the like,
    /// so nothing resolves RR_MIC_LEO without an explicit mapping - and it is
    /// the single most visited location in a typical log.
    /// </summary>
    [Theory]
    [InlineData("RR_MIC_LEO", "Port Tressler", "microTech")]
    [InlineData("RR_CRU_LEO", "Seraphim Station", "Crusader")]

    // The same station also shows up in the rs_ext form, sometimes numbered.
    [InlineData("rs_ext_cru-leo1", "Seraphim Station", "Crusader")]
    [InlineData("rs_ext_mic-leo", "Port Tressler", "microTech")]
    [InlineData("RR_ARC_LEO", "Baijini Point", "ArcCorp")]
    [InlineData("RR_HUR_LEO", "Everus Harbor", "Hurston")]
    public void Resolves_low_orbit_stations_by_name(string id, string name, string body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(name, location.DisplayName);
        Assert.Equal(body, location.Body);
        Assert.Equal(LocationKind.Station, location.Kind);
    }

    [Theory]
    [InlineData("RR_MIC_L1", "microTech L1 Rest Stop", "microTech")]
    [InlineData("RR_S4_L2", "microTech L2 Rest Stop", "microTech")]
    [InlineData("RR_P5_L2", "Pyro V L2 Rest Stop", "Pyro V")]
    [InlineData("RR_P3_L3", "Bloom L3 Rest Stop", "Bloom")]
    public void Resolves_rest_stops(string id, string name, string body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.True(location.IsResolved);
        Assert.Equal(name, location.DisplayName);
        Assert.Equal(body, location.Body);
        Assert.Equal(LocationKind.RestStop, location.Kind);
    }

    [Fact]
    public void Resolves_jump_point_rest_stops()
    {
        var location = LocationResolver.Resolve("RR_JP_StantonPyro");

        Assert.Equal(LocationKind.RestStop, location.Kind);
        Assert.Contains("Jump Point", location.DisplayName);
    }

    [Theory]
    [InlineData("LOC_rs_ext_stan-pyro_jp1", "Stanton – Pyro Jump Point")]
    [InlineData("rs_ext_pyro-stan_jp1", "Pyro – Stanton Jump Point")]
    [InlineData("LOC_rs_ext_stan-terra_jp1", "Stanton – Terra Jump Point")]
    public void Resolves_jump_points(string id, string name)
    {
        var location = LocationResolver.Resolve(id);

        Assert.True(location.IsResolved);
        Assert.Equal(name, location.DisplayName);
        Assert.Equal(LocationKind.JumpPoint, location.Kind);
    }

    [Fact]
    public void Strips_loc_prefix_before_resolving()
    {
        Assert.Equal(
            LocationResolver.Resolve("RR_S4_L1").DisplayName,
            LocationResolver.Resolve("LOC_RR_S4_L1").DisplayName);
    }

    [Theory]
    [InlineData("OOC_Stanton_4_Microtech", "microTech")]
    [InlineData("OOC_Stanton_2_Crusader", "Crusader")]
    [InlineData("OOC_Stanton_3_ArcCorp", "ArcCorp")]
    [InlineData("OOC_Stanton_4a_Calliope", "Calliope")]
    public void Resolves_orbital_containers_to_bodies(string id, string body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal("Stanton", location.System);
        Assert.Equal(body, location.DisplayName);
    }

    [Theory]
    [InlineData("NewBabbage_LOC", "New Babbage")]
    [InlineData("ObjectContainer_Lorville_City", "Lorville")]
    [InlineData("Area18_City_objectContainer", "Area18")]
    [InlineData("Orison_LOC", "Orison")]
    public void Resolves_bare_city_containers(string id, string name)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(name, location.DisplayName);
        Assert.Equal(LocationKind.City, location.Kind);
    }

    [Fact]
    public void Resolves_pyro_outposts_to_their_moon()
    {
        var location = LocationResolver.Resolve("Pyro2_Outpost_col_m_scrp_indy_001");

        Assert.Equal("Pyro", location.System);
        Assert.Equal("Monox", location.Body);
        Assert.Equal(LocationKind.Outpost, location.Kind);
    }

    [Fact]
    public void Resolves_distribution_centres_with_their_operator()
    {
        var location = LocationResolver.Resolve("Stanton4_DistributionCentre_Covalex_S4DC05");

        Assert.Equal(LocationKind.DistributionCentre, location.Kind);
        Assert.Contains("Covalex", location.DisplayName);
        Assert.Equal("microTech", location.Body);
    }

    [Theory]
    [InlineData("NavPoint_Dynamic_759722455016", LocationKind.NavPoint)]
    [InlineData("MISSION_QT_Beacon_9944292405804", LocationKind.MissionBeacon)]
    [InlineData("ObjectContainer_RestStop", LocationKind.RestStop)]
    public void Resolves_generic_markers(string id, LocationKind kind)
    {
        Assert.Equal(kind, LocationResolver.Resolve(id).Kind);
    }

    [Fact]
    public void Strips_guid_and_socpak_decorations()
    {
        var location = LocationResolver.Resolve(
            "shubin_cluster_001_frost_{93F1D47F-1BF4-46B6-A9B9-4A70881EFB2C}.socpak");

        Assert.Equal(LocationKind.Outpost, location.Kind);
        Assert.Equal("Shubin Mining Cluster", location.DisplayName);
    }

    /// <summary>
    /// Unknown ids must survive as unmapped nodes carrying their raw id, so new
    /// CIG content appears as a gap on the map rather than silently vanishing.
    /// </summary>
    [Fact]
    public void Unknown_ids_are_preserved_not_dropped()
    {
        var location = LocationResolver.Resolve("Stanton9_SomePlaceCigAddsLater");

        Assert.False(location.IsResolved);
        Assert.Equal("Stanton9_SomePlaceCigAddsLater", location.RawId);
        Assert.NotEmpty(location.DisplayName);
    }

    [Fact]
    public void Handles_empty_input()
    {
        Assert.False(LocationResolver.Resolve("").IsResolved);
    }

    /// <summary>Ids that put the system in the middle or at the end.</summary>
    [Theory]
    [InlineData("TheCollectorAsteroid_Stanton4", "Stanton", "microTech", LocationKind.Asteroid)]
    [InlineData("Outpost_OLP_Stanton1b_Vivere", "Stanton", "Aberdeen", LocationKind.Outpost)]
    public void Resolves_ids_with_an_embedded_system(
        string id, string system, string body, LocationKind kind)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(system, location.System);
        Assert.Equal(body, location.Body);
        Assert.Equal(kind, location.Kind);
    }

    [Theory]
    [InlineData("Nyx_Levski", "Levski", "Nyx", LocationKind.City)]
    [InlineData("GrimHEX", "GrimHEX", "Stanton", LocationKind.Station)]
    [InlineData("Port Tressler", "Port Tressler", "Stanton", LocationKind.Station)]
    public void Resolves_places_known_only_by_name(
        string id, string name, string system, LocationKind kind)
    {
        var location = LocationResolver.Resolve(id);

        Assert.True(location.IsResolved);
        Assert.Equal(name, location.DisplayName);
        Assert.Equal(system, location.System);
        Assert.Equal(kind, location.Kind);
    }

    /// <summary>
    /// Facilities kept their names but fell through to Unknown before site-token
    /// classification was added.
    /// </summary>
    [Theory]
    [InlineData("Stanton4c_ASD_Delve_Facility_005", LocationKind.Outpost, "Euterpe")]
    [InlineData("Stanton4_Shubin_SM0_22", LocationKind.Mine, "microTech")]
    [InlineData("Stanton4c_IndyFarm_BudsGrowery", LocationKind.Outpost, "Euterpe")]
    [InlineData("Pyro1_ASD_Monorail_LazarusTransportHub_Phoenix_1A", LocationKind.Station, "Pyro I")]
    public void Classifies_facilities_by_their_site_tokens(
        string id, LocationKind kind, string body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(kind, location.Kind);
        Assert.Equal(body, location.Body);
        Assert.NotEqual(id, location.DisplayName);
    }

    [Fact]
    public void Caches_repeated_lookups()
    {
        Assert.Same(
            LocationResolver.Resolve("RR_MIC_LEO"),
            LocationResolver.Resolve("RR_MIC_LEO"));
    }
}
