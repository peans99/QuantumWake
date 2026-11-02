namespace Quantumwake.WebTests;

/// <summary>
/// Rep and blueprints said as the installed game's figures, with a StarStrings
/// tag named as such only where the game had no word on a contract.
/// </summary>
/// <remarks>
/// The game knows all 751 contracts this install has logged, and its figures
/// agreed with all 38 single-figure tags and all 145 ladders the mod wrote. So
/// the normal case is now "the game says", and a page that still credited the
/// mod would be telling the pilot something that is no longer where the number
/// came from.
/// </remarks>
public class GameRepFiguresTests
{
    private static Page Contracts(string rows)
    {
        var page = new Page();
        page.Serve("/api/contracts?days=0", rows);
        page.Do("await loadContractList();");
        return page;
    }

    private const string Cells = "__dom.node('#contracts-table tbody')";

    [Fact]
    public void A_rep_chip_from_the_game_says_so()
    {
        var page = Contracts("""
            [{ "at": "2026-10-24T18:00:00Z", "name": "Member | Stellar Small Haul | to Gaslight", "issuer": "Red Wind",
               "outcome": "Completed", "rep": 500, "repSource": "game", "blueprint": false }]
            """);

        var title = page.Text($"{Cells}.byClass('tag-rep')[0].title");

        Assert.Equal("What the installed game says finishing this contract pays", title);
        Assert.DoesNotContain("StarStrings", title);
    }

    [Fact]
    public void A_rep_chip_from_a_tag_still_credits_the_mod()
    {
        var page = Contracts("""
            [{ "at": "2026-05-03T18:00:00Z", "name": "Large Covalex Shipment Needs Recovering", "issuer": "Covalex",
               "outcome": "Completed", "rep": 200, "repSource": "tag" }]
            """);

        Assert.Contains("StarStrings", page.Text($"{Cells}.byClass('tag-rep')[0].title"));
    }

    [Theory]
    [InlineData("1", "The installed game says finishing this contract awards a blueprint")]
    [InlineData("0.25", "The installed game gives this contract a 25% chance of a blueprint")]
    [InlineData("null", "Tagged as awarding a blueprint")]
    public void The_blueprint_chip_gives_the_games_chance(string chance, string expected)
    {
        var page = Contracts($$"""
            [{ "at": "2026-10-24T18:00:00Z", "name": "FTL Courier", "issuer": "FTL",
               "outcome": "Completed", "blueprint": true, "blueprintChance": {{chance}} }]
            """);

        Assert.Equal(expected, page.Text($"{Cells}.byClass('tag-bp')[0].title"));
    }

    [Fact]
    public void A_standing_row_says_where_its_rep_came_from()
    {
        var page = new Page();

        Assert.Equal("What the installed game says 3 finished contracts paid", page.Text("repFromTitle(3, 0)"));
        Assert.Equal("Read from 3 contract titles the StarStrings mod has annotated", page.Text("repFromTitle(3, 3)"));
        Assert.Equal("3 finished contracts: 2 by the installed game, 1 by a StarStrings title tag the game had no word on",
            page.Text("repFromTitle(3, 1)"));
    }

    [Fact]
    public void The_rep_run_credits_the_game_when_the_figure_is_the_games()
    {
        var page = new Page();

        var said = page.Text("""
            repRunPaySource({ place: { rank: 'Member' }, pay: { rep: 500, source: 'game', contract: 'Member', at: null } })
            """);

        Assert.Equal("What a Member contract of theirs pays, by the installed game’s own contracts. Type over it if a delivery pays differently.", said);
    }

    /// <summary>Headhunters have two progressions; the run says which one their work moves.</summary>
    [Fact]
    public void The_rep_run_says_when_a_figure_is_for_a_ladder_moved_by_other_contracts()
    {
        var page = new Page();

        Assert.StartsWith("What their contracts that move this ladder pay, by the installed game.",
            page.Text("repRunPaySource({ place: { rank: 'Rank I' }, pay: { rep: 500, source: 'game-open', contract: 'Rank I' } })"));
        Assert.StartsWith("What most of their contracts that move this ladder pay, by the installed game: 25 contracts, from 10 to 1,000.",
            page.Text("repRunPaySource({ place: { rank: 'New Customer' }, pay: { rep: 100, source: 'game-open', contract: 'New Customer', low: 10, high: 1000, contracts: 25 } })"));
        Assert.Equal("By the installed game, none of their contracts moves this ladder: their work moves Standing. Type what one pays here if you know.",
            page.Text("repRunPaySource({ place: { rank: 'Rank I' }, pay: { rep: 0, source: 'elsewhere', contract: 'Standing' } })"));
    }

    [Fact]
    public void In_french_the_new_sentences_are_translated()
    {
        var page = new Page("fr");

        Assert.StartsWith("Ce que le jeu installé dit qu’ont rapporté 3 contrats terminés", page.Text("repFromTitle(3, 0)"));
        Assert.StartsWith("Le jeu installé donne à ce contrat 25%", page.Text("blueprintChipTitle(0.25)"));
        Assert.StartsWith("Ce que rapporte un de leurs contrats de rang Member",
            page.Text("repRunPaySource({ place: { rank: 'Member' }, pay: { rep: 500, source: 'game', contract: 'Member' } })"));
    }
}
