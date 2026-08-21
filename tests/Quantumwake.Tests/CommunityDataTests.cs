using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Digesting the community dataset. Fixtures mirror the real scunpacked shapes -
/// the fields we read plus fields we ignore - so schema drift in what we do not
/// read cannot break the lookup.
/// </summary>
public class CommunityDataTests
{
    private const string Commodities =
        """
        [
          {"UUID":"b999ef65-35be-45bf-908a-5eac6e06ba12","Key":"Waste","Name":"Waste",
           "Description":"Unwanted and unusable materials.","CommodityGroups":["Organic"]},
          {"UUID":"a8d60f68-349e-4755-98fc-9a0a8dc349a5","Key":"Dynaflex","Name":"DynaFlex",
           "CommodityGroups":["Manmade"]},
          {"UUID":"1b4c4042-5fdc-4b52-bec4-07085cb3520a","Key":"Tin","Name":null},
          {"Key":"NoUuid","Name":"Should be skipped"},
          {"UUID":"not-a-guid","Name":"Should also be skipped"},
          {"UUID":"91ad8b9e-426e-462a-a7f5-cf06eb79971a","Key":"EvidenceBox","Name":"<= PLACEHOLDER =>"}
        ]
        """;

    private const string TradeLocations =
        """
        [
          {"CommodityUUID":"b999ef65-35be-45bf-908a-5eac6e06ba12","CommodityName":"Waste",
           "SoldAt":[
             {"TradeLocationClassName":"DC_Stan_Hurston_S1_Farnesway_CargoShop","TradeLocationDisplayName":"On-Call Area"},
             {"TradeLocationClassName":"DC_Stan_Hurston_S1_Farnesway_SecurityWing","TradeLocationDisplayName":null},
             {"TradeLocationClassName":"RestStop_Stan_MIC_L1","TradeLocationDisplayName":"MIC-L1"}
           ],
           "BoughtAt":[
             {"TradeLocationClassName":"Outpost_Stan_Hur_S1_HDMS-Oparei_Store","TradeLocationDisplayName":null}
           ]}
        ]
        """;

    [Fact]
    public void Maps_uuid_to_display_name()
    {
        var digest = CommunityData.Digest(Commodities, TradeLocations);

        Assert.Equal("Waste", digest["b999ef65-35be-45bf-908a-5eac6e06ba12"].Name);
        Assert.Equal("DynaFlex", digest["a8d60f68-349e-4755-98fc-9a0a8dc349a5"].Name);
        Assert.Equal(["Organic"], digest["b999ef65-35be-45bf-908a-5eac6e06ba12"].Groups);
    }

    [Fact]
    public void Falls_back_to_the_key_when_the_name_is_null()
    {
        var digest = CommunityData.Digest(Commodities, TradeLocations);

        Assert.Equal("Tin", digest["1b4c4042-5fdc-4b52-bec4-07085cb3520a"].Name);
    }

    [Fact]
    public void Skips_entries_without_a_usable_id()
    {
        Assert.Equal(3, CommunityData.Digest(Commodities, TradeLocations).Count);
    }

    /// <summary>
    /// Two rooms of the Farnesway facility must collapse to one facility key -
    /// the map and the market count facilities, not rooms.
    /// </summary>
    [Fact]
    public void Rolls_rooms_up_to_facilities()
    {
        var waste = CommunityData.Digest(Commodities, TradeLocations)["b999ef65-35be-45bf-908a-5eac6e06ba12"];

        Assert.Equal(2, waste.Sold.Count);
        Assert.Contains("DC_Stan_Hurston_S1_Farnesway", waste.Sold);
        Assert.Contains("RestStop_Stan_MIC_L1", waste.Sold);
        Assert.Equal(["Outpost_Stan_Hur_S1_HDMS-Oparei"], waste.Bought);
    }

    [Fact]
    public void A_commodity_without_trade_rows_still_gets_a_name()
    {
        var digest = CommunityData.Digest(Commodities, TradeLocations);

        Assert.Empty(digest["a8d60f68-349e-4755-98fc-9a0a8dc349a5"].Sold);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var digest = CommunityData.Digest(Commodities, TradeLocations);

        // The log writes ids in either case across patches.
        Assert.True(digest.ContainsKey("B999EF65-35BE-45BF-908A-5EAC6E06BA12"));
    }

    [Fact]
    public void Tolerates_non_array_json()
    {
        Assert.Empty(CommunityData.Digest("""{"unexpected":"shape"}""", "[]"));
    }
}
