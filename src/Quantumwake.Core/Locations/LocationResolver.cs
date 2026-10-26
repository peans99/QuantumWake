using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Quantumwake.Core.Locations;

/// <summary>
/// Turns raw log location ids into displayable, mappable locations.
/// </summary>
/// <remarks>
/// <para>
/// Star Citizen location ids are structured, which is what makes a real map
/// possible without any positional data:
/// </para>
/// <code>
/// Stanton4_NewBabbage               -> New Babbage, microTech
/// Stanton4b_RayariHydro_Cantwell    -> Rayari Cantwell, Clio
/// RR_MIC_L1                         -> microTech L1 Rest Stop
/// LOC_rs_ext_stan-pyro_jp1          -> Stanton - Pyro Jump Point
/// Pyro2_Outpost_col_m_scrp_indy_001 -> Monox outpost
/// </code>
/// <para>
/// Rules are applied in order, most specific first. Anything unmatched comes
/// back as <see cref="ResolvedLocation.Unresolved"/> with the raw id intact -
/// new locations must show up as gaps on the map, never disappear.
/// </para>
/// </remarks>
public static partial class LocationResolver
{
    private static readonly ConcurrentDictionary<string, ResolvedLocation> Cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Optional hook returning the game's own name for a location id.
    /// </summary>
    /// <remarks>
    /// The game knows names no rule could derive - <c>RR_P5_L2</c> is "Gaslight",
    /// <c>Pyro2_Outpost_col_m_scrp_indy_001</c> is "Sunset Mesa" - so when one is
    /// available it wins over anything the rules below produce. System, body and
    /// kind still come from the id, because the name alone does not carry them.
    /// This is an ambient hook rather than a constructor argument because the
    /// resolver is static and called from deep inside parsing.
    /// </remarks>
    public static Func<string, string?>? NameLookup { get; set; }

    /// <summary>Drops cached resolutions, needed after <see cref="NameLookup"/> changes.</summary>
    public static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Ids that name a category rather than a place.
    /// </summary>
    /// <remarks>
    /// Quantum destinations are not always specific. <c>ObjectContainer_RestStop</c>
    /// is used for <i>every</i> rest stop - a trip from Area18 to
    /// "ObjectContainer_RestStop" actually arrived at microTech LEO. Counting
    /// these as one place merges unrelated destinations into a meaningless
    /// bucket, so callers should resolve them to the location actually reached.
    /// </remarks>
    public static bool IsAmbiguous(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return true;

        return rawId.Equals("ObjectContainer_RestStop", StringComparison.OrdinalIgnoreCase)
            || rawId.Equals("RestStop", StringComparison.OrdinalIgnoreCase)
            || NavPointRegex.IsMatch(rawId)
            || MissionBeaconRegex.IsMatch(rawId)
            || MarkerRegex.IsMatch(rawId)
            || OutpostClusterRegex.IsMatch(rawId);
    }

    /// <summary>Resolves an id, caching the result.</summary>
    public static ResolvedLocation Resolve(string rawId)
    {
        if (string.IsNullOrWhiteSpace(rawId))
            return ResolvedLocation.Unresolved(rawId ?? string.Empty);

        return Cache.GetOrAdd(rawId, ResolveUncached);
    }

