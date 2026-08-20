using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace SCCompanion.Core.Locations;

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
            || MissionBeaconRegex.IsMatch(rawId);
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

        return TryNamedPlace(rawId, id)
            ?? TryJumpPointRestStop(rawId, id)
            ?? TryRestStop(rawId, id)
            ?? TryJumpPoint(rawId, id)
            ?? TryRsExt(rawId, id)
            ?? TryPlanetary(rawId, id)
            ?? TryOrbital(rawId, id)
            ?? TryEmbeddedSystem(rawId, id)
            ?? TryNavPoint(rawId, id)
            ?? TryWellKnown(rawId, id)
            ?? ResolvedLocation.Unresolved(rawId);
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
            return new ResolvedLocation(rawId, "Mission Beacon", null, null, LocationKind.MissionBeacon, true);

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

        var mine = MineRegex.Match(id);
        if (mine.Success)
        {
            var systemToken = mine.Groups["system"].Value;
            var number = mine.Groups["body"].Value;
            var bodies = systemToken.Equals("pyro", StringComparison.OrdinalIgnoreCase)
                ? Universe.PyroBodies
                : Universe.StantonBodies;

            var body = bodies.TryGetValue(number, out var b) ? b : null;
            return new ResolvedLocation(
                rawId,
                body is null ? "Mine" : $"{body} Mine",
                Title(systemToken),
                body,
                LocationKind.Mine,
                body is not null);
        }

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

    [GeneratedRegex(@"^NavPoint_\w+_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NavPointRegex { get; }

    [GeneratedRegex(@"^MISSION_QT_\w*Beacon_\d+$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MissionBeaconRegex { get; }

    [GeneratedRegex(@"^ab_mine_(?<system>[a-z]+?)(?<body>\d[a-z]?)_", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MineRegex { get; }

    [GeneratedRegex(@"_?\{[0-9A-Fa-f-]{36}\}", RegexOptions.Compiled)]
    private static partial Regex GuidSuffixRegex { get; }

    [GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled)]
    private static partial Regex CamelBoundaryRegex { get; }
}
