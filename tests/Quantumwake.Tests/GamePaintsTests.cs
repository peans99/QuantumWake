using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// The hull-to-paint join and the default liveries, on hand-made lists.
/// </summary>
/// <remarks>
/// The reader itself is proven against the real archive (docs/datacore.md
/// records the counts). What is pinned here is the part that is a rule rather
/// than a read: which paints a hull gets, that the default livery comes first
/// when the files hold one, and that a hull without one is not handed some
/// other hull's - a Corsair has no stock render in the files, and showing it
/// the Clipper's would be a lie in the game's own colours.
/// </remarks>
public class GamePaintsTests
{
    private static readonly IReadOnlyList<GamePaint> Paints =
    [
        new("Paint_Clipper_Red_Black_White", "Clipper Ember Livery", "Paint_Clipper", GamePaints.RenderFolder + "Paint_Clipper_Red_Black_White_Logo.dds"),
        new("Paint_Clipper_Black_Cream_Red", "Clipper Auspicious Livery", "Paint_Clipper", GamePaints.RenderFolder + "Paint_Clipper_Black_Cream_Red_Logo.dds"),
        new("Paint_Corsair_Olive_Olive_Yellow", "Corsair Commando Livery", "Paint_Corsair", GamePaints.RenderFolder + "Paint_Corsair_Olive_Olive_Yellow_Icon.dds"),
        new("Paint_Hermes_Blue_White", "Hermes Frost Livery", "Paint_Hermes", GamePaints.RenderFolder + "Paint_Hermes_Blue_White_Icon.dds"),
    ];

    private static readonly string[] Folder =
    [
        GamePaints.RenderFolder + "Paint_Clipper_Red_Black_White_Logo.dds",
        GamePaints.RenderFolder + "Paint_Clipper_Black_Cream_Red_Logo.dds",
        GamePaints.RenderFolder + "paint_clipper_default.dds",
        GamePaints.RenderFolder + "Paint_RSI_Hermes_Default.dds",
        GamePaints.RenderFolder + "Paint_Corsair_Olive_Olive_Yellow_Icon.dds",
        // A split mip chain is not a picture on its own.
        GamePaints.RenderFolder + "Paint_Meteor_Default_Icon.dds.4",
        // A default that a paint item already claims is that paint, not a second one.
        GamePaints.RenderFolder + "Paint_Hermes_Blue_White_Icon.dds",
        @"Data\UI\Textures\EA\VehicleIcons\VehicleIcon_DRAK_Corsair.dds",
    ];

    [Fact]
    public void A_default_livery_is_lifted_from_the_folder_when_no_paint_item_claims_it()
    {
        var stock = GamePaints.Stock(Folder, Paints);

        Assert.Equal(["paint_clipper_default", "Paint_RSI_Hermes_Default"], stock.Select(p => p.Item).ToArray());
        Assert.All(stock, p => Assert.True(p.Stock));
        Assert.All(stock, p => Assert.Equal("Default livery", p.Name));
        Assert.Equal(GamePaints.RenderFolder + "paint_clipper_default.dds", stock[0].Render);
    }

    [Fact]
    public void The_default_livery_comes_first_for_its_hull_and_only_its_hull()
    {
        var all = Paints.Concat(GamePaints.Stock(Folder, Paints)).ToList();

        var clipper = GamePaints.ForHull(all, "DRAK_Clipper");
        Assert.Equal(3, clipper.Count);
        Assert.True(clipper[0].Stock);
        Assert.Equal("Clipper Auspicious Livery", clipper[1].Name);

        // The maker's code can sit in the file name; the hull still finds it.
        var hermes = GamePaints.ForHull(all, "RSI_Hermes");
        Assert.Equal(["Paint_RSI_Hermes_Default", "Paint_Hermes_Blue_White"], hermes.Select(p => p.Item).ToArray());

        // No stock render for the Corsair: its list is its paints, nothing borrowed.
        var corsair = GamePaints.ForHull(all, "DRAK_Corsair");
        Assert.Single(corsair);
        Assert.False(corsair[0].Stock);
    }
}
