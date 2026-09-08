namespace Quantumwake.WebTests;

/// <summary>
/// What the Contracts page says about where a contract's name came from.
/// </summary>
/// <remarks>
/// The page splits a contract into Issuer, Type, Difficulty and System, and
/// every one of those is cut out of the game's own identifier rather than out
/// of a title the game displayed. Unlabelled, "A To B Processed Agricultural
/// Supplies Stanton4 Small Grade4" reads as a name somebody wrote; it is an id,
/// and "Small Grade4" is a ship-size class and a tier. These tests hold the
/// page to saying so, and to saying it with a count rather than a disclaimer.
/// </remarks>
public class ContractIdNameTests
{
    private static string Row(string name, bool fromGameId, string issuer = "Covalex") => $$"""
        {"at":"2026-08-30T10:00:00Z","name":"{{name}}","issuer":"{{issuer}}",
         "type":"Recover Cargo","system":"Stanton","difficulty":"Hard",
         "outcome":"Completed","steps":0,"stepsDone":0,"minutes":null,
         "rep":null,"blueprint":false,"fromGameId":{{(fromGameId ? "true" : "false")}}}
        """;

    private static Page Listed(params string[] rows)
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", $"[{string.Join(",", rows)}]");
        page.Do("await loadContractList();");
        return page;
    }

    private static string Note(Page page) => page.NodeText("#contracts-id-note");

    private static bool NoteShown(Page page) => !page.Truth("__dom.node('#contracts-id-note').hidden");

    [Fact]
    public void A_list_that_is_all_identifiers_says_so()
    {
        var page = Listed(Row("Haul Cargo · A To B", true), Row("Covalex · Recover Cargo", true));

        Assert.True(NoteShown(page));
        Assert.Contains("Every one of these 2", Note(page));
        Assert.Contains("Small Grade4", Note(page));
    }

    /// <summary>The mixed case is a count, because a count can be checked.</summary>
    [Fact]
    public void A_mixed_list_counts_them()
    {
        var page = Listed(
            Row("Haul Cargo · A To B", true),
            Row("Deliver the package to Area18", false),
            Row("Covalex · Recover Cargo", true));

        Assert.True(NoteShown(page));
        Assert.Contains("2 of these 3", Note(page));
    }

    /// <summary>
    /// The day the game logs real titles, the line goes away by itself rather
    /// than becoming a disclaimer nobody needs.
    /// </summary>
    [Fact]
    public void Real_titles_are_not_apologised_for()
    {
        var page = Listed(Row("Deliver the package to Area18", false));

        Assert.False(NoteShown(page));
    }

    [Fact]
    public void An_empty_range_says_nothing_about_names()
    {
        var page = Listed();

        Assert.False(NoteShown(page));
    }

    /// <summary>
    /// The row's own tooltip carries it too: the note explains the table, the
    /// tooltip explains the row somebody is actually pointing at.
    /// </summary>
    [Fact]
    public void The_row_tooltip_says_whose_words_these_are()
    {
        var page = Listed(Row("Haul Cargo · A To B", true));

        var tip = page.Text("__dom.node('#contracts-table tbody').children[0].title");

        Assert.Contains("Haul Cargo · A To B", tip);
        Assert.Contains("the game's own id", tip);
    }

    [Fact]
    public void A_real_title_gets_the_plain_tooltip()
    {
        var page = Listed(Row("Deliver the package to Area18", false));

        var tip = page.Text("__dom.node('#contracts-table tbody').children[0].title");

        Assert.Equal("Deliver the package to Area18", tip);
    }
}
