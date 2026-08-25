namespace Quantumwake.WebTests;

/// <summary>
/// The Ledger: every confirmed movement of money, and the four figures above it.
/// </summary>
/// <remarks>
/// This page is arithmetic on somebody's own money, so the failures that matter
/// are quiet ones - a sign dropped, an unconfirmed amount presented as settled,
/// a page of results that silently loses the rest. None of it was reachable by a
/// test until the harness learned to answer a table's tbody.
/// </remarks>
public class LedgerTests
{
    private const string Entries = """
        [{"at":"2026-08-20T09:00:00+00:00","kind":"Cargo sale","what":"Agricium","where":"Port Tressler",
          "shop":"TDD","amount":288000,"confirmed":true,"running":288000},
         {"at":"2026-08-19T09:00:00+00:00","kind":"Purchase","what":"MedPen","where":"Lorville",
          "shop":"Cubby Blast","amount":-4000,"confirmed":true,"running":284000},
         {"at":"2026-08-18T09:00:00+00:00","kind":"Cargo buy","what":"Waste","where":"Lorville",
          "shop":"TDD","amount":-64000,"confirmed":false,"running":220000}]
        """;

    private static Page Loaded(string entries = Entries)
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", entries);
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");
        return page;
    }

    [Fact]
    public void Money_in_and_out_are_totalled_apart_and_netted()
    {
        var summary = Loaded().NodeText("#ledger-summary");

        Assert.Contains("Money in", summary);
        Assert.Contains("Money out", summary);

        // 288,000 in; 68,000 out; 220,000 net gain.
        Assert.Contains("288,000", summary);
        Assert.Contains("68,000", summary);
        Assert.Contains("Net gain", summary);
        Assert.Contains("220,000", summary);
    }

    /// <summary>
    /// A net loss is a different word, not a negative number with a positive
    /// label. Getting this wrong reads as a profitable week.
    /// </summary>
    [Fact]
    public void A_week_that_lost_money_says_so()
    {
        var summary = Loaded("""
            [{"at":"2026-08-20T09:00:00+00:00","kind":"Purchase","what":"A ship","where":"Area18",
              "shop":"New Deal","amount":-500000,"confirmed":true,"running":-500000}]
            """).NodeText("#ledger-summary");

        Assert.Contains("Net loss", summary);
        Assert.DoesNotContain("Net gain", summary);
        Assert.Contains("500,000", summary);
    }

    /// <summary>
    /// The logs record a request to buy, not the till's answer, so an amount
    /// nobody confirmed is marked rather than presented as settled.
    /// </summary>
    [Fact]
    public void An_unconfirmed_amount_is_marked_rather_than_shown_as_settled()
    {
        var rows = Loaded().NodeText("#ledger-table tbody");

        // The tilde belongs to the cargo buy only.
        Assert.Contains("~", rows);
        Assert.Contains("−~64,000", rows);
        Assert.Contains("+288,000", rows);
        Assert.DoesNotContain("+~288,000", rows);
    }

    /// <summary>
    /// Money out is drawn with a minus sign, and the sign is the whole meaning
    /// of the row.
    /// </summary>
    [Fact]
    public void Money_leaving_is_signed_differently_from_money_arriving()
    {
        var page = Loaded();

        var classes = page.Text("__dom.node('#ledger-table tbody').descendants()"
            + ".filter(n => n.tagName === 'td' && n.classList.contains('num'))"
            + ".map(n => n.className).join('|')");

        Assert.Contains("inward", classes);
        Assert.Contains("outward", classes);
    }

    [Fact]
    public void An_empty_range_says_so_rather_than_drawing_an_empty_table()
    {
        Assert.Contains("No transactions in that range", Loaded("[]").NodeText("#ledger-table tbody"));
    }

    /// <summary>
    /// The page is paged, and a page that silently dropped the rest would look
    /// exactly like a quiet month.
    /// </summary>
    [Fact]
    public void More_movements_than_fit_on_a_page_are_paged_rather_than_lost()
    {
        var many = string.Join(",", Enumerable.Range(0, 120).Select(i =>
            $"{{\"at\":\"2026-08-20T09:00:00+00:00\",\"kind\":\"Sale\",\"what\":\"Thing {i}\","
            + "\"where\":\"Port Tressler\",\"shop\":\"TDD\",\"amount\":100,\"confirmed\":true,\"running\":100}"));

        var page = Loaded("[" + many + "]");

        Assert.Contains("120", page.NodeText("#ledger-summary"));

        var drawn = page.Count("__dom.node('#ledger-table tbody').descendants()"
            + ".filter(n => n.tagName === 'tr').length");

        Assert.True(drawn < 120, $"the whole list was drawn at once ({drawn} rows)");
        Assert.True(drawn > 0);
    }
}
