using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Reading what <c>/showlocation</c> puts on the clipboard.
/// </summary>
/// <remarks>
/// The reading below is the real one, copied out of the game. See
/// <c>docs/precise-poi.md</c>: it is 15.0000 gigametres from the system centre,
/// which is the kind of round number that means something is there.
/// </remarks>
public class ShipPositionTests
{
    private const string Real =
        "Coordinates: x:-9641671346.904709 y:-11490734321.189394 z:-91805.115677";

    [Fact]
    public void The_real_reading_parses()
    {
        var position = ShipPosition.Parse(Real);

        Assert.NotNull(position);
        Assert.Equal(-9641671346.904709, position.X, 3);
        Assert.Equal(-11490734321.189394, position.Y, 3);
        Assert.Equal(-91805.115677, position.Z, 3);
    }

    /// <summary>
    /// Fifteen gigametres to within forty kilometres - three parts in a
    /// million. The figure the panel leads with, so it is worth pinning.
    /// </summary>
    [Fact]
    public void The_distance_from_the_centre_comes_out_at_fifteen_gigametres()
    {
        var position = ShipPosition.Parse(Real)!;

        Assert.Equal(15.0, position.GigametresFromCentre, 3);
    }

    /// <summary>
    /// Z is 91 km against horizontal distances of eleven billion, so leaving it
    /// out of the distance changes nothing - which is the measurement behind
    /// the app storing flat positions.
    /// </summary>
    [Fact]
    public void Height_makes_no_difference_worth_having()
    {
        var flat = ShipPosition.Parse(Real)!.FromCentre;
        var withHeight = ShipPosition.Parse(Real.Replace("-91805.115677", "0"))!.FromCentre;

        Assert.Equal(flat, withHeight, 1);
    }

    [Fact]
    public void The_prefix_is_not_the_part_that_matters()
    {
        Assert.NotNull(ShipPosition.Parse(
            "x:-9641671346.904709 y:-11490734321.189394 z:-91805.115677"));
    }

    /// <summary>
    /// The clipboard is whatever was last copied, so this is asked of shopping
    /// lists and chat messages as often as it is of a location.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("see you at Everus")]
    [InlineData("x:1 y:2")]
    [InlineData("x:no y:2 z:3")]
    public void Anything_that_is_not_a_reading_is_refused(string? text)
    {
        Assert.Null(ShipPosition.Parse(text));
    }

    /// <summary>
    /// The game writes a full stop whatever the machine's regional settings
    /// say. Parsing this with a comma separator yields a number nine orders of
    /// magnitude wrong rather than failing, which is the worst kind of bug.
    /// </summary>
    [Fact]
    public void A_comma_decimal_machine_reads_the_same_number()
    {
        var original = System.Globalization.CultureInfo.CurrentCulture;

        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                new System.Globalization.CultureInfo("fr-FR");

            Assert.Equal(15.0, ShipPosition.Parse(Real)!.GigametresFromCentre, 3);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }
}

/// <summary>What the screen panel has been allowed to do.</summary>
public class ScreenSettingsTests
{
    /// <summary>
    /// Reading a pilot's screenshots is not something to arrive switched on
    /// because a default said so.
    /// </summary>
    [Fact]
    public void Nothing_is_allowed_until_it_is_asked_for()
    {
        Assert.Equal(ScreenMode.Off, new ScreenSettings().Mode);
        Assert.False(new ScreenSettings().Watch);
    }

    /// <summary>
    /// Watching with nothing switched on would let the panel come back saying
    /// it is watching while it does nothing at all.
    /// </summary>
    [Fact]
    public void Watching_with_the_panel_off_is_not_a_state()
    {
        var settled = ScreenSettings.Clean(ScreenMode.Off, watch: true);

        Assert.False(settled.Watch);
    }

    [Fact]
    public void Watching_holds_once_something_is_switched_on()
    {
        Assert.True(ScreenSettings.Clean(ScreenMode.CopyOnly, watch: true).Watch);
        Assert.True(ScreenSettings.Clean(ScreenMode.Screenshots, watch: true).Watch);
    }

    [Fact]
    public void A_store_remembers_across_a_restart()
    {
        var directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            new ScreenSettingsStore(directory).Save(ScreenMode.Screenshots, true);

            var reopened = new ScreenSettingsStore(directory).Current;

            Assert.Equal(ScreenMode.Screenshots, reopened.Mode);
            Assert.True(reopened.Watch);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
