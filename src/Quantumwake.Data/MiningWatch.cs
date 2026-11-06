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
public sealed record MiningPicture(
    bool Active,
    string? State,
    double Fill,
    double? BelowBy,
    int Cracked,
    DateTimeOffset? LastCrackAt,
    IReadOnlyList<CombatCallout> Callouts,
    PingReading? Ping = null,
    DateTimeOffset? PingAt = null);

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

    /// <summary>One frame's reading - null when no window bar was on it. Returns what is to be said now.</summary>
    public IReadOnlyList<CombatCallout> See(ChargeReading? reading, DateTimeOffset at)
    {
        var now = new List<CombatCallout>();

        if (reading is null)
        {
            _present = 0;
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
            // The first frame of a bar counts towards agreeing on its state,
            // so two frames in the window are enough to say so.
            if (++_present < 2)
            {
                _pending = reading.State;
                _agreed = 1;
                return now;
            }
            _active = true;
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
        _cracked, _lastCrack, [.. Enumerable.Reverse(_said).Take(8)], _ping, _pingAt);

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
    public IReadOnlyList<CombatCallout> SeePing(double? value, IReadOnlyList<Quantumwake.Core.GameData.RadarRow> rows, DateTimeOffset at)
    {
        var now = new List<CombatCallout>();
        if (value is not { } v) { _pingPending = null; return now; }

        if (_pingPending != v) { _pingPending = v; return now; }

        _ping = new PingReading(v, PingReadout.Decode(v, rows));
        _pingAt = at;
        if (_pingSaid == v && at - _pingSaidAt < TimeSpan.FromSeconds(30)) return now;
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
