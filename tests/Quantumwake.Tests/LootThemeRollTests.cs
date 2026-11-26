using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Pool filters as ordered steps, and theme layers that switch later ones off,
/// on shapes taken from 4.10.201 LIVE and the 5.0 PTU (5.0.202.46035).
/// </summary>
/// <remarks>
/// The DataCore says what each theme holds and the order categories apply in
/// (<c>themeCategoryOrdering</c>). It does not say how the draw runs, so these
/// pin the reading <c>docs/loot.md</c> states, as <see cref="LootRollTests"/> does.
/// </remarks>
public class LootThemeRollTests
{
    private static readonly Guid Event = Guid.NewGuid(), DCDelving = Guid.NewGuid(),
        Faction = Guid.NewGuid(), Salamanders = Guid.NewGuid(), Moraine = Guid.NewGuid(),
        HighTech = Guid.NewGuid(), FactionCategory = Guid.NewGuid(), FlavourCategory = Guid.NewGuid();

    private static IReadOnlySet<Guid> Tags(params Guid[] tags) => tags.ToHashSet();
    private static LootCandidate Item(string cls, params Guid[] tags) => new(cls, Tags(tags));
    private static LootTerm Has(Guid tag) => new(Tags(tag), Tags());
    private static LootTerm Lacks(Guid tag) => new(Tags(), Tags(tag));
    private static readonly IReadOnlyList<IReadOnlyList<LootBand>> NoLayers = [];

    private static LootEntry[] OneChoice(params LootCandidate[] pool) =>
        [new LootEntry("Event gear", 1, [new LootChoice("Event gear", 1, pool)])];

    [Fact]
    public void An_additive_step_puts_back_what_an_earlier_step_struck()
    {
        // V3PoolFilter_DCDelving on LIVE: no faction, no special events, then
        // "Event" Additive on #DCDelving. Read as a positive Cumulative filter
        // it kept only DCDelving gear from a pool that had just lost every
        // event item, and 79 event rolls on LIVE came out empty.
        var table = OneChoice(
            Item("dcd_helmet", Event, DCDelving),
            Item("luminalia_hat", Event),
            Item("plain_helmet"));

        var (odds, unfilled) = LootRoll.Filtered(table,
            [new LootFilterStep(Has(Event)), new LootFilterStep(Has(DCDelving), Restore: true)],
            NoLayers);

        Assert.Empty(unfilled);
        Assert.Equal(["dcd_helmet", "plain_helmet"], odds.Select(o => o.ItemClass).Order());
    }

    [Fact]
    public void A_restore_only_returns_what_was_struck_before_it()
    {
        // Steps are a sequence: a strike after the restore still strikes.
        var table = OneChoice(Item("dcd_helmet", Event, DCDelving), Item("plain_helmet"));

        var (odds, _) = LootRoll.Filtered(table,
            [new LootFilterStep(Has(DCDelving), Restore: true), new LootFilterStep(Has(Event))],
            NoLayers);

        Assert.Equal("plain_helmet", Assert.Single(odds).ItemClass);
    }

    [Fact]
    public void A_faction_theme_keeps_its_own_gear_and_no_other_factions()
    {
        // Theme_Faction_Salamanders: "No Faction" strikes #Faction, then
        // "Specified faction" Additive puts back #Salamanders.
        var table = OneChoice(
            Item("salamander_jacket", Faction, Salamanders),
            Item("moraine_jacket", Faction, Moraine),
            Item("plain_jacket"));

        var (odds, _) = LootRoll.Filtered(table,
            [new LootFilterStep(Has(Faction)), new LootFilterStep(Has(Salamanders), Restore: true)],
            NoLayers);

        Assert.DoesNotContain(odds, o => o.ItemClass == "moraine_jacket");
        Assert.Equal(0.5, odds.Single(o => o.ItemClass == "salamander_jacket").PerRoll, 6);
    }

    [Fact]
    public void Drawing_the_faction_band_switches_off_the_flavour_layers_after_it()
    {
        // Theme_Faction_Salamanders draws "Faction" 0.25 against "Not
        // specified faction" 1, and the faction band carries
        // LootSubThemeModifier_Ignore over #Flavour. A Flavour_Tech layer
        // follows it in themeCategoryOrdering.
        var table = OneChoice(
            Item("sal_hightech", Salamanders, HighTech),
            Item("sal_lowtech", Salamanders),
            Item("gen_hightech", HighTech),
            Item("gen_lowtech"));

        IReadOnlyList<IReadOnlyList<LootBand>> layers =
        [
            [
                new LootBand("Faction", 0.25, Has(Salamanders), FactionCategory, Tags(FlavourCategory)),
                new LootBand("Not specified faction", 1, Lacks(Salamanders), FactionCategory),
            ],
            [
                new LootBand("HighTech", 3, Has(HighTech), FlavourCategory),
                new LootBand("Other", 1, Lacks(HighTech), FlavourCategory),
            ],
        ];

        var (odds, _) = LootRoll.Filtered(table, [], layers);
        double P(string item) => odds.Single(o => o.ItemClass == item).PerRoll;

        // Faction band, 0.2: the flavour layer is off, so the pick is uniform.
        Assert.Equal(0.1, P("sal_hightech"), 6);
        Assert.Equal(0.1, P("sal_lowtech"), 6);
        // The other band, 0.8: the flavour layer still applies, 3 to 1.
        Assert.Equal(0.6, P("gen_hightech"), 6);
        Assert.Equal(0.2, P("gen_lowtech"), 6);
        Assert.Equal(1, odds.Sum(o => o.PerRoll), 6);
    }

    [Fact]
    public void Exclusions_alone_read_exactly_as_before()
    {
        // The 4.10 path: every exclusion a strike, order irrelevant. 254 of
        // LIVE's 333 rolls are byte-identical through Filtered.
        var table = OneChoice(Item("a", Event), Item("b", Faction), Item("c"));
        IReadOnlyList<LootTerm> excluded = [Has(Event), Has(Faction)];

        var before = LootRoll.Resolve(table, excluded, NoLayers);
        var after = LootRoll.Filtered(table, [.. excluded.Reverse().Select(t => new LootFilterStep(t))], NoLayers);

        Assert.Equal(before.Odds, after.Odds);
        Assert.Equal("c", Assert.Single(after.Odds).ItemClass);
    }
}
