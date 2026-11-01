using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// A hull's capacity from the grids its loadout places: whole 1.25 m cells,
/// each grid counted as often as the loadout places it.
/// </summary>
public class CargoGridsTests
{
    private static CargoGrid Grid(string cls, double x, double y, double z, double scu) =>
        new(cls, null, x, y, z, scu, new Vec3(0, 0, 0), new Vec3(0, 0, 0), false);

    /// <summary>
    /// The Freelancer's mid grid is placed on a left and a right port: counted
    /// twice it is the 66 SCU the type-sum missed by 6.
    /// </summary>
    [Fact]
    public void A_grid_placed_twice_counts_twice()
    {
        var hull = new InstallCargoHull(true,
        [
            Grid("MISC_Freelancer_CargoGrid_Rear", 2.5, 11.25, 3.75, 54),
            Grid("MISC_Freelancer_CargoGrid_Mid", 1.25, 2.5, 1.25, 2),
            Grid("MISC_Freelancer_CargoGrid_Mid", 1.25, 2.5, 1.25, 2),
        ]);

        Assert.Equal(58, hull.Scu);
        Assert.Equal(58, GameCargoGrids.Scu(hull.Grids));
    }
}
