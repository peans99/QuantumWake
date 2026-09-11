namespace Quantumwake.Data;

/// <summary>
/// How far a saved point is from a copied location.
/// </summary>
/// <param name="Metres">Straight-line distance in the game's own metres, all three axes.</param>
/// <param name="SameSystem">
/// Whether both sides are known to be in one system. A <c>/showlocation</c> is
/// relative to the system the pilot is in, so the same numbers mean different
/// places in Stanton and Pyro - a distance across systems is arithmetic on two
/// unrelated frames, and the page says so rather than printing it as a range.
/// </param>
public sealed record PointDistance(PinnedLocation Point, double Metres, bool SameSystem);

/// <summary>
/// The first question a coordinate can answer that a name never could: how
/// far is the nearest thing I marked?
/// </summary>
/// <remarks>
/// Nothing here decides what "at" a point means. A metre is absurd and a
/// gigametre is useless, and the right answer differs between a cave mouth
/// and a belt; the distances are shown and the pilot judges. Systems are
/// compared by name, and a side with no system - a pin the logs could not
/// place and the pilot has not named - is never called the same system.
/// </remarks>
public static class PointDistances
{
    /// <summary>Every point with its distance from the copy, nearest first, same-system points before the rest.</summary>
    public static IReadOnlyList<PointDistance> From(
        double x, double y, double z, string? system, IEnumerable<PinnedLocation> points)
    {
        return [.. points
            .Select(point => new PointDistance(point, Between(x, y, z, point), SameSystem(system, point.System)))
            .OrderByDescending(d => d.SameSystem)
            .ThenBy(d => d.Metres)];
    }

    public static double Between(double x, double y, double z, PinnedLocation point) =>
        Between(x, y, z, point.X, point.Y, point.Z);

    public static double Between(double x, double y, double z, double px, double py, double pz)
    {
        var dx = px - x;
        var dy = py - y;
        var dz = pz - z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    /// <summary>Both named, and the same name; a side with no system is never the same system.</summary>
    public static bool SameSystem(string? a, string? b) =>
        a is { Length: > 0 } && b is { Length: > 0 } && string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>A saved point as the page shows it beside a fresh copy: which, how far, and whether the frames match.</summary>
public sealed record NearPoint(DateTimeOffset SourceAt, string Label, string? System, double Metres, bool SameSystem)
{
    public static NearPoint Of(PointDistance d) => new(
        d.Point.SourceAt,
        d.Point.Label ?? d.Point.Believed ?? "Copied location",
        d.Point.System,
        d.Metres,
        d.SameSystem);
}

/// <summary>
/// The points measured from wherever the pilot last copied, for the Points
/// page. Null <see cref="From"/> when nothing has been copied yet.
/// </summary>
public sealed record PointsFromHere(ClipboardSighting? From, IReadOnlyList<NearPoint> Points);
