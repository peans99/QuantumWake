using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What an item is worth, as opposed to what it costs at the one terminal
/// selling it cheapest. The two answer different questions and this install
/// has an item where they differ tenfold.
/// </summary>
public class UexTypicalPriceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-typical-{Guid.NewGuid():N}");

    public UexTypicalPriceTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// The cache only loads as a set: prices.json is the gate, and the ids and
    /// terminals beside it are read unguarded, so a directory holding item
    /// prices alone loads nothing at all.
    /// </summary>
    private void SeedTheCacheItLoadsAsASet()
    {
        File.WriteAllText(Path.Combine(_directory, "prices.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "commodity-ids.json"), "{}");
        File.WriteAllText(Path.Combine(_directory, "terminals.json"), "[]");
    }

    private UexData Seeded(string uuid, decimal cheapest, params decimal[] terminals)
    {
        SeedTheCacheItLoadsAsASet();

        File.WriteAllText(
            Path.Combine(_directory, "item-prices.json"),
            $$"""{"{{uuid}}":{{cheapest}}}""");

        var rows = string.Join(",", terminals.Select((b, i) =>
            $$"""{"Terminal":"T{{i}}","Buy":{{b}}}"""));

        File.WriteAllText(
            Path.Combine(_directory, "item-market.json"),
            $$"""{"{{uuid}}":[{{rows}}]}""");

        return new UexData(_directory);
    }

    /// <summary>
    /// The MaxLift Tractor Beam as this install sees it: stocked near 19,175
    /// almost everywhere and at 1,975 in one place. The cheapest understates it
    /// tenfold, and a mean would still be pulled down by the odd row.
    /// </summary>
    [Fact]
    public void One_odd_terminal_does_not_move_the_typical_price()
    {
        var uex = Seeded("beam", 1975, 1975, 19175, 19175, 19175, 19175);

        Assert.Equal(1975, uex.ItemPrice("beam"));
        Assert.Equal(19175, uex.TypicalItemPrice("beam"));
    }

    [Fact]
    public void An_even_number_of_terminals_takes_the_middle_pair()
    {
        var uex = Seeded("part", 100, 100, 200, 300, 400);

        Assert.Equal(250, uex.TypicalItemPrice("part"));
    }

    /// <summary>
    /// A price with no per-terminal rows behind it is still a price. Falling
    /// through to null would drop items over a gap in the market table alone.
    /// </summary>
    [Fact]
    public void A_price_with_no_terminal_rows_falls_back_to_the_cheapest()
    {
        SeedTheCacheItLoadsAsASet();
        File.WriteAllText(Path.Combine(_directory, "item-prices.json"), """{"lonely":4200}""");

        Assert.Equal(4200, new UexData(_directory).TypicalItemPrice("lonely"));
    }

    [Fact]
    public void An_item_nothing_stocks_has_no_typical_price()
    {
        Assert.Null(Seeded("beam", 1975, 1975).TypicalItemPrice("unstocked"));
        Assert.Null(Seeded("beam", 1975, 1975).TypicalItemPrice(null));
    }
}
