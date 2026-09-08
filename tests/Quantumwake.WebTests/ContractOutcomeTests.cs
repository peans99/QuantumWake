namespace Quantumwake.WebTests;

/// <summary>
/// Telling a contract you dropped from one you lost.
/// </summary>
/// <remarks>
/// The two were counted and labelled together, which read as though every bad
/// ending were a decision the player made. This install has 64 of the one and
/// 5 of the other.
/// </remarks>
public class ContractOutcomeTests
{
    private static Page Rendered(int abandoned, int failed)
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", "[]");
        page.Serve("/api/standing?days=0", "[]");

        page.Do($$"""
            renderContracts({
              contractsSeen: 100,
              contractsCompleted: 80,
              contractsAbandoned: {{abandoned}},
              contractsFailed: {{failed}},
              contractIssuers: [],
              contractTypes: []
            });
            """);

        return page;
    }

    [Fact]
    public void A_lost_contract_is_counted_where_it_can_be_seen()
    {
        var summary = Rendered(abandoned: 64, failed: 5).NodeText("#contract-summary");

        Assert.Contains("Abandoned", summary);
        Assert.Contains("64", summary);
        Assert.Contains("Failed", summary);
        Assert.Contains("5", summary);
    }

    /// <summary>
    /// A permanent zero beside four real figures is furniture, and on an
    /// install too new to have lost one it would read as "nothing ever went
    /// wrong" rather than "nothing has yet".
    /// </summary>
    [Fact]
    public void No_failures_means_no_tile_rather_than_a_zero()
    {
        var summary = Rendered(abandoned: 64, failed: 0).NodeText("#contract-summary");

        Assert.DoesNotContain("Failed", summary);
        Assert.Contains("Abandoned", summary);
    }

    [Fact]
    public void The_summary_still_leads_with_what_was_finished()
    {
        var summary = Rendered(abandoned: 64, failed: 5).NodeText("#contract-summary");

        Assert.Contains("Completed", summary);
        Assert.Contains("80", summary);
        Assert.Contains("Completion rate", summary);
        Assert.Contains("80%", summary);
    }

    /// <summary>
    /// The wire spells the outcome as the enum does. Reading it in another
    /// case has cost a green suite before, so the exact string is asserted.
    /// </summary>
    [Fact]
    public void A_failed_contract_is_labelled_as_something_that_happened_to_you()
    {
        var page = new Page();

        page.Serve("/api/contracts?days=0", """
            [{ "at": "2026-05-03T18:00:00Z", "name": "Ling_Stanton_Easy_RecoverCargo",
               "issuer": "Ling", "type": "Recover Cargo", "difficulty": "Easy",
               "system": "Stanton", "outcome": "Failed" }]
            """);

        page.Do("await loadContractList();");

        var text = page.NodeText("#contracts-table tbody");

        Assert.Contains("failed", text);
        Assert.DoesNotContain("abandoned", text);
    }
}
