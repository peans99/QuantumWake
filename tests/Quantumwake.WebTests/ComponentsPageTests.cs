namespace Quantumwake.WebTests;

/// <summary>
/// Reference → Components: a tab per kind of ship part, each with the figures
/// that kind is chosen on. The figures are this install's community digest:
/// the CF-337 Panther's 616 DPS, the 5CA 'Akura' shield's 100,000 HP.
/// </summary>
public class ComponentsPageTests
{
    private const string Components = """
        {"source":"community","uex":true,"parts":[
          {"className":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater","type":"WeaponGun","subType":"Gun","size":3,"grade":1,
           "maker":"Klaus & Werner","price":36308,"stockedAt":29,"bestRoll":null,"npcShips":215,"fittedTo":68,
           "stats":{"health":1500,"em":400,"powerUseMax":1.2,"weapon":{"dps":616,"alpha":44,"rateOfFire":840,"range":1800,"ammoSpeed":1500}}},
          {"className":"BEHR_LaserCannon_S2","name":"M5A Cannon","type":"WeaponGun","subType":"Gun","size":2,"grade":1,
           "maker":"Behring","price":20000,"stockedAt":10,"bestRoll":0.004,"npcShips":0,"fittedTo":3,
           "stats":{"health":900,"weapon":{"dps":420,"alpha":210,"rateOfFire":120,"range":2400,"ammoSpeed":1600}}},
          {"className":"SHLD_BEHR_S03_5CA_SCItem","name":"5CA 'Akura'","type":"Shield","subType":"UNDEFINED","size":3,"grade":3,
           "maker":"Behring","price":60000,"stockedAt":3,"npcShips":35,"fittedTo":8,
           "stats":{"health":1100,"em":1800,"powerUseMax":4,"shield":{"hp":100000,"regen":19000,"downedDelay":12.1,"damagedDelay":6.05}}}
        ]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/reference/components", Components);
        page.Do("await loadComponentsRef();");
        return page;
    }

    private const string Body = "__dom.node('#components-table').querySelector('tbody')";

    private static string Row(Page page, int row) => page.Text($"{Body}.children[{row}].textContent");

    [Fact]
    public void Each_kind_is_its_own_tab_with_its_count()
    {
        var page = Loaded();

        var tabs = page.NodeText("#components-tabs");
        Assert.Contains("Weapons 2", tabs);
        Assert.Contains("Shields 1", tabs);
        Assert.Contains("Power plants 0", tabs);
    }

    /// <summary>A gun is chosen on its damage, so the weapons tab opens sorted by DPS, largest first.</summary>
    [Fact]
    public void Weapons_open_sorted_by_dps()
    {
        var page = Loaded();

        Assert.Contains("CF-337 Panther Repeater", Row(page, 0));
        Assert.Contains("616", Row(page, 0));
        Assert.Contains("M5A Cannon", Row(page, 1));
        Assert.Contains("DPS", page.Text("__dom.node('#components-table').querySelector('thead').textContent"));
    }

    [Fact]
    public void A_heading_click_sorts_by_it_and_a_second_click_turns_it_round()
    {
        var page = Loaded();
        // Size is the first column after the name; largest first, then smallest.
        page.Do("__dom.node('#components-table').querySelector('thead').querySelectorAll('th')[1].fire('click');");
        Assert.Contains("CF-337", Row(page, 0));

        page.Do("__dom.node('#components-table').querySelector('thead').querySelectorAll('th')[1].fire('click');");
        Assert.Contains("M5A Cannon", Row(page, 0));
    }

    [Fact]
    public void The_shields_tab_shows_what_a_shield_is_chosen_on()
    {
        var page = Loaded();
        page.Do("__dom.node('#components-tabs').querySelectorAll('button')[3].fire('click');");

        var head = page.Text("__dom.node('#components-table').querySelector('thead').textContent");
        Assert.Contains("HP", head);
        Assert.Contains("Regen", head);
        Assert.Contains("100,000", Row(page, 0));
        Assert.DoesNotContain("UNDEFINED", Row(page, 0));
        Assert.Equal("true", page.Text("__dom.node('#components-tabs').querySelectorAll('button')[3].getAttribute('aria-pressed')"));
        Assert.Equal("false", page.Text("__dom.node('#components-tabs').querySelectorAll('button')[0].getAttribute('aria-pressed')"));
    }

    [Fact]
    public void A_name_opens_the_parts_reference_card()
    {
        var page = Loaded();
        page.Serve("/api/reference/item?id=KLWE_LaserRepeater_S3", """
            {"className":"KLWE_LaserRepeater_S3","card":{"kind":"part","name":"CF-337 Panther Repeater","facts":[]},"shops":[],"stockOn":[]}
            """);
        page.Do($"await {Body}.byClass('commodity-open')[0].fire('click');");

        Assert.Equal("CF-337 Panther Repeater", page.NodeText("#ref-modal-title"));
    }

    [Fact]
    public void Size_narrows_the_list()
    {
        var page = Loaded();
        page.Do("__dom.node('#components-size').value = '2'; renderComponentsRef();");

        Assert.Equal(1, page.Count($"{Body}.children.length"));
        Assert.Contains("M5A Cannon", Row(page, 0));
    }

    /// <summary>
    /// Without the community download there are no figures, and the page says
    /// so rather than printing dashes that read as zeros.
    /// </summary>
    [Fact]
    public void Without_the_community_figures_the_page_says_why()
    {
        var page = new Page();
        page.Serve("/api/reference/components", """
            {"source":"install","uex":false,"parts":[{"className":"KLWE_LaserRepeater_S3","name":"CF-337 Panther Repeater",
              "type":"WeaponGun","size":3,"grade":1,"maker":"Klaus & Werner","stockedAt":0,"npcShips":0,"fittedTo":0,"stats":null}]}
            """);
        page.Do("await loadComponentsRef();");

        Assert.False(page.Truth("__dom.node('#components-figures-note').hidden"));
    }
}