    private static ResolvedLocation ResolveUncached(string rawId)
    {
        // Strip decorations that wrap otherwise-normal ids.
        var id = rawId;
        if (id.EndsWith(".socpak", StringComparison.OrdinalIgnoreCase))
            id = id[..^".socpak".Length];

        id = GuidSuffixRegex.Replace(id, string.Empty);
        id = id.TrimStart('\'').Trim();

        if (id.StartsWith("LOC_", StringComparison.OrdinalIgnoreCase))
            id = id[4..];
        if (id.StartsWith("OOC_", StringComparison.OrdinalIgnoreCase))
            id = id[4..];

        id = id.Replace("_objectContainer", string.Empty, StringComparison.OrdinalIgnoreCase)
               .Replace("ObjectContainer_", string.Empty, StringComparison.OrdinalIgnoreCase)
               .Replace("_LOC", string.Empty, StringComparison.OrdinalIgnoreCase);

        // Only as a suffix: "_OC" anywhere else would bite into "_occu_".
        if (id.EndsWith("_OC", StringComparison.OrdinalIgnoreCase))
            id = id[..^3];

        var resolved = TryNamedPlace(rawId, id)
            ?? TryNyx(rawId, id)
            ?? TryJumpPointRestStop(rawId, id)
            ?? TryRestStop(rawId, id)
            ?? TryJumpPoint(rawId, id)
            ?? TryRsExt(rawId, id)
            ?? TryPlanetary(rawId, id)
            ?? TryOrbital(rawId, id)
            ?? TryAsteroidBase(rawId, id)
            ?? TryEmbeddedSystem(rawId, id)
            ?? TryNavPoint(rawId, id)
            ?? TryWellKnown(rawId, id)
            ?? TryMarker(rawId, id)
            ?? TryOrbitalPlatform(rawId, id)
            ?? TryUnnamedSite(rawId, id)
            ?? ResolvedLocation.Unresolved(rawId);

        // The game's own name beats a derived one, but the id still supplies the
        // system, body and kind that the name cannot.
        var official = NameLookup?.Invoke(rawId) ?? NameLookup?.Invoke(id);

        if (!string.IsNullOrWhiteSpace(official))
            resolved = resolved with { DisplayName = official, IsResolved = true };

        return resolved;
    }

    /// <summary>
    /// Nyx, which follows none of Stanton's or Pyro's naming conventions.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The system is entirely absent from the id grammar the other two share -
    /// no numbered bodies, no <c>RR_</c> rest stops - so without a rule of its
    /// own the whole system collapsed to a single node, Levski, which is the one
    /// place the well-known table happened to list. The localisation table
    /// actually names about forty places there.
    /// </para>
    /// <code>
    /// Nyx_SocialStation_003    -> People's Service Station Theta
    /// Nyx_RockCracker_007      -> QV Breaker Station BRK-267
    /// Nyx_OutlawStation_Keeger -> Moraine Base           (Keeger Belt)
    /// Nyx_Stanton_JPStation    -> Stanton Gateway
    /// Nyx_AsteroidBelt1        -> Glaciem Ring
    /// </code>
    /// <para>
    /// The names all come from the game itself, so this only has to supply the
    /// system, body and kind that the id encodes and the name does not.
    /// </para>
    /// </remarks>
    private static ResolvedLocation? TryNyx(string rawId, string id)
    {
        if (!id.StartsWith("Nyx", StringComparison.OrdinalIgnoreCase)
            && !NyxGatewayRegex.IsMatch(id))
        {
            return null;
        }

        // Belts are the bodies here, and several ids name theirs in passing.
        var body = id.Contains("Glaciem", StringComparison.OrdinalIgnoreCase) ? "Glaciem Ring"
            : id.Contains("Keeger", StringComparison.OrdinalIgnoreCase) ? "Keeger Belt"
            : id.Contains("Levski", StringComparison.OrdinalIgnoreCase) ? "Delamar"
            : null;

        // Nyx_TSG_QVExtractionStation_028 and its siblings have no entry in the
        // game's names. The number stays, as BRK-267 does on a breaker station:
        // they are different stations, and a respawn is told from a revive by
        // the name changing - one name for all of them turned waking at 028
        // after going down in a station interior into "revived where you fell".
        var extraction = QvExtractionRegex.Match(id);
        if (extraction.Success)
        {
            var name = extraction.Groups["n"].Success
                ? $"QV Extraction Station {extraction.Groups["n"].Value}"
                : "QV Extraction Station";
            return new ResolvedLocation(rawId, name, "Nyx", body, LocationKind.Station, true);
        }

        var kind = NyxKind(id);
        if (kind is null)
            return null;

        return new ResolvedLocation(rawId, Spaced(id), "Nyx", body, kind.Value, true);
    }

