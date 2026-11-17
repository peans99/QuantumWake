using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Which counter a shopping-list search names beside each thing (1.2.60), and
/// the order the things come in.
/// </summary>
public sealed class NearestSellersTests
{
    private static readonly Dictionary<string, SellerPlace> Places = new()
    {
        ["Shop New Babbage"] = new("Stanton4_NewBabbage", "New Babbage", "microTech", "Stanton"),
        ["Admin Port Tressler"] = new("Stanton4_PortTressler", "Port Tressler", "microTech", "Stanton"),
        ["Shop Area18"] = new("Stanton3_Area18", "Area18", "ArcCorp", "Stanton"),
        ["Shop Lorville"] = new("Stanton1_Lorville", "Lorville", "Hurston", "Stanton"),
        ["Guns Checkmate"] = new("Pyro_Checkmate", "Checkmate", "Pyro II", "Pyro"),
    };

    private static readonly ResolvedPlace NewBabbage = new("Stanton4_NewBabbage", "New Babbage", "microTech", "Stanton");

    // microTech to ArcCorp 20 Gm, to Hurston 40; another system has none.
    private static double? Gm(StopWhere a, StopWhere b) =>
        a.System != b.System ? null
        : a.Body == b.Body ? 0
        : (a.Body, b.Body) switch { ("microTech", "ArcCorp") => 20, ("microTech", "Hurston") => 40, _ => null };

    private static SellerPlace? Resolve(string terminal) => Places.GetValueOrDefault(terminal);

    private static BuyableThing Thing(string name, params (string Terminal, decimal Price)[] counters) =>
        new(name, "item", [.. counters.Select(c => new SellerCounter(c.Terminal, c.Price))]);

    private static readonly BuyableThing[] Things =
    [
        Thing("Rifle", ("Shop Lorville", 900), ("Shop Area18", 1000), ("Guns Checkmate", 500)),
        Thing("MedPen", ("Shop New Babbage", 300), ("Shop Lorville", 200)),
        Thing("Helmet", ("Admin Port Tressler", 700)),
        Thing("Hat", ("Somewhere Unknown", 50)),
        Thing("Nothing Sells This"),
    ];

    [Fact]
    public void Nearest_names_the_closest_counter_and_puts_here_then_the_same_moon_first()
    {
        var rows = NearestSellers.Rank(Things, NewBabbage, Resolve, Gm);

        Assert.Equal(["MedPen", "Helmet", "Rifle", "Hat", "Nothing Sells This"], rows.Select(r => r.Name));
        Assert.True(rows[0].Here);
        Assert.Equal((0d, "Port Tressler"), (rows[1].Gm!.Value, rows[1].Place!));
        Assert.Equal(("Area18", 20d, 3), (rows[2].Place!, rows[2].Gm!.Value, rows[2].Counters));
        Assert.Null(rows[3].Gm);
        Assert.Equal(0, rows[4].Counters);
    }

    [Fact]
    public void Cheapest_names_the_cheapest_counter_wherever_it_is()
    {
        var rows = NearestSellers.Rank(Things, NewBabbage, Resolve, Gm, SellerOrder.Cheapest);

        var rifle = rows.Single(r => r.Name == "Rifle");
        Assert.Equal(("Checkmate", 500m), (rifle.Place!, rifle.Price!.Value));
        Assert.Null(rifle.Gm);
        Assert.Equal("Lorville", rows.Single(r => r.Name == "MedPen").Place);
    }

    [Fact]
    public void Fewest_stops_names_the_place_the_rest_of_the_list_already_goes_to()
    {
        // Lorville sells the MedPen already on the list, so the rifle is named
        // there rather than at nearer Area18, and comes first.
        var rows = NearestSellers.Rank(Things, NewBabbage, Resolve, Gm, SellerOrder.FewestStops, ["MedPen"]);

        Assert.Equal("Rifle", rows[0].Name);
        Assert.Equal(("Lorville", 1), (rows[0].Place!, rows[0].AlsoSells));
    }

    [Fact]
    public void With_nowhere_to_measure_from_nothing_is_here_and_nothing_has_a_distance()
    {
        var rows = NearestSellers.Rank(Things, null, Resolve, Gm);

        Assert.DoesNotContain(rows, r => r.Here || r.Gm is not null);
        Assert.Equal("Nothing Sells This", rows[^1].Name);
    }

    [Fact]
    public void Two_desks_in_one_station_are_one_place()
    {
        var places = new Dictionary<string, SellerPlace>(Places) { ["Admin New Babbage"] = Places["Shop New Babbage"] };
        var rows = NearestSellers.Rank([Thing("Cap", ("Shop New Babbage", 10), ("Admin New Babbage", 12))],
            NewBabbage, t => places.GetValueOrDefault(t), Gm);

        Assert.Equal(1, Assert.Single(rows).Counters);
    }
}
