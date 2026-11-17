namespace Quantumwake.WebTests;

/// <summary>
/// Finding something to put on a shopping list (1.2.60): a search over what can
/// be bought, each with the counter worth naming - nearest, cheapest, or the one
/// that is also a stop for the rest of the list - in place of one dropdown of
/// 2,565 names in alphabetical order.
/// </summary>
public sealed class JobFindTests
{
    private const string Near = """
        {"from":{"name":"New Babbage","id":"Stanton4_NewBabbage","body":"microTech","system":"Stanton","source":"live"},
         "sort":"nearest","pricesKnown":true,
         "rows":[
          {"name":"MedPen","kind":"item","terminal":"Platinum Bay, New Babbage","place":"New Babbage","placeId":"Stanton4_NewBabbage","body":"microTech","system":"Stanton","here":true,"gm":0,"price":220,"counters":9,"alsoSells":0},
          {"name":"MedPen Holster","kind":"item","terminal":"Shubin Interstellar","place":"Shubin Mining Facility SMCa-6","placeId":"x","body":"microTech","system":"Stanton","here":false,"gm":0,"price":90,"counters":2,"alsoSells":0},
          {"name":"Hadanite","kind":"commodity","terminal":"Admin - Port Tressler","place":"Port Tressler","placeId":"y","body":"microTech","system":"Stanton","here":false,"gm":0,"price":1000,"counters":1,"alsoSells":0},
          {"name":"Agricium","kind":"commodity","terminal":"TDD - Area 18","place":"Area18","placeId":"z","body":"ArcCorp","system":"Stanton","here":false,"gm":21.736,"price":2500,"counters":4,"alsoSells":0},
          {"name":"Ardor Rifle","kind":"item","terminal":"Guns Checkmate","place":"Checkmate","placeId":"w","system":"Pyro","here":false,"price":5000,"counters":1,"alsoSells":0},
          {"name":"Old Hat","kind":"item","here":false,"counters":0,"alsoSells":0}]}
        """;

    private static Page Opened(string near = Near)
    {
        var page = new Page();
        page.Serve("/api/shopping/near?sort=nearest", near);
        page.Do("atlas = []; openJobForm(); await loadJobFind();");
        return page;
    }

    private const string Rows = "__dom.node('#job-find-list').byClass('job-find-row')";

    [Fact]
    public void Opening_the_form_offers_what_is_sold_nearest_first_and_says_measured_from_where()
    {
        var page = Opened();

        Assert.Contains("GET /api/shopping/near?sort=nearest", page.Fetched());
        Assert.Contains("Measured from where you are: New Babbage (Stanton).", page.NodeText("#job-find-from"));

        // Nothing typed: everything with a counter, in the server's order; the
        // thing nobody sells waits to be asked for by name.
        Assert.Equal(5, page.Count($"{Rows}.length"));
        Assert.Equal("MedPenHere, at New Babbage · 220 aUEC · nearest of 9 places", page.Text($"{Rows}[0].textContent"));
        Assert.Contains("Port Tressler, also on microTech · 1,000 aUEC/SCU", page.Text($"{Rows}[2].textContent"));
        Assert.Contains("Area18 · 21.7 Gm · 2,500 aUEC/SCU", page.Text($"{Rows}[3].textContent"));
        Assert.Contains("Checkmate · Pyro", page.Text($"{Rows}[4].textContent"));
    }

    [Fact]
    public void Typing_narrows_it_and_a_pick_goes_into_the_box()
    {
        var page = Opened();

        page.Do("""
            __dom.node('#job-items').value = 'Hadanite 23\n';
            __dom.node('#job-find-text').value = 'medpen';
            __dom.node('#job-find-text').fire('input');
            """);
        Assert.Equal(2, page.Count($"{Rows}.length"));

        page.Do($"{Rows}[1].fire('click');");

        Assert.Equal("Hadanite 23\nMedPen Holster\n", page.Text("__dom.node('#job-items').value"));
        Assert.Equal("", page.Text("__dom.node('#job-find-text').value"));
    }

    [Fact]
    public void The_exact_name_comes_first_and_a_name_on_file_with_no_counter_says_so()
    {
        var page = Opened();

        page.Do("""
            __dom.node('#job-find-text').value = 'old hat';
            __dom.node('#job-find-text').fire('input');
            """);
        Assert.Equal("Old HatNo counter on file sells it", page.Text($"{Rows}[0].textContent"));

        page.Do("""
            __dom.node('#job-find-text').value = 'nothing like it';
            __dom.node('#job-find-text').fire('input');
            """);
        Assert.Equal(0, page.Count($"{Rows}.length"));
        Assert.Contains("can still go in the box", page.NodeText("#job-find-list"));
    }

    [Fact]
    public void Fewest_stops_asks_with_the_lines_already_on_the_list_and_says_what_else_a_place_sells()
    {
        var page = Opened();
        page.Serve("/api/shopping/near?sort=stops&with=Hadanite%7CMedPen", """
            {"from":null,"sort":"stops","pricesKnown":true,
             "rows":[{"name":"MedPen Holster","kind":"item","place":"New Babbage","here":false,"price":90,"counters":2,"alsoSells":2},
                     {"name":"Agricium","kind":"commodity","place":"Area18","here":false,"price":2500,"counters":4,"alsoSells":0}]}
            """);

        page.Do("""
            __dom.node('#job-items').value = 'Hadanite 23\nMedPen 4\n';
            await __dom.node('#job-find-sort').fire('click', { target: { dataset: { sort: 'stops' } } });
            await loadJobFind();
            """);

        Assert.Contains("GET /api/shopping/near?sort=stops&with=Hadanite%7CMedPen", page.Fetched());
        Assert.Equal(1, page.Count($"{Rows}.length"));
        Assert.Contains("sells 2 other things on your list", page.Text($"{Rows}[0].textContent"));
    }

    [Fact]
    public void With_nowhere_to_measure_from_it_says_so_and_waits_for_a_name()
    {
        var page = Opened("""{"from":null,"sort":"nearest","pricesKnown":true,"rows":[{"name":"MedPen","kind":"item","place":"Area18","here":false,"price":220,"counters":3,"alsoSells":0}]}""");

        Assert.Contains("No place to measure from yet", page.NodeText("#job-find-from"));
        Assert.Equal(0, page.Count($"{Rows}.length"));
    }

    [Fact]
    public void A_failed_lookup_points_at_the_box()
    {
        var page = new Page();
        page.Do("atlas = []; openJobForm(); await loadJobFind();");

        Assert.Contains("Type the name in the box instead", page.NodeText("#job-find-from"));
    }
}
