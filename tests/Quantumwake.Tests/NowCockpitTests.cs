using Quantumwake.Core.State;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The two live readings the MFD pages need and no report can supply: the
/// contract being flown right now, and what the commodity counters recorded
/// this session. Both come off the session in progress, which reaches the
/// store only when the log rotates.
/// </summary>
public class NowCockpitTests
{
    private static readonly DateTimeOffset Noon = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static SessionSummary Session(
        IEnumerable<ContractRecord>? contracts = null, IEnumerable<CommodityTrade>? trades = null) => new()
        {
            Id = "live",
            SourceFile = "Game.log",
            StartedAt = Noon,
            EndedAt = Noon.AddHours(1),
            Contracts = [.. contracts ?? []],
            Trades = [.. trades ?? []]
        };

    private static ContractRecord Contract(
        string name, ContractOutcome outcome = ContractOutcome.InProgress,
        int minutes = 0, int steps = 0, int done = 0, DateTimeOffset? completedAt = null) =>
        // Accepted:false is what every record carries - nothing has ever set it.
        // See ContractRecord.Accepted; a filter on it returns nothing, always.
        new(Noon.AddMinutes(minutes), name, name, "Hurston Dynamics", "Stanton", "Medium", "Delivery", false)
        { Outcome = outcome, Steps = steps, StepsDone = done, CompletedAt = completedAt };

    [Fact]
    public void OnlyContractsTheLogsHaveNotClosedAreOpen()
    {
        var open = NowContract.OpenIn(Session([
            Contract("Finished", outcome: ContractOutcome.Completed),
            Contract("Walked away from", outcome: ContractOutcome.Abandoned),
            Contract("Lost", outcome: ContractOutcome.Failed),
            Contract("Closed but still marked in progress", completedAt: Noon.AddMinutes(20)),
            Contract("Seen, no objective state yet", outcome: ContractOutcome.Unknown, minutes: 5),
            Contract("Under way", steps: 5, done: 3, minutes: 10)]));

        Assert.Equal(["Under way", "Seen, no objective state yet"], open.Select(c => c.Name));
        Assert.Equal(3, open[0].StepsDone);
        Assert.Equal(5, open[0].Steps);
        Assert.Equal("Hurston Dynamics", open[0].Issuer);
        Assert.Equal(Noon.AddMinutes(10), open[0].Since);
    }

    /// <summary>A contract reads as its own title, not as one carrying research.</summary>
    [Fact]
    public void AnnotatedTitlesArriveWithoutTheirTags()
    {
        var open = NowContract.OpenIn(Session([Contract("Recover the cargo [150 Rep] [BP]")]));
        Assert.Equal("Recover the cargo", open[0].Name);
    }

    /// <summary>
    /// Six is plenty for a cockpit page, and the snapshot this rides is pushed
    /// every second.
    /// </summary>
    [Fact]
    public void OpenContractsAreCappedAndNewestFirst()
    {
        var open = NowContract.OpenIn(Session(
            [.. Enumerable.Range(0, 12).Select(i => Contract($"Job {i}", minutes: i))]));
        Assert.Equal(6, open.Count);
        Assert.Equal("Job 11", open[0].Name);
        Assert.Equal("Job 6", open[^1].Name);
    }

    [Fact]
    public void NothingAtACounterMeansNoCargoReadingRatherThanZeroes()
    {
        Assert.Null(NowCargo.From(Session(), _ => null));
    }

    [Fact]
    public void CargoTotalsBuysAndSellsApartAndNamesTheNewestMove()
    {
        var cargo = NowCargo.From(Session(trades: [
            new(Noon.AddMinutes(30), "Area18 TDD", 182_400m, 96, false, "buy", "agricium"),
            new(Noon.AddMinutes(50), "Everus Harbor", 61_000m, 24, true, "sell", "titanium"),
            new(Noon.AddMinutes(10), "Lorville CBD", 9_000m, 8, false, "buy", "unknown-to-everyone")]),
            id => id == "agricium" ? "Agricium" : id == "titanium" ? "Titanium" : null);

        Assert.Equal(104, cargo!.BoughtScu);
        Assert.Equal(24, cargo.SoldScu);

        // Newest by timestamp, not by position: the list is appended as events land.
        Assert.Equal("Everus Harbor", cargo.Last!.Shop);
        Assert.True(cargo.Last.Sell);
        Assert.Equal("Titanium", cargo.Last.Commodity);
    }

    /// <summary>
    /// An id nothing can name stays unnamed. Showing the raw id would put a
    /// cargo on the page that nobody ever carried.
    /// </summary>
    [Fact]
    public void AnUnresolvedCommodityIsLeftUnnamedRatherThanShownAsItsId()
    {
        var cargo = NowCargo.From(
            Session(trades: [new(Noon, "Lorville CBD", 9_000m, 8, false, "buy", "resource_xyz")]),
            _ => null);

        Assert.Null(cargo!.Last!.Commodity);
        Assert.Equal(8, cargo.Last.Scu);
        Assert.Equal(0, cargo.SoldScu);
    }
}
