using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// How far a saved point is from a fresh copy - the first thing a coordinate
/// answers that a name never could.
/// </summary>
/// <remarks>
/// The trap is the frame. <c>/showlocation</c> is relative to the system the
/// pilot is in, so a Pyro point measured from a Stanton copy is a number with
/// no meaning, and the one rule under test is that such a number is never
/// presented as a distance in the same breath as a real one.
/// </remarks>
public class PointDistanceTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 9, 2, 10, 0, TimeSpan.Zero);

    private static PinnedLocation Point(string label, double x, double y, double z, string? system) =>
        new(At, At, x, y, z, 0, null, system, label);

    [Fact]
    public void Distance_is_straight_line_on_all_three_axes()
    {
        var point = Point("p", 3_000, 4_000, 12_000, "Pyro");

        Assert.Equal(13_000, PointDistances.Between(0, 0, 0, point), 6);
    }

    [Fact]
    public void Points_come_nearest_first_and_a_point_in_another_system_after_every_point_in_this_one()
    {
        var points = new[]
        {
            Point("far, same system", 1_000_000, 0, 0, "Pyro"),
            Point("near, other system", 10, 0, 0, "Stanton"),
            Point("near, same system", 5_000, 0, 0, "Pyro"),
        };

        var ordered = PointDistances.From(0, 0, 0, "Pyro", points);

        Assert.Equal(["near, same system", "far, same system", "near, other system"],
            ordered.Select(d => d.Point.Label).ToList());
        Assert.True(ordered[0].SameSystem);
        Assert.False(ordered[2].SameSystem);
        Assert.Equal(10, ordered[2].Metres, 6);
    }

    /// <summary>
    /// A side with no system is not "the same system": a pin the logs could not
    /// place is a pin whose frame is unknown, and the distance is flagged.
    /// </summary>
    [Fact]
    public void An_unplaced_side_is_never_called_the_same_system()
    {
        var point = Point("p", 1, 0, 0, null);

        Assert.False(PointDistances.From(0, 0, 0, "Pyro", [point]).Single().SameSystem);
        Assert.False(PointDistances.From(0, 0, 0, null, [Point("q", 1, 0, 0, "Pyro")]).Single().SameSystem);
    }

    [Fact]
    public void A_near_point_carries_the_label_or_falls_back_to_the_belief()
    {
        var named = NearPoint.Of(PointDistances.From(0, 0, 0, "Pyro", [Point("Shelf", 1, 0, 0, "Pyro")]).Single());
        Assert.Equal("Shelf", named.Label);

        var unnamed = NearPoint.Of(PointDistances.From(0, 0, 0, "Pyro",
            [new PinnedLocation(At, At, 1, 0, 0, 0, "Ruin Station", "Pyro")]).Single());
        Assert.Equal("Ruin Station", unnamed.Label);
    }
}
