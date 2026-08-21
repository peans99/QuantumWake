namespace Verselog.Core.Locations;

/// <summary>What kind of place a location id refers to.</summary>
public enum LocationKind
{
    Unknown,
    City,
    RestStop,
    Station,
    Outpost,
    DistributionCentre,
    Research,
    JumpPoint,
    Asteroid,
    Mine,
    Planet,
    Moon,
    NavPoint,
    MissionBeacon
}

/// <summary>
/// A location id resolved into something displayable and mappable.
/// </summary>
/// <param name="RawId">The original id from the log, always preserved.</param>
/// <param name="DisplayName">Human-readable name.</param>
/// <param name="System">Star system, e.g. <c>Stanton</c>. Null when unknown.</param>
/// <param name="Body">Planet or moon the location sits on or orbits.</param>
/// <param name="Kind">Category, used to pick a map marker.</param>
/// <param name="IsResolved">
/// False when nothing matched and the raw id is being shown as-is. Unresolved
/// locations are rendered as "unmapped" nodes rather than dropped, so new
/// content shows up as a gap instead of vanishing.
/// </param>
public sealed record ResolvedLocation(
    string RawId,
    string DisplayName,
    string? System,
    string? Body,
    LocationKind Kind,
    bool IsResolved)
{
    public static ResolvedLocation Unresolved(string rawId) =>
        new(rawId, rawId, null, null, LocationKind.Unknown, false);
}

