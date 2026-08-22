namespace Quantumwake.WebTests;

/// <summary>
/// The same shopping list read as a set of landings rather than a set of
/// things: what one stop is worth, and what is left for the next.
/// </summary>
/// <remarks>
/// The point of a shopping list is rotating around the map picking things up,
/// which is a question about places. Both views write into one answer, so the
/// tests care most about that: what is ticked here must be what the plan is
/// built from, and a choice made in one view must survive the other.
/// </remarks>
public class ShoppingByLocationTests
{
    /// <summary>
    /// Area18 has both; Levski has only Laranite, cheaper, and is lawless.
    /// Cheapest first, which is the order the server hands them over in.
    /// </summary>
    private const string LaraniteSellers = """
        {"name":"Laranite","kind":"commodity","sellers":[
          {"kind":"commodity","terminal":"Levski","placeId":"Nyx_Levski","place":"Levski",
           "system":"Nyx","security":"lawless","price":7000,"scu":900},
          {"kind":"commodity","terminal":"Area18","placeId":"Stanton3_Area18","place":"Area18",
           "system":"Stanton","security":"monitored","price":7400,"scu":1050}
        ]}
        """;

    private const string AgriciumSellers = """
        {"name":"Agricium","kind":"commodity","sellers":[
          {"kind":"commodity","terminal":"Area18","placeId":"Stanton3_Area18","place":"Area18",
           "system":"Stanton","security":"monitored","price":6900,"scu":800}
        ]}
        """;

    private const string ShieldSellers = """
        {"name":"Bulwark","kind":"item","sellers":[
          {"kind":"item","terminal":"Dumper's GrimHEX","placeId":"GrimHEX","place":"GrimHEX",
           "system":"Stanton","security":"monitored","price":49500,"scu":0}
        ]}
        """;

    private static Page Chooser()
    {
        var page = new Page();
        page.Serve("/api/shopping/sellers?name=Laranite", LaraniteSellers);
        page.Serve("/api/shopping/sellers?name=Agricium", AgriciumSellers);
        page.Serve("/api/shopping/sellers?name=Bulwark", ShieldSellers);
        page.Serve("/api/trips", "[]");

        page.Do("""
            atlas = [];
            await loadTrips();

            const job = {
              id: 'j1',
              title: 'Restock',
              items: [
                { name: 'Laranite', needed: 10, unit: 'SCU', have: false },
                { name: 'Agricium', needed: 5, unit: 'SCU', have: false },
                { name: 'Bulwark', needed: 1, unit: '', have: false }
              ],
            };

            await planShoppingTrip(job, __dom.node('#test-card'));
            """);

        return page;
    }

    /// <summary>The second segmented button is "By location".</summary>
    private static void ShowLocations(Page page) =>
        page.Do("__dom.node('#test-card').byClass('chooser-views')[0].byClass('active').length; "
                + "__dom.node('#test-card').byClass('chooser-views')[0].children[1].fire('click');");

    private const string Stops = "__dom.node('#test-card').byClass('chooser-stop')";

    private static void Tick(Page page, string terminal)
    {
        var stop = $"{Stops}.find(s => s.byClass('name')[0].textContent.includes('{terminal}'))";

        page.Do($"{stop}.byClass('stop-tick')[0].children[0].checked = true; "
                + $"{stop}.byClass('stop-tick')[0].children[0].fire('change');");
    }

    /// <summary>
    /// The panel arrives with the default plan ticked, so a test about what
    /// one stop does has to start from nothing chosen.
    /// </summary>
    private static void UntickAll(Page page) =>
        page.Do($$"""
            for (const stop of {{Stops}}) {
              const box = stop.byClass('stop-tick')[0].children[0];
              if (box.checked) { box.checked = false; box.fire('change'); }
            }
            """);

    [Fact]
    public void A_counter_appears_once_however_much_of_the_list_it_carries()
    {
        var page = Chooser();
        ShowLocations(page);

        Assert.Equal(3, page.Count(Stops + ".length"));
    }

    /// <summary>
    /// The landing worth most comes first: a stop that covers two things beats
    /// one that covers one, whatever the prices are.
    /// </summary>
    [Fact]
    public void The_stop_that_covers_most_of_the_list_leads()
    {
        var page = Chooser();
        ShowLocations(page);

        Assert.Contains("Area18", page.Text($"{Stops}[0].byClass('name')[0].textContent"));
        Assert.Contains("2 of 3", page.Text($"{Stops}[0].byClass('stop-sum')[0].textContent"));
    }

