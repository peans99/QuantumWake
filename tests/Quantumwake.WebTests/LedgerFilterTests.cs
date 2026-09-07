namespace Quantumwake.WebTests;

/// <summary>
/// Filtering the Ledger by kind, and what that does to the figures above it.
/// </summary>
/// <remarks>
/// The trap is the summary. Totals that ignore the filter answer a question
/// nobody asked; totals that follow it are right, but then the "why this
/// number?" beside them would explain the unfiltered figure with records that
/// do not add up to what is on screen. The control goes away while a filter is
/// on, and the page says so.
/// </remarks>
public class LedgerFilterTests
{
    private const string Entries = """
        [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium","where":"Port Tressler",
          "shop":"TDD","amount":288000,"confirmed":true,"running":288000},
         {"at":"2026-08-19T09:00:00+00:00","kind":"Contract paid","what":"Cargo haul","where":"Hurston",
          "shop":"","amount":50250,"confirmed":true,"running":338250},
         {"at":"2026-08-18T09:00:00+00:00","kind":"Item bought","what":"MedPen","where":"Lorville",
          "shop":"Cubby Blast","amount":-4000,"confirmed":true,"running":334250}]
        """;

    private static Page Loaded(string entries = Entries)
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", entries);
        page.Do("__dom.node('#ledger-period').value = '0'; ledgerHidden = new Set(); await loadLedger();");
        return page;
    }

    private static string Press(string kind) =>
        "Array.from(__dom.node('#ledger-kinds').querySelectorAll('button'))"
        + $".find((b) => b.textContent === '{kind}').click();";

    [Fact]
    public void The_kinds_actually_present_get_a_toggle()
    {
        var text = Loaded().NodeText("#ledger-kinds");

        Assert.Contains("Cargo sold", text);
        Assert.Contains("Contract paid", text);
        Assert.Contains("Item bought", text);
    }

    /// <summary>
    /// A toggle for something that never happens is a control that empties the
    /// table and teaches nothing.
    /// </summary>
    [Fact]
    public void One_kind_is_not_a_choice()
    {
        var page = Loaded("""
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium","where":"P",
              "shop":"TDD","amount":288000,"confirmed":true,"running":288000}]
            """);

        Assert.Equal(0, page.Count("__dom.node('#ledger-kinds').querySelectorAll('button').length"));
    }

    /// <summary>
    /// The reason to filter at all: seeing what contracts actually paid,
    /// without cargo drowning it.
    /// </summary>
    [Fact]
    public void Hiding_the_others_leaves_the_contract_money()
    {
        var page = Loaded();
        page.Do(Press("Cargo sold"));
        page.Do(Press("Item bought"));

        var summary = page.NodeText("#ledger-summary");

        // Money in is now the contract payout alone, not the sum with cargo.
        Assert.Contains("50,250", summary);
        Assert.DoesNotContain("338,250", summary);
        Assert.Contains("Cargo haul", page.NodeText("#ledger-table tbody"));
        Assert.DoesNotContain("Agricium", page.NodeText("#ledger-table tbody"));
    }

    /// <summary>
    /// The server explains the whole figure. Offering that beside a filtered
    /// one would answer with records that do not add up to what is on screen.
    /// </summary>
    [Fact]
    public void The_why_control_goes_away_while_a_filter_is_on()
    {
        var page = Loaded();

        Assert.Equal(3, page.Count("__dom.node('#ledger-summary').byClass('why').length"));

        page.Do(Press("Cargo sold"));

        Assert.Equal(0, page.Count("__dom.node('#ledger-summary').byClass('why').length"));
        Assert.Contains("Totals cover only what is shown", page.NodeText("#ledger-kinds"));
    }

    [Fact]
    public void Turning_a_kind_back_on_restores_the_figures_and_the_control()
    {
        var page = Loaded();
        page.Do(Press("Cargo sold"));
        page.Do(Press("Cargo sold"));

        // Both kinds counted again: 288,000 of cargo plus 50,250 of contract.
        Assert.Contains("338,250", page.NodeText("#ledger-summary"));
        Assert.Equal(3, page.Count("__dom.node('#ledger-summary').byClass('why').length"));
    }

    /// <summary>
    /// The pager counts what is on screen. Left counting the fetched rows it
    /// would offer pages that render nothing.
    /// </summary>
    [Fact]
    public void The_pager_counts_what_the_filter_left()
    {
        var page = Loaded();
        page.Do(Press("Cargo sold"));
        page.Do(Press("Item bought"));

        Assert.Contains("1–1 of 1", page.NodeText("#ledger-pager"));
    }

    /// <summary>
    /// A filter describes the fetched period, not every period the page will
    /// ever load. Keeping it when the reader chooses a different range can
    /// hide that range's only kind; one kind has no toggle, leaving a fetched
    /// transaction invisible with no way to bring it back.
    /// </summary>
    [Fact]
    public void Changing_period_does_not_hide_the_new_periods_only_kind()
    {
        var page = Loaded();
        page.Do(Press("Cargo sold"));
        page.Serve("/api/ledger?days=7", """
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sold","what":"Agricium","where":"P",
              "shop":"TDD","amount":288000,"confirmed":true,"running":288000}]
            """);

        page.Do("__dom.node('#ledger-period').value = '7'; await loadLedger();");

        Assert.Contains("Agricium", page.NodeText("#ledger-table tbody"));
        Assert.DoesNotContain("No transactions", page.NodeText("#ledger-table tbody"));
    }
}
