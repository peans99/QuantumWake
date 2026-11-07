using System.Globalization;
using System.Text.RegularExpressions;
using Quantumwake.Core.GameData;

namespace Quantumwake.Data;

/// <summary>One way a ping is a whole number of one kind of rock.</summary>
/// <param name="Count">How many rocks.</param>
/// <param name="Name">The rock as a pilot says it - "Riccite", "Hand-mining deposit".</param>
/// <param name="Tier">Its rarity, for ship rocks.</param>
/// <param name="Signature">One rock's figure.</param>
/// <param name="Wanted">A rock the pilot marked as wanted on Mining → Radar.</param>
public sealed record PingMatch(int Count, string Name, string? Tier, double Signature, bool Wanted = false);

/// <summary>A ping read off the HUD and what it is.</summary>
/// <param name="Value">The figure as read.</param>
/// <param name="Matches">Every exact reading, fewest rocks first, the pilot's wanted rocks ahead of the rest; empty when it is no whole number of any one rock.</param>
/// <param name="Place">The place it was read against, when one was; see <see cref="PingPlaces"/>.</param>
/// <param name="Chosen">That place was picked by the pilot rather than taken from where the log puts them.</param>
/// <param name="Wanted">One of its readings is a rock the pilot marked as wanted.</param>
/// <param name="At">When it was read.</param>
public sealed record PingReading(
    double Value, IReadOnlyList<PingMatch> Matches,
    string? Place = null, bool Chosen = false, bool Wanted = false, DateTimeOffset? At = null);

/// <summary>
/// The mining radar's signature readout - the boxed figure with a pin icon
/// just above the compass heading, the signature of whatever the pilot is
/// pointing at after a ping - read off the screen and decoded.
/// </summary>
/// <remarks>
/// <para>
/// Measured on RisboTheBlade's "4.7 RICCITE - How To Scan For It, Signatures,
/// Number Of Rocks" (<https://www.youtube.com/watch?v=WyCv6-DQYDE>, 2560x1440),
/// 80 frames two seconds apart from 02:40: the box sat at 1280, 500 on every
/// frame that had one - the screen's centre, a third of the way down - and
/// read 17,080, 12,810, 25,620, 96,000, 10,155 and 47,385 as the pilot swept
/// across contacts. 10,155 is three Riccite (3,385 each), which is what the
/// video's own caption says; 17,080 is four of 4,270.
/// </para>
/// <para>
/// Windows' engine read nothing in that box on any of the 80 frames, so it is
/// read up close by the panel reader (ONNX). The compass headings under it
/// are three digits with no separator and are left alone.
/// </para>
/// <para>
/// The decoding is the Radar page's, moved here so the live loop can say it:
/// exact multiples only - the files give whole figures and so does the radar
/// - up to twenty rocks, fewest first.
/// </para>
/// </remarks>
public static partial class PingReadout
{
    /// <summary>The most rocks a ping is read as; the Radar page's limit.</summary>
    public const int MostRocks = 20;

    /// <summary>Where the readout sits: centred, a third of the way down. Centre-relative in frame heights, so an ultrawide frame looks above its own compass.</summary>
    public static ScreenPatch Patch(int width, int height) =>
        new(width / 2.0 - height * 0.12, height * 0.31, height * 0.24, height * 0.07);

    /// <summary>The figure in the box, or null: "10,155", "12.810", "96000" - never a three-digit heading.</summary>
    public static double? Value(IReadOnlyList<ScreenTextLine> lines)
    {
        foreach (var line in lines.OrderBy(l => l.Top))
        {
            var text = line.Text.Trim().Replace(" ", "");
            if (Figure().Match(text) is not { Success: true } m) continue;
            var digits = m.Groups["n"].Value.Replace(",", "").Replace(".", "");
            if (double.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) && v >= 1000)
                return v;
        }
        return null;
    }

    /// <summary>Every way <paramref name="ping"/> is a whole number of one row's rocks, fewest first.</summary>
    public static IReadOnlyList<PingMatch> Decode(double ping, IEnumerable<RadarRow> rows)
    {
        if (ping <= 0) return [];
        var found = new List<PingMatch>();
        foreach (var row in rows)
        {
            if (row.Signature <= 0) continue;
            var count = (int)Math.Round(ping / row.Signature);
            if (count is < 1 or > MostRocks) continue;
            if (Math.Abs(ping - count * row.Signature) < 0.5)
                found.Add(new PingMatch(count, Spoken(row), row.Tier, row.Signature));
        }
        return [.. found.OrderBy(m => m.Count).ThenBy(m => m.Signature)];
    }

    /// <summary>The rock as said aloud; the Radar page's names, in English because the voice is.</summary>
    public static string Spoken(RadarRow row) => row.Kind switch
    {
        "ship" => row.Name,
        "asteroid" => $"{row.Name[0]}-type asteroid",
        "hand" => "Hand-mining deposit",
        "ground" => "Ground-vehicle deposit",
        _ => "Older deposit rock",
    };

    private static readonly string[] Numbers =
        ["zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten",
         "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen", "Twenty"];

    /// <summary>"Three Riccite", or "Three Riccite or Two Iron" for a figure two readings share; null with none.</summary>
    public static string? Sentence(PingReading reading)
    {
        if (reading.Matches.Count == 0) return null;
        // "wanted" straight after the rock it is about - "Two Quantainium,
        // wanted, or Three Iron" - and Wanted() below has put that one first.
        string Say(PingMatch m) => $"{Numbers[m.Count]} {m.Name}{(m.Wanted ? ", wanted" : "")}";
        return reading.Matches.Count == 1 ? Say(reading.Matches[0]) : $"{Say(reading.Matches[0])} or {Say(reading.Matches[1])}";
    }

    /// <summary>
    /// The readings with the pilot's wanted rocks first, and whether there are
    /// any; matches keep their fewest-first order within each half.
    /// </summary>
    public static (IReadOnlyList<PingMatch> Matches, bool Wanted) Wanted(IReadOnlyList<PingMatch> matches, IReadOnlySet<string>? wanted)
    {
        if (wanted is null || wanted.Count == 0) return (matches, false);
        var hits = matches.Where(m => wanted.Contains(m.Name)).Select(m => m with { Wanted = true }).ToList();
        return hits.Count == 0 ? (matches, false) : ([.. hits, .. matches.Where(m => !wanted.Contains(m.Name))], true);
    }

    /// <summary>The number words the voice pack records; see tools/voice-pack.</summary>
    public static IReadOnlyList<string> NumberWords => Numbers[1..];

    // A thousands separator read as either mark, or four to six bare digits.
    [GeneratedRegex(@"^(?<n>\d{1,3}[,.]\d{3}|\d{4,6})$", RegexOptions.CultureInvariant)]
    private static partial Regex Figure();
}
