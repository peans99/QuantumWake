namespace Quantumwake.Data;

/// <summary>What the fracture page shows.</summary>
/// <param name="Active">A fracture is under way: the window bar is on screen.</param>
/// <param name="State">below, in or above the window; null when not fracturing.</param>
/// <param name="Fill">The crack's progress, 0 to 1 - how much of the window bar has turned green.</param>
/// <param name="BelowBy">How far under the window the charge is, in bar heights.</param>
/// <param name="Cracked">Rocks cracked since the app started.</param>
/// <param name="LastCrackAt">When the last one cracked.</param>
/// <param name="Callouts">The last few things said, newest first.</param>
/// <param name="Ping">The last radar signature read and what it decodes to, while it is recent.</param>
/// <param name="PingAt">When it was read.</param>
/// <param name="Pings">The pings read this session, newest first - a sweep across a field to come back to.</param>
public sealed record MiningPicture(
    bool Active,
    string? State,
    double Fill,
    double? BelowBy,
    int Cracked,
    DateTimeOffset? LastCrackAt,
    IReadOnlyList<CombatCallout> Callouts,
    PingReading? Ping = null,
    DateTimeOffset? PingAt = null,
    IReadOnlyList<PingReading>? Pings = null);

/// <summary>How a ping is to be read: against which place, and which rocks the pilot is after.</summary>
/// <param name="Place">The place to read against, or null for every rock.</param>
/// <param name="Chosen">The pilot picked it, rather than the log's location.</param>
/// <param name="Wanted">Rocks marked wanted on Mining → Radar, by the name the voice says.</param>
/// <param name="WantedOnly">Say only pings that could be a wanted rock; the rest are still listed.</param>
public sealed record PingOptions(PingPlace? Place = null, bool Chosen = false, IReadOnlySet<string>? Wanted = null, bool WantedOnly = false);

/// <summary>
/// Turns the charge read each second into the moments worth saying while a
/// rock is being cracked: into the window, out of it, over it, cracked.
/// </summary>
/// <remarks>
/// <para>
/// Like <see cref="CombatWatch"/> it speaks on changes, and every change waits
/// for two frames that agree: over 845 frames of the 2026-08-31 stream the
/// reader made a window bar of single frames of the ship's floor and of the
/// cargo arc, a frame at a time.
/// </para>
/// <para>
/// A crack is the bar going away after it filled. On that stream the bar
/// vanished at 91-97% filled on every rock that broke and went on to
/// extraction; a bar that goes away at 20% is the laser let go, not a crack.
/// </para>
/// </remarks>
public sealed class MiningWatch
{
    /// <summary>A bar that went away at least this full went away because the rock broke.</summary>
    private const double CrackedAt = 0.85;

    private readonly List<CombatCallout> _said = [];
    private bool _active;
    private int _present, _absent;
    private string? _state, _pending;
    private int _agreed;
    private double _fill, _maxFill;
    private double? _belowBy;
    private int _cracked;
    private DateTimeOffset? _lastCrack;
    private bool _wasIn;

    /// <summary>
    /// Frames a bar has to hold still in before it is taken for the fracture
    /// HUD (1.1.18; it was two, anywhere). A pilot heard mining callouts in
    /// fights and at a Fleet terminal, with no rock in sight: a yellow or green
    /// box of the bar's size turns up in combat markers and terminal screens,
    /// but those move or come and go, and the real bar sits in one place for
    /// as long as the laser is on it - every fracture on the 2026-08-31
    /// stream held still for its first three frames.
    /// </summary>
    public const int StartFrames = 3;

    /// <summary>
    /// The fullest a bar may be when it is first seen. A fresh window starts
    /// yellow and greens from the left: on that stream fractures were first
    /// seen 0-37% green, and two single-frame sightings at 57%. A solid green
    /// box at 100% is a marker or a gauge, not a window being filled.
    /// </summary>
    public const double StartFillAtMost = 0.6;

    private ScreenPatch? _startBar;

    /// <summary>Same place, give or take the bar growing taller as the charge climbs.</summary>
    private static bool Still(ScreenPatch was, ScreenPatch now)
    {
        static double cx(ScreenPatch p) => p.Left + p.Width / 2;
        static double cy(ScreenPatch p) => p.Top + p.Height / 2;
        return Math.Abs(cx(was) - cx(now)) <= Math.Max(was.Width, now.Width) * 0.5
            && Math.Abs(cy(was) - cy(now)) <= Math.Max(was.Height, now.Height) * 1.5;
    }

