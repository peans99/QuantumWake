namespace Quantumwake.WebTests;

/// <summary>
/// The Armoury's suit calculator: a suit's weight, carry space, speed and G
/// tolerance, started from the loadout and changed piece by piece.
/// </summary>
public class SuitCalculatorTests
{
    // Figures as the game files give them: a medium set sums to -0.5 and a
    // standard undersuit is +0.9; profile 1 is the player's first, verbatim.
    private const string Armoury = """
        {
          "ready": true,
          "armour": [
            {"family":"Clash","slot":"Helmet","weight":"Medium","mass":2,"gForceResistance":-0.0625,"capacityMicroScu":0,"name":"Clash Helmet","pieces":[{"class":"med_helmet","uuid":"u-helmet","name":"Clash Helmet"}]},
            {"family":"Clash","slot":"Core","weight":"Medium","mass":5,"gForceResistance":-0.25,"capacityMicroScu":12000,"name":"Clash Core","pieces":[{"class":"med_core","uuid":"u-core","name":"Clash Core"}]},
            {"family":"Clash","slot":"Arms","weight":"Medium","mass":2,"gForceResistance":-0.0625,"capacityMicroScu":0,"name":"Clash Arms","pieces":[{"class":"med_arms","uuid":"u-arms","name":"Clash Arms"}]},
            {"family":"Clash","slot":"Legs","weight":"Medium","mass":3,"gForceResistance":-0.125,"capacityMicroScu":3000,"name":"Clash Legs","pieces":[{"class":"med_legs","uuid":"u-legs","name":"Clash Legs"}]},
            {"family":"Odyssey II","slot":"Undersuit","weight":"","mass":1,"gForceResistance":0.9,"capacityMicroScu":0,"name":"Odyssey II Undersuit","pieces":[{"class":"undersuit","uuid":"u-under","name":"Odyssey II Undersuit Alpha"}]},
            {"family":"Testudo","slot":"Core","weight":"Heavy","mass":8,"gForceResistance":-0.5,"capacityMicroScu":15000,"movementRecord":"WearMovement.Heavy","name":"Testudo Core","pieces":[{"class":"heavy_core","uuid":"u-heavy","name":"Testudo Core"}]}
          ],
          "weapons": [
            {"class":"behr_rifle","uuid":"u-rifle","name":"P4-AR Rifle","kind":"Rifle","mass":2.65,"finishes":[{"class":"behr_rifle_tan","uuid":"u-rifle-tan","name":"P4-AR Rifle Tan"}]},
            {"class":"klwe_pistol","uuid":"u-pistol","name":"Arclight Pistol","kind":"Pistol","mass":1.2,"finishes":[]}
          ],
          "attachments": [{"class":"scope","uuid":"u-scope","name":"EE04","mass":0.3,"finishes":[]}],
          "throwables": [{"class":"frag","uuid":"u-frag","name":"MK-4 Frag Grenade","mass":0.5,"finishes":[]}],
          "melee": [],
          "body": {
            "gForce": [{"index":1,"worst":{"tolerance":3,"maxGees":5},"default":{"tolerance":5,"maxGees":7},"best":{"tolerance":8,"maxGees":12}}],
            "walkSpeed": 2.25, "runSpeed": 5.58, "sprintSpeed": 7.2
          }
        }
        """;

