using Quantumwake.Core.Locations;

namespace Quantumwake.Tests;

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

    /// <summary>
    /// Nyx shares none of the id grammar Stanton and Pyro use, so without a rule
    /// of its own the whole system collapsed to Levski - the one place the
    /// well-known table happened to list - and the map showed a system of one.
    /// </summary>
    /// <remarks>
    /// Display names come from the localisation table at runtime, which the test
    /// has no access to, so these assert the system and kind the id encodes.
    /// </remarks>
    [Theory]
    [InlineData("Nyx_SocialStation_003", LocationKind.Station, null)]
    [InlineData("Nyx_RockCracker_007", LocationKind.Mine, null)]
    [InlineData("Nyx_OutlawStation_Keeger", LocationKind.Station, "Keeger Belt")]
    [InlineData("Nyx_Glaciem_Segment_Social_001", LocationKind.Station, "Glaciem Ring")]
    [InlineData("Nyx_AsteroidBelt1", LocationKind.Asteroid, null)]
    [InlineData("Nyx_JumpPoint_Stanton", LocationKind.JumpPoint, null)]

    // A gateway station sits on the far side of a jump, so Nyx's are filed
    // under the system they are reached from.
    [InlineData("Nyx_Stanton_JPStation", LocationKind.Station, null)]
    [InlineData("Stanton_Nyx_JPStation", LocationKind.Station, null)]
    public void Resolves_Nyx(string id, LocationKind kind, string? body)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal("Nyx", location.System);
        Assert.Equal(kind, location.Kind);
        Assert.Equal(body, location.Body);
        Assert.True(location.IsResolved);
    }

    /// <summary>
    /// The Nyx prefix is also all over the UI strings and the bodies themselves.
    /// None of those is somewhere you fly to, so they must not become map nodes.
    /// </summary>
    [Theory]
    [InlineData("Nyx_Star")]
    [InlineData("Nyx2")]
    [InlineData("nyx_transit_Glaciem_01")]
    public void Ignores_Nyx_ids_that_are_not_places(string id)
    {
        Assert.NotEqual("Nyx", LocationResolver.Resolve(id).System);
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

    /// <summary>
    /// Quantum targets that used to reach the live feed as their raw id - 54 of
    /// the 203 distinct targets selected on this install. None names a place
    /// the map could hold, but each says what it is.
    /// </summary>
    [Theory]
    [InlineData("MISSION_QT_Distress_Beacon_SupportAttackedShip_863273994735", "Distress Beacon", LocationKind.MissionBeacon)]
    [InlineData("MISSION_QT_Quantum_Beacon_LongRange_864896026617", "Mission Beacon", LocationKind.MissionBeacon)]
    [InlineData("MISSION_QT_Quantum_Beacon_ShortRange_Salvage_527766621511", "Mission Beacon", LocationKind.MissionBeacon)]
    [InlineData("PartyMemberMarker_297611060881", "Party Member", LocationKind.NavPoint)]
    [InlineData("UnattendedVehicleMarker_848089599253", "Unattended Ship", LocationKind.NavPoint)]
    [InlineData("rayari_cluster_001_frost_{43C7065A-E2FC-4386-AEB6-CA4BF813F5B0}.socpak", "Rayari Research Cluster", LocationKind.Research)]
    [InlineData("hurdyn_cluster_003_sand_red_{877EEE75-D129-4CF2-BBB4-39693B27EEEF}.socpak", "HurDyn Mining Cluster", LocationKind.Mine)]
    [InlineData("arccorp_cluster_004_dust_{E43B2A37-F470-4E93-8E49-E317E21DA116}.socpak", "ArcCorp Mining Cluster", LocationKind.Mine)]
    [InlineData("indy_farmer_001_frost_{D855A44F-A730-4C30-8165-EC314B7C4235}.socpak", "Independent Farm", LocationKind.Outpost)]
    [InlineData("stash_001_frost_{CCC84E13-6B15-4F3D-BF90-557F40DBDF24}.socpak", "Stash", LocationKind.Outpost)]
    [InlineData("levski_all-001", "Levski", LocationKind.City)]
    [InlineData("GrimHEX_OC", "GrimHEX", LocationKind.Station)]
    [InlineData("oc_st4_drlct_rclmr_sfce_frst_ovgr_a", "Derelict Reclaimer, microTech", LocationKind.Unknown)]
    [InlineData("AsteroidCluster_Pyro_RegionB_BGR-560", "Asteroid Cluster BGR-560", LocationKind.Asteroid)]
    [InlineData("ab_mine_pyro_regionb_med_008", "Region B Mining Base", LocationKind.Mine)]
    [InlineData("RegionA_1base_ab_pyro_final_set_encounter-003", "Region A Asteroid Base", LocationKind.Asteroid)]
    [InlineData("util_a_orbital_001_occu_a_final-001", "Orbital Platform", LocationKind.Station)]
    [InlineData("Nyx_TSG_QVExtractionStation_028", "QV Extraction Station 028", LocationKind.Station)]
    [InlineData("Nyx_TSG_QVExtractionStation_Interior", "QV Extraction Station", LocationKind.Station)]
    public void Names_quantum_targets_the_id_describes_but_does_not_name(
        string id, string name, LocationKind kind)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(name, location.DisplayName);
        Assert.Equal(kind, location.Kind);
    }

    /// <summary>
    /// The system token sits mid-id, which the embedded-system rule also
    /// matches - it used to win and call these "ab mine med 011, microTech".
    /// </summary>
    [Theory]
    [InlineData("ab_mine_stanton4_med_011", "Mining Base, microTech", "microTech", LocationKind.Mine)]
    [InlineData("ab_mine_stanton2_med_006", "Mining Base, Crusader", "Crusader", LocationKind.Mine)]
    [InlineData("ab_mine_stanton4_sml_006", "Mining Base, microTech", "microTech", LocationKind.Mine)]
    [InlineData("ab_collector_gas_Stanton4", "Gas Collector, microTech", "microTech", LocationKind.Asteroid)]
    public void Names_asteroid_bases_by_the_planet_they_circle(
        string id, string name, string body, LocationKind kind)
    {
        var location = LocationResolver.Resolve(id);

        Assert.Equal(name, location.DisplayName);
        Assert.Equal("Stanton", location.System);
        Assert.Equal(body, location.Body);
        Assert.Equal(kind, location.Kind);
    }

    /// <summary>
    /// A marker or an operator's cluster says what was aimed at, not which
    /// place - so the jump waits for the arrival to name it, as a rest stop does.
    /// </summary>
    [Theory]
    [InlineData("PartyMemberMarker_297611060881")]
    [InlineData("UnattendedVehicleMarker_848089599253")]
    [InlineData("rayari_cluster_001_frost_{43C7065A-E2FC-4386-AEB6-CA4BF813F5B0}.socpak")]
    [InlineData("MISSION_QT_Distress_Beacon_SupportAttackedShip_863273994735")]
    public void Treats_markers_and_clusters_as_categories(string id) =>
        Assert.True(LocationResolver.IsAmbiguous(id));

    /// <summary>
    /// Platform ids number a platform without its moon, and the game's table
    /// reuses the numbers. These five entries are the real ones.
    /// </summary>
    private static string? Platforms(string id) => id switch
    {
        "Stanton1b_OLP_001" => "Ruptura OLP",
        "Stanton1b_OLP_002" => "Vivere OLP",
        "Stanton2b_OLP_001" => "Lamina OLP",
        "Stanton2b_OLP_002" => "Attritus OLP",
        "Pyro5a_OLP_001" => "Lamina OLP",
        _ => null
    };

    [Fact]
    public void Names_every_platform_a_shared_number_could_be()
    {
        var (name, system, body) = LocationResolver.Platform("002", Platforms);

        Assert.Equal("Attritus OLP or Vivere OLP", name);
        Assert.Equal("Stanton", system);
        Assert.Null(body);
    }

    [Fact]
    public void Leaves_the_system_open_when_a_number_is_used_in_two()
    {
        var (name, system, _) = LocationResolver.Platform("001", Platforms);

        Assert.Equal("Lamina OLP or Ruptura OLP", name);
        Assert.Null(system);
    }

    [Fact]
    public void Names_a_platform_whose_number_is_its_own()
    {
        var (name, system, body) = LocationResolver.Platform(
            "003", id => id == "Stanton4a_OLP_003" ? "Example OLP" : null);

        Assert.Equal("Example OLP", name);
        Assert.Equal("Stanton", system);
        Assert.Equal("Calliope", body);
    }

    [Fact]
    public void Calls_a_platform_a_platform_without_the_games_names()
    {
        var (name, system, body) = LocationResolver.Platform("002", lookup: null);

        Assert.Equal("Orbital Laser Platform", name);
        Assert.Null(system);
        Assert.Null(body);
    }

    [Fact]
    public void Caches_repeated_lookups()
    {
        Assert.Same(
            LocationResolver.Resolve("RR_MIC_LEO"),
            LocationResolver.Resolve("RR_MIC_LEO"));
    }
}