    /// <summary>One frame's reading - null when no window bar was on it. Returns what is to be said now.</summary>
    /// <param name="mayStart">
    /// False in a fight: a bar not already followed is not taken up then. One
    /// already followed carries on - a rock under fire is still a rock.
    /// </param>
    public IReadOnlyList<CombatCallout> See(ChargeReading? reading, DateTimeOffset at, bool mayStart = true)
    {
        var now = new List<CombatCallout>();

        if (reading is null)
        {
            _present = 0;
            _startBar = null;
            if (!_active) return now;
            if (++_absent < 2) return now;

            // The bar has gone: cracked if it had filled, let go if not.
            if (_maxFill >= CrackedAt)
            {
                _cracked++;
                _lastCrack = at;
                now.Add(Say(at, "cracked", "Rock cracked"));
            }
            _active = false;
            _state = _pending = null;
            _agreed = 0;
            _maxFill = _fill = 0;
            _belowBy = null;
            _wasIn = false;
            return now;
        }

        _absent = 0;
        if (!_active)
        {
            if (!mayStart)
            {
                _present = 0;
                _startBar = null;
                return now;
            }

            // A run of sightings starts on a mostly yellow bar, and goes on
            // only while the bar stays where it was.
            if (_present == 0 || _startBar is not { } before || !Still(before, reading.Bar))
            {
                _present = 0;
                if (reading.Fill > StartFillAtMost)
                {
                    _startBar = null;
                    return now;
                }
            }
            _startBar = reading.Bar;

            // The run's frames count towards agreeing on its state, so the
            // frame that starts following can already say it.
            if (++_present == 1) { _pending = reading.State; _agreed = 1; }
            else if (reading.State == _pending) _agreed++;
            else { _pending = reading.State; _agreed = 1; }
            if (_present < StartFrames) return now;
            _active = true;
            _startBar = null;

            // This frame's own vote is cast below, like every followed frame's.
            _agreed--;
        }

        _fill = reading.Fill;
        _maxFill = Math.Max(_maxFill, reading.Fill);
        _belowBy = reading.BelowBy;

        if (reading.State == _pending) _agreed++;
        else { _pending = reading.State; _agreed = 1; }
        if (_agreed < 2 || _pending == _state) return now;

        var was = _state;
        _state = _pending;
        switch (_state)
        {
            case "in":
                _wasIn = true;
                now.Add(Say(at, "window", "In the window"));
                break;
            case "above":
                now.Add(Say(at, "overcharge", "Overcharge, back off"));
                break;
            case "below" when was == "in" && _wasIn:
                now.Add(Say(at, "window", "Out of the window"));
                break;
        }

        return now;
    }

    public MiningPicture Picture() => new(
        _active, _active ? _state : null, _active ? _fill : 0, _active ? _belowBy : null,
        _cracked, _lastCrack, [.. Enumerable.Reverse(_said).Take(8)], _ping, _pingAt, [.. Enumerable.Reverse(_pings)]);

    /// <summary>The most pings kept: a sweep of one field, not the evening.</summary>
    public const int PingsKept = 12;

    private readonly List<PingReading> _pings = [];

    private PingReading? _ping;
    private DateTimeOffset? _pingAt;
    private double? _pingPending;
    private double? _pingSaid;
    private DateTimeOffset _pingSaidAt;

    /// <summary>
    /// One frame's radar signature readout - null when the box was not on it -
    /// decoded against <paramref name="rows"/>. A figure is said once two reads
    /// agree on it, and not again for half a minute: the pilot sweeping across
    /// the same cluster is not news, a different figure is.
    /// </summary>
    /// <remarks>
    /// The ping is read against the rocks that spawn at the place in
    /// <paramref name="options"/>, and against every rock when none of those
    /// reads it: a figure that is a whole number of a rock the table says is
    /// not here says more about the place being wrong - the log's guess, or a
    /// pick left over from the last session - than about the radar. Every agreed ping
    /// goes into the history - a different figure, or the same one after half
    /// a minute - and with <see cref="PingOptions.WantedOnly"/> only the ones
    /// that could be a wanted rock are said.
    /// </remarks>
    public IReadOnlyList<CombatCallout> SeePing(double? value, IReadOnlyList<Quantumwake.Core.GameData.RadarRow> rows, DateTimeOffset at,
        PingOptions? options = null)
    {
        var now = new List<CombatCallout>();
        if (value is not { } v) { _pingPending = null; return now; }

        if (_pingPending != v) { _pingPending = v; return now; }

        var place = options?.Place;
        var here = PingPlaces.Rows(rows, place);
        var decoded = PingReadout.Decode(v, here);
        if (ReferenceEquals(here, rows)) place = null;          // nothing at the place to read against
        else if (decoded.Count == 0)
        {
            decoded = PingReadout.Decode(v, rows);
            if (decoded.Count > 0) place = null;
        }

        var (matches, wanted) = PingReadout.Wanted(decoded, options?.Wanted);
        _ping = new PingReading(v, matches, place?.Name, options?.Chosen ?? false, wanted, at);
        _pingAt = at;

        // Held on one contact, the same figure every few seconds is one entry
        // brought up to date; the same figure after half a minute away is
        // another rock of the same size.
        if (_pings.Count > 0 && _pings[^1].Value == v && at - (_pings[^1].At ?? at) <= TimeSpan.FromSeconds(30))
            _pings[^1] = _ping;
        else
        {
            _pings.Add(_ping);
            if (_pings.Count > PingsKept) _pings.RemoveAt(0);
        }

        if (_pingSaid == v && at - _pingSaidAt < TimeSpan.FromSeconds(30)) return now;
        if (options?.WantedOnly == true && !wanted) return now;
        if (PingReadout.Sentence(_ping) is not { } sentence) return now;

        _pingSaid = v;
        _pingSaidAt = at;
        now.Add(Say(at, "ping", sentence));
        return now;
    }

    private CombatCallout Say(DateTimeOffset at, string kind, string text)
    {
        var callout = new CombatCallout(at, kind, text);
        _said.Add(callout);
        if (_said.Count > 30) _said.RemoveAt(0);
        return callout;
    }
}
