using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Data;

/// <summary>
/// Three things the cockpit HUD writes that a pilot can miss in a fight: the
/// "INBOUND MISSILE" warning, "HOSTILE TERRITORY", and a mission's health bar
/// - an escort's hull, a relay's, a defended ship's.
/// </summary>
/// <remarks>
/// <para>
/// All three are text, and none of them is in the log: the 2026-11-06 session
/// has one line naming a missile at all, a launcher detached from a wreck.
/// Measured on the same recording as the target panel - 137 frames of two
/// Tactical Strike Group fights at 3440x1440, read by the Windows engine the
/// live loop reads whole frames with; see docs/screen-insight.md, "Missile,
/// territory and objective health (1.1.14)".
/// </para>
/// <para>
/// The missile warning is red, about 30 pixels tall at 1440 lines, and drawn
/// on the canopy rather than the screen, so it moves with the pilot's head: it
/// sat at 1487,413 on one frame and 1617,311 on another. It was up for about
/// two seconds each time, which a whole-frame read every third second can
/// miss, so the band it moves in is read up close on every tick of a fight -
/// see <see cref="MissileBand"/>.
/// </para>
/// </remarks>
public static partial class HudText
{
    /// <summary>
    /// Where the missile warning moves: the middle of the screen, from a sixth
    /// to a third of the way down. Centre-relative in frame heights, so an
    /// ultrawide frame looks in front of the pilot and not at its edges.
    /// </summary>
    public static ScreenPatch MissileBand(int width, int height) =>
        new(width / 2.0 - height * 0.32, height * 0.17, height * 0.64, height * 0.2);

    /// <summary>Whether lines read in or over the band say the missile warning; chat elsewhere on screen that mentions missiles does not count.</summary>
    public static bool Missile(IEnumerable<ScreenTextLine> lines, int width, int height)
    {
        var band = MissileBand(width, height);
        return lines.Any(l => Within(l, band) && MissileWords().IsMatch(Squash(l.Text)));
    }

    /// <summary>
    /// Whether a whole frame shows a ship's flight HUD: two or more of the
    /// words it always draws - the fuel gauges, the SCM/NAV mode, GUN or MSL,
    /// ESP, CPLD.
    /// </summary>
    /// <remarks>
    /// The log cannot say when the pilot takes a seat in 4.10: there is no
    /// seat or control line in the 2026-11-06 session's 33,215. Two words
    /// rather than one because one alone is easy to meet on foot - a shop
    /// lists guns. Two were on 125 of the recording's 137 cockpit frames, and
    /// on 5 of the 154 screenshots in this install, all five taken in a
    /// cockpit.
    /// </remarks>
    public static bool FlightHud(IEnumerable<ScreenTextLine> lines)
    {
        var found = new HashSet<string>();
        foreach (var line in lines)
            foreach (var word in Words().Split(line.Text.ToUpperInvariant()))
                if (FlightWords.Contains(word))
                    found.Add(word.EndsWith("FUEL", StringComparison.Ordinal) ? "FUEL" : word == "GUNS" ? "GUN" : word);
        return found.Count >= 2;
    }

    private static readonly HashSet<string> FlightWords =
        new(["H-FUEL", "Q-FUEL", "SCM", "NAV", "GUN", "GUNS", "MSL", "ESP", "CPLD"], StringComparer.Ordinal);

    /// <summary>Whether the "HOSTILE TERRITORY" banner is on a whole frame: the top fifth, the middle third across.</summary>
    public static bool HostileTerritory(IEnumerable<ScreenTextLine> lines, int width, int height) =>
        lines.Any(l => l.Top < height * 0.2 && l.Left > width / 3.0 && l.Left < width * 2 / 3.0
            && TerritoryWords().IsMatch(Squash(l.Text)));

