namespace Quantumwake.WebTests;

/// <summary>
/// A Nyx I place's land says how meeting its people is set up, in the game's
/// own two words rather than a "hostile" nobody has checked.
/// </summary>
public class LandEncounterTests
{
    [Fact]
    public void Combat_lands_are_marked_and_named_as_the_files_name_them()
    {
        var page = new Page();

        Assert.Equal("combat encounters, easy|true", page.Text("(() => { const m = landEncounter('CombatEasy'); return m.text + '|' + m.combat; })()"));
        Assert.Equal("social encounters, easy|false", page.Text("(() => { const m = landEncounter('SocialEasy'); return m.text + '|' + m.combat; })()"));
    }

    [Fact]
    public void A_place_outside_a_land_gets_nothing()
    {
        var page = new Page();

        Assert.Equal("null|null", page.Text("String(landEncounter(null)) + '|' + String(landEncounter(''))"));
    }

    [Fact]
    public void The_places_table_puts_the_encounter_beside_the_kind_of_place()
    {
        var page = new Page();

        page.Do("""
            const table = lootPlacesTable([
              { name: 'Battlefall Farm', body: 'Nyx I', chance: 0.02, spots: 1, containerName: '',
                label: 'Loot · Generic · HCD · Shattered Blade land', encounter: 'CombatEasy', respawnSeconds: 0 },
              { name: 'Happy Crest', body: 'Nyx I', chance: 0.02, spots: 1, containerName: '',
                label: 'Loot · Generic · HCD · Shubin land', encounter: null, respawnSeconds: 0 },
            ]);
            __dom.node('#loot-where').append(table);
            """);

        var text = page.NodeText("#loot-where");
        Assert.Contains("Shattered Blade land · combat encounters, easy", text);
        Assert.DoesNotContain("Shubin land ·", text);
    }
}
