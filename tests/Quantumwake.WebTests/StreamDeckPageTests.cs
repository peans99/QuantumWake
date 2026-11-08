namespace Quantumwake.WebTests;
public class StreamDeckPageTests
{
    [Fact] public void Page_explains_missing_profiles_and_refuses_empty_exports()
    {
        var page = new Page();
        page.Serve("/api/streamdeck?defaults=false", "{\"ready\":true,\"profileFound\":false,\"commands\":[]}");
        page.Do("await loadStreamDeck();");
        Assert.Contains("No saved keybinding profile", page.NodeText("#deck-status"));
        Assert.True(page.Truth("__dom.node('#deck-download').disabled && __dom.node('#deck-open').disabled"));
    }
    [Fact] public void Existing_categories_start_selected_and_extended_is_optional()
    {
        var page = new Page();
        page.Serve("/api/streamdeck?defaults=false", "{\"ready\":true,\"profileFound\":true,\"commands\":[{\"id\":\"preset/flight\",\"group\":\"Flight\",\"label\":\"Gear\",\"key\":\"n\",\"preset\":true},{\"id\":\"preset/extra\",\"group\":\"Extended\",\"label\":\"Pins\",\"key\":\"0\",\"preset\":true}]}");
        page.Do("await loadStreamDeck();");
        Assert.True(page.Truth("deckSelection.has('preset/flight') && !deckSelection.has('preset/extra')"));
        Assert.Contains("Flight",page.NodeText("#deck-categories"));
        Assert.Contains("Extended",page.NodeText("#deck-categories"));
    }
    [Fact] public void Search_matches_commands_and_unbound_actions_are_not_selected_for_export()
    {
        var page=new Page();
        page.Do("deckCommands = [{id:'gear',group:'Flight',label:'Landing Gear',key:'n',problem:null},{id:'door',group:'Flight',label:'Door',key:'',problem:'Unbound'}]; deckSelection = new Set(['gear','door']);");
        Assert.True(page.Truth("deckSelected().length === 1 && deckSelected()[0].id === 'gear'"));
        page.Do("__dom.node('#deck-search').value = 'landing';");
        Assert.True(page.Truth("deckShown().length === 1 && deckShown()[0].id === 'gear'"));
    }
}
