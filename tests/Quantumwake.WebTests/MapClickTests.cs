namespace Quantumwake.WebTests;

/// <summary>
/// The invisible circle that catches a click on a map mark.
/// </summary>
/// <remarks>
/// This is the whole of a bug that read as three: places that would not open
/// their card, double-click that would not open the trade panel, and stops
/// that could not be added - all of them nodes whose click target had been
/// covered by a neighbour's. Half the map was unreachable and nothing looked
/// wrong, so the rule is pinned here rather than left to be re-noticed.
/// </remarks>
public class MapClickTests
{
    private static double Pad(double radius, string room) =>
        new Page().Number($"hitPad({radius}, {room})");

    private static double Pad(double radius, double room) =>
        Pad(radius, room.ToString(System.Globalization.CultureInfo.InvariantCulture));

    [Fact]
    public void A_lone_mark_keeps_the_generous_pad()
    {
        Assert.Equal(15, Pad(7, "Infinity"));
    }

    [Fact]
    public void Pads_meet_but_do_not_cross_where_the_marks_have_room()
    {
        const double radius = 7;

        // Two marks with clear air between them: 4 units of gap on top of the
        // marks themselves.
        const double gap = radius * 2 + 4;

        Assert.True(Pad(radius, gap / 2) * 2 <= gap, "a pad reached across the gap into the next mark");
    }

    [Fact]
    public void The_mark_is_always_its_own_target()
    {
        // Shoulder to shoulder the pads must overlap - clicking what you can
        // see has to work, and two marks that close overlap anyway.
        Assert.True(Pad(7, 0.4) >= 7);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(11)]
    public void The_pad_grows_with_the_mark_until_it_meets_the_neighbour(double radius)
    {
        Assert.Equal(radius + 8, Pad(radius, 1000));
        Assert.Equal(radius + 1, Pad(radius, 0));
    }
}
