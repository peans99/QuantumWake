namespace Quantumwake.WebTests;

public sealed class LoadoutScreenTests
{
    [Fact]
    public void Current_kit_is_arranged_as_a_character_screen_with_every_observed_slot()
    {
        var page = new Page();

        page.Do("""
            renderLoadout({
              loadoutAsOf: '2026-08-25T20:00:00Z',
              loadout: [
                { port: 'helmet_attach', category: 'Armour', label: 'Head', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'ADP_Armor_Helmet', count: 1 }] },
                { port: 'armor_core', category: 'Armour', label: 'Core', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'ADP_Armor_Core', count: 1 }] },
                { port: 'weapon_attach_hand_right', category: 'Weapons', label: 'Right hand', slotCount: 1,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'behring_p4_ar', count: 1 }] },
                { port: 'grenade_attach', category: 'Throwables', label: 'Grenades', slotCount: 2,
                  currentSeen: '2026-08-25T20:00:00Z', items: [{ name: 'frag_grenade', count: 2 }] }
              ]
            });
            """);

        Assert.Equal(1, page.Count("__dom.node('#loadout-grid').byClass('loadout-character').length"));
        Assert.Equal(1, page.Count("__dom.node('#loadout-grid').byClass('loadout-figure').length"));
        Assert.Equal(4, page.Count("__dom.node('#loadout-grid').byClass('loadout-slot').length"));
        Assert.Contains("Right hand", page.NodeText("#loadout-grid"));
        Assert.Contains("×2", page.NodeText("#loadout-grid"));
    }

    [Fact]
    public void Loadout_search_keeps_the_character_screen_and_only_matching_slots()
    {
        var page = new Page();

        page.Do("""
            __dom.node('#loadout-search').value = 'p4';
            renderLoadout({ loadout: [
              { port: 'helmet_attach', category: 'Armour', label: 'Head', slotCount: 1,
                items: [{ name: 'ADP_Armor_Helmet', count: 1 }] },
              { port: 'weapon_attach_hand_right', category: 'Weapons', label: 'Right hand', slotCount: 1,
                items: [{ name: 'behring_p4_ar', count: 1 }] }
            ] });
            """);

        Assert.Equal(1, page.Count("__dom.node('#loadout-grid').byClass('loadout-slot').length"));
        Assert.Contains("behring p4 ar", page.NodeText("#loadout-grid"));
        Assert.DoesNotContain("ADP Armor Helmet", page.NodeText("#loadout-grid"));
    }
}
