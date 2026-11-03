using System.Text.RegularExpressions;
using Quantumwake.Core.Locations;

namespace Quantumwake.Core.GameData;

/// <summary>One planet or moon's baseline atmosphere, as the install states it.</summary>
/// <param name="Id">The body's designator, <c>Stanton1b</c>.</param>
/// <param name="Name">"Aberdeen": the star map's own name, or the app's table where the record has none.</param>
/// <param name="System">"Stanton" or "Pyro".</param>
/// <param name="Parent">The planet a moon circles, by name; null for a planet.</param>
/// <param name="Kelvin">The baseline temperature, in kelvin as the file stores it.</param>
/// <param name="Pressure">The game's pressure figure; a room at its default reads 1.</param>
/// <param name="Humidity">0 to 1.</param>
/// <param name="NightCooling">
/// The day-night profile's base cooling rate, the game's own figure and unit;
/// null when the body has no profile. Not a night temperature, which the files
/// do not give.
/// </param>
public sealed record GameClimate(
    string Id, string Name, string System, string? Parent,
    double Kelvin, double Pressure, double Humidity, double? NightCooling)
{
    /// <summary>The baseline in °C, which is how a suit's range is written.</summary>
    public double Celsius => Math.Round(Kelvin - 273.15, 1);
}

/// <summary>Every body's baseline, and what a body copes with unprotected.</summary>
/// <param name="BodyMin">The coldest an unprotected body is comfortable in, °C.</param>
/// <param name="BodyMax">The hottest.</param>
/// <param name="VisorFrost">The suit temperature at which a visor is fully frosted, °C.</param>
public sealed record GameClimateData(IReadOnlyList<GameClimate> Bodies, double BodyMin, double BodyMax, double? VisorFrost)
{
    public static GameClimateData Empty { get; } = new([], 4, 35, null);

    /// <summary>Whether a range in °C takes in a body's baseline.</summary>
    public static bool Covers(double min, double max, GameClimate body) => min <= body.Celsius && body.Celsius <= max;
}

/// <summary>
/// Reads each planet and moon's baseline temperature, pressure and humidity.
/// </summary>
/// <remarks>
/// <para>
/// Each body has an <c>AtmosphereStateTemplate</c> named for it -
/// <c>Stanton1b</c> - whose <c>state</c> overrides temperature (kelvin),
/// pressure and humidity. Measured on 4.10.2: 67 templates, 28 of them a
/// planet or moon - Stanton's 16 and Pyro's 12; the rest are gas clouds,
/// asteroid fields, two prison rooms and vacuum. Aberdeen 523 K (250 °C), Arial 225 °C, Pyro I 226 °C at the hot
/// end; Euterpe -73 °C, Terminus -133 °C at the cold.
/// </para>
/// <para>
/// It is a baseline, and everything built on it says so. A body's
/// <c>PlanetDayNightTemperatureTemplate</c> holds only cooling rates - the
/// Earth-like profile -2.5, the moon-like -16 - and not a night temperature,
/// and biome, altitude and caves are not here at all. Daymar's baseline reads
/// 160 °C, which a pilot standing at Shubin's pads will not recognise as noon.
/// </para>
/// <para>
/// What a body copes with unprotected is <c>ActorEnvironmentComponent</c>'s
/// <c>bodyDefaultMin/MaxTemperatureThreshold</c>: 4 to 35 °C. A suit's range
/// is its armour's <c>TemperatureResistance</c>; see <see cref="ArmourPiece"/>.
/// </para>
/// </remarks>
public static partial class GameClimates
{
    [GeneratedRegex(@"^(?<system>Stanton|Pyro)(?<body>\d)(?<moon>[a-z])?$", RegexOptions.IgnoreCase)]
    private static partial Regex BodyId();

