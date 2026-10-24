using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A Rep app reading set against the installed ladders, and the page built
/// from a run of them. The ladders below are 4.10.2's, as the DataCore states
/// them: Hauling from <c>ReputationScope_Hauling</c>, Standing from
/// <c>FactionReputationScope</c>, and the two that share every rank name -
/// Hired Muscle and the Headhunters' ship combat - told apart by perk and key.
/// </summary>
public class ReputationLadderTests
{
    private static GameRepRank R(string name, long min, string? perk = null) => new(name, perk, min);

    private static readonly GameRepScope Hauling = new("ReputationScope_Hauling", "Hauling", "@RepScope_TransportGuild_Name", 367601,
    [
        R("Not Eligible", -1000), R("Trainee", 0), R("Rookie", 50), R("Junior", 250),
        R("Member", 5250), R("Experienced", 27750), R("Senior", 77750), R("Master", 237750),
    ]);

    private static readonly GameRepScope Standing = new("FactionReputationScope", "Standing", "@RepScope_Standing_Name", 95250,
    [
        R("Neutral", 0), R("Jr. Contractor", 800), R("Contractor", 2200), R("Sr. Contractor", 5800),
        R("Veteran Contractor", 15000), R("Head Contractor", 38000), R("Elite Contractor", 95250),
    ]);

    private static readonly GameRepScope HiredMuscle = new("ReputationScope_HiredMuscle", "Hired Muscle", "@RepScope_HiredMuscle_Name", 1600001,
    [
        R("Not Eligible", -1000), R("Applicant", 0), R("Rank I", 1), R("Rank II", 5000), R("Rank III", 30000),
        R("Rank IV", 120000), R("Rank V", 480000), R("Rank VI", 1600000),
    ]);

    private const string More = "Additional Contracts";

    private static readonly GameRepScope Headhunters = new("ReputationScope_ShipCombat_HeadHunters", null, "@RepScope_ShipCombat_HeadHunters_Name", 480001,
    [
        R("Not Eligible", -1000), R("Applicant", 0), R("Rank I", 1, More), R("Rank II", 3000, More), R("Rank III", 10000, More),
        R("Rank IV", 40000, More), R("Rank V", 200000, More), R("Rank VI", 480000, More),
    ]);

    private static readonly GameReputationCatalogue Catalogue = new([Hauling, Standing, HiredMuscle, Headhunters]);

    private static ReputationReading Reading(string org, string section, (string Name, string? Perk, double Fill)[] cards) =>
        new ReputationReading(org, "NEUTRAL", null, [], section, null)
            .WithBars([.. cards.Select(c => new RepRankCard(c.Name, c.Perk, c.Fill))]);

    private static ReputationReading RedWind(double junior, double member) => Reading("RED WIND LINEHAUL", "HAULING",
    [
        ("NOT ELIGIBLE", null, 0), ("TRAINEE", null, 1), ("ROOKIE", null, 1), ("JUNIOR", null, junior),
        ("MEMBER", null, member), ("EXPERIENCED", null, 0), ("SENIOR", null, 0),
    ]);

    /// <summary>Red Wind on 24 October: Member, 54% of the way to Experienced.</summary>
    [Fact]
    public void The_bar_is_laid_on_the_ladder_and_said_as_an_estimate_with_its_margin()
    {
        var place = ReputationLadders.Place(RedWind(1, 0.54), Catalogue)!;

        Assert.Equal("ReputationScope_Hauling", place.Scope);
        Assert.Equal("Member", place.Rank);
        Assert.Equal(5250, place.Floor);
        Assert.Equal(27750, place.Next);
        Assert.Equal(5250 + 12150, place.Estimate);

        // One pixel of a 170-pixel bar across 22,500 reputation.
        Assert.Equal(132, place.Resolution);
    }

    /// <summary>Seven cards of an eight-rank ladder: MASTER was scrolled off the frame.</summary>
    [Fact]
    public void A_ladder_with_cards_scrolled_off_still_matches()
    {
        Assert.Same(Hauling, ReputationLadders.Match(RedWind(1, 0.54), Catalogue));
    }

    [Fact]
    public void Ladders_that_share_every_name_are_told_apart_by_perk_and_by_the_untranslated_key()
    {
        var cards = new (string, string?, double)[]
        {
            ("NOT ELIGIBLE", null, 0), ("APPLICANT", null, 1), ("RANK I", "ADDITIONAL CONTRACTS", 0.678),
            ("RANK II", "ADDITIONAL CONTRACTS", 0), ("RANK III", "ADDITIONAL CONTRACTS", 0),
            ("RANK", "ADDITIONAL CONTRACTS", 0), ("RANK V", "ADDITIONAL CONTRACTS", 0),
        };

        // The heading as the engine read the key the game showed in its place.
        var keyed = Reading("HEADHUNTERS", "@REPSCOPE-SHIPCOMBAT-HEADHUNTERS-NAME", cards);
        Assert.Same(Headhunters, ReputationLadders.Match(keyed, Catalogue));

        // And by the perks alone, on the September frame whose heading did not read.
        var bare = new ReputationReading("HEADHUNTERS", "NEUTRAL", null, [], null, null)
            .WithBars([.. cards.Select(c => new RepRankCard(c.Item1, c.Item2, c.Item3))]);
        Assert.Same(Headhunters, ReputationLadders.Match(bare, Catalogue));

        var place = ReputationLadders.Place(keyed, Catalogue)!;
        Assert.Equal("Rank I", place.Rank);
        Assert.Equal(1 + (long)Math.Round(2999 * 0.678), place.Estimate);
    }

