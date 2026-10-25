namespace Quantumwake.WebTests;

/// <summary>
/// The rep run on the Flight plan page: one small delivery flown again and
/// again until the next rank.
/// </summary>
/// <remarks>
/// The arithmetic is the Reputation page's estimate plus what one delivery
/// pays, so what is under test is mostly that the page says where each figure
/// came from - a StarStrings tag is research, not the game - and that the stops
/// it writes name the rank on the drop that reaches it.
/// </remarks>
public class RepRunTests
{
    /// <summary>Red Wind Member 54% on 24 October, with the Member figure off a haul's ladder tag.</summary>
    private const string Board = """
        {"organisations":[
          {"name":"RED WIND LINEHAUL","standing":"NEUTRAL","section":"Hauling","rank":"MEMBER","progress":0.54,
           "place":{"scope":"ReputationScope_Hauling","section":"Hauling","rank":"Member","rankIndex":4,"rankCount":8,
                    "floor":5250,"next":27750,"estimate":17405,"resolution":132,"perk":null},
           "ladder":[{"name":"Not Eligible","perk":null,"minReputation":-1000,"fill":0},
                     {"name":"Trainee","perk":null,"minReputation":0,"fill":1},
                     {"name":"Rookie","perk":null,"minReputation":50,"fill":1},
                     {"name":"Junior","perk":null,"minReputation":250,"fill":1},
                     {"name":"Member","perk":null,"minReputation":5250,"fill":0.54},
                     {"name":"Experienced","perk":null,"minReputation":27750,"fill":0},
                     {"name":"Senior","perk":null,"minReputation":77750,"fill":0},
                     {"name":"Master","perk":null,"minReputation":237750,"fill":null}],
           "lastSeen":"2026-10-24T18:59:59Z","lastShot":"a.jpg","history":[],"change":null,"work":null,
           "pay":{"rep":500,"source":"ladder","contract":"Member | Stellar Medium Haul | to Ruin Station","at":"2026-10-24T20:25:31Z"}},
          {"name":"COVALEX","standing":"NEUTRAL","section":null,"rank":null,"progress":null,"place":null,"ladder":[],
           "lastSeen":"2026-10-24T18:59:50Z","lastShot":"c.jpg","history":[],"change":null,"work":null,"pay":null}],
         "readings":2,"unmeasured":0,"unmeasurable":0,"canRead":true,"watching":true,"ladders":46}
        """;

    private const string Host = "__dom.node('#rep-run-body')";

    private static string Control(string id) => $"{Host}.descendants().find(n => n.id === '{id}')";

    private static Page Loaded(string board = Board, string? lang = null)
    {
        var page = lang is null ? new Page() : new Page(lang);
        page.Serve("/api/reputation", board);
        page.Serve("/api/trips", "[]");
        page.Do("""
            atlas = [{ name: 'Ruin Station', rawId: 'Pyro_RuinStation' }, { name: 'Checkmate', rawId: 'Pyro_Checkmate' }];
            await loadRepRun();
            """);
        return page;
    }

    private static void Type(Page page, string id, string value) =>
        page.Do($"{{ const input = {Control(id)}; input.value = {Page.Quote(value)}; input.fire('input'); }}");

    [Fact]
    public void The_run_starts_from_the_rank_read_and_the_figure_the_tag_gives()
    {
        var page = Loaded();
        var text = page.NodeText("#rep-run-body");

        Assert.Contains("Member · about 17,405 rep ±132 · 10,345 rep to Experienced", text);
        Assert.Equal("500", page.Text($"{Control("rep-run-rep")}.value"));
        Assert.Contains("The Member figure on the rep tag of “Member | Stellar Medium Haul | to Ruin Station”", text);
        Assert.Contains("StarStrings’ research, not the game’s", text);
        Assert.Contains("21 deliveries at 500 rep reach Experienced.", text);

        // Only an organisation placed on a ladder can be climbed: Covalex has no rank read.
        Assert.Equal(1, page.Count($"{Control("rep-run-org")}.options.length"));
    }