    [Fact]
    public void Lawless_space_is_said_on_the_stop()
    {
        var page = Chooser();
        ShowLocations(page);

        Assert.Equal(1, page.Count($"{Stops}.filter(s => s.byClass('sec-lawless').length).length"));
    }

    [Fact]
    public void Ticking_a_stop_puts_everything_it_carries_on_the_plan()
    {
        var page = Chooser();
        ShowLocations(page);
        UntickAll(page);
        Tick(page, "Area18");

        // Area18 supplies two of the three, and says so on its own chips.
        Assert.Equal(2, page.Count($"{Stops}[0].byClass('stop-item').filter(i => i.classList.contains('mine')).length"));
    }

    /// <summary>
    /// First come: a second stop is for what is still missing, so the same
    /// thing is never bought twice on one run.
    /// </summary>
    [Fact]
    public void A_later_stop_only_takes_what_is_still_open()
    {
        var page = Chooser();
        ShowLocations(page);
        UntickAll(page);
        Tick(page, "Area18");
        Tick(page, "Levski");

        var levski = $"{Stops}.find(s => s.byClass('name')[0].textContent.includes('Levski'))";

        Assert.Equal(1, page.Count($"{levski}.byClass('stop-item').filter(i => i.classList.contains('taken')).length"));
        Assert.Equal(0, page.Count($"{levski}.byClass('stop-item').filter(i => i.classList.contains('mine')).length"));
    }

    /// <summary>
    /// Dropping a stop does not drop what it was carrying: the things it had
    /// fall to whatever other ticked stop can supply them, which is the whole
    /// reason to look at a list this way - one landing fewer, same shopping.
    /// </summary>
    [Fact]
    public void Unticking_a_stop_moves_its_things_to_one_that_is_left()
    {
        var page = Chooser();
        ShowLocations(page);

        var levski = $"{Stops}.find(s => s.byClass('name')[0].textContent.includes('Levski'))";
        page.Do($"{levski}.byClass('stop-tick')[0].children[0].checked = false; "
                + $"{levski}.byClass('stop-tick')[0].children[0].fire('change');");

        page.Do("__dom.node('#test-card').byClass('chooser-foot')[0].byClass('ghost')[0].fire('click');");

        var body = page.BodyOf("/api/trips");

        // Laranite was Levski's; Area18 sells it too, and was already a stop.
        Assert.Contains("Laranite 10 SCU, Agricium 5 SCU", body);
        Assert.DoesNotContain("Levski", body);
        Assert.Contains("GrimHEX", body);
    }

    /// <summary>
    /// Buying each thing where it is cheapest is one landing per thing, and
    /// fuel and time cost more than the difference. One click has to be able
    /// to say "cover this in as few landings as you can".
    /// </summary>
    [Fact]
    public void Fewest_stops_covers_the_list_in_as_few_landings_as_it_can()
    {
        var page = Chooser();
        ShowLocations(page);

        page.Do("__dom.node('#test-card').byClass('chooser-actions')[0].byClass('ghost')[0].fire('click');");

        // Area18 has both commodities, GrimHEX has the shield: two landings
        // rather than the three the cheapest-each default asked for.
        Assert.Contains("2 stops", page.Text("__dom.node('#test-card').byClass('chooser-foot')[0].textContent"));

        var ticked = $"{Stops}.filter(s => s.byClass('stop-tick')[0].children[0].checked).length";
        Assert.Equal(2, page.Count(ticked));
    }

    /// <summary>
    /// The panel opens with a plan already made - the cheapest seller that can
    /// fill each line - so this view has to arrive showing that plan. Ticks
    /// that started blank while the count at the foot said "2 stops" would be
    /// two answers to one question.
    /// </summary>
    [Fact]
    public void The_plan_already_made_arrives_as_ticked_stops()
    {
        var page = Chooser();
        ShowLocations(page);

        var ticked = $"{Stops}.filter(s => s.byClass('stop-tick')[0].children[0].checked).length";

        // Levski is the cheaper Laranite, Area18 the only Agricium, GrimHEX
        // the only shield: three defaults, three ticks.
        Assert.Equal(3, page.Count(ticked));
    }
}
