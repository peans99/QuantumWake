using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Reading a text mod's annotations off a contract title.
/// </summary>
/// <remarks>
/// The game logs the title it displays, and that title comes from the
/// localisation file StarStrings replaces - so with the mod installed, the
/// reward is in the log. The fixtures here are real lines from its
/// <c>global.ini</c>, markup and all.
/// </remarks>
public class ContractTagsTests
{
    [Theory]
    [InlineData("Salvager Needed (Lrg. Special Order of RMC / UCM / Components) <EM4>[150 Rep] [BP]*</EM4>", 150)]
    [InlineData("Salvager Needed (Med. Supply of RMC / UCM) <EM4>[100 Rep]</EM4>", 100)]
    [InlineData("ATLS Orange Line <EM4>[16000 Rep]</EM4>", 16000)]
    [InlineData("Something <EM4>[+250 rep]</EM4>", 250)]
    [InlineData("Punished <EM4>[-30 Rep]</EM4>", -30)]
    public void The_reward_is_read_off_the_title(string title, int expected)
    {
        Assert.Equal(expected, ContractTags.RepFrom(title));
    }

    /// <summary>
    /// Most titles say nothing, and that is not the same as paying nothing.
    /// Anything built on this has to be able to tell the two apart.
    /// </summary>
    [Theory]
    [InlineData("ENTRY LVL. COURIER NEEDED IN STANTON:")]
    [InlineData("Verified Bounty: Kami Quick (HRT)")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Reputation matters here")]
    public void A_title_that_says_nothing_returns_nothing(string? title)
    {
        Assert.Null(ContractTags.RepFrom(title));
    }

    [Theory]
    [InlineData("Salvager Needed <EM4>[150 Rep] [BP]*</EM4>", true)]
    [InlineData("Salvager Needed <EM4>[BP]</EM4>", true)]
    [InlineData("Salvager Needed <EM4>[150 Rep]</EM4>", false)]
    public void A_blueprint_tag_is_its_own_fact(string title, bool expected)
    {
        Assert.Equal(expected, ContractTags.AwardsBlueprint(title));
    }

    /// <summary>A contract still has to read as its own name once the numbers are lifted off.</summary>
    [Theory]
    [InlineData("Salvager Needed (Med. Supply of RMC / UCM) <EM4>[100 Rep] [BP]*</EM4>",
        "Salvager Needed (Med. Supply of RMC / UCM)")]
    [InlineData("ENTRY LVL. COURIER NEEDED IN STANTON: ", "ENTRY LVL. COURIER NEEDED IN STANTON")]
    [InlineData("Plain Title", "Plain Title")]
    public void The_name_survives_the_tags_coming_off(string title, string expected)
    {
        Assert.Equal(expected, ContractTags.Clean(title));
    }

    /// <summary>
    /// The game writes both spellings of the same people, and a page that
    /// lists them twice is wrong about who you have been working for.
    /// </summary>
    [Fact]
    public void Spacing_never_makes_two_factions_out_of_one()
    {
        Assert.Equal(ContractTags.IssuerKey("Red Wind"), ContractTags.IssuerKey("Redwind"));
        Assert.Equal(ContractTags.IssuerKey("Red Wind"), ContractTags.IssuerKey("RED WIND"));
    }

    [Fact]
    public void A_known_abbreviation_joins_its_full_name()
    {
        Assert.Equal(ContractTags.IssuerKey("Bounty Hunters Guild"), ContractTags.IssuerKey("BHG"));
    }

    /// <summary>
    /// Only names the logs actually show both ways are merged. Guessing is how
    /// a page ends up quietly wrong.
    /// </summary>
    [Fact]
    public void Two_different_names_stay_two_factions()
    {
        Assert.NotEqual(ContractTags.IssuerKey("Covalex"), ContractTags.IssuerKey("Coval"));
        Assert.NotEqual(ContractTags.IssuerKey("Hockrow"), ContractTags.IssuerKey("Ling"));
    }
}
