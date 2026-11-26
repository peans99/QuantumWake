using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// 5.0's guild ladder against the faction ladder, as the PTU (5.0.202.46035)
/// states both: the same seven names under the same heading with no perks, and
/// different thresholds. Only the organisation can say which a page is.
/// </summary>
public class GuildLadderTests
{
    private static GameRepRank R(string name, long min) => new(name, null, min);

    private static readonly string[] Names =
        ["Applicant", "Jr. Contractor", "Contractor", "Sr. Contractor", "Veteran Contractor", "Head Contractor", "Elite Contractor"];

    private static readonly GameRepScope Faction = new("FactionReputationScope", "Standing", "@mobiGlas_Reputation_Standing", 238000,
        [.. Names.Zip([0L, 800, 2200, 5800, 15000, 38000, 95250], (n, m) => R(n, m))]);

    private static readonly GameRepScope Guild = new("ReputationScope_Guild", "Standing", "@mobiGlas_Reputation_Standing", 18461,
        [.. Names.Zip([0L, 513, 2051, 4615, 8205, 12820, 18461], (n, m) => R(n, m))]);

    private static GameReputationCatalogue Catalogue => new([Faction, Guild],
    [
        new("FactionReputation_Guild_Industrial", "United Resource Workers", null, "ReputationScope_Guild"),
        new("FactionReputation_Lawful_ShubinInterstellar", "Shubin Interstellar", null, "FactionReputationScope"),
    ])
    {
        Guilds = [new("FactionReputation_Guild_Industrial", "United Resource Workers", "ReputationScope_Guild",
            ["FactionReputation_Lawful_ShubinInterstellar"])],
        // CB_URW_S_Shubin_... pays both, URW on the guild ladder and Shubin on its own.
        Missions = new Dictionary<string, GameMissionPay>
        {
            ["a"] = new("CB_URW_S_Shubin_1", [
                new("FactionReputation_Guild_Industrial", "ReputationScope_Guild", 100, MissionOutcomes.Completed),
                new("FactionReputation_Lawful_ShubinInterstellar", "FactionReputationScope", 100, MissionOutcomes.Completed)]),
            ["b"] = new("CB_URW_S_Shubin_2", [
                new("FactionReputation_Guild_Industrial", "ReputationScope_Guild", 250, MissionOutcomes.Completed),
                new("FactionReputation_Guild_Industrial", "ReputationScope_Guild", -50, MissionOutcomes.Failed)]),
        },
    };

    private static ReputationReading Page(string organisation) => new(
        organisation, null, "CONTRACTOR", [], "STANDING",
        [.. Names.Select(n => new RepRankCard(n.ToUpperInvariant(), null, null))], 0.5);

    [Fact]
    public void A_guild_page_is_put_on_the_guild_ladder()
    {
        var place = ReputationLadders.Place(Page("UNITED RESOURCE WORKERS"), Catalogue)!;

        Assert.Equal("ReputationScope_Guild", place.Scope);
        Assert.Equal(2051 + (4615 - 2051) / 2, place.Estimate);
    }

    [Fact]
    public void A_faction_page_with_the_same_cards_stays_on_the_faction_ladder()
    {
        Assert.Equal("FactionReputationScope", ReputationLadders.Match(Page("SHUBIN INTERSTELLAR"), Catalogue)?.Id);
    }

    [Fact]
    public void A_guild_lists_its_ladder_its_members_and_what_its_contracts_pay_it()
    {
        var guild = Assert.Single(ReputationBoards.Guilds(Catalogue, []));

        Assert.Equal("United Resource Workers", guild.Name);
        Assert.Equal(513, guild.Ladder[1].MinReputation);
        Assert.Equal("Shubin Interstellar", Assert.Single(guild.Members).Name);
        Assert.False(guild.Members[0].OnPage);
        // The failure penalty is not pay.
        Assert.Equal((2, 100, 250), (guild.Contracts, guild.PayLow, guild.PayHigh));
        Assert.Null(guild.Rank);
    }

    [Fact]
    public void Before_five_there_are_no_guilds() =>
        Assert.Empty(ReputationBoards.Guilds(new GameReputationCatalogue([Faction], []), []));
}
