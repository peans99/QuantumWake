using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Ranking places for a whole list, with odds of the size this install gives:
/// a rifle at a quarter of a percent a slot, its magazine at sixteen.
/// </summary>
public class LootBasketTests
{
    private static GameLootPlaceHit At(string? name, string body, double chance, int spots = 1) =>
        new(name, body,
            new GameLootHit(new LootSource("slot", "Preset", "", "Crate", 1, null, "Table", null, null, 1), chance, "via"),
            spots, 1, name ?? body);

    [Fact]
    public void A_place_with_more_of_the_list_beats_one_with_better_odds_on_less()
    {
        var ranked = LootBasket.Rank(
        [
            ("rifle", [At("Dupree", "Hurston", 0.0025), At("Outpost", "Daymar", 0.01)]),
            ("magazine", [At("Dupree", "Hurston", 0.16), At("Cave", "Cellin", 0.5)]),
        ]);

        Assert.Equal("Dupree", ranked[0].Name);
        Assert.Equal(2, ranked[0].Items.Count);
        // Then by the chances summed: the cave's one item at 50% before the outpost's at 1%.
        Assert.Equal(["Cave", "Outpost"], ranked.Skip(1).Select(p => p.Name));
    }

    [Fact]
    public void An_item_counts_once_per_place_at_its_best()
    {
        var ranked = LootBasket.Rank([("rifle", [At("Dupree", "Hurston", 0.001), At("Dupree", "Hurston", 0.003)])]);

        var only = Assert.Single(ranked);
        Assert.Equal(0.003, Assert.Single(only.Items).Chance, 6);
    }

    [Fact]
    public void A_named_place_comes_before_an_unmarked_spot_of_the_same_worth()
    {
        var ranked = LootBasket.Rank([("rifle", [At(null, "Cellin", 0.1), At("Dupree", "Hurston", 0.1)])]);

        Assert.Equal(["Dupree", null], ranked.Select(p => p.Name));
    }

    [Fact]
    public void Unmarked_spots_with_the_same_yield_are_one_row()
    {
        var ranked = LootBasket.Rank(
        [
            ("magazine", [At(null, "Daymar", 0.16), At(null, "Cellin", 0.16), At(null, "Clio", 0.16), At("Dupree", "Hurston", 0.16)]),
        ]);

        Assert.Equal(2, ranked.Count);
        Assert.Contains(ranked, p => p.Name is null && p.Body == "Cellin, Clio, Daymar");
    }

    [Fact]
    public void Nothing_wanted_ranks_nothing()
    {
        Assert.Empty(LootBasket.Rank([("rifle", [])]));
    }
}