    /// <summary>What sort of place a Nyx id names, or null if it is not one.</summary>
    private static LocationKind? NyxKind(string id)
    {
        // Ordered most specific first: "Nyx_Pyro_JPStation" is a station, while
        // "Nyx_JumpPoint_Pyro" is the jump point it guards.
        if (id.Contains("JPStation", StringComparison.OrdinalIgnoreCase))
            return LocationKind.Station;

        if (id.Contains("JumpPoint", StringComparison.OrdinalIgnoreCase))
            return LocationKind.JumpPoint;

        if (id.Contains("RockCracker", StringComparison.OrdinalIgnoreCase))
            return LocationKind.Mine;

        if (id.Contains("SocialStation", StringComparison.OrdinalIgnoreCase)
            || id.Contains("OutlawStation", StringComparison.OrdinalIgnoreCase)
            || id.Contains("Segment_Social", StringComparison.OrdinalIgnoreCase))
        {
            return LocationKind.Station;
        }

        if (id.Contains("AsteroidBelt", StringComparison.OrdinalIgnoreCase)
            || id.Contains("AsteroidCluster", StringComparison.OrdinalIgnoreCase))
        {
            return LocationKind.Asteroid;
        }

        if (id.Contains("Levski", StringComparison.OrdinalIgnoreCase))
            return LocationKind.City;

        // Bare bodies, the star, transit points and UI strings all share the
        // Nyx prefix. None of them is somewhere you fly to.
        return null;
    }

    /// <summary>Places known only by name, with no system prefix in the id.</summary>
    private static ResolvedLocation? TryNamedPlace(string rawId, string id)
    {
        if (!Universe.WellKnown.TryGetValue(id, out var place))
            return null;

        return new ResolvedLocation(rawId, place.Name, place.System, place.Body, place.Kind, true);
    }

    /// <summary>
    /// Ids carrying the system and body somewhere other than the start:
    /// <c>TheCollectorAsteroid_Stanton4</c>, <c>Outpost_OLP_Stanton1b_Vivere</c>.
    /// </summary>
    private static ResolvedLocation? TryEmbeddedSystem(string rawId, string id)
    {
        var m = EmbeddedSystemRegex.Match(id);
        if (!m.Success)
            return null;

        var systemToken = m.Groups["system"].Value;
        var bodyToken = m.Groups["body"].Value;

        var (system, bodies) = systemToken.Equals("Pyro", StringComparison.OrdinalIgnoreCase)
            ? ("Pyro", Universe.PyroBodies)
            : ("Stanton", Universe.StantonBodies);

        var body = bodies.TryGetValue(bodyToken, out var b) ? b : null;

        // Everything except the system token describes the site.
        var rest = EmbeddedSystemRegex.Replace(id, "_").Trim('_');
        var (name, kind) = DescribeSite(rest, body);

        return new ResolvedLocation(rawId, name, system, body, kind, body is not null);
    }

    /// <summary>
    /// <c>ab_mine_stanton4_med_011</c>, <c>ab_collector_gas_Stanton4</c>: a base
    /// in the asteroids around a planet.
    /// </summary>
    /// <remarks>
    /// Ahead of <see cref="TryEmbeddedSystem"/>, which also matches the
    /// <c>_stanton4_</c> in these and used to name 18 mining bases on this
    /// install "ab mine med 011, microTech". The game calls each one
    /// "Mining Base #XJZ-JR2"; the code is not in the id, so the planet stands in.
    /// </remarks>
    private static ResolvedLocation? TryAsteroidBase(string rawId, string id)
    {
        var m = AsteroidBaseRegex.Match(id);
        if (!m.Success)
            return null;

        var system = m.Groups["system"].Value.Equals("pyro", StringComparison.OrdinalIgnoreCase) ? "Pyro" : "Stanton";
        var bodies = system == "Pyro" ? Universe.PyroBodies : Universe.StantonBodies;
        var body = bodies.GetValueOrDefault(m.Groups["body"].Value);

        var (name, kind) = m.Groups["what"].Value.Equals("mine", StringComparison.OrdinalIgnoreCase)
            ? ("Mining Base", LocationKind.Mine)
            : ("Gas Collector", LocationKind.Asteroid);

        return new ResolvedLocation(
            rawId, body is null ? name : $"{name}, {body}", system, body, kind, body is not null);
    }