    private const string Loadout = """
        [
          {"port":"Armor_Helmet","items":[{"name":"Clash Helmet","count":1,"reference":{"type":"Char_Armor_Helmet","uuid":"u-helmet"}}]},
          {"port":"helmethook_attach","items":[{"name":"Clash Helmet","count":1,"reference":{"type":"Char_Armor_Helmet","uuid":"u-helmet"}}]},
          {"port":"Armor_Torso","items":[{"name":"Clash Core","count":1,"reference":{"type":"Char_Armor_Torso","uuid":"u-core"}}]},
          {"port":"Armor_Arms","items":[{"name":"Clash Arms","count":1,"reference":{"type":"Char_Armor_Arms","uuid":"u-arms"}}]},
          {"port":"Armor_Legs","items":[{"name":"Clash Legs","count":1,"reference":{"type":"Char_Armor_Legs","uuid":"u-legs"}}]},
          {"port":"Armor_Undersuit","items":[{"name":"Odyssey II Undersuit Alpha","count":1,"reference":{"type":"Char_Armor_Undersuit","uuid":"u-under"}}]},
          {"port":"wep_stocked","items":[{"name":"P4-AR Rifle Tan","count":1,"reference":{"type":"WeaponPersonal","uuid":"u-rifle-tan"}}]},
          {"port":"wep_sidearm","items":[{"name":"Arclight Pistol","count":1,"reference":{"type":"WeaponPersonal","uuid":"u-pistol"}}]},
          {"port":"grenade_attach","items":[{"name":"MK-4 Frag Grenade","count":2,"reference":{"type":"WeaponPersonal","uuid":"u-frag"}}]},
          {"port":"medPen_attach","items":[{"name":"MedPen (Hemozal)","count":1,"reference":{"type":"FPS_Consumable"}}]}
        ]
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/loadout", Loadout);
        page.Do("armouryModel = " + Armoury + ";" + """
            armourySuitIndex = null;
            armourySuit = null;
            try { localStorage.removeItem('qw-armoury-suit'); } catch {}
            await suitCopyLoadout();
            """);
        return page;
    }

    /// <summary>
    /// The loadout fills the suit: armour by slot, a gun worn in a finish as
    /// its family, the helmet on its hook not counted twice, grenades by count.
    /// </summary>
    [Fact]
    public void The_loadout_is_the_starting_suit()
    {
        var page = Loaded();
        var text = page.NodeText("#armoury-suit");

        // 2 + 5 + 2 + 3 + 1 armour, 2.65 + 1.2 guns, 2 × 0.5 grenades.
        Assert.Equal(17.85, page.Number("suitTotals(armourySuit).mass"), 3);
        Assert.Equal("behr_rifle_tan", page.Text("armourySuit.weapons.primary"));
        Assert.Contains("Copied from the loadout", text);
        Assert.Contains("15 mSCU", text);
    }

    /// <summary>A medium set and an undersuit: -0.5 + 0.9.</summary>
    [Fact]
    public void G_resistance_is_the_sum_of_the_pieces()
    {
        var page = Loaded();

        Assert.Equal(0.4, page.Number("suitTotals(armourySuit).resistance"), 4);
        Assert.Contains("+40%", page.NodeText("#armoury-suit"));
    }

    /// <summary>
    /// The check against the outside: guides give a Tailwind flight suit
    /// 97.5% and 95% with its helmet. The files say +0.975 and -0.025.
    /// </summary>
    [Fact]
    public void A_tailwind_suit_and_helmet_read_as_the_guides_say()
    {
        var page = Loaded();

        Assert.Equal("+97.5%", page.Text("suitPercent(0.975)"));
        Assert.Equal("+95%", page.Text("suitPercent(0.975 + -0.025)"));
        Assert.Equal("−12.5%", page.Text("suitPercent(-0.125)"));
    }

    /// <summary>
    /// The files give worst, usual and best; between them is a straight line,
    /// clamped at the ends, and said to be inferred.
    /// </summary>
    [Fact]
    public void A_tier_between_the_ends_is_a_straight_line()
    {
        var page = Loaded();
        const string profile = "armouryModel.body.gForce[0]";

        Assert.Equal(3, page.Number($"suitBlend({profile}, -1, 'tolerance')"), 4);
        Assert.Equal(5, page.Number($"suitBlend({profile}, 0, 'tolerance')"), 4);
        Assert.Equal(6.2, page.Number($"suitBlend({profile}, 0.4, 'tolerance')"), 4);
        Assert.Equal(12, page.Number($"suitBlend({profile}, 3, 'maxGees')"), 4);
        Assert.Contains("assumes a straight line", page.NodeText("#armoury-suit"));
    }

    /// <summary>What the files leave empty counts as 0, and the page says so.</summary>
    [Fact]
    public void Empty_fields_count_as_zero_and_say_so()
    {
        var page = Loaded();
        var text = page.NodeText("#armoury-suit");

        Assert.Contains("MedPen (Hemozal)", text);
        Assert.Contains("mass not known - counted as 0", text);
        Assert.Contains("change these by 0%", text);
        Assert.Contains("5.6 m/s", text);
    }

    /// <summary>
    /// A magazine is not on any Armoury list, but the loadout carries the
    /// install's own mass for it, so it is weighed rather than counted as 0.
    /// </summary>
    [Fact]
    public void Items_the_armoury_does_not_list_are_weighed_from_the_install()
    {
        var page = Loaded();
        page.Serve("/api/loadout", """
            [{"port":"magazine_attach","items":[{"name":"P4-AR Magazine","count":3,"mass":0.4,"reference":{"type":"WeaponAttachment"}}]}]
            """);

        page.Do("await suitCopyLoadout();");

        Assert.Equal(1.2, page.Number("suitTotals(armourySuit).carriedMass"), 4);
        Assert.Equal(0, page.Number("suitTotals(armourySuit).unknown"));
    }

    /// <summary>Strip all leaves nothing: 0 kg and no G either way.</summary>
    [Fact]
    public void Strip_all_empties_the_suit()
    {
        var page = Loaded();

        page.Do("armourySuit = emptySuit(); armourySuit.from = 'stripped'; renderArmourySuit();");

        Assert.Equal(0, page.Number("suitTotals(armourySuit).mass"));
        Assert.Equal(0, page.Number("suitTotals(armourySuit).resistance"));
        Assert.Contains("Stripped", page.NodeText("#armoury-suit"));
    }

    /// <summary>
    /// Swapping one piece changes the figures, and a piece naming a movement
    /// record the app cannot read is named rather than counted as nothing.
    /// </summary>
    [Fact]
    public void Swapping_a_piece_changes_the_figures()
    {
        var page = Loaded();

        page.Do("armourySuit.slots.Core = 'heavy_core'; renderArmourySuit();");

        Assert.Equal(0.15, page.Number("suitTotals(armourySuit).resistance"), 4);
        Assert.Contains("WearMovement.Heavy", page.NodeText("#armoury-suit"));
    }

    /// <summary>The outfit becomes kit lines by name - worn, guns, carried - with repeats counted (1.2.34).</summary>
    [Fact]
    public void The_outfit_becomes_kit_lines_by_name()
    {
        var page = Loaded();
        var items = page.Text("suitKitItems(armourySuit).map(i => i.name + ' x' + i.quantity).join('|')");

        Assert.Equal("Clash Helmet x1|Clash Core x1|Clash Arms x1|Clash Legs x1|Odyssey II Undersuit Alpha x1|P4-AR Rifle Tan x1|Arclight Pistol x1|MK-4 Frag Grenade x2|MedPen (Hemozal) x1", items);
    }

    [Fact]
    public void Save_as_kit_posts_the_outfit_and_offers_prepare()
    {
        var page = Loaded();
        page.Serve("/api/kits", """{"id":"k1","name":"Clash run","items":[],"createdAt":"2026-11-11T00:00:00Z"}""");
        page.Do("window.__status = document.createElement('span'); await saveSuitAsKit('Clash run', window.__status);");

        var sent = page.BodyOf("/api/kits");
        Assert.Contains("\"name\":\"Clash run\"", sent);
        Assert.Contains("{\"name\":\"MK-4 Frag Grenade\",\"quantity\":2,\"optional\":false}", sent);
        Assert.Contains("Saved as the kit Clash run, 9 lines.", page.Text("__status.textContent"));
        Assert.Contains("Prepare it", page.Text("__status.textContent"));
    }

    [Fact]
    public void A_stripped_suit_saves_no_kit()
    {
        var page = Loaded();
        page.Do("armourySuit = emptySuit(); window.__status = document.createElement('span'); await saveSuitAsKit('Nothing', window.__status);");

        Assert.Contains("nothing", page.Text("__status.textContent").ToLowerInvariant());
        Assert.DoesNotContain("POST /api/kits", page.Fetched());
    }
}
