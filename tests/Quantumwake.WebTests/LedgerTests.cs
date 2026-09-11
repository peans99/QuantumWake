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

    // ---- cash on hand ----

    private const string Standing = """
        {"read":{"shot":"ScreenShot-2026-09-10_20-53-54-CF5.jpg","shotAt":"2026-09-11T00:53:54Z",
         "balance":2092773,"movedSince":-30000,"movementsSince":2,"estimate":2062773}}
        """;

    private static Page WithWallet(string standing)
    {
        var page = new Page();
        page.Serve("/api/ledger?days=0", Entries);
        page.Serve("/api/ledger/wallet", standing);
        page.Do("__dom.node('#ledger-period').value = '0'; await loadLedger();");
        return page;
    }

    /// <summary>
    /// The one figure the ledger cannot make: it is the screen's, dated by the
    /// screenshot, and the movement since is the logs' and is said to be.
    /// </summary>
    [Fact]
    public void Cash_on_hand_is_the_screens_figure_with_when_it_was_read()
    {
        var page = WithWallet(Standing);

        Assert.False(page.Truth("__dom.node('#ledger-wallet').hidden"));

        var card = page.NodeText("#ledger-wallet");
        Assert.Contains("Cash on hand", card);
        Assert.Contains("2,092,773 aUEC", card);
        Assert.Contains("Last updated", card);
        Assert.Contains("ScreenShot-2026-09-10_20-53-54-CF5.jpg", card);
    }

    /// <summary>
    /// A figure carried forward by the logs is an estimate and is called one,
    /// with the sign and the count it rests on.
    /// </summary>
    [Fact]
    public void The_movement_since_is_shown_and_the_carried_figure_is_called_an_estimate()
    {
        var card = WithWallet(Standing).NodeText("#ledger-wallet");

        Assert.Contains("2 movements", card);
        Assert.Contains("−30,000 aUEC", card);
        Assert.Contains("about 2,062,773 aUEC now", card);
        Assert.Contains("estimate", card);
    }

    [Fact]
    public void With_nothing_moved_since_the_figure_stands_as_the_latest_word()
    {
        var card = WithWallet("""
            {"read":{"shot":"a.jpg","shotAt":"2026-09-11T00:53:54Z",
             "balance":2092773,"movedSince":0,"movementsSince":0,"estimate":2092773}}
            """).NodeText("#ledger-wallet");

        Assert.Contains("Nothing has moved", card);
        Assert.DoesNotContain("estimate", card);
    }

    /// <summary>
    /// No reading is a sentence saying what would fill the card, not a zero -
    /// a zero balance is a claim about somebody's money.
    /// </summary>
    [Fact]
    public void No_reading_yet_says_what_would_fill_the_card()
    {
        var card = WithWallet("""{"read":null}""").NodeText("#ledger-wallet");

        Assert.Contains("No screenshot has shown your balance", card);
        Assert.Contains("kiosk", card);
        Assert.DoesNotContain("0 aUEC", card);
    }

    /// <summary>
    /// The card is fetched after the table, and a card that will not load
    /// costs the ledger nothing.
    /// </summary>
    [Fact]
    public void A_cash_card_that_will_not_load_hides_and_leaves_the_ledger_alone()
    {
        var page = Loaded();

        Assert.True(page.Truth("__dom.node('#ledger-wallet').hidden"));
        Assert.Contains("288,000", page.NodeText("#ledger-summary"));
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