    /// <summary><c>RR_JP_StantonPyro</c> - a rest stop sitting at a jump point.</summary>
    private static ResolvedLocation? TryJumpPointRestStop(string rawId, string id)
    {
        var m = RestStopJumpPointRegex.Match(id);
        if (!m.Success)
            return null;

        var route = m.Groups["route"].Value;
        var name = Universe.JumpPoints.TryGetValue(route, out var known)
            ? $"{known} Rest Stop"
            : $"{Spaced(route)} Jump Point Rest Stop";

        return new ResolvedLocation(rawId, name, SystemFromRoute(route), null, LocationKind.RestStop, true);
    }

    /// <summary><c>RR_MIC_L1</c>, <c>RR_S4_L2</c>, <c>RR_CRU_LEO</c>.</summary>
    private static ResolvedLocation? TryRestStop(string rawId, string id)
    {
        var m = RestStopRegex.Match(id);
        if (!m.Success)
            return null;

        var bodyToken = m.Groups["body"].Value;
        var slot = m.Groups["slot"].Value.ToUpperInvariant();

        if (!Universe.RestStopBodies.TryGetValue(bodyToken, out var body))
            return new ResolvedLocation(rawId, $"{bodyToken} {slot} Rest Stop", null, null, LocationKind.RestStop, false);

        // LEO stations are named places, not rest stops.
        if (slot == "LEO" && Universe.LeoStations.TryGetValue(bodyToken, out var station))
            return new ResolvedLocation(rawId, station, body.System, body.Body, LocationKind.Station, true);

        return new ResolvedLocation(
            rawId,
            $"{body.Body} {slot} Rest Stop",
            body.System,
            body.Body,
            LocationKind.RestStop,
            true);
    }

    /// <summary><c>rs_ext_stan-pyro_jp1</c>.</summary>
    private static ResolvedLocation? TryJumpPoint(string rawId, string id)
    {
        var m = JumpPointRegex.Match(id);
        if (!m.Success)
            return null;

        var key = $"{m.Groups["from"].Value}-{m.Groups["to"].Value}";
        var name = Universe.JumpPoints.TryGetValue(key, out var known)
            ? known
            : $"{Title(m.Groups["from"].Value)} – {Title(m.Groups["to"].Value)} Jump Point";

        return new ResolvedLocation(
            rawId, name, SystemFromToken(m.Groups["from"].Value), null, LocationKind.JumpPoint, true);
    }

    /// <summary><c>rs_ext_cru-leo1</c>, <c>rs_ext_pyro3_l3</c>.</summary>
    private static ResolvedLocation? TryRsExt(string rawId, string id)
    {
        var m = RsExtRegex.Match(id);
        if (!m.Success)
            return null;

        var bodyToken = m.Groups["body"].Value;
        var slot = m.Groups["slot"].Value.ToUpperInvariant();

        // Low orbit carries a real station, and the id sometimes numbers it:
        // rs_ext_cru-leo1 is Seraphim Station, not "Crusader LEO1 Rest Stop".
        // Same treatment as the RR_CRU_LEO form of the same place.
        if (slot.StartsWith("LEO", StringComparison.Ordinal)
            && Universe.LeoStations.TryGetValue(bodyToken, out var leo))
        {
            var known = Universe.RestStopBodies.TryGetValue(bodyToken, out var orbit)
                ? orbit
                : default;

            return new ResolvedLocation(
                rawId, leo, known.System, known.Body, LocationKind.Station, true);
        }

        // "pyro3" style: system name plus body number.
        var pyro = PyroNumberedRegex.Match(bodyToken);
        if (pyro.Success && Universe.PyroBodies.TryGetValue(pyro.Groups["n"].Value, out var pyroBody))
            return new ResolvedLocation(rawId, $"{pyroBody} {slot} Rest Stop", "Pyro", pyroBody, LocationKind.RestStop, true);

        if (Universe.RestStopBodies.TryGetValue(bodyToken, out var body))
            return new ResolvedLocation(rawId, $"{body.Body} {slot} Rest Stop", body.System, body.Body, LocationKind.RestStop, true);

        return new ResolvedLocation(rawId, $"{Title(bodyToken)} {slot} Rest Stop", null, null, LocationKind.RestStop, false);
    }

