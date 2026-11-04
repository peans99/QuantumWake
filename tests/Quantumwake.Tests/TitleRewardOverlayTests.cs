using Quantumwake.Core.GameData;
using Quantumwake.Core.State;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Contract titles that say what finishing them pays, now that the item
/// labels are the only text table installed and StarStrings is not there to
/// say it.
/// </summary>
/// <remarks>
/// The wording is StarStrings' own, read out of the table it left in this
/// install's backups: <c>&lt;EM4&gt;[100 Rep] [BP]*&lt;/EM4&gt;</c> after the
/// title, after the route on a haul.
/// </remarks>
public class TitleRewardOverlayTests
{
    private static readonly Dictionary<string, GameTitleReward> Rewards = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Adagio_Industrial_Component_Salvage_Levski_M_Title_001"] = new(100, 100, true),
        ["Battaglia_RPT_ScanMine_02_title"] = new(200, 200, false, 20),
        ["Covalex_HaulCargo_AToB_title"] = new(100, 150, false),
        ["Nothing_Paid_title"] = new(0, 0, false),
    };

    private static readonly Dictionary<string, string> Routes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Covalex_HaulCargo_AToB_title"] = GameMissions.BothEnds,
    };

    private const string Vanilla =
        "Adagio_Industrial_Component_Salvage_Levski_M_Title_001,P=Salvager Needed (Med. Special Order of RMC / UCM / Components)\r\n" +
        "Battaglia_RPT_ScanMine_02_title=Survey Needed\r\n" +
        "Covalex_HaulCargo_AToB_title=~mission(ReputationRank) Rank - Direct ~mission(CargoGradeToken) Cargo Haul\r\n" +
        "Nothing_Paid_title=Just Because\r\n" +
        "Covalex_HaulCargo_AToB_desc=Haul it.\r\n";

    private static TextOverlayPlan Build(TextOverlayOptions? options = null) =>
        TextOverlay.Build(Vanilla, _ => true, null, options, Routes, Rewards);

    [Fact]
    public void A_title_says_the_rep_it_pays_and_a_blueprint_chance_as_StarStrings_did()
    {
        var plan = Build();

        Assert.Contains("Adagio_Industrial_Component_Salvage_Levski_M_Title_001,P=Salvager Needed (Med. Special Order of RMC / UCM / Components) <EM4>[100 Rep] [BP]*</EM4>\r\n", plan.Content);
        Assert.Equal(TextOverlay.ContractTitleCategory,
            plan.Changes.Single(c => c.ItemClass.StartsWith("Adagio", StringComparison.Ordinal)).Category);
    }

    /// <summary>
    /// StarStrings summed a second ladder into one figure - 220 for a
    /// Battaglia survey that pays 200 Standing and 20 Affinity. The sum is
    /// still there to read, and so are its parts.
    /// </summary>
    [Fact]
    public void A_second_ladder_is_said_as_an_addition()
    {
        Assert.Contains("Battaglia_RPT_ScanMine_02_title=Survey Needed <EM4>[200+20 Rep]</EM4>\r\n", Build().Content);
    }

    [Fact]
    public void A_haul_says_its_route_then_its_pay_and_the_planner_still_reads_the_route()
    {
        var plan = Build();
        var line = plan.Content.Split("\r\n").Single(l => l.StartsWith("Covalex_HaulCargo_AToB_title=", StringComparison.Ordinal));

        Assert.EndsWith("| ~mission(Location|name) > ~mission(Destination|name) <EM4>[100-150 Rep]</EM4>", line);
        Assert.Equal(1, plan.Routed);
        Assert.Equal(3, plan.Rewarded);

        var shown = line[(line.IndexOf('=') + 1)..]
            .Replace("~mission(ReputationRank)", "Junior")
            .Replace("~mission(CargoGradeToken)", "Small")
            .Replace("~mission(Location|name)", "Port Tressler")
            .Replace("~mission(Destination|name)", "NB Int. Spaceport");
        var route = HaulingContract.RouteFromTitle(shown);
        Assert.Equal("Port Tressler", route?.Pickup);
        Assert.Equal("NB Int. Spaceport", route?.Delivery);
    }

    [Fact]
    public void Titles_that_pay_nothing_and_lines_that_are_not_titles_are_left_alone()
    {
        var plan = Build();

        Assert.Contains("Nothing_Paid_title=Just Because\r\n", plan.Content);
        Assert.Contains("Covalex_HaulCargo_AToB_desc=Haul it.\r\n", plan.Content);
    }

    [Fact]
    public void The_pay_can_be_turned_off()
    {
        var plan = Build(new TextOverlayOptions(Rewards: false));

        Assert.DoesNotContain(" Rep]", plan.Content);
        Assert.Equal(0, plan.Rewarded);
    }

    [Fact]
    public void A_title_that_already_says_its_rep_is_not_told_twice()
    {
        var plan = TextOverlay.Build("Battaglia_RPT_ScanMine_02_title=Survey Needed <EM4>[220 Rep]</EM4>\r\n",
            _ => true, null, null, null, Rewards);

        Assert.Equal(0, plan.Rewarded);
    }
}
