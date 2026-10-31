namespace Quantumwake.WebTests;

/// <summary>
/// Where the game's own loot tables say an item comes from, beside what the
/// logs say was found. The rows are this install's: the S-38 reads 1.755% a
/// roll from a small weapons box, three times over in one preset because it
/// lists the box under Common, Uncommon and Rare.
/// </summary>
public class LootWhereTests
{
    private const string Pickups = """
        [{"at":"2026-08-20T09:00:00+00:00","item":"S-38 Pistol","itemClass":"behr_pistol_ballistic_01",
          "place":"Orison","category":"Weapons","price":null,
          "bestRoll":0.01755,"bestFrom":"Distribution Centres · High Security","lootSources":12},
         {"at":"2026-08-19T09:00:00+00:00","item":"Goldfish","itemClass":"GoldFish",
          "place":"Orison","category":"Other","price":null,"lootSources":0}]
        """;

    private const string Where = """
        {"item":"behr_pistol_ballistic_01","name":"S-38 Pistol","tables":333,"hits":[
          {"kind":"slot","preset":"V3SlotPreset_Loot_Military","label":"Loot · Military","group":"Weapons Small Rare",
           "container":"Lootable_Container_Weapons_Small","containerName":"Weapons box","slotChance":1,
           "table":"V3LootTable_Weapons_Small","perRoll":0.01755,"via":"Small weapons / Pistols"},
          {"kind":"slot","preset":"V3SlotPreset_Loot_Military","label":"Loot · Military","group":"Weapons Small Common",
           "container":"Lootable_Container_Weapons_Small","containerName":"Weapons box","slotChance":1,
           "table":"V3LootTable_Weapons_Small","perRoll":0.01755,"via":"Small weapons / Pistols"},
          {"kind":"slot","preset":"LootGeneration_SlotPreset_Station","label":"Station","container":"Lootable_Generated_Container_06_dirty",
           "containerName":"Standard Container","slotChance":0.4,"respawnSeconds":1200,
           "table":"GeneralLoot","perRoll":0.00232,"via":"LootArchetype_Weapons / Pistol"},
          {"kind":"body","preset":"Corpse_Civilian","label":"Civilian","container":"Corpse_Civilian",
           "containerName":"Corpse_Civilian","slotChance":1,"table":"GeneralLoot","perRoll":0.00232,"via":"LootArchetype_Weapons / Pistol"}
        ]}
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/loot?days=0", Pickups);
        page.Serve("/api/loot/where?item=behr_pistol_ballistic_01", Where);
        page.Do("__dom.node('#loot-period').value = '0'; await loadLoot();");
        return page;
    }

    [Fact]
    public void A_pickup_shows_its_best_roll_beside_its_price()
    {
        Assert.Contains("1.8%", Loaded().NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// The goldfish came out of no table - it is a hangar decoration - so its
    /// odds cell is a dash, not "0%", which would claim it cannot be had.
    /// </summary>
    [Fact]
    public void An_item_no_table_rolls_gets_a_dash_and_no_link()
    {
        var page = Loaded();

        Assert.Equal(1, page.Count("__dom.node('#loot-table tbody').querySelectorAll('.link').length"));
        Assert.DoesNotContain("0%", page.NodeText("#loot-table tbody"));
    }

    /// <summary>
    /// One preset listing the same box under three rarity groups is one place
    /// to go, not three; the panel rolls hits up by kind of place.
    /// </summary>
    [Fact]
    public void Where_an_item_comes_from_reads_one_row_per_kind_of_place()
    {
        var page = Loaded();
        page.Do("await showLootWhere('behr_pistol_ballistic_01');");

        Assert.False(page.Truth("__dom.node('#loot-where').hidden"));
        Assert.Equal(3, page.Count("__dom.node('#loot-where').querySelectorAll('tbody')[0].children.length"));

        var text = page.NodeText("#loot-where");
        Assert.Contains("Loot · Military", text);
        Assert.Contains("20 min", text);
        Assert.Contains("2 kinds of place", text);
        Assert.Contains("1 kind of body", text);
    }

    /// <summary>
    /// Armour colours sit near a thousandth of a percent. Rounded to whole
    /// percent they would all read "0%".
    /// </summary>
    [Theory]
    [InlineData(0.01755, "1.8%")]
    [InlineData(0.2174, "22%")]
    [InlineData(0.0011, "0.11%")]
    [InlineData(0.0000091, "0.001%")]
    [InlineData(0.000000001, "<0.001%")]
    public void Small_odds_keep_enough_places_to_say_something(double perRoll, string shown)
    {
        Assert.Equal(shown, new Page().Text($"lootPercent({perRoll.ToString(System.Globalization.CultureInfo.InvariantCulture)})"));
    }
}