    /// <summary>
    /// <c>Stanton4_NewBabbage</c>, <c>Stanton4b_RayariHydro_Cantwell</c>,
    /// <c>Pyro2_Outpost_col_m_scrp_indy_001</c>.
    /// </summary>
    private static ResolvedLocation? TryPlanetary(string rawId, string id)
    {
        var m = PlanetaryRegex.Match(id);
        if (!m.Success)
            return null;

        var systemToken = m.Groups["system"].Value;
        var bodyToken = m.Groups["body"].Value;
        var rest = m.Groups["rest"].Value;

        var (system, bodies) = systemToken.Equals("Pyro", StringComparison.OrdinalIgnoreCase)
            ? ("Pyro", Universe.PyroBodies)
            : ("Stanton", Universe.StantonBodies);

        var resolvedBody = bodies.TryGetValue(bodyToken, out var b) ? b : null;
        var (name, kind) = DescribeSite(rest, resolvedBody);

        return new ResolvedLocation(
            rawId,
            name,
            system,
            resolvedBody,
            kind,
            resolvedBody is not null);
    }

    /// <summary><c>Stanton_4_Microtech</c> from an OOC id - the body itself.</summary>
    private static ResolvedLocation? TryOrbital(string rawId, string id)
    {
        var m = OrbitalRegex.Match(id);
        if (!m.Success)
            return null;

        var systemToken = m.Groups["system"].Value;
        var bodyToken = m.Groups["body"].Value;

        var (system, bodies) = systemToken.Equals("Pyro", StringComparison.OrdinalIgnoreCase)
            ? ("Pyro", Universe.PyroBodies)
            : ("Stanton", Universe.StantonBodies);

        var resolvedBody = bodies.TryGetValue(bodyToken, out var b)
            ? b
            : Title(m.Groups["name"].Value);

        var kind = bodyToken.Length > 1 ? LocationKind.Moon : LocationKind.Planet;

        return new ResolvedLocation(rawId, resolvedBody, system, resolvedBody, kind, true);
    }

    private static ResolvedLocation? TryNavPoint(string rawId, string id)
    {
        if (NavPointRegex.IsMatch(id))
            return new ResolvedLocation(rawId, "Nav Point", null, null, LocationKind.NavPoint, true);

        if (MissionBeaconRegex.IsMatch(id))
        {
            var name = id.Contains("Distress", StringComparison.OrdinalIgnoreCase) ? "Distress Beacon" : "Mission Beacon";
            return new ResolvedLocation(rawId, name, null, null, LocationKind.MissionBeacon, true);
        }

        return null;
    }

    /// <summary>
    /// <c>PartyMemberMarker_297611060881</c>, <c>UnattendedVehicleMarker_848089599253</c>:
    /// a marker hung on something that moves, so it names who or what was
    /// followed and never where. The arrival says where.
    /// </summary>
    private static ResolvedLocation? TryMarker(string rawId, string id)
    {
        var m = MarkerRegex.Match(id);
        if (!m.Success)
            return null;

        var name = m.Groups["what"].Value.Equals("PartyMember", StringComparison.OrdinalIgnoreCase)
            ? "Party Member"
            : "Unattended Ship";

        return new ResolvedLocation(rawId, name, null, null, LocationKind.NavPoint, true);
    }

    /// <summary>
    /// <c>orbtl_002_plat_001_util_a_orbital_001_occu_a_final</c>, an orbital
    /// laser platform.
    /// </summary>
    /// <remarks>
    /// The id numbers the platform but never says which moon it circles, and
    /// the numbers repeat: the game's own table has <c>Stanton1b_OLP_002</c>
    /// "Vivere OLP" and <c>Stanton2b_OLP_002</c> "Attritus OLP". When only one
    /// body has that number it is the platform; when several do, all of them
    /// are named rather than one picked. The log does name the platform in a
    /// routing line, but only when a moon is in the way - 252 of 1,022 target
    /// selections on this install had one, and most of those name the body
    /// being routed round rather than the destination.
    /// </remarks>
    private static ResolvedLocation? TryOrbitalPlatform(string rawId, string id)
    {
        var m = OrbitalPlatformRegex.Match(id);
        if (m.Success)
        {
            var (name, system, body) = Platform(m.Groups["n"].Value, NameLookup);
            return new ResolvedLocation(rawId, name, system, body, LocationKind.Station, body is not null);
        }

        // A platform's interior without the orbtl_ prefix that numbers it.
        if (id.StartsWith("util_a_orbital_", StringComparison.OrdinalIgnoreCase))
            return new ResolvedLocation(rawId, "Orbital Platform", null, null, LocationKind.Station, false);

        return null;
    }

