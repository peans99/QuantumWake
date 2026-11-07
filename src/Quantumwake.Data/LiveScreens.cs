using System.Text.Json;

namespace Quantumwake.Data;

/// <summary>
/// Decides which live frames of the screens a screenshot is read for - a
/// loadout, the map, a kiosk, the Contracts and Rep apps, the Fleet Manager, a
/// refinery - are kept, and under what name.
/// </summary>
/// <remarks>
/// <para>
/// With reading the screen on, these are what the pilot no longer has to press
/// Print Screen for. A frame is read every two seconds and a terminal stays up
/// for a minute, so keeping each one would put thirty readings of one kiosk in
/// a log that keeps the newest <see cref="ScreenReadingStore.Keep"/>, and push
/// out the screenshots the pilot actually took. So a screen gets one reading,
/// named for when it was first kept, the way <see cref="LiveRocks"/> keeps a
/// rock.
/// </para>
/// <para>
/// A screen is the same screen while it names the same thing - the same ship
/// in the loadout, the same place on the map, the same contract selected, the
/// same organisation open - and while it keeps being seen: two minutes without
/// it and the next look is a new reading. A later frame replaces the kept one
/// when it read more of the screen, or when it read as much and says something
/// different on two frames running: a kiosk's stock after a purchase is the
/// same kiosk saying something new, while a digit the engine reads one way and
/// then the other is not worth rewriting the log for.
/// </para>
/// <para>
/// Nothing is kept from a single frame. A screen seen once and gone - a tab
/// flicked past, a frame mistaken for a terminal - is not what a screenshot
/// would have been taken of. It needs the next whole-frame read, about two
/// seconds later, to say the same screen again.
/// </para>
/// <para>
/// The rock scan stays with <see cref="LiveRocks"/>. A mobiGlas app this app
/// has no reader for is kept only for the balance in its bar, as one wallet
/// reading (1.1.22); without a balance it would be the same unreadable line
/// every two seconds, and is not kept.
/// </para>
/// </remarks>
public sealed class LiveScreens
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(2);

    private sealed class Held
    {
        public required string Shot;
        public required int Score;
        public required string Says;
        public DateTimeOffset LastSeen;
    }

    private readonly Dictionary<string, Held> _held = new(StringComparer.Ordinal);
    private string? _lastScreen;
    private string? _lastSays;
    private DateTimeOffset _lastAt = DateTimeOffset.MinValue;

    /// <summary>
    /// Two frames are running when this close: a whole frame is read every two
    /// seconds, every third second while a ship is locked, and a slow read on
    /// an ultrawide has taken most of a second on its own.
    /// </summary>
    private static readonly TimeSpan Running = TimeSpan.FromSeconds(8);

    /// <summary>A mobiGlas app this app has no reader for, kept for the balance in its bar alone.</summary>
    private const string WalletScreen = "wallet";

    /// <summary>
    /// The name to keep this frame's reading under, or null when it is not
    /// kept: not a screen kept live, read too little to name, seen once so far,
    /// or no better than what is already kept.
    /// </summary>
    public string? Keep(ScreenSighting sighting, DateTimeOffset at)
    {
        var screen = Screen(sighting);
        var says = screen is null ? null : Says(sighting);

        // Running means the last whole frame read, a couple of seconds ago -
        // not the last one before the pilot alt-tabbed away for ten minutes.
        var running = at - _lastAt <= Running;
        var seenBefore = running && screen is not null && screen == _lastScreen;
        var saidBefore = seenBefore && says == _lastSays;
        _lastScreen = screen;
        _lastSays = says;
        _lastAt = at;

        foreach (var stale in _held.Where(h => at - h.Value.LastSeen > Gap).Select(h => h.Key).ToList())
            _held.Remove(stale);

        if (screen is null || says is null) return null;

        var score = Score(sighting);

        if (_held.TryGetValue(screen, out var held))
        {
            held.LastSeen = at;
            var better = score > held.Score
                || (score == held.Score && says != held.Says && saidBefore);
            if (!better) return null;

            held.Score = score;
            held.Says = says;
            return held.Shot;
        }

        // A balance is the whole of a wallet reading, and the Ledger believes
        // it, so it has to read the same figure twice before it is kept at
        // all: a digit misread on one frame must not become the cash baseline
        // for the seconds until the next frame corrects it.
        if (!seenBefore || (screen == WalletScreen && !saidBefore)) return null;

        var shot = $"live-{at.ToUniversalTime():yyyyMMdd-HHmmss}";
        _held[screen] = new Held { Shot = shot, Score = score, Says = says, LastSeen = at };
        return shot;
    }

    /// <summary>
    /// What makes two frames the same screen, or null when the frame is not
    /// one kept live or read too little to say which it is.
    /// </summary>
    public static string? Screen(ScreenSighting s)
    {
        static string? Named(string prefix, params string?[] parts) =>
            parts.All(string.IsNullOrWhiteSpace) ? null
            : prefix + ":" + string.Join("|", parts.Select(p => p?.Trim().ToUpperInvariant() ?? ""));

        return s.Kind switch
        {
            // Not a tooltip: hovering down an inventory would keep one an item
            // every few seconds, and in a log of three hundred that pushes out
            // the one loadout reading of a ship. A tooltip is still read and
            // shown as the last thing seen.
            ScreenKind.Loadout => s.Loadout is { } l ? Named("loadout", l.Ship ?? l.ShipRead) : null,
            ScreenKind.Map => s.Map is { } m ? Named("map", m.SystemRead, m.PlaceRead) : null,
            ScreenKind.Contracts => s.Contracts is { } c
                ? c.SelectedTitle is { Length: > 0 } title ? Named("contracts", title)
                : c.Cards.Count > 0 || c.Accepted is not null ? "contracts:list" : null
                : null,
            ScreenKind.Fleet => s.Fleet is { Ships.Count: > 0 } ? "fleet" : null,
            ScreenKind.Reputation => s.Reputation is { } r ? Named("rep", r.Organisation, r.Section) : null,
            ScreenKind.Kiosk => s.Kiosk is { Rows.Count: > 0 } k
                ? (k.Buying == false ? "kiosk:sell:" : "kiosk:buy:") + (k.Ship ?? k.ShipRead ?? "").ToUpperInvariant()
                : null,
            ScreenKind.Refinery => s.Refinery is { } f ? $"refinery:{f.Stage}:{f.Station?.ToUpperInvariant()}" : null,

            // The confirmation by the contracts on it, so two hand-overs are
            // two readings; the list as one screen, renewed as cargo moves.
            // The confirmation was up five or six seconds each time on the
            // video, enough for the two running frames a keep needs - a pilot
            // who clicks CONFIRM at once leaves nothing behind.
            ScreenKind.Freight => s.Freight is { } fr
                ? fr.Stage == "confirm"
                    ? fr.Orders.Count > 0 ? "freight:confirm:" + string.Join("+", fr.Orders.Select(o => o.Title.ToUpperInvariant())) : null
                    : "freight:list"
                : null,

            // A mobiGlas app with no reader still carries the balance in its
            // bar - the home screen is where a pilot glances at their cash -
            // and a screenshot of it kept that. Kept live as one reading,
            // whichever app it was on, renewed when the figure changes.
            ScreenKind.MobiGlas => s.Wallet?.Balance is not null ? WalletScreen : null,
            _ => null,
        };
    }

    /// <summary>Everything the frame's readers made of it, as one string to compare frames by.</summary>
    private static string Says(ScreenSighting s) => JsonSerializer.Serialize(new object?[]
    {
        s.Loadout, s.Map, s.Wallet?.Balance, s.Contracts, s.Fleet, s.Reputation, s.Kiosk, s.Refinery, s.Freight,
    });

    /// <summary>
    /// How much of the screen a reading got: every value its readers filled
    /// in, counted alike. Crude, and the same rule for every screen, which is
    /// the point - a field that read is worth more than one that did not.
    /// </summary>
    public static int Score(ScreenSighting s)
    {
        using var doc = JsonDocument.Parse(Says(s));
        return Count(doc.RootElement);

        static int Count(JsonElement e) => e.ValueKind switch
        {
            JsonValueKind.Object => e.EnumerateObject().Sum(p => Count(p.Value)),
            JsonValueKind.Array => e.EnumerateArray().Sum(Count),
            JsonValueKind.Null or JsonValueKind.Undefined => 0,
            JsonValueKind.String => e.GetString() is { Length: > 0 } ? 1 : 0,
            JsonValueKind.False => 0,
            _ => 1,
        };
    }
}