    public static GameClimateData Read(DataCore core, IReadOnlyDictionary<string, string> text)
    {
        var atmosphere = core.StructIndexOf("AtmosphereStateTemplate");
        if (atmosphere < 0) return GameClimateData.Empty;

        var dayNight = core.StructIndexOf("PlanetDayNightTemperatureTemplate");
        var environment = core.StructIndexOf("ActorEnvironmentComponent");

        var starMap = new Dictionary<string, DataRecord>(StringComparer.OrdinalIgnoreCase);
        var cooling = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var templates = new List<DataRecord>();
        DataRecord? actor = null;

        foreach (var record in core.Records())
        {
            if (record.StructIndex == atmosphere) templates.Add(record);
            else if (record.StructIndex == environment) actor ??= record;
            else if (record.Name.StartsWith("StarMapObject.", StringComparison.Ordinal)) starMap.TryAdd(Bare(record.Name), record);
            else if (record.StructIndex == dayNight)
            {
                var at = core.InstanceAt(record, record.VariantIndex);
                var (paramsAt, parameters) = core.FieldAt(at, record.StructIndex, "dayNightTemperatureParams");
                if (parameters is not null && core.SingleAt(paramsAt, parameters.StructIndex, "DayNightCoolingBaseRate") is { } rate)
                    cooling[Bare(record.Name)] = Math.Round(rate, 3);
            }
        }

        var bodies = new List<GameClimate>();

        foreach (var template in templates)
        {
            var id = Bare(template.Name);
            var match = BodyId().Match(id);
            if (!match.Success) continue;

            var at = core.InstanceAt(template, template.VariantIndex);
            var (stateAt, state) = core.FieldAt(at, template.StructIndex, "state");
            if (state is null || core.SingleAt(stateAt, state.StructIndex, "temperature") is not { } kelvin) continue;

            var system = match.Groups["system"].Value.Equals("Pyro", StringComparison.OrdinalIgnoreCase) ? "Pyro" : "Stanton";
            var planet = match.Groups["body"].Value;
            var moon = match.Groups["moon"].Success ? match.Groups["moon"].Value.ToLowerInvariant() : null;

            bodies.Add(new GameClimate(
                id,
                Name(core, text, starMap, id, system, planet + moon),
                system,
                moon is null ? null : Name(core, text, starMap, system + planet, system, planet),
                Math.Round(kelvin, 2),
                Math.Round(core.SingleAt(stateAt, state.StructIndex, "pressure") ?? 0, 3),
                Math.Round(core.SingleAt(stateAt, state.StructIndex, "humidity") ?? 0, 3),
                cooling.TryGetValue(id, out var rate) ? rate : null));
        }

        double min = 4, max = 35;
        double? frost = null;

        if (actor is not null)
        {
            var at = core.InstanceAt(actor, actor.VariantIndex);
            var (tAt, t) = core.FieldAt(at, actor.StructIndex, "temperatureParams");
            if (t is not null)
            {
                min = core.SingleAt(tAt, t.StructIndex, "bodyDefaultMinTemperatureThreshold") ?? min;
                max = core.SingleAt(tAt, t.StructIndex, "bodyDefaultMaxTemperatureThreshold") ?? max;
            }

            var (vAt, v) = core.FieldAt(at, actor.StructIndex, "frostedVisorParams");
            if (v is not null) frost = core.SingleAt(vAt, v.StructIndex, "suitTemperatureMaxFrost");
        }

        return new GameClimateData(
            [.. bodies.OrderBy(b => b.System, StringComparer.Ordinal).ThenBy(b => b.Id, StringComparer.OrdinalIgnoreCase)],
            min, max, frost);
    }

    /// <summary>
    /// The star map's name for a body, which is the game's own word - Pyro V's
    /// moons are Ignis, Vatra, Adir, Fairo, Fuego and Vuur there and nowhere in
    /// this app's table - else the table, else the designator.
    /// </summary>
    private static string Name(
        DataCore core, IReadOnlyDictionary<string, string> text, IReadOnlyDictionary<string, DataRecord> starMap,
        string id, string system, string designator)
    {
        if (starMap.TryGetValue(id, out var record))
        {
            var key = core.StringAt(core.InstanceAt(record, record.VariantIndex), record.StructIndex, "name");
            if (key is { Length: > 1 } && text.TryGetValue(key.TrimStart('@'), out var english)
                && english.Length > 0 && !english.StartsWith("<=", StringComparison.Ordinal))
                return english.Trim();
        }

        var table = system == "Pyro" ? Universe.PyroBodies : Universe.StantonBodies;
        return table.TryGetValue(designator, out var named) ? named : id;
    }

    private static string Bare(string name) => name.Contains('.') ? name[(name.IndexOf('.') + 1)..] : name;
}