    /// <summary>The platform, or platforms, the game's names give a number.</summary>
    internal static (string Name, string? System, string? Body) Platform(
        string number, Func<string, string?>? lookup)
    {
        var found = new List<(string Name, string System, string? Body)>();

        if (lookup is not null)
        {
            foreach (var (system, bodies) in new[] { ("Stanton", Universe.StantonBodies), ("Pyro", Universe.PyroBodies) })
            {
                // Moons are asked for by letter rather than from the table, which
                // lists none of Pyro's - and Pyro5a_OLP_001 is a platform too.
                foreach (var planet in bodies.Keys.Where(k => k.All(char.IsDigit)))
                {
                    foreach (var token in "abcdef".Select(letter => planet + letter).Prepend(planet))
                    {
                        if (lookup($"{system}{token}_OLP_{number}") is { Length: > 0 } name)
                            found.Add((name, system, bodies.GetValueOrDefault(token)));
                    }
                }
            }
        }

        if (found.Count == 1)
            return found[0];

        if (found.Count == 0)
            return ("Orbital Laser Platform", null, null);

        var names = found.Select(f => f.Name).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var systems = found.Select(f => f.System).Distinct().ToList();
        return (string.Join(" or ", names), systems.Count == 1 ? systems[0] : null, null);
    }

    /// <summary>
    /// Sites the id describes but does not name: one operator's cluster of
    /// surface outposts, a farm, a stash, a derelict, Pyro's asteroid regions.
    /// </summary>
    /// <remarks>
    /// <c>rayari_cluster_001_frost</c> says whose outposts and on what ground,
    /// never which one, so like <c>shubin_cluster</c> it gets the operator's
    /// name and the arrival supplies the outpost.
    /// </remarks>
    private static ResolvedLocation? TryUnnamedSite(string rawId, string id)
    {
        if (id.StartsWith("levski_", StringComparison.OrdinalIgnoreCase))
        {
            var levski = Universe.WellKnown["Levski"];
            return new ResolvedLocation(rawId, levski.Name, levski.System, levski.Body, levski.Kind, true);
        }

        var cluster = OutpostClusterRegex.Match(id);
        if (cluster.Success)
        {
            var (name, kind) = cluster.Groups["op"].Value.ToLowerInvariant() switch
            {
                "rayari" => ("Rayari Research Cluster", LocationKind.Research),
                "hurdyn" => ("HurDyn Mining Cluster", LocationKind.Mine),
                "arccorp" => ("ArcCorp Mining Cluster", LocationKind.Mine),
                var other => ($"{Title(other)} Outpost Cluster", LocationKind.Outpost)
            };

            return new ResolvedLocation(rawId, name, null, null, kind, true);
        }

        if (id.StartsWith("indy_farmer_", StringComparison.OrdinalIgnoreCase))
            return new ResolvedLocation(rawId, "Independent Farm", null, null, LocationKind.Outpost, true);

        if (id.StartsWith("stash_", StringComparison.OrdinalIgnoreCase))
            return new ResolvedLocation(rawId, "Stash", null, null, LocationKind.Outpost, true);

        var derelict = DerelictRegex.Match(id);
        if (derelict.Success)
        {
            var body = Universe.StantonBodies.GetValueOrDefault(derelict.Groups["body"].Value);
            var ship = derelict.Groups["ship"].Value.Equals("rclmr", StringComparison.OrdinalIgnoreCase)
                ? "Derelict Reclaimer"
                : "Derelict Ship";

            return new ResolvedLocation(
                rawId, body is null ? ship : $"{ship}, {body}", "Stanton", body, LocationKind.Unknown, body is not null);
        }

        var region = PyroRegionRegex.Match(id);
        if (region.Success)
        {
            var letter = region.Groups["region"].Value.ToUpperInvariant();
            var (name, kind) = region.Groups["code"].Success
                ? ($"Asteroid Cluster {region.Groups["code"].Value}", LocationKind.Asteroid)
                : region.Groups["mine"].Success
                    ? ($"Region {letter} Mining Base", LocationKind.Mine)
                    : ($"Region {letter} Asteroid Base", LocationKind.Asteroid);

            return new ResolvedLocation(rawId, name, "Pyro", null, kind, true);
        }

        return null;
    }

