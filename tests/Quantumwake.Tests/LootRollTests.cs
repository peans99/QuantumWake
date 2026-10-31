using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// The arithmetic of one loot roll, on shapes taken from this install's
/// tables (read through <c>--loot</c> on 2026-10-31).
/// </summary>
/// <remarks>
/// The game's draw is not in the files, so these pin the reading the page
/// states rather than a truth: weighted picks, empty entries and bands
/// skipped, a uniform pick at the bottom. The case each guards is named.
/// </remarks>
public class LootRollTests
{
    private static readonly Guid Common = Guid.NewGuid(), Rare = Guid.NewGuid(), Faction = Guid.NewGuid();

    private static IReadOnlySet<Guid> Tags(params Guid[] tags) => tags.ToHashSet();
    private static LootCandidate Item(string cls, params Guid[] tags) => new(cls, Tags(tags));
    private static LootTerm Has(Guid tag) => new(Tags(tag), Tags());
    private static readonly IReadOnlyList<IReadOnlyList<LootBand>> NoLayers = [];

    [Fact]
    public void Weights_multiply_down_the_chain()
    {
        // V3LootTable_Weapons_Small: ammo 1, weapons 0.25; within weapons,
        // pistols 1.5 against SMGs 1.
        var table = new[]
        {
            new LootEntry("Small ammo", 1, [new LootChoice("Pistol ammo", 1, [Item("mag")])]),
            new LootEntry("Small weapons", 0.25,
            [
                new LootChoice("Pistols", 1.5, [Item("pistol_a"), Item("pistol_b")]),
                new LootChoice("SMG", 1, [Item("smg")]),
            ]),
        };

        var (odds, unfilled) = LootRoll.Resolve(table, [], NoLayers);

        Assert.Empty(unfilled);
        Assert.Equal(0.8, odds.Single(o => o.ItemClass == "mag").PerRoll, 6);
        Assert.Equal(0.2 * 0.6 / 2, odds.Single(o => o.ItemClass == "pistol_a").PerRoll, 6);
        Assert.Equal(0.2 * 0.4, odds.Single(o => o.ItemClass == "smg").PerRoll, 6);
        Assert.Equal(1, odds.Sum(o => o.PerRoll), 6);
    }

    [Fact]
    public void A_mission_slot_nothing_fills_gives_its_weight_to_the_rest()
    {
        // V3LootTable_Armour_Small: 26 of 32 parts of weight are mission-item
        // slots selecting ArmourMissionItem_* tags no item carries. Counted,
        // the crate would roll nothing four times in five.
        var table = new[]
        {
            new LootEntry("Light helmet", 1, [new LootChoice("Light helmet", 1, [Item("helmet")])]),
            new LootEntry("Mission item 10", 10, [new LootChoice("Mission item 10", 1, [])]),
        };

        var (odds, unfilled) = LootRoll.Resolve(table, [], NoLayers);

        Assert.Equal(1, odds.Single().PerRoll, 6);
        Assert.Equal(["Mission item 10"], unfilled);
    }

    [Fact]
    public void A_pool_filter_strikes_items_before_the_weights_are_shared()
    {
        var table = new[]
        {
            new LootEntry("Weapons", 1, [new LootChoice("Pistols", 1, [Item("plain"), Item("faction", Faction)])]),
        };

        var (odds, _) = LootRoll.Resolve(table, [Has(Faction)], NoLayers);

        Assert.Equal("plain", odds.Single().ItemClass);
        Assert.Equal(1, odds.Single().PerRoll, 6);
    }

    [Fact]
    public void A_rarity_band_is_drawn_before_the_item()
    {
        // V3SecondaryChoices_Rarity_Common: Common 100, Rare 1. Three common
        // pistols and one rare share the roll by band, not by head count.
        var pool = new[] { Item("c1", Common), Item("c2", Common), Item("c3", Common), Item("r1", Rare) };
        var table = new[] { new LootEntry("Weapons", 1, [new LootChoice("Pistols", 1, pool)]) };
        IReadOnlyList<IReadOnlyList<LootBand>> layers =
            [[new LootBand("Common", 100, Has(Common)), new LootBand("Rare", 1, Has(Rare))]];

        var (odds, _) = LootRoll.Resolve(table, [], layers);

        Assert.Equal(1.0 / 101, odds.Single(o => o.ItemClass == "r1").PerRoll, 6);
        Assert.Equal(100.0 / 101 / 3, odds.Single(o => o.ItemClass == "c1").PerRoll, 6);
    }

    [Fact]
    public void A_band_with_nothing_in_it_does_not_waste_the_roll()
    {
        // The Epic band at 0.1 is empty for most pools; it must not take a
        // thousandth of every roll to nowhere.
        var table = new[] { new LootEntry("Weapons", 1, [new LootChoice("Pistols", 1, [Item("c1", Common)])]) };
        IReadOnlyList<IReadOnlyList<LootBand>> layers =
            [[new LootBand("Common", 100, Has(Common)), new LootBand("Rare", 1, Has(Rare))]];

        var (odds, _) = LootRoll.Resolve(table, [], layers);

        Assert.Equal(1, odds.Single().PerRoll, 6);
    }

    [Fact]
    public void A_layer_that_matches_nothing_in_the_pool_leaves_it_whole()
    {
        // The faction layer on a pool with no tags of either kind, such as a
        // class named outright with none.
        var table = new[] { new LootEntry("Scrip", 1, [new LootChoice("Scrip", 1, [Item("scrip")])]) };
        IReadOnlyList<IReadOnlyList<LootBand>> layers = [[new LootBand("Rare", 1, Has(Rare))]];

        var (odds, _) = LootRoll.Resolve(table, [], layers);

        Assert.Equal(1, odds.Single().PerRoll, 6);
    }
}