    [Fact]
    public void Nothing_is_written_until_both_ends_are_named()
    {
        var page = Loaded();

        Assert.True(page.Truth($"{Host}.byClass('rep-run-make')[0].disabled"));

        Type(page, "rep-run-pickup", "Checkmate");
        Type(page, "rep-run-drop", "Ruin Station");

        Assert.False(page.Truth($"{Host}.byClass('rep-run-make')[0].disabled"));
    }

    /// <summary>
    /// Each delivery is a pickup and a drop; the drops carry where the pilot
    /// should be by then, and the one that crosses into Experienced says so.
    /// </summary>
    [Fact]
    public void The_plan_names_the_rank_on_the_drop_that_reaches_it()
    {
        var page = Loaded();
        Type(page, "rep-run-pickup", "Checkmate");
        Type(page, "rep-run-drop", "Ruin Station");

        page.Do($"await {Host}.byClass('rep-run-make')[0].fire('click');");

        const string sent = "JSON.parse(__fetch.calls.filter(c => c.url === '/api/trips' && c.body).pop().body)";
        Assert.Equal("RED WIND LINEHAUL rep run · 21 deliveries", page.Text($"{sent}.title"));
        Assert.Equal(42, page.Count($"{sent}.stops.length"));

        Assert.Equal("Pyro_Checkmate", page.Text($"{sent}.stops[0].placeId"));
        Assert.Equal("Delivery 1 of 21 · pick up the crate", page.Text($"{sent}.stops[0].note"));
        Assert.Equal("collect", page.Text($"{sent}.stops[0].actions[0].kind"));

        Assert.Equal("Pyro_RuinStation", page.Text($"{sent}.stops[1].placeId"));
        Assert.Equal("Delivery 1 of 21 · about 17,905 rep, Member 56%", page.Text($"{sent}.stops[1].note"));
        Assert.Equal("unload", page.Text($"{sent}.stops[1].actions[0].kind"));

        Assert.Equal("Delivery 20 of 21 · about 27,405 rep, Member 98%", page.Text($"{sent}.stops[39].note"));
        Assert.Equal("Delivery 21 of 21 · about 27,905 rep — Experienced reached", page.Text($"{sent}.stops[41].note"));
    }

    /// <summary>A long climb is written a shift at a time, and the page says how to carry on.</summary>
    [Fact]
    public void A_long_climb_is_planned_a_shift_at_a_time()
    {
        var page = Loaded();
        Type(page, "rep-run-rep", "50");

        var text = page.Text($"{Host}.byClass('rep-run-summary')[0].textContent");
        Assert.Contains("207 deliveries at 50 rep reach Experienced.", text);
        Assert.Contains("This plan writes out the first 25", text);

        Assert.Equal(25, page.Count("repRunPlan(repRunCandidates(repRunBoard)[0], 50, 25).deliveries.length"));
    }

    [Fact]
    public void Without_a_tag_the_figure_is_asked_for_rather_than_guessed()
    {
        var page = Loaded(Board.Replace(
            "\"pay\":{\"rep\":500,\"source\":\"ladder\",\"contract\":\"Member | Stellar Medium Haul | to Ruin Station\",\"at\":\"2026-10-24T20:25:31Z\"}",
            "\"pay\":null"));

        Assert.Equal("", page.Text($"{Control("rep-run-rep")}.value"));
        Assert.Contains("None of their contracts in your logs carries a rep tag", page.NodeText("#rep-run-body"));
        Assert.Contains("Type what one delivery pays", page.Text($"{Host}.byClass('rep-run-summary')[0].textContent"));
        Assert.True(page.Truth($"{Host}.byClass('rep-run-make')[0].disabled"));
    }

    [Fact]
    public void With_no_rep_screenshot_the_page_says_how_to_get_one()
    {
        var page = Loaded("""{"organisations":[],"readings":0,"unmeasured":0,"unmeasurable":0,"canRead":true,"watching":true,"ladders":46}""");

        Assert.Contains("Take a Rep app screenshot of the organisation you are working for", page.NodeText("#rep-run-body"));
    }

    [Fact]
    public void The_run_reads_in_french()
    {
        var page = Loaded(lang: "fr");
        var text = page.NodeText("#rep-run-body");

        Assert.Contains("21 livraisons à 500 de réputation atteignent Experienced.", text);
        Assert.Contains("une recherche de StarStrings, pas une donnée du jeu", text);
    }
}
