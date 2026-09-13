namespace Quantumwake.WebTests;

/// <summary>
/// The ship catalogue's buy and rent cells: the cheapest is the number, and
/// every other shop and desk is on the hover, so the nearest can be chosen.
/// </summary>
public class ShipCatalogueTests
{
    private const string Catalogue = """
        [{"name":"Drake Cutlass Black","career":"Combat","role":"Medium Fighter","crew":2,"isSpaceship":true,
          "expeditedCost":5000,"standardClaimTime":10,"cargoScu":46,"scmSpeed":200,"maxSpeed":1100,"shieldHp":10000,"health":20000,
          "price":{"price":1400000,"terminal":"Astro Armada"},
          "shops":[{"price":1400000,"terminal":"Astro Armada"},{"price":1500000,"terminal":"New Deal"},{"price":1500000,"terminal":"Crusader Showroom"}],
          "rental":{"vehicle":"Cutlass Black","terminal":"Vantage Rental","price":29000},
          "rentals":[{"vehicle":"Cutlass Black","terminal":"Vantage Rental","price":29000},{"vehicle":"Cutlass Black","terminal":"Traveler Rentals","price":34000}]},
         {"name":"Aegis Idris","career":"Combat","role":"Frigate","crew":10,"isSpaceship":true,
          "expeditedCost":0,"standardClaimTime":0,"cargoScu":0,"scmSpeed":0,"maxSpeed":0,"shieldHp":0,"health":0,
          "price":null,"shops":[],"rental":null,"rentals":[]}]
        """;

    private static Page Loaded()
    {
        var page = new Page();
        page.Serve("/api/reference/ships", Catalogue);
        page.Do("await loadShipsRef();");
        return page;
    }

    private static string Cell(Page page, int row, int column, string what) =>
        page.Text($"__dom.node('#ships-table tbody').children[{row}].children[{column}].{what}");

    [Fact]
    public void Every_shop_is_on_the_hover_of_the_buy_cells_cheapest_first_and_the_count_is_shown()
    {
        var page = Loaded();

        Assert.Contains("Astro Armada", Cell(page, 0, 11, "textContent"));
        Assert.Contains("+2", Cell(page, 0, 11, "textContent"));

        var hover = Cell(page, 0, 11, "title");
        Assert.StartsWith("Sold at (3):", hover);
        Assert.Contains("Astro Armada — ", hover);
        Assert.Contains("New Deal — ", hover);
        Assert.Contains("Crusader Showroom — ", hover);
        Assert.True(hover.IndexOf("Astro Armada", StringComparison.Ordinal) < hover.IndexOf("New Deal", StringComparison.Ordinal));
        Assert.Equal(hover, Cell(page, 0, 10, "title"));
    }

    [Fact]
    public void Every_rental_desk_is_on_the_hover_of_the_rent_cell()
    {
        var page = Loaded();

        Assert.Contains("+1", Cell(page, 0, 12, "textContent"));
        var hover = Cell(page, 0, 12, "title");
        Assert.StartsWith("Rents at (2):", hover);
        Assert.Contains("Vantage Rental — ", hover);
        Assert.Contains("Traveler Rentals — ", hover);
    }

    [Fact]
    public void A_ship_nobody_sells_or_rents_has_no_hover_and_no_count()
    {
        var page = Loaded();

        Assert.Equal("not sold", Cell(page, 1, 10, "textContent"));
        Assert.Equal("—", Cell(page, 1, 11, "textContent"));
        Assert.Equal("—", Cell(page, 1, 12, "textContent"));
        Assert.True(page.Truth("!__dom.node('#ships-table tbody').children[1].children[11].title"));
    }
}
