using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// A reading from the game's <c>/showlocation</c> command.
/// </summary>
/// <remarks>
/// <para>
/// Metres, in a frame centred on whichever system the pilot is in - which the
/// reading itself does not name, and which is the whole reason this joins onto
/// the app's own idea of where they were.
/// </para>
/// <para>
/// See <c>docs/precise-poi.md</c>. The reading it is built on measured 15.0000
/// gigametres from the system centre with a Z of -91.8 km against horizontal
/// distances of eleven billion, which is why the app's existing position data
/// is flat and why <see cref="FromCentre"/> ignores Z.
/// </para>
/// </remarks>
public sealed partial record ShipPosition(double X, double Y, double Z)
{
    /// <summary>Metres from the system centre, on the plane.</summary>
    /// <remarks>
    /// Flat on purpose. The system is a plane for every practical purpose -
    /// measured, not assumed - and including a Z that never exceeds a hundred
    /// kilometres would add noise to a figure in the billions.
    /// </remarks>
    public double FromCentre => Math.Sqrt((X * X) + (Y * Y));

    /// <summary>Gigametres from the system centre, which is the readable unit.</summary>
    public double GigametresFromCentre => FromCentre / 1_000_000_000d;

    /// <summary>
    /// Reads what the game put on the clipboard, or null if it is not that.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately forgiving about what surrounds the numbers and strict about
    /// the numbers themselves. The clipboard is whatever the pilot last copied,
    /// so this is asked of shopping lists and chat messages as often as it is
    /// of a location, and anything less than all three axes is not a reading.
    /// </para>
    /// <para>
    /// Invariant culture on purpose: the game writes a full stop whatever the
    /// machine's regional settings say, and parsing "-9641671346.904709" with a
    /// comma decimal separator silently yields a number nine orders of
    /// magnitude wrong rather than failing.
    /// </para>
    /// </remarks>
    public static ShipPosition? Parse(string? clipboard)
    {
        if (clipboard is not { Length: > 0 }) return null;

        var match = ReadingRegex.Match(clipboard);
        if (!match.Success) return null;

        return Number(match, "x") is { } x
            && Number(match, "y") is { } y
            && Number(match, "z") is { } z
            ? new ShipPosition(x, y, z)
            : null;
    }

    private static double? Number(Match match, string axis) =>
        double.TryParse(
            match.Groups[axis].ValueSpan,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var value)
            ? value
            : null;

    // The three axes in order, each labelled, with whatever separates them.
    // Anchored on nothing: the reading has been seen with and without a
    // "Coordinates:" prefix, and the prefix is not the part that matters.
    [GeneratedRegex(
        @"x:\s*(?<x>-?\d+(?:\.\d+)?)\s*[,;]?\s*" +
        @"y:\s*(?<y>-?\d+(?:\.\d+)?)\s*[,;]?\s*" +
        @"z:\s*(?<z>-?\d+(?:\.\d+)?)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ReadingRegex { get; }
}
