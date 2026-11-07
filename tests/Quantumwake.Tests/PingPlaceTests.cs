using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// 1.1.15: a ping read against the rocks that spawn where the pilot is, the
/// rocks they are after said as wanted, and the sweep kept as a list.
/// </summary>
public sealed class PingPlaceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-pings-" + Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset T0 = new(2026, 11, 6, 22, 0, 0, TimeSpan.Zero);

    public PingPlaceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    // The rows a 4.10 install gives, cut down to the ones that collide.
    private static readonly List<RadarRow> Rows =
    [
        new(3385, "ship", "Riccite", "Epic", ["Riccite"], true, true),
        new(3170, "ship", "Quantainium", "Legendary", ["Quantainium"], true, true),
        new(4270, "ship", "Iron", "Common", ["Iron"], true, true),
        new(3000, "hand", "Hand-mining deposit", null, ["Aphorite", "Hadanite"], true, false),
        new(4000, "ground", "Ground-vehicle deposit", null, ["Beradom"], true, false),
        new(4000, "asteroid", "I-type asteroid", null, ["Ice"], false, true),
        new(4000, "deposit", "Felsic", null, ["Iron"], true, false),
    ];

    private static GameSpawn Spawn(string place, string group, string resource, string? system = null) =>
        new(resource, null, null, null, "mineable", place, system, group, 0.1, 0.5, null, 3600);

    private static readonly IReadOnlyList<PingPlace> Places = PingPlaces.From(
    [
        Spawn("Lagrange A", "SpaceShip Mineables", "Riccite Ore"),
        Spawn("Lagrange A", "SpaceShip Mineables", "Iron Ore"),
        Spawn("Daymar", "SpaceShip Mineables", "Quantainium", "Stanton"),
        Spawn("Daymar", "SpaceShip Mineables", "Iron Ore", "Stanton"),
        Spawn("Daymar", "FPS Mineables", "Hadanite", "Stanton"),
        Spawn("Daymar", "GroundVehicle Mineables", "GroundVehicle Beradom", "Stanton"),
        new("Wreckage", null, null, null, "salvageable", "Daymar", "Stanton", "Salvage", 0.1, 1, null, null),
    ]);

    [Theory]
    [InlineData("Riccite Ore", "riccite")]
    [InlineData("Aluminium Ore", "aluminum")]
    [InlineData("Aluminum", "aluminum")]
    [InlineData("Sileron Ore", "stileron")]
    [InlineData("Stileron", "stileron")]
    [InlineData("Raw Ice", "ice")]
    [InlineData("Hephaestanite (R)", "hephaestanite")]
    [InlineData("Quantainium (Raw)", "quantainium")]
    public void The_tables_and_the_radar_spell_an_ore_alike(string text, string ore) => Assert.Equal(ore, PingPlaces.Ore(text));

    [Fact]
    public void A_place_is_a_field_or_a_surface_with_its_own_rocks()
    {
        var field = Assert.Single(Places, p => p.Name == "Lagrange A");
        Assert.True(field.Asteroids);
        Assert.Equal(["iron", "riccite"], field.Ores.Order());
        Assert.False(field.Hand);

        var moon = Assert.Single(Places, p => p.Name == "Daymar");
        Assert.False(moon.Asteroids);
        Assert.Equal("Stanton", moon.System);
        Assert.True(moon.Hand);
        Assert.True(moon.Ground);
    }

    [Fact]
    public void Twelve_thousand_in_a_field_is_three_I_type_asteroids()
    {
        var field = Places.Single(p => p.Name == "Lagrange A");
        var anywhere = PingReadout.Decode(12000, Rows);
        var here = PingReadout.Decode(12000, PingPlaces.Rows(Rows, field));

        Assert.True(anywhere.Count > 1);
        var only = Assert.Single(here);
        Assert.Equal("I-type asteroid", only.Name);
        Assert.Equal(3, only.Count);
    }

    [Fact]
    public void On_a_moon_the_asteroid_classes_and_absent_ores_drop_out()
    {
        var moon = Places.Single(p => p.Name == "Daymar");
        var rows = PingPlaces.Rows(Rows, moon);

        Assert.DoesNotContain(rows, r => r.Kind == "asteroid");
        Assert.DoesNotContain(rows, r => r.Name == "Riccite");
        Assert.Contains(rows, r => r.Name == "Quantainium");
        Assert.Contains(rows, r => r.Kind == "ground");
        Assert.Contains(rows, r => r.Kind == "hand");
    }

    [Fact]
    public void A_place_that_would_leave_nothing_reads_against_everything()
    {
        // Only Riccite in the rows, and Daymar spawns none: the table is not
        // taken over the ping.
        var moon = Places.Single(p => p.Name == "Daymar");
        var riccite = Rows.Where(r => r.Name == "Riccite").ToList();
        Assert.Same(riccite, PingPlaces.Rows(riccite, moon));
        Assert.Same(Rows, PingPlaces.Rows(Rows, null));
    }

    [Fact]
    public void The_log_place_is_found_by_body_then_place_then_jump()
    {
        Assert.Equal("Daymar", PingPlaces.Find(Places, "daymar", "Shubin SCD-1")?.Name);
        Assert.Equal("Lagrange A", PingPlaces.Find(Places, null, "", "Lagrange A")?.Name);
        Assert.Null(PingPlaces.Find(Places, "Port Olisar", null));
    }

    // ---- wanted, and the sweep ----

    private static IReadOnlyList<CombatCallout> Twice(MiningWatch watch, double ping, IReadOnlyList<RadarRow> rows, DateTimeOffset at, PingOptions? options = null)
    {
        watch.SeePing(ping, rows, at, options);
        return watch.SeePing(ping, rows, at.AddSeconds(2), options);
    }

    [Fact]
    public void A_wanted_rock_is_said_first_with_wanted_after_it()
    {
        // 6340 = two Quantainium (3170) - and, against these rows, nothing else.
        var watch = new MiningWatch();
        var wanted = new HashSet<string> { "Quantainium" };
        var daymar = Places.Single(p => p.Name == "Daymar");
        var said = Assert.Single(Twice(watch, 6340, Rows, T0, new PingOptions(daymar, false, wanted)));
        Assert.Equal("Two Quantainium, wanted", said.Text);

        var ping = watch.Picture().Ping!;
        Assert.True(ping.Wanted);
        Assert.Equal("Daymar", ping.Place);
        Assert.True(ping.Matches[0].Wanted);
    }

    [Fact]
    public void A_ping_the_place_cannot_read_is_read_against_every_rock()
    {
        // Two Riccite at Daymar, which spawns none: the place is what is wrong.
        var watch = new MiningWatch();
        var daymar = Places.Single(p => p.Name == "Daymar");
        var said = Assert.Single(Twice(watch, 6770, Rows, T0, new PingOptions(daymar)));
        Assert.Equal("Two Riccite", said.Text);
        Assert.Null(watch.Picture().Ping!.Place);

        // And a field's narrowing is kept when it does read the ping.
        var field = Places.Single(p => p.Name == "Lagrange A");
        Twice(watch, 12000, Rows, T0.AddMinutes(1), new PingOptions(field));
        var ping = watch.Picture().Ping!;
        Assert.Equal("Lagrange A", ping.Place);
        Assert.Equal("I-type asteroid", Assert.Single(ping.Matches).Name);
    }

    [Fact]
    public void Wanted_goes_after_the_rock_it_is_about_when_two_readings_share_a_figure()
    {
        var rows = new List<RadarRow>
        {
            new(4000, "ground", "Ground-vehicle deposit", null, ["Beradom"], true, false),
            new(3000, "hand", "Hand-mining deposit", null, ["Hadanite"], true, false),
        };
        var said = Assert.Single(Twice(new MiningWatch(), 12000, rows, T0,
            new PingOptions(Wanted: new HashSet<string> { "Ground-vehicle deposit" })));
        Assert.Equal("Three Ground-vehicle deposit, wanted or Four Hand-mining deposit", said.Text);
    }

    [Fact]
    public void Wanted_only_keeps_quiet_about_the_rest_and_still_lists_them()
    {
        var watch = new MiningWatch();
        var options = new PingOptions(Wanted: new HashSet<string> { "Quantainium" }, WantedOnly: true);
        Assert.Empty(Twice(watch, 8540, Rows, T0, options));                     // two Iron
        Assert.Single(Twice(watch, 6340, Rows, T0.AddSeconds(10), options));     // two Quantainium

        var pings = watch.Picture().Pings!;
        Assert.Equal([6340.0, 8540.0], pings.Select(p => p.Value));
    }

    [Fact]
    public void Holding_one_contact_is_one_entry_and_a_sweep_is_a_list()
    {
        var watch = new MiningWatch();
        Twice(watch, 6770, Rows, T0);                       // two Riccite
        Twice(watch, 6770, Rows, T0.AddSeconds(10));        // still on it
        Twice(watch, 6770, Rows, T0.AddSeconds(25));
        Twice(watch, 8540, Rows, T0.AddSeconds(40));
        Twice(watch, 6770, Rows, T0.AddSeconds(120));       // another rock of the same size

        Assert.Equal([6770.0, 8540.0, 6770.0], watch.Picture().Pings!.Select(p => p.Value));

        for (var n = 0; n < 20; n++) Twice(watch, 3170 * (1 + n % 3), Rows, T0.AddMinutes(5 + n));
        Assert.Equal(MiningWatch.PingsKept, watch.Picture().Pings!.Count);
    }

    // ---- the settings ----

    [Fact]
    public void How_pings_are_read_is_kept_and_auto_clears_the_place()
    {
        var store = new LiveScreenStore(_root);
        store.Answer(true);
        store.SetPing(place: "Lagrange A", wanted: ["Quantainium", "Riccite"], wantedOnly: true);

        var again = new LiveScreenStore(_root).Current;
        Assert.Equal("Lagrange A", again.PingPlace);
        Assert.Equal(["Quantainium", "Riccite"], again.Wanted);
        Assert.True(again.WantedOnly);

        store.SetPing(place: "auto", wanted: []);
        Assert.Null(store.Current.PingPlace);
        Assert.Null(store.Current.Wanted);
        Assert.True(store.Current.WantedOnly);

        store.SetPing(place: LiveScreenSettings.Anywhere);
        Assert.Equal("*", store.Current.PingPlace);
    }
}
