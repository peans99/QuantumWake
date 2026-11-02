using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Hauling titles that ask the game for their places, so the logs name where a
/// haul goes without StarStrings installed.
/// </summary>
/// <remarks>
/// The places are not in the game files - one Red Wind definition was logged
/// going to Checkmate, Gaslight and Ruin Station - but the game fills
/// <c>~mission(Location|name)</c> and <c>~mission(Destination|name)</c> into a
/// title when it shows the contract. The keys and their vanilla text below are
/// 4.10.2's.
/// </remarks>
public class HaulTitleOverlayTests
{
    private static readonly Dictionary<string, string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Covalex_HaulCargo_AToB_title"] = GameMissions.BothEnds,
        ["RedWind_HaulCargo_MultiToSingle_title_01"] = GameMissions.DeliveryEnd,
        ["RedWind_HaulCargo_SingleToMulti_title_01"] = GameMissions.PickupEnd,
    };

    private const string Vanilla =
        "Covalex_HaulCargo_AToB_title=~mission(ReputationRank) Rank - Direct ~mission(CargoGradeToken) Cargo Haul\r\n" +
        "RedWind_HaulCargo_MultiToSingle_title_01,P=~mission(ReputationRank) | Stellar ~mission(CargoGradeToken) Haul\r\n" +
        "RedWind_HaulCargo_SingleToMulti_title_01=~mission(ReputationRank) | Stellar ~mission(CargoGradeToken) Haul  \r\n" +
        "Covalex_HaulCargo_AToB_desc=Haul it.\r\n";

    private static TextOverlayPlan Build(string ini, TextOverlayOptions? options = null) =>
        TextOverlay.Build(ini, _ => true, null, options, Routes);

    [Fact]
    public void Each_title_asks_for_the_ends_it_can_name()
    {
        var plan = Build(Vanilla);

        Assert.Equal(3, plan.Routed);
        Assert.Contains("Covalex_HaulCargo_AToB_title=~mission(ReputationRank) Rank - Direct ~mission(CargoGradeToken) Cargo Haul | ~mission(Location|name) > ~mission(Destination|name)\r\n", plan.Content);
        Assert.Contains("RedWind_HaulCargo_MultiToSingle_title_01,P=~mission(ReputationRank) | Stellar ~mission(CargoGradeToken) Haul | to ~mission(Destination|name)\r\n", plan.Content);

        // Trailing spaces stay where the game put them, after the addition.
        Assert.Contains("Haul | from ~mission(Location|name)  \r\n", plan.Content);
        Assert.Contains("Covalex_HaulCargo_AToB_desc=Haul it.\r\n", plan.Content);
        Assert.All(plan.Changes, c => Assert.Equal(TextOverlay.HaulTitleCategory, c.Category));
    }

    /// <summary>The whole point: what the game then shows is what the hauling planner already reads.</summary>
    [Theory]
    [InlineData("Covalex_HaulCargo_AToB_title", "Port Tressler", "NB Int. Spaceport")]
    [InlineData("RedWind_HaulCargo_MultiToSingle_title_01,P", null, "Ruin Station")]
    [InlineData("RedWind_HaulCargo_SingleToMulti_title_01", "Canard View", null)]
    public void The_title_the_game_renders_names_the_route(string key, string? pickup, string? delivery)
    {
        var line = Build(Vanilla).Content.Split("\r\n").Single(l => l.StartsWith(key + "=", StringComparison.Ordinal));
        var shown = line[(line.IndexOf('=') + 1)..]
            .Replace("~mission(ReputationRank)", "Junior")
            .Replace("~mission(CargoGradeToken)", "Small")
            .Replace("~mission(Location|name)", pickup ?? "Pickupname")
            .Replace("~mission(Destination|name)", delivery ?? "Destinationname")
            .TrimEnd() + ":";

        var route = HaulingContract.RouteFromTitle(shown)!;

        Assert.Equal(pickup, route.Pickup);
        Assert.Equal(delivery, route.Delivery);
    }

    /// <summary>Layered over StarStrings, its titles already ask; a second pair would print every place twice.</summary>
    [Fact]
    public void A_title_that_already_asks_is_left_alone()
    {
        const string mod = "Covalex_HaulCargo_AToB_title=~mission(ReputationRank) | <EM3>DIRECT</EM3> ~mission(CargoGradeToken) Haul | ~mission(Location|name) > ~mission(Destination|name) <EM4>[BP]*</EM4>\r\n";

        var plan = Build(mod);

        Assert.Equal(0, plan.Routed);
        Assert.Equal(mod, plan.Content);
    }

    /// <summary>
    /// A key read by its name: the Covalex hauls flown here pick their title at
    /// run time, no contract record names the key, and all 54 logged under the
    /// SingleToMulti one read "from Port Tressler".
    /// </summary>
    [Theory]
    [InlineData("Covalex_HaulCargo_SingleToMulti_title", GameMissions.PickupEnd)]
    [InlineData("RedWind_HaulCargo_MultiToSingle_title_01", GameMissions.DeliveryEnd)]
    [InlineData("Covalex_HaulCargo_AToB_Rehire_title", GameMissions.BothEnds)]
    [InlineData("Covalex_HaulCargo_AToB_desc", null)]
    [InlineData("iasi_Hauling_Small_Title", null)]
    public void A_title_key_that_names_its_shape_is_read_by_its_name(string key, string? ends)
    {
        Assert.Equal(ends, GameMissions.EndsByName(key));
    }

    [Fact]
    public void Turned_off_the_titles_are_the_games_own()
    {
        var plan = Build(Vanilla, new TextOverlayOptions(Routes: false));

        Assert.Equal(0, plan.Routed);
        Assert.Equal(Vanilla, plan.Content);
    }
}