    /// <summary>
    /// The mission panel's health figures on a whole frame: escort for a
    /// "Hull Integrity" bar, objective for any "Health" one, with the lowest
    /// figure of each kind.
    /// </summary>
    /// <remarks>
    /// The panel is the top-right block under the contract's title. Its label
    /// and figure are read as one line ("Power Relay Health 100%") or two, the
    /// figure a row's height above the label's middle ("24%" at 232 beside
    /// "Ship Health:" at 250). A figure without its % is not taken: "248" was
    /// read for 24%, and a hull called at 248 is worse than one not called.
    /// </remarks>
    public static IReadOnlyDictionary<string, int> Health(IReadOnlyList<ScreenTextLine> lines, int width, int height)
    {
        var found = new Dictionary<string, int>();
        var u = height / 1440.0;

        foreach (var label in lines)
        {
            if (label.Left < width * 0.7 || label.Top > height * 0.35) continue;
            var squashed = Squash(label.Text);
            string? what = HullWords().IsMatch(squashed) ? "escort" : HealthWords().IsMatch(squashed) ? "objective" : null;
            if (what is null) continue;

            var percent = Percent(label.Text);
            if (percent is null)
            {
                var middle = label.Top + label.Height / 2;
                percent = lines
                    .Where(f => f != label && f.Left > label.Left && Math.Abs(f.Top + f.Height / 2 - middle) <= 45 * u)
                    .OrderBy(f => Math.Abs(f.Top + f.Height / 2 - middle))
                    .Select(f => Percent(f.Text))
                    .FirstOrDefault(p => p is not null);
            }

            if (percent is { } p && (!found.TryGetValue(what, out var lowest) || p < lowest))
                found[what] = p;
        }

        return found;
    }

    private static int? Percent(string text)
    {
        if (Figure().Match(text) is not { Success: true } m) return null;
        var digits = m.Groups["n"].Value.Replace('O', '0').Replace('o', '0');
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 100 ? n : null;
    }

    private static bool Within(ScreenTextLine line, ScreenPatch band) =>
        line.Left >= band.Left - band.Height * 0.2 && line.Left <= band.Left + band.Width
        && line.Top >= band.Top - band.Height * 0.2 && line.Top <= band.Top + band.Height;

    /// <summary>Letters and digits only, upper case: the engines split and join words differently.</summary>
    private static string Squash(string text) =>
        new([.. text.ToUpperInvariant().Where(char.IsLetterOrDigit)]);

    // "INBOUND MISSILE", and how the engine bent it on the recording -
    // INBOUND MISILE, INBLNDISILE - or might: MISSLE, M1SSILE, INB0UND.
    [GeneratedRegex("INB[A-Z0]{1,3}ND|M[I1L]S{1,2}[I1L]?LE|[I1L]S{1,2}[I1L]LE$", RegexOptions.CultureInvariant)]
    private static partial Regex MissileWords();

    // Words split on anything but letters, digits and the fuel gauges' hyphen.
    [GeneratedRegex("[^A-Z0-9-]+", RegexOptions.CultureInvariant)]
    private static partial Regex Words();

    [GeneratedRegex("H[O0]ST[I1L]LETERR", RegexOptions.CultureInvariant)]
    private static partial Regex TerritoryWords();

    // "Ship Hull Integrity", read as HullIntegrity, Integity, HllIntegrity, ullIntegrity.
    [GeneratedRegex("INTEG|NTEGR|HULL", RegexOptions.CultureInvariant)]
    private static partial Regex HullWords();

    [GeneratedRegex("HEALTH|HEALT|EALTH", RegexOptions.CultureInvariant)]
    private static partial Regex HealthWords();

    [GeneratedRegex(@"(?<![\d])(?<n>[\dOo]{1,3})\s*%", RegexOptions.CultureInvariant)]
    private static partial Regex Figure();
}

/// <summary>
/// Says the HUD's warnings once each: the missile when it appears, hostile
/// territory on the way in, a mission's health as it falls through half and a
/// quarter.
/// </summary>
public sealed class HudWatch
{
    /// <summary>Gone this long, a missile warning that comes back is another missile.</summary>
    public static readonly TimeSpan MissileRearm = TimeSpan.FromSeconds(3);

    /// <summary>Gone this long, the territory banner coming back is entering it again; shorter is the engine missing it.</summary>
    public static readonly TimeSpan TerritoryRearm = TimeSpan.FromSeconds(90);