    [Fact]
    public void Cards_no_ladder_has_are_left_unplaced()
    {
        var odd = Reading("NOBODY", "RACING", [("FIRST", null, 0.5), ("SECOND", null, 0), ("THIRD", null, 0)]);

        Assert.Null(ReputationLadders.Match(odd, Catalogue));
        Assert.Null(ReputationLadders.Place(odd, Catalogue));
    }

    private static readonly DateTimeOffset September = new(2026, 9, 8, 19, 49, 3, TimeSpan.Zero);
    private static readonly DateTimeOffset October = new(2026, 10, 24, 12, 59, 59, TimeSpan.Zero);

    private static ContractRecord Haul(string issuer, DateTimeOffset at, ContractOutcome outcome = ContractOutcome.Completed) =>
        new(at.AddMinutes(-40), "raw", "Red Wind · Haul", issuer, "Stanton", "Easy", "Hauling", true)
        {
            Outcome = outcome,
            CompletedAt = at,
        };

    /// <summary>
    /// The two Red Wind photographs on this install, and the hauls the logs
    /// hold between them: Junior 60% on 8 September, Member 54% on 24 October.
    /// </summary>
    [Fact]
    public void Two_photographs_make_a_change_and_the_logs_say_what_was_done_in_between()
    {
        var history = new[]
        {
            new RepSighting("ScreenShot-2026-10-24_14-59-59-A70.jpg", October, RedWind(1, 0.54)),
            new RepSighting("ScreenShot-2026-09-08_21-49-03-64C.jpg", September, RedWind(0.604, 0)),
        };

        var contracts = new[]
        {
            Haul("Red Wind", September.AddDays(-3)),
            Haul("Red Wind", September.AddDays(10)),
            Haul("Red Wind", September.AddDays(20)),
            Haul("Red Wind", September.AddDays(21), ContractOutcome.Abandoned),
            Haul("Inter Sec", September.AddDays(12)),
        };

        var board = ReputationBoards.Build(history, Catalogue, contracts);
        var org = Assert.Single(board.Organisations);

        Assert.Equal("RED WIND LINEHAUL", org.Name);
        Assert.Equal("MEMBER", org.Rank);
        Assert.Equal(2, org.History.Count);
        Assert.Equal(September, org.History[0].At);

        var change = org.Change!;
        Assert.Equal(1, change.Ranks);
        Assert.Equal(17400 - (250 + (long)Math.Round(5000 * 0.604)), change.Estimate);

        // Finished in between, by the log's spelling, and not Intersec's.
        Assert.Equal("Red Wind", change.Work!.Issuer);
        Assert.Equal(2, change.Work.Completed);
        Assert.Equal(1, change.Work.Abandoned);
        Assert.Equal(3, org.Work!.Completed);

        Assert.Equal(8, org.Ladder.Count);
        Assert.Equal(0.54, org.Ladder[4].Fill);
        Assert.Null(org.Ladder[7].Fill);
        Assert.Equal(0, board.Unmeasured);
    }

    /// <summary>A reading the bars never measured stays on the page, counted, with no rank and no change.</summary>
    [Fact]
    public void A_reading_from_before_the_bars_is_counted_as_unmeasured()
    {
        var old = new ReputationReading("COVALEX", "NEUTRAL", null, []);
        var board = ReputationBoards.Build([new RepSighting("a.jpg", September, old)], Catalogue, []);

        var org = Assert.Single(board.Organisations);
        Assert.Null(org.Rank);
        Assert.Null(org.Change);
        Assert.Null(org.Work);
        Assert.Equal(1, board.Unmeasured);
    }

    [Fact]
    public void A_dismissed_reading_is_not_believed()
    {
        var board = ReputationBoards.Build(
            [new RepSighting("a.jpg", October, RedWind(1, 0.54), Dismissed: true)], Catalogue, []);

        Assert.Empty(board.Organisations);
    }

    /// <summary>
    /// The app prints INTERSEC DEFENSE SOLUTIONS and the record says "InterSec
    /// Defense Solutions"; the letters are the same, and that is the match.
    /// Dead Saints' record names no logo, so its card gets none.
    /// </summary>
    [Fact]
    public void A_logo_is_found_by_the_games_name_and_only_where_the_game_has_one()
    {
        var catalogue = Catalogue with
        {
            Organisations =
            [
                new("FactionReputation_Lawful_InterSecDefenseSolutions", "InterSec Defense Solutions",
                    @"Data\UI\SharedAssets\ManufacturerLogos\rep_ui_icon_logo_intersec.dds"),
                new("FactionReputation_Unlawful_DeadSaints", "Dead Saints", null),
            ],
        };

        Assert.Equal("/api/reputation/logo/FactionReputation_Lawful_InterSecDefenseSolutions",
            ReputationBoards.LogoOf("INTERSEC DEFENSE SOLUTIONS", catalogue));
        Assert.Null(ReputationBoards.LogoOf("DEAD SAINTS", catalogue));
        Assert.Null(ReputationBoards.LogoOf("RED WIND LINEHAUL", catalogue));

        Assert.Equal(@"Data\UI\SharedAssets\ManufacturerLogos\rep_ui_icon_logo_intersec.dds",
            ReputationBoards.LogoEntry("FactionReputation_Lawful_InterSecDefenseSolutions", catalogue));
        Assert.Null(ReputationBoards.LogoEntry("Data", catalogue));
    }
}