    /// <summary>Bare landing-zone and generic tokens that carry no system prefix.</summary>
    private static ResolvedLocation? TryWellKnown(string rawId, string id)
    {
        var trimmed = id.Replace("_City", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (Universe.Cities.TryGetValue(trimmed, out var city))
        {
            var system = trimmed.Equals("Area061", StringComparison.OrdinalIgnoreCase) ? "Stanton" : "Stanton";
            return new ResolvedLocation(rawId, city, system, null, LocationKind.City, true);
        }

        if (trimmed.Equals("RestStop", StringComparison.OrdinalIgnoreCase))
            return new ResolvedLocation(rawId, "Rest Stop", null, null, LocationKind.RestStop, true);

        if (id.StartsWith("shubin", StringComparison.OrdinalIgnoreCase))
            return new ResolvedLocation(rawId, "Shubin Mining Cluster", null, null, LocationKind.Outpost, true);

        return null;
    }

    /// <summary>Names the site portion of a planetary id and picks its kind.</summary>
    private static (string Name, LocationKind Kind) DescribeSite(string rest, string? body)
    {
        var suffix = body is null ? string.Empty : $", {body}";

        if (Universe.Cities.TryGetValue(rest, out var city))
            return (city, LocationKind.City);

        var parts = rest.Split('_', StringSplitOptions.RemoveEmptyEntries);

        // Stanton3b_ArcCorp_Area061
        foreach (var part in parts)
        {
            if (Universe.Cities.TryGetValue(part, out var namedCity))
                return (namedCity, LocationKind.City);
        }

        if (rest.Contains("DistributionCentre", StringComparison.OrdinalIgnoreCase))
        {
            var op = parts.Select(p => Universe.Operators.GetValueOrDefault(p)).FirstOrDefault(o => o is not null);
            return ($"{op ?? "Distribution"} Distribution Centre{suffix}", LocationKind.DistributionCentre);
        }

        if (rest.Contains("Rayari", StringComparison.OrdinalIgnoreCase))
        {
            var site = parts.LastOrDefault() ?? "Research";
            return ($"Rayari {Title(site)}{suffix}", LocationKind.Research);
        }

        // A bare "Outpost_..." id carries no distinguishing name of its own.
        if (parts.Length > 0 && parts[0].Equals("Outpost", StringComparison.OrdinalIgnoreCase) && parts.Length <= 2)
            return ($"Outpost{(body is null ? string.Empty : $" on {body}")}", LocationKind.Outpost);

        // Otherwise keep the descriptive name and classify it by its tokens.
        foreach (var (token, kind) in Universe.SiteKinds)
        {
            if (rest.Contains(token, StringComparison.OrdinalIgnoreCase))
                return ($"{Spaced(rest)}{suffix}", kind);
        }

        return ($"{Spaced(rest)}{suffix}", LocationKind.Unknown);
    }

    private static string? SystemFromRoute(string route) =>
        route.StartsWith("Pyro", StringComparison.OrdinalIgnoreCase) ? "Pyro" : "Stanton";

    private static string? SystemFromToken(string token) => token.ToLowerInvariant() switch
    {
        "stan" => "Stanton",
        "pyro" => "Pyro",
        "terra" => "Terra",
        "cru" => "Stanton",
        _ => null
    };

    private static string Title(string value) =>
        value.Length == 0 ? value : char.ToUpperInvariant(value[0]) + value[1..];

    /// <summary>Splits camel case while leaving acronyms intact.</summary>
    private static string Spaced(string value) =>
        CamelBoundaryRegex.Replace(value.Replace('_', ' '), " ").Replace("  ", " ").Trim();

    [GeneratedRegex(@"^(?:RR_JP_)(?<route>\w+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RestStopJumpPointRegex { get; }

    [GeneratedRegex(@"^RR_(?<body>[A-Za-z0-9]+)_(?<slot>L\d|LEO|L\d[A-Za-z]?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RestStopRegex { get; }

    [GeneratedRegex(@"^rs_ext_(?<from>[a-z]+)-(?<to>[a-z]+)_jp\d*$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex JumpPointRegex { get; }

    [GeneratedRegex(@"^rs_ext_(?<body>[a-z]+\d*)[-_](?<slot>l\d|leo\d?|l\d[a-z]?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex RsExtRegex { get; }

    [GeneratedRegex(@"^(?:pyro)(?<n>\d)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PyroNumberedRegex { get; }

    [GeneratedRegex(@"^(?<system>Stanton|Pyro)(?<body>\d[a-z]?)_(?<rest>.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PlanetaryRegex { get; }

    [GeneratedRegex(@"^(?<system>Stanton|Pyro)_(?<body>\d[a-z]?)_(?<name>\w+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OrbitalRegex { get; }

    // System + body appearing anywhere but the start of the id.
    [GeneratedRegex(@"_(?<system>Stanton|Pyro)(?<body>\d[a-z]?)(?=_|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex EmbeddedSystemRegex { get; }

    // The gateway stations on the far side of a jump are named for the system
    // they lead to, so Nyx's sit under Stanton's and Pyro's prefixes.
    [GeneratedRegex(@"^(?:Stanton|Pyro)_(?:Nyx_JPStation|JumpPoint_Nyx)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NyxGatewayRegex { get; }

    [GeneratedRegex(@"^NavPoint_\w+_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NavPointRegex { get; }

    // MISSION_QT_Bounty_Beacon_N, and the ones that say what the beacon is for
    // after it: MISSION_QT_Distress_Beacon_SupportAttackedShip_N.
    [GeneratedRegex(@"^MISSION_QT_\w*Beacon(?:_[A-Za-z]\w*?)?_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MissionBeaconRegex { get; }

    [GeneratedRegex(@"^(?<what>PartyMember|UnattendedVehicle)Marker_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MarkerRegex { get; }

    [GeneratedRegex(@"^orbtl_(?<n>\d+)_plat_", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OrbitalPlatformRegex { get; }

    // Matched against the raw id too, by IsAmbiguous, so no anchor at the end.
    [GeneratedRegex(@"^(?<op>[a-z]+)_cluster_\d+_", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OutpostClusterRegex { get; }

    // oc_st4_drlct_rclmr_sfce_frst_ovgr_a: a Reclaimer derelict on microTech's surface.
    [GeneratedRegex(@"^oc_st(?<body>\d[a-z]?)_drlct_(?<ship>[a-z]+)_", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DerelictRegex { get; }

    // AsteroidCluster_Pyro_RegionB_BGR-560, ab_mine_pyro_regionb_med_008,
    // RegionA_1base_ab_pyro_final_set_encounter-003.
    [GeneratedRegex(
        @"^(?:AsteroidCluster_Pyro_Region(?<region>[a-z])_(?<code>[A-Z]+-\d+)|(?<mine>ab_mine_)pyro_region(?<region>[a-z])_|Region(?<region>[a-z])_\w*?_ab_pyro_)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PyroRegionRegex { get; }

    [GeneratedRegex(@"QVExtractionStation(?:_(?<n>\d+))?", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex QvExtractionRegex { get; }

    // ab_mine_stanton4_med_011, ab_collector_gas_Stanton4.
    [GeneratedRegex(@"^ab_(?<what>mine|collector)_(?:[a-z]+_)?(?<system>stanton|pyro)(?<body>\d[a-z]?)(?:_|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex AsteroidBaseRegex { get; }

    [GeneratedRegex(@"_?\{[0-9A-Fa-f-]{36}\}", RegexOptions.Compiled)]
    private static partial Regex GuidSuffixRegex { get; }

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled)]
    private static partial Regex CamelBoundaryRegex { get; }
}
