namespace Quantumwake.WebTests;

/// <summary>
/// A list that says where it is for.
/// </summary>
/// <remarks>
/// Saying "this is my Area18 run" up front is a different statement from being
/// told afterwards which counter is cheapest: it means the landing is already
/// decided, and the plan should be built around it. The cheapest seller of a
/// common good is routinely three jumps out of the way, and flying there to
/// save 400 aUEC on ten SCU is a worse run than buying it where you already
/// are.
/// </remarks>
public class ShoppingDestinationTests
{
    /// <summary>Levski is cheaper; Area18 is where the player said they'd be.</summary>
    private const string LaraniteSellers = """
        {"name":"Laranite","kind":"commodity","sellers":[
          {"kind":"commodity","terminal":"Levski","placeId":"Nyx_Levski","place":"Levski",
           "system":"Nyx","security":"lawless","price":7000,"scu":900},
          {"kind":"commodity","terminal":"TDD Area 18","placeId":"Stanton3_Area18","place":"Area18",
           "system":"Stanton","security":"monitored","price":7400,"scu":1050}
        ]}
        """;

    private static Page Chooser(string destination, string destinationId)
    {
        var page = new Page();
        page.Serve("/api/shopping/sellers?name=Laranite", LaraniteSellers);
        page.Serve("/api/trips", "[]");

        page.Do($$"""
            atlas = [];
            await loadTrips();

            const job = {
              id: 'j1',
              title: 'Restock',
              destination: {{(destination is null ? "null" : $"'{destination}'")}},
              destinationId: {{(destinationId is null ? "null" : $"'{destinationId}'")}},
              items: [{ name: 'Laranite', needed: 10, unit: 'SCU', have: false }],
            };

            await planShoppingTrip(job, __dom.node('#test-card'));
            """);

        return page;
    }

    private const string Rows = "__dom.node('#test-card').byClass('chooser-row')";

    [Fact]
    public void With_no_destination_the_cheapest_that_can_fill_the_order_wins()
    {
        var page = Chooser(null, null);

        Assert.Equal("Levski", page.Text($"{Rows}[0].children[1].value"));
    }

    [Fact]
    public void A_list_written_for_a_place_starts_at_that_place()
    {
        var page = Chooser("Area18", "Stanton3_Area18");

        Assert.Equal("TDD Area 18", page.Text($"{Rows}[0].children[1].value"));
    }

    /// <summary>
    /// The id is what the map draws with, but a name typed by hand is still an
    /// answer - the counter and the place are named differently ("TDD Area 18"
    /// against "Area18") and the match has to survive that.
    /// </summary>
    [Fact]
    public void A_destination_typed_without_a_map_id_still_counts()
    {
        var page = Chooser("Area18", null);

        Assert.Equal("TDD Area 18", page.Text($"{Rows}[0].children[1].value"));
    }

    [Fact]
    public void The_panel_says_which_place_it_is_favouring()
    {
        var page = Chooser("Area18", "Stanton3_Area18");

        Assert.Contains("Area18 first",
            page.Text("__dom.node('#test-card').byClass('chooser-head')[0].textContent"));
    }

    /// <summary>
    /// A destination nothing on the list is sold at must not silently empty the
    /// plan: the run still happens, it just cannot start there.
    /// </summary>
    [Fact]
    public void A_destination_that_sells_none_of_it_falls_back()
    {
        var page = Chooser("New Babbage", "Stanton4_NewBabbage");

        Assert.Equal("Levski", page.Text($"{Rows}[0].children[1].value"));
    }
}