/// <summary>
/// Static universe data for the systems seen in logs.
/// </summary>
/// <remarks>
/// Deliberately committed as code rather than fetched at runtime: the app makes
/// no network calls in standalone mode. Seeded from observed log ids and public
/// starmap data, then hand-corrected. Incomplete by nature - CIG ships new
/// locations every patch - which is why unresolved ids stay visible.
/// </remarks>
public static class Universe
{
    /// <summary>Planet/moon designators to names, per system.</summary>
    public static readonly IReadOnlyDictionary<string, string> StantonBodies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Hurston",
            ["1a"] = "Arial",
            ["1b"] = "Aberdeen",
            ["1c"] = "Magda",
            ["1d"] = "Ita",
            ["2"] = "Crusader",
            ["2a"] = "Cellin",
            ["2b"] = "Daymar",
            ["2c"] = "Yela",
            ["3"] = "ArcCorp",
            ["3a"] = "Lyria",
            ["3b"] = "Wala",
            ["4"] = "microTech",
            ["4a"] = "Calliope",
            ["4b"] = "Clio",
            ["4c"] = "Euterpe"
        };

    public static readonly IReadOnlyDictionary<string, string> PyroBodies =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["1"] = "Pyro I",
            ["2"] = "Monox",
            ["3"] = "Bloom",
            ["4"] = "Pyro IV",
            ["5"] = "Pyro V",
            ["6"] = "Terminus"
        };

    /// <summary>Rest-stop body prefixes, e.g. <c>RR_MIC_LEO</c>.</summary>
    public static readonly IReadOnlyDictionary<string, (string System, string Body)> RestStopBodies =
        new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["MIC"] = ("Stanton", "microTech"),
            ["CRU"] = ("Stanton", "Crusader"),
            ["HUR"] = ("Stanton", "Hurston"),
            ["ARC"] = ("Stanton", "ArcCorp"),
            ["S1"] = ("Stanton", "Hurston"),
            ["S2"] = ("Stanton", "Crusader"),
            ["S3"] = ("Stanton", "ArcCorp"),
            ["S4"] = ("Stanton", "microTech"),
            ["P1"] = ("Pyro", "Pyro I"),
            ["P2"] = ("Pyro", "Monox"),
            ["P3"] = ("Pyro", "Bloom"),
            ["P4"] = ("Pyro", "Pyro IV"),
            ["P5"] = ("Pyro", "Pyro V"),
            ["P6"] = ("Pyro", "Terminus")
        };

    /// <summary>
    /// Low-orbit stations, by the body token used in <c>RR_&lt;BODY&gt;_LEO</c> ids.
    /// </summary>
    /// <remarks>
    /// These have real names and are not "rest stops" at all - <c>RR_CRU_LEO</c>
    /// is Seraphim Station and <c>RR_MIC_LEO</c> is Port Tressler. The
    /// localisation table has no <c>RR_*_LEO</c> key to resolve them (it files
    /// them under <c>Stanton2_Transfer_Seraphim</c> and the like), so without
    /// this table the rules below invent a generic name for the single most
    /// visited location in a typical log.
    /// </remarks>
    public static readonly IReadOnlyDictionary<string, string> LeoStations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HUR"] = "Everus Harbor",
            ["S1"] = "Everus Harbor",
            ["CRU"] = "Seraphim Station",
            ["S2"] = "Seraphim Station",
            ["ARC"] = "Baijini Point",
            ["S3"] = "Baijini Point",
            ["MIC"] = "Port Tressler",
            ["S4"] = "Port Tressler"
        };

    /// <summary>Landing zones keyed by the token that appears in log ids.</summary>
    public static readonly IReadOnlyDictionary<string, string> Cities =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["NewBabbage"] = "New Babbage",
            ["Lorville"] = "Lorville",
            ["Orison"] = "Orison",
            ["Area18"] = "Area18",
            ["Area061"] = "Area 061"
        };

    /// <summary>Known jump-point route codes.</summary>
    public static readonly IReadOnlyDictionary<string, string> JumpPoints =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["stan-pyro"] = "Stanton – Pyro Jump Point",
            ["pyro-stan"] = "Pyro – Stanton Jump Point",
            ["stan-terra"] = "Stanton – Terra Jump Point",
            ["terra-stan"] = "Terra – Stanton Jump Point",
            ["StantonPyro"] = "Stanton – Pyro Jump Point",
            ["PyroStanton"] = "Pyro – Stanton Jump Point",
            ["StantonTerra"] = "Stanton – Terra Jump Point",
            ["TerraStanton"] = "Terra – Stanton Jump Point"
        };

    /// <summary>
    /// Places whose ids carry no system prefix and must be known by name.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string System, string? Body, LocationKind Kind)> WellKnown =
        new Dictionary<string, (string, string, string?, LocationKind)>(StringComparer.OrdinalIgnoreCase)
        {
            ["Nyx_Levski"] = ("Levski", "Nyx", "Delamar", LocationKind.City),
            ["Levski"] = ("Levski", "Nyx", "Delamar", LocationKind.City),
            ["GrimHEX"] = ("GrimHEX", "Stanton", "Yela", LocationKind.Station),
            ["Port_Tressler"] = ("Port Tressler", "Stanton", "microTech", LocationKind.Station),
            ["Port Tressler"] = ("Port Tressler", "Stanton", "microTech", LocationKind.Station),
            ["Seraphim_Station"] = ("Seraphim Station", "Stanton", "Crusader", LocationKind.Station),
            ["Seraphim Station"] = ("Seraphim Station", "Stanton", "Crusader", LocationKind.Station),
            ["Everus_Harbor"] = ("Everus Harbor", "Stanton", "Hurston", LocationKind.Station),
            ["Baijini_Point"] = ("Baijini Point", "Stanton", "ArcCorp", LocationKind.Station)
        };

    /// <summary>
    /// Site-name tokens that identify what a facility is. Checked in order, so
    /// more specific tokens come first.
    /// </summary>
    public static readonly IReadOnlyList<(string Token, LocationKind Kind)> SiteKinds =
    [
        ("TransportHub", LocationKind.Station),
        ("Monorail", LocationKind.Station),
        ("DistributionCentre", LocationKind.DistributionCentre),
        ("Delve", LocationKind.Outpost),
        ("Shubin", LocationKind.Mine),
        ("Mine", LocationKind.Mine),
        ("Farm", LocationKind.Outpost),
        ("Growery", LocationKind.Outpost),
        ("Rayari", LocationKind.Research),
        ("Research", LocationKind.Research),
        ("Hydro", LocationKind.Research),
        ("Asteroid", LocationKind.Asteroid),
        ("Outpost", LocationKind.Outpost),
        ("Station", LocationKind.Station)
    ];

    /// <summary>Company tokens appearing in facility ids.</summary>
    public static readonly IReadOnlyDictionary<string, string> Operators =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RayariHydro"] = "Rayari",
            ["Covalex"] = "Covalex",
            ["SakuraSun"] = "Sakura Sun",
            ["ArcCorp"] = "ArcCorp",
            ["Shubin"] = "Shubin",
            ["MicroTech"] = "microTech",
            ["HDMS"] = "HDMS"
        };
}
