using System.Text.RegularExpressions;
using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>One place's rocks, as its deposit table lists them.</summary>
/// <param name="Name">The table's place - "Daymar", "Pyro IV", "Lagrange A", "Stanton2c Belt".</param>
/// <param name="System">Its system when the designation says so; null for belts and Lagrange points.</param>
/// <param name="Asteroids">An asteroid field rather than a planet or moon's surface.</param>
/// <param name="Ores">The ship rocks that spawn there, by ore, as <see cref="PingPlaces.Ore"/> spells them.</param>
/// <param name="Hand">Whether hand-mining deposits spawn there.</param>
/// <param name="Ground">Whether ground-vehicle deposits do.</param>
public sealed record PingPlace(string Name, string? System, bool Asteroids, IReadOnlySet<string> Ores, bool Hand, bool Ground);

/// <summary>Which place a ping was read against, and how it was chosen.</summary>
/// <param name="Name">The place, or null when the ping was read against every rock.</param>
/// <param name="Chosen">Picked by the pilot rather than taken from where the log puts them.</param>
public sealed record PingWhere(string? Name, bool Chosen);

/// <summary>
/// Narrows what a ping can be to the rocks that spawn where the pilot is.
/// </summary>
/// <remarks>
/// <para>
/// A radar figure is a count times one rock's signature, and several rocks
/// share a signature - every I-type asteroid, ground-vehicle deposit and older
/// deposit rock reads 4,000 - so 12,000 alone is three of any of them. Where
/// the pilot is settles most of that: an asteroid field has no ground-vehicle
/// deposits and a moon has no I-type asteroids, and a place's deposit table
/// names the ship rocks it spawns. Those tables are the install's own
/// <c>HarvestableProviderPreset</c>s (<see cref="GameSpawns"/>): 45 places
/// with mineables on 4.10, each with its SpaceShip, GroundVehicle and FPS
/// groups.
/// </para>
/// <para>
/// The table names a ship rock by the deposit it is - "Riccite Ore",
/// "Aluminium Ore", "Sileron Ore" - and the radar row by the class's ore -
/// Riccite, Aluminum, Stileron - so both are brought to one spelling by
/// <see cref="Ore"/>. A filter that leaves nothing is not applied: a ping the
/// table says cannot be here is still a ping, and a reading of everything is
/// more use than silence.
/// </para>
/// </remarks>
public static partial class PingPlaces
{
    /// <summary>Every place with mineables, by name.</summary>
    public static IReadOnlyList<PingPlace> From(IEnumerable<GameSpawn> spawns)
    {
        var places = new List<PingPlace>();
        foreach (var at in spawns.Where(s => s.Kind == "mineable").GroupBy(s => s.Location, StringComparer.OrdinalIgnoreCase))
        {
            var ores = new HashSet<string>(StringComparer.Ordinal);
            bool hand = false, ground = false;
            foreach (var spawn in at)
            {
                if (spawn.Group.StartsWith("SpaceShip", StringComparison.OrdinalIgnoreCase)) ores.Add(Ore(spawn.Resource));
                else if (spawn.Group.StartsWith("FPS", StringComparison.OrdinalIgnoreCase)) hand = true;
                else if (spawn.Group.StartsWith("GroundVehicle", StringComparison.OrdinalIgnoreCase)) ground = true;
            }
            places.Add(new PingPlace(at.Key, at.First().System, AsteroidField().IsMatch(at.Key), ores, hand, ground));
        }
        return [.. places.OrderBy(p => p.System ?? "~").ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>"Riccite Ore", "Raw Ice", "Aluminium Ore" and Riccite, Ice, Aluminum alike, as one spelling.</summary>
    public static string Ore(string text)
    {
        var words = OreWords().Replace(text.ToLowerInvariant(), " ").Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w is not ("ore" or "raw" or "r" or "groundvehicle"));
        var ore = string.Join(' ', words);
        return ore switch
        {
            "aluminium" => "aluminum",
            "sileron" => "stileron",
            "quantanium" => "quantainium",
            _ => ore,
        };
    }

    /// <summary>Whether a radar row's rock can spawn at a place.</summary>
    public static bool Fits(RadarRow row, PingPlace place) => row.Kind switch
    {
        "ship" => (place.Asteroids ? row.Asteroid : row.Surface) && place.Ores.Contains(Ore(row.Name)),
        "asteroid" => place.Asteroids,
        "hand" => !place.Asteroids && place.Hand,
        "ground" => !place.Asteroids && place.Ground,
        // The older deposit rocks are placed by no table this reads, so a
        // surface keeps them rather than ruling them out on no evidence.
        _ => !place.Asteroids,
    };

    /// <summary>The rows a ping is read against at <paramref name="place"/>; every row when there is none, or when none would be left.</summary>
    public static IReadOnlyList<RadarRow> Rows(IReadOnlyList<RadarRow> rows, PingPlace? place)
    {
        if (place is null) return rows;
        var here = rows.Where(r => Fits(r, place)).ToList();
        return here.Count == 0 ? rows : here;
    }

    /// <summary>
    /// The place the log puts the pilot at, from the most specific name it
    /// has: the body, then the place, then where a quantum jump is headed.
    /// Exact names only - a station is not its moon's surface.
    /// </summary>
    public static PingPlace? Find(IReadOnlyList<PingPlace> places, params string?[] names)
    {
        foreach (var name in names)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            if (places.FirstOrDefault(p => string.Equals(p.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)) is { } found)
                return found;
        }
        return null;
    }

    // Belts, rings, halos, clusters, Lagrange points and Pyro's named fields
    // (Cool01, Warm02, DeepSpaceAsteroids): everything that is not a body.
    [GeneratedRegex(@"asteroid|lagrange|belt|ring|halo|cluster|cool\d|warm\d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex AsteroidField();

    [GeneratedRegex(@"[^a-z]+", RegexOptions.CultureInvariant)]
    private static partial Regex OreWords();
}
