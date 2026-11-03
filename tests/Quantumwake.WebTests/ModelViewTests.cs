namespace Quantumwake.WebTests;

/// <summary>
/// The 3D view as app.js sees it: the Reference page that lists what can be
/// opened, and what the Garage and the Armoury hand the viewer. The viewer
/// itself is a WebGL module and is checked by rendering it, not here.
/// </summary>
public class ModelViewTests
{
    private const string List = """
        {
          "stamp": "8df295d25b39ca0",
          "vehicles": [
            {"cls":"DRAK_Corsair","name":"Drake Corsair","maker":"DRAK","paints":10},
            {"cls":"ANVL_Hornet_F7C_Mk2","name":"Anvil F7C Hornet Mk II","maker":"ANVL","paints":14},
            {"cls":"TMBL_Cyclone","name":"Tumbril Cyclone","maker":"TMBL","paints":0}
          ],
          "weapons": [
            {"cls":"behr_rifle_ballistic_01","name":"P4-AR Rifle","maker":"BEHR","paints":8}
          ]
        }
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/models/list", List);
        page.Do("await loadModelsPage();");
        return page;
    }

    [Fact]
    public void The_page_lists_the_hulls_the_install_can_draw()
    {
        var page = Loaded();

        var grid = page.NodeText("#models-grid");
        Assert.Contains("Drake Corsair", grid);
        Assert.Contains("Tumbril Cyclone", grid);
        Assert.Contains("10 paints", grid);
        Assert.DoesNotContain("P4-AR Rifle", grid);
        Assert.Contains("3 ships and vehicles", page.NodeText("#models-note"));
    }

    [Fact]
    public void Personal_weapons_are_the_other_list()
    {
        var page = Loaded();

        page.Do("__dom.node('#models-kind').value = 'weapons'; renderModelsPage();");

        var grid = page.NodeText("#models-grid");
        Assert.Contains("P4-AR Rifle", grid);
        Assert.DoesNotContain("Drake Corsair", grid);
        Assert.Contains("1 personal weapon", page.NodeText("#models-note"));
    }

    /// <summary>By name, by class and by maker - the class is what a log line or a dump calls it.</summary>
    [Fact]
    public void The_list_narrows_by_name_class_or_maker()
    {
        var page = Loaded();

        page.Do("__dom.node('#models-search').value = 'anvl'; renderModelsPage();");
        var grid = page.NodeText("#models-grid");
        Assert.Contains("Hornet", grid);
        Assert.DoesNotContain("Corsair", grid);

        page.Do("__dom.node('#models-search').value = 'drak_corsair'; renderModelsPage();");
        Assert.Contains("Drake Corsair", page.NodeText("#models-grid"));
    }

    [Fact]
    public void Without_an_install_the_page_says_so()
    {
        var page = new Page();
        page.Fail("/api/models/list", 404, """{"message":"No game install to read models from."}""");

        page.Do("await loadModelsPage();");

        Assert.Contains("No game install", page.NodeText("#models-note"));
        Assert.Equal("", page.NodeText("#models-grid"));
    }

    /// <summary>
    /// The bench's ids are positions in the dump's list, which the viewer
    /// cannot match; the hardpoint chain is what it builds its own tree from.
    /// Only what was changed goes: the viewer already draws the stock part.
    /// </summary>
    [Fact]
    public void A_garage_build_hands_the_viewer_its_changed_ports_by_hardpoint()
    {
        var page = new Page();

        var fits = page.Text("""
            JSON.stringify(garageModelFits({ ports: [
              { portId: 'loadout.65.loadout.0', path: 'hardpoint_cheek_weapon_left/hardpoint_class_2', class: 'KLWE_LaserRepeater_S4', changed: true },
              { portId: 'loadout.66.loadout.0', path: 'hardpoint_cheek_weapon_right/hardpoint_class_2', class: 'BEHR_LaserCannon_S4', changed: false },
              { portId: 'loadout.70', path: 'hardpoint_weapon_wing_top', class: null, changed: true },
              { portId: 'loadout.71', path: null, class: 'X', changed: true }
            ] }))
            """);

        Assert.Equal(
            """[{"path":"hardpoint_cheek_weapon_left/hardpoint_class_2","cls":"KLWE_LaserRepeater_S4"},{"path":"hardpoint_weapon_wing_top","cls":null}]""",
            fits);
    }

    /// <summary>
    /// The Armoury's fit is kept per slot; the viewer wants the port the
    /// attachment goes in. An emptied slot that ships filled is said too, so
    /// the stock magazine or sight is taken off rather than left drawn.
    /// </summary>
    [Fact]
    public void An_armoury_fit_hands_the_viewer_its_chosen_attachments_by_port()
    {
        var page = new Page();

        var fits = page.Text("""
            (() => {
              const w = { class: 'behr_rifle_ballistic_01', ports: [
                { slot: 'Sight', name: 'optics_attach', default: null },
                { slot: 'Barrel', name: 'barrel_attach', default: 'behr_barrel_comp_s1' },
                { slot: 'Underbarrel', name: 'underbarrel_attach', default: null },
                { slot: 'Magazine', name: 'magazine_attach', default: 'behr_mag', fixed: true }
              ] };
              armouryFits[w.class] = { Sight: 'tsar_optics_tsco_x4_s2', Barrel: '', Underbarrel: '' };
              return JSON.stringify(armouryModelFits(w));
            })()
            """);

        Assert.Equal(
            """[{"path":"optics_attach","cls":"tsar_optics_tsco_x4_s2"},{"path":"barrel_attach","cls":null}]""",
            fits);
    }
}
