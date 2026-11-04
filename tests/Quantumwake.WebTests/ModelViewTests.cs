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
            {"cls":"DRAK_Corsair","name":"Drake Corsair","maker":"DRAK","makerName":"Drake Interplanetary","paints":10},
            {"cls":"DRAK_Cutlass_Black","name":"Drake Cutlass Black","maker":"DRAK","makerName":"Drake Interplanetary","paints":12},
            {"cls":"ANVL_Hornet_F7C_Mk2","name":"Anvil F7C Hornet Mk II","maker":"ANVL","makerName":"Anvil Aerospace","paints":14},
            {"cls":"TMBL_Cyclone","name":"Tumbril Cyclone","maker":"TMBL","makerName":null,"paints":0}
          ],
          "weapons": [
            {"cls":"behr_rifle_ballistic_01","name":"P4-AR Rifle","maker":"BEHR","makerName":"Behring","paints":8}
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

    /// <summary>
    /// Three hundred hulls in one grid gave no way in, so the page opens on
    /// the makers - by the game's own name for them where the files carry it,
    /// by the app's table where they do not - with how many each builds.
    /// </summary>
    [Fact]
    public void The_page_opens_on_the_makers()
    {
        var page = Loaded();

        var grid = page.NodeText("#models-grid");
        Assert.Contains("Drake Interplanetary", grid);
        Assert.Contains("2 ships and vehicles", grid);
        Assert.Contains("Anvil Aerospace", grid);
        Assert.Contains("Tumbril", grid);
        Assert.DoesNotContain("Corsair", grid);
        Assert.Contains("4 ships and vehicles", page.NodeText("#models-note"));
    }

    /// <summary>Under its maker a hull is named without the maker again.</summary>
    [Fact]
    public void A_maker_opens_its_own_models()
    {
        var page = Loaded();

        page.Do("modelsMaker = 'DRAK'; renderModelsPage();");

        var grid = page.NodeText("#models-grid");
        Assert.Contains("Corsair", grid);
        Assert.Contains("Cutlass Black", grid);
        Assert.DoesNotContain("Drake Corsair", grid);
        Assert.DoesNotContain("Hornet", grid);
        Assert.Contains("All makers", grid);
    }

    /// <summary>A rifle's name does not start with its maker, so the game's word for the maker is what heads it.</summary>
    [Fact]
    public void Personal_weapons_are_the_other_list()
    {
        var page = Loaded();

        page.Do("__dom.node('#models-kind').value = 'weapons'; renderModelsPage();");
        Assert.Contains("Behring", page.NodeText("#models-grid"));
        Assert.DoesNotContain("Drake", page.NodeText("#models-grid"));

        page.Do("modelsMaker = 'BEHR'; renderModelsPage();");
        Assert.Contains("P4-AR Rifle", page.NodeText("#models-grid"));
    }

    /// <summary>A search skips the makers and answers across all of them, by name, class or maker.</summary>
    [Fact]
    public void A_search_answers_across_every_maker()
    {
        var page = Loaded();

        page.Do("__dom.node('#models-search').value = 'hornet'; renderModelsPage();");
        var grid = page.NodeText("#models-grid");
        Assert.Contains("F7C Hornet Mk II", grid);
        Assert.Contains("Anvil Aerospace", grid);
        Assert.DoesNotContain("Corsair", grid);

        page.Do("__dom.node('#models-search').value = 'drak_corsair'; renderModelsPage();");
        Assert.Contains("Corsair", page.NodeText("#models-grid"));
        Assert.Contains("1 match", page.NodeText("#models-note"));
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
