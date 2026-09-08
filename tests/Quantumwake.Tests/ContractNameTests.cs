using Quantumwake.Core.State;

namespace Quantumwake.Tests;

/// <summary>
/// Whether a contract's name is a title or the game's own identifier.
/// </summary>
/// <remarks>
/// The marker line the game writes carries a contract definition id and never
/// the localised title, so nearly every name this app shows is its own
/// rendering of an id. Saying so is the point of the flag: "Small Grade4" is a
/// ship-size class and a tier, not a phrase anybody wrote, and a page that
/// prints it unlabelled invents a title the game never showed. Across this
/// install's 159 logs, 124 of 126 distinct contract strings carry no display
/// text at all.
/// </remarks>
public class ContractNameTests
{
    [Theory]
    [InlineData("HaulCargo_AToB_Refined_Ore_Quartz_Stanton4_Small_Grade")]
    [InlineData("Covalex_Stanton_VeryHard_RecoverCargo")]
    [InlineData("BountyHuntersGuild_Bounty_Stanton_Easy_0")]
    [InlineData("GillysPilotSchool_Mission06_2")]
    public void An_identifier_is_recognised_as_one(string raw) =>
        Assert.True(ContractNameParser.Parse(raw).FromGameId);

    /// <summary>
    /// Two of this install's strings carry a space inside one token. They are
    /// still identifiers, and the underscores are what say so.
    /// </summary>
    [Fact]
    public void A_space_inside_a_token_does_not_make_it_a_title() =>
        Assert.True(ContractNameParser.Parse("Redwind_ASD_Medical Supplies_0").FromGameId);

    [Theory]
    [InlineData("Deliver the package to Area18")]
    [InlineData("[150 Rep] Eliminate the Boss")]
    public void Real_wording_is_left_alone(string raw) =>
        Assert.False(ContractNameParser.Parse(raw).FromGameId);

    [Fact]
    public void Nothing_at_all_is_not_an_identifier()
    {
        Assert.False(ContractNameParser.IsGameId(null));
        Assert.False(ContractNameParser.IsGameId("   "));
    }

    /// <summary>
    /// The flag is about provenance, not about how the name reads: the same
    /// string still decomposes into the facets the columns use.
    /// </summary>
    [Fact]
    public void An_identifier_still_parses_into_facets()
    {
        var parsed = ContractNameParser.Parse("Covalex_Stanton_VeryHard_RecoverCargo");

        Assert.True(parsed.FromGameId);
        Assert.Equal("Covalex", parsed.Issuer);
        Assert.Equal("Stanton", parsed.System);
        Assert.Equal("Very Hard", parsed.Difficulty);
        Assert.Equal("Recover Cargo", parsed.Type);
    }
}
