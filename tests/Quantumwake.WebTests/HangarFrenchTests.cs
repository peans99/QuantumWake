namespace Quantumwake.WebTests;

/// <summary>
/// The Hangar group in French: Fleet, Hangar, Loadout, Stash, Loot, Garage
/// and Armoury, with the saved kits.
/// </summary>
/// <remarks>
/// Item, ship and maker names are the game's, and so are the slot labels the
/// server makes out of port names and a gun's kind. The server's fixed words
/// - an item's category, a kit line's holding - are keyed in English and
/// translated where drawn, so a filter still matches what the server sent.
/// </remarks>
public class HangarFrenchTests
{
    [Fact]
    public void A_prepared_kit_reads_in_french_and_still_asks_about_sightings()
    {
        var page = new Page("fr");
        page.Serve("/api/kits", """[{"id":"k1","name":"Bounty kit","items":[{"name":"MedPen","quantity":4}]}]""");
        page.Serve("/api/kits/k1/prepare", KitPanelTests.Prepared);
        page.Do("await loadKits(); await prepareKit('k1');");

        Assert.Contains("1 objet", page.NodeText("#kit-list"));
        Assert.Equal("Préparation : Bounty kit", page.NodeText("#kit-prepare-title"));

        var table = page.NodeText("#kit-prepare-table");
        Assert.Contains("Vous le portez", table);
        Assert.Contains("Vu en dernier en stockage — Port Tressler", table);
        Assert.Contains("Jamais vu nulle part", table);
        Assert.Contains("facultatif", table);
        Assert.Contains("Je l’ai", table);
        Assert.Contains("MedPen", table);
    }

    [Fact]
    public void The_loadout_reads_in_french_and_keeps_the_slot_labels()
    {
        var page = new Page("fr");
        page.Do("""
            renderLoadout({
              loadoutAsOf: '2026-08-25T20:00:00Z',
              loadout: [
                { port: 'helmet_attach', category: 'Armour', label: 'Head', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'ADP_Armor_Helmet', count: 1 }] },
                { port: 'armor_core', category: 'Armour', label: 'Core', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'ADP_Armor_Core', count: 1,
                    reference: { name: 'Odyssey Core', manufacturer: 'RSI', type: 'Armor', subType: 'Light', size: 2, grade: 3 } }] },
                { port: 'weapon_attach_hand_right', category: 'Weapons', label: 'Right hand', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'behring_p4_ar', count: 1 }] },
                { port: 'grenade_attach', category: 'Throwables', label: 'Grenades', slotCount: 2,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'frag_grenade', count: 2 }] }
              ]
            });
            """);

        // The figure is written as markup, which the stub keeps as written.
        Assert.Contains("PROFIL D’ARMURE : LÉGÈRE",
            page.Text("__dom.node('#loadout-grid').byClass('loadout-figure')[0].innerHTML"));

        var grid = page.NodeText("#loadout-grid");
        Assert.Contains("2/6 zones observées", grid);
        Assert.Contains("1 arme observée", grid);
        Assert.Contains("2 transportés", grid);
        Assert.Contains("Non observé : Sous-couche · Sac · Bras · Jambes", grid);
        Assert.Contains("2 emplacements rangés", grid);
        Assert.Contains("RSI · Armor / Light · S2 · Grade C", grid);
        // The slot label is the server's reading of a port name, so it stays.
        Assert.Contains("Examiner · Right hand", grid);
    }

    [Fact]
    public void Loot_categories_read_in_french_while_the_filter_keeps_the_servers_words()
    {
        var page = new Page("fr");
        page.Serve("/api/loot?days=0", """
            [{"at":"2026-08-20T09:00:00+00:00","item":"P4-AR","itemClass":"behr_rifle_ballistic_01",
              "place":"Port Tressler","category":"Weapons"},
             {"at":"2026-08-18T09:00:00+00:00","item":"Rifle magazine","itemClass":"behr_rifle_mag_01",
              "place":"Port Tressler","category":"Ammo"}]
            """);
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");

        Assert.Equal("|Ammo|Weapons", page.Text("__dom.node('#loot-kind').options.map(o => o.value).join('|')"));
        Assert.Equal("Tout type|Munitions|Armes", page.Text("__dom.node('#loot-kind').options.map(o => o.textContent).join('|')"));
        Assert.Contains("Armes", page.NodeText("#loot-table tbody"));

        page.Do("__dom.node('#loot-kind').value = 'Ammo'; renderLoot(lastLootRows);");
        Assert.DoesNotContain("P4-AR", page.NodeText("#loot-table tbody"));
    }

    [Fact]
    public void A_gun_reads_its_holster_and_damage_in_french()
    {
        var page = new Page("fr");
        page.Serve("/api/armoury", ArmouryPageTests.Model);
        page.Do("await loadArmoury();");

        var p4 = page.Text("__dom.node('#armoury-guns tbody').children[2].textContent");
        Assert.Contains("P4-AR Rifle", p4);
        Assert.Contains("Étui moyen", p4);
        Assert.Contains("12 balistique", p4);
        Assert.Contains("4,138 aUEC", p4);
    }
}