    /// <summary>Unseen this long, a mission's health is forgotten - the next one starts afresh.</summary>
    public static readonly TimeSpan HealthForget = TimeSpan.FromMinutes(5);

    private static readonly int[] Bands = [50, 25];

    private DateTimeOffset? _missileAt;
    private bool _missileUp;
    private DateTimeOffset? _territoryAt;
    private DateTimeOffset? _flightAt;

    /// <summary>
    /// Gone this long, the flight HUD coming back is a ship powering up. Long,
    /// because the engine misses it on one cockpit frame in eleven and a
    /// pilot looking round the cabin hides it too: "online" twice in a flight
    /// is worse than not saying it after a short walk. Two minutes until
    /// 1.1.18, when a pilot who never left the seat heard it again and again:
    /// a spell in mobiGlas or a busy fight hides the HUD's words that long.
    /// </summary>
    public static readonly TimeSpan FlightRearm = TimeSpan.FromMinutes(15);
    private readonly Dictionary<string, Health> _health = [];

    private sealed class Health
    {
        public int Last;
        public DateTimeOffset At;
        public int Said = 100;
    }

    /// <summary>Whether the missile warning was showing on the last read of it.</summary>
    public bool MissileShowing => _missileUp;

    /// <summary>Whether the territory banner has been seen lately - the live loop's cue to look for missiles between whole-frame reads.</summary>
    public bool Hostile(DateTimeOffset at) => _territoryAt is { } seen && at - seen < TerritoryRearm;

    /// <summary>
    /// One look at the missile band. Said on the first read that has it -
    /// a second could be the missile arriving - and not again until it has
    /// been gone for <see cref="MissileRearm"/>.
    /// </summary>
    public IReadOnlyList<CombatCallout> SeeMissile(bool showing, DateTimeOffset at)
    {
        if (showing)
        {
            _missileAt = at;
            if (_missileUp) return [];
            _missileUp = true;
            return [new CombatCallout(at, "missile", "Missile inbound")];
        }

        if (_missileUp && _missileAt is { } seen && at - seen >= MissileRearm) _missileUp = false;
        return [];
    }

    /// <summary>One whole frame's flight HUD, banner and health figures.</summary>
    public IReadOnlyList<CombatCallout> SeeWhole(bool territory, IReadOnlyDictionary<string, int> health, DateTimeOffset at, bool flight = false)
    {
        var now = new List<CombatCallout>();

        if (flight)
        {
            if (_flightAt is not { } flew || at - flew >= FlightRearm)
                now.Add(new CombatCallout(at, "online", "Quantum Wake system online"));
            _flightAt = at;
        }

        if (territory)
        {
            if (_territoryAt is not { } last || at - last >= TerritoryRearm)
                now.Add(new CombatCallout(at, "territory", "Entering hostile territory"));
            _territoryAt = at;
        }

        foreach (var gone in _health.Where(h => at - h.Value.At > HealthForget).Select(h => h.Key).ToList())
            _health.Remove(gone);

        foreach (var (what, percent) in health)
        {
            if (!_health.TryGetValue(what, out var h))
            {
                _health[what] = new Health { Last = percent, At = at };
                continue;
            }

            // One frame's "10%" for a "100%" is the engine, so a figure is
            // taken on its own only when the bar was already falling and this
            // is a plausible step further down; otherwise two reads in a row,
            // and the milder of them. Both are needed: on the recording a power
            // relay went 100, 83, 66, 50, 16 in six seconds, two whole-frame
            // reads apart, and agreeing pairs alone said nothing until it was
            // gone. A bar back near full is a new objective or a repair, and
            // is watched afresh.
            var falling = h.Last < 90 && percent <= h.Last && h.Last - percent <= 40;
            var agreed = falling ? percent : Math.Max(percent, h.Last);
            h.Last = percent;
            h.At = at;

            if (agreed >= 90) { h.Said = 100; continue; }

            var band = Bands.Where(b => agreed < b).DefaultIfEmpty(100).Min();
            if (band < h.Said)
            {
                h.Said = band;
                now.Add(new CombatCallout(at, "objective-health",
                    $"{(what == "escort" ? "Escort hull" : "Objective health")} below {band} percent"));
            }
        }

        return now;
    }
}
