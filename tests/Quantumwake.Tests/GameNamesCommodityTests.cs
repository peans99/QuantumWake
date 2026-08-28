using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Commodities in the game's own text table.
/// </summary>
/// <remarks>
/// They sit under <c>items_commodities_</c>, which is neither of the two
/// prefixes the reader used to look for, so 307 commodity names went unread,
/// along with the descriptions for 174 of them. Fixtures are real key shapes
/// from a 4.10.191.2241 install.
/// </remarks>
public class GameNamesCommodityTests
{
    private const string Ini = """
        items_commodities_Agricium=Agricium
        items_commodities_Agricium_desc=A rare and valuable silvery metal with a blue-green sheen.
        items_commodities_Laranite=Laranite
        items_commodities_Coal=Coal
        item_Name_behr_rifle_ballistic_01=P4-AR Rifle
        vehicle_NameANVL_Hornet=Anvil Hornet
        """;

    [Fact]
    public void It_reads_commodity_names_the_reader_used_to_walk_past()
    {
        var names = GameNames.Parse(Ini);

        Assert.Equal(3, names.CommodityCount);
        Assert.Equal("Agricium", names.Commodity("Agricium"));
        Assert.Equal("Coal", names.Commodity("Coal"));
    }

    /// <summary>
    /// Looked up by displayed name, because whoever wants a description arrived
    /// holding one from the community dataset rather than an ini key.
    /// </summary>
    [Fact]
    public void A_description_is_found_by_the_name_the_player_sees()
    {
        var names = GameNames.Parse(Ini);

        Assert.StartsWith("A rare and valuable silvery metal", names.CommodityDescription("Agricium"));
    }

    /// <summary>
    /// Most commodities have a name and no blurb, and that is not a failure -
    /// 307 names on this install, 174 of which the community dataset can match
    /// to a description.
    /// </summary>
    [Fact]
    public void A_commodity_with_no_description_returns_null_rather_than_its_name()
    {
        var names = GameNames.Parse(Ini);

        Assert.Null(names.CommodityDescription("Laranite"));
        Assert.Null(names.CommodityDescription("Nothing At All"));
    }

    /// <summary>
    /// The description key must not be mistaken for a commodity of its own, or
    /// the table grows a second "Agricium_desc" entry nobody can look up.
    /// </summary>
    [Fact]
    public void A_description_key_does_not_become_a_commodity()
    {
        var names = GameNames.Parse(Ini);

        Assert.Null(names.Commodity("Agricium_desc"));
    }

    /// <summary>
    /// The prefixes are neighbours in the file and must not capture each other:
    /// "items_commodities_" begins with "item", which is exactly why a careless
    /// prefix test would swallow it.
    /// </summary>
    [Fact]
    public void Items_and_vehicles_are_still_read_alongside()
    {
        var names = GameNames.Parse(Ini);

        Assert.Equal(1, names.ItemCount);
        Assert.Equal(1, names.VehicleCount);
        Assert.Null(names.Commodity("behr_rifle_ballistic_01"));
    }
}
