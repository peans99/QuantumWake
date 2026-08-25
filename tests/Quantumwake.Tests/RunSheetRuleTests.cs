using Quantumwake.Data;
using System.Globalization;

namespace Quantumwake.Tests;

/// <summary>
/// The rules a run sheet is read by, wherever it is read.
/// </summary>
/// <remarks>
/// Three of these were duplicated per caller and one of the copies had already
/// been left behind, which is the whole reason they are shared now.
/// </remarks>
public class RunSheetRuleTests
{
    private static TripStop Stop(bool done, params RunAction[] actions) =>
        new("s1", "RR_MIC_LEO", "Port Tressler", null, done, null, actions);

    private static RunAction Action(bool done) =>
        new("a1", "load", "Load 96 SCU Agricium", 96, "SCU", done, null);

    /// <summary>
    /// Landing ticks the stop, so anything selecting on Done alone loses it at
    /// the exact moment its run sheet applies. Three places ask this.
    /// </summary>
    [Theory]
    [InlineData(false, false, true)]   // not reached yet
    [InlineData(true, false, true)]    // landed, work outstanding
    [InlineData(true, true, false)]    // landed and everything crossed off
    public void A_stop_is_outstanding_until_it_is_reached_and_its_work_is_done(
        bool reached, bool workDone, bool expected)
    {
        Assert.Equal(expected, Trip.Outstanding(Stop(reached, Action(workDone))));
    }

    [Fact]
    public void A_stop_with_no_run_sheet_is_finished_the_moment_it_is_reached()
    {
        Assert.False(Trip.Outstanding(Stop(done: true)));
        Assert.True(Trip.Outstanding(Stop(done: false)));
    }

    [Fact]
    public void Next_and_Done_read_the_same_rule()
    {
        var trip = new Trip("t1", "Supply run", DateTimeOffset.UtcNow,
            [Stop(done: true, Action(done: false))]);

        Assert.NotNull(trip.Next);
        Assert.False(trip.Done);
    }

    /// <summary>
    /// A kind added to one path and not the other would arrive as a plain "do"
    /// from somebody's file while working on the page, which is exactly the
    /// drift the shared list exists to stop.
    /// </summary>
    [Theory]
    [InlineData("load", "load")]
    [InlineData("REFUEL", "refuel")]
    [InlineData("Sell", "sell")]
    [InlineData("negotiate", "do")]
    [InlineData(null, "do")]
    [InlineData("", "do")]
    public void A_kind_is_one_of_the_known_ones_or_a_plain_do(string? given, string expected)
    {
        Assert.Equal(expected, RunAction.CleanKind(given));
    }

    [Theory]
    [InlineData("scu", "SCU")]
    [InlineData("aUEC", "aUEC")]
    [InlineData("units", "units")]
    [InlineData("bananas", null)]
    [InlineData(null, null)]
    public void A_unit_is_one_of_the_known_ones_or_nothing(string? given, string? expected)
    {
        Assert.Equal(expected, RunAction.CleanUnit(given));
    }

    [Theory]
    [InlineData(96, 96)]
    [InlineData(0, 0)]
    [InlineData(-5, null)]
    [InlineData(2_000_000, null)]
    [InlineData(null, null)]
    public void A_quantity_outside_what_a_hold_could_carry_is_dropped(int? given, int? expected)
    {
        // xUnit hands an InlineData integer across as an int, so the cast is here
        // rather than in the attribute.
        Assert.Equal((decimal?)expected, RunAction.CleanQuantity(given));
    }
}

/// <summary>
/// The simulator writes numbers the parser has to read back.
/// </summary>
/// <remarks>
/// The buy line carries centi-SCU, which is the one conversion these scenarios
/// exist to prove, and it is written with a decimal point. On a machine whose
/// culture uses a comma the interpolation would emit "1600,000000" - which the
/// parser's digits-and-dots pattern cannot match, so every simulated purchase
/// would vanish from the one test built to catch exactly that.
/// </remarks>
public class SimulatedNumberCultureTests
{
    [Fact]
    public void A_simulated_purchase_is_written_with_a_decimal_point_in_any_culture()
    {
        var original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            var path = Path.Combine(Path.GetTempPath(), $"qw-culture-{Guid.NewGuid():N}.log");

            try
            {
                using (var writer = new Quantumwake.LogSim.LogWriter(path))
                {
                    writer.CommodityTrade(
                        DateTimeOffset.UtcNow, "geid", 63980m, 320,
                        "resource-guid", isSell: false, mode: "");
                }

                var text = File.ReadAllText(path);

                Assert.Contains("quantity[32000.000000 cSCU]", text);
                Assert.DoesNotContain(",000000", text);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
