namespace Quantumwake.WebTests;

/// <summary>
/// Named setups on the pages (1.2.36): Routes presets kept by the app rather
/// than one browser, moved across once from where they used to live, and the
/// Stream Deck choice coming back after a reload.
/// </summary>
public class SavedSetupsTests
{
    private const string Deck = """
        {"ready":true,"profileFound":true,"commands":[
          {"id":"preset/flight","group":"Flight","label":"Gear","key":"n","preset":true},
          {"id":"preset/extra","group":"Extended","label":"Pins","key":"0","preset":true}]}
        """;

    [Fact]
    public void Presets_come_from_the_app_and_fill_the_picker()
    {
        var page = new Page();
        page.Serve("/api/setups?kind=route-preset",
            """[{"id":"p1","kind":"route-preset","name":"Hauling","data":{"ship":"C2","capital":"500000","ranking":"reliable"}}]""");
        page.Do("await loadRouteProfiles();");

        Assert.Equal("Hauling", page.Text("routeProfiles[0].name"));
        Assert.Equal("500000", page.Text("routeProfiles[0].capital"));
        Assert.Contains("Hauling", page.NodeText("#routes-profile"));
    }

    [Fact]
    public void Presets_kept_in_the_browser_are_moved_to_the_app_once()
    {
        var page = new Page();
        page.Serve("/api/setups?kind=route-preset", "[]");
        page.Serve("/api/setups", """{"id":"p9","kind":"route-preset","name":"Old one","data":{}}""");
        page.Do("""
            localStorage.setItem('qw-route-profiles', JSON.stringify([{ id: 'x', name: 'Old one', capital: '250000', ship: 'Cutlass' }]));
            await loadRouteProfiles();
            """);

        var sent = page.BodyOf("/api/setups");
        Assert.Contains("\"kind\":\"route-preset\"", sent);
        Assert.Contains("\"name\":\"Old one\"", sent);
        Assert.Contains("\"capital\":\"250000\"", sent);
        Assert.DoesNotContain("\"id\":\"x\"", sent);
        Assert.True(page.Truth("localStorage.getItem('qw-route-profiles') === null"));
    }

    [Fact]
    public void The_last_deck_choice_comes_back_instead_of_the_defaults()
    {
        var page = new Page();
        page.Serve("/api/setups?kind=deck",
            """[{"id":"d1","kind":"deck","name":"Last used","data":{"model":"xl","source":"current","actions":["preset/extra"]}}]""");
        page.Serve("/api/streamdeck?defaults=false", Deck);
        page.Do("await loadStreamDeck();");

        Assert.True(page.Truth("deckSelection.has('preset/extra') && !deckSelection.has('preset/flight')"));
        Assert.Equal("xl", page.Text("__dom.node('#deck-model').value"));
    }

    [Fact]
    public void With_nothing_kept_the_deck_starts_from_the_default_categories()
    {
        var page = new Page();
        page.Serve("/api/setups?kind=deck", "[]");
        page.Serve("/api/streamdeck?defaults=false", Deck);
        page.Do("await loadStreamDeck();");

        Assert.True(page.Truth("deckSelection.has('preset/flight') && !deckSelection.has('preset/extra')"));
    }

    [Fact]
    public void Named_deck_choices_are_listed_but_last_used_is_not()
    {
        var page = new Page();
        page.Serve("/api/setups?kind=deck", """
            [{"id":"d1","kind":"deck","name":"Last used","data":{"model":"15","actions":[]}},
             {"id":"d2","kind":"deck","name":"Mining","data":{"model":"xl","actions":["preset/extra"]}}]
            """);
        page.Do("await loadDeckSetups();");

        var options = page.NodeText("#deck-saved");
        Assert.Contains("Mining", options);
        Assert.DoesNotContain("Last used", options);
    }
}
