using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Parsing of the community commodity dataset. The fixture mirrors the real
/// scunpacked shape - UUID, Key, Name plus fields we ignore - so schema drift
/// in what we do not read cannot break the lookup.
/// </summary>
public class CommunityDataTests
{
    private const string Fixture =
        """
        [
          {"UUID":"b999ef65-35be-45bf-908a-5eac6e06ba12","Key":"Waste","Name":"Waste",
           "Description":"Unwanted and unusable materials.","CommodityGroups":["Organic"]},
          {"UUID":"a8d60f68-349e-4755-98fc-9a0a8dc349a5","Key":"Dynaflex","Name":"DynaFlex"},
          {"UUID":"1b4c4042-5fdc-4b52-bec4-07085cb3520a","Key":"Tin","Name":null},
          {"Key":"NoUuid","Name":"Should be skipped"},
          {"UUID":"not-a-guid","Name":"Should also be skipped"}
        ]
        """;

    [Fact]
    public void Maps_uuid_to_display_name()
    {
        var names = CommunityData.Parse(Fixture);

        Assert.Equal("Waste", names["b999ef65-35be-45bf-908a-5eac6e06ba12"]);
        Assert.Equal("DynaFlex", names["a8d60f68-349e-4755-98fc-9a0a8dc349a5"]);
    }

    [Fact]
    public void Falls_back_to_the_key_when_the_name_is_null()
    {
        var names = CommunityData.Parse(Fixture);

        Assert.Equal("Tin", names["1b4c4042-5fdc-4b52-bec4-07085cb3520a"]);
    }

    [Fact]
    public void Skips_entries_without_a_usable_id()
    {
        var names = CommunityData.Parse(Fixture);

        Assert.Equal(3, names.Count);
    }

    [Fact]
    public void Lookup_is_case_insensitive()
    {
        var names = CommunityData.Parse(Fixture);

        // The log writes ids in either case across patches.
        Assert.True(names.ContainsKey("B999EF65-35BE-45BF-908A-5EAC6E06BA12"));
    }

    [Fact]
    public void Tolerates_non_array_json()
    {
        Assert.Empty(CommunityData.Parse("""{"unexpected":"shape"}"""));
    }
}
