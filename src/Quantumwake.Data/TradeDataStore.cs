using System.Text.Json;
using Quantumwake.Core;

namespace Quantumwake.Data;

/// <summary>What the player has said about keeping market prices current.</summary>
/// <param name="Asked">
/// Whether they have been asked at all. Asked once, like update checks: a
/// question that returns after being answered is nagging.
/// </param>
/// <param name="Automatic">Refresh in the background, without asking again.</param>
/// <param name="LastCheckedAt">
/// When a refresh was last <em>attempted</em>, which is not when the data was
/// last fetched - a failed attempt has to count, or an unreachable UEX would be
/// retried on every tick for as long as the app is open.
/// </param>
public sealed record TradeDataPreference(
    bool Asked = false,
    bool Automatic = false,
    DateTimeOffset? LastCheckedAt = null);

/// <summary>
/// Whether prices may be refetched without being asked each time.
/// </summary>
/// <remarks>
/// <para>
/// Market prices are the one dataset here with a shelf life. Everything else
/// the app reads is either local or effectively static - a commodity's name
/// does not change while you play - but a price fetched on Friday describes a
/// market that has moved by Sunday, and a stale price shown without comment is
/// worse than no price, because it looks like an answer.
/// </para>
/// <para>
/// That makes this the first thing in the app with a reason to reach the
/// network on its own, which is why it is a preference and not a default. The
/// standing promise is that the app connects only when asked; this keeps that
/// promise by making the asking durable rather than per-fetch. Off unless the
/// player turns it on, in Settings or during setup, and revocable in the same
/// place.
/// </para>
/// <para>
/// Kept in its own file beside the other authored settings, for the reason
/// <see cref="UpdateStore"/> gives: a preference deliberately chosen should not
/// share a file with something rewritten dozens of times a session.
/// </para>
/// </remarks>
public sealed class TradeDataStore
{
    /// <summary>
    /// How old prices may get before a refresh is due.
    /// </summary>
    /// <remarks>
    /// Six hours is a compromise between a table that describes today's market
    /// and a third party's server being asked for a 15 MB table by every copy
    /// of this app that happens to be open. UEX is a volunteer project; the
    /// polite interval is the long one.
    /// </remarks>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromHours(6);

    /// <summary>
    /// How long to wait after a failed attempt before trying again.
    /// </summary>
    /// <remarks>
    /// Shorter than <see cref="StaleAfter"/>, because a refresh that failed
    /// left the prices as stale as they were; long enough that an outage is not
    /// hammered. The gap is why a failed attempt still updates
    /// <see cref="TradeDataPreference.LastCheckedAt"/>.
    /// </remarks>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(30);

    private readonly string _path;
    private readonly Lock _gate = new();
    private TradeDataPreference _preference = new();

    public TradeDataStore(string? directory = null)
    {
        _path = Path.Combine(directory ?? AppPaths.Root, "trade-data.json");
        Load();
    }

    public TradeDataPreference Current
    {
        get { lock (_gate) return _preference; }
    }

    /// <summary>Records the answer to the question, whichever way it went.</summary>
    /// <remarks>
    /// Answering at all sets <see cref="TradeDataPreference.Asked"/>, a refusal
    /// included: "no" is an answer, and asking again next launch would be asking
    /// someone to say no repeatedly.
    /// </remarks>
    public TradeDataPreference Answer(bool automatic)
    {
        lock (_gate)
        {
            _preference = _preference with { Asked = true, Automatic = automatic };
            Save();
            return _preference;
        }
    }

    /// <summary>Notes that a refresh was attempted, successfully or not.</summary>
    /// <param name="now">
    /// The current time, passed in for the same reason <see cref="IsDue"/> takes
    /// one: the two are compared against each other, so a test that can move one
    /// clock but not the other cannot exercise the backoff at all.
    /// </param>
    public TradeDataPreference Checked(DateTimeOffset? now = null)
    {
        lock (_gate)
        {
            _preference = _preference with { LastCheckedAt = now ?? DateTimeOffset.UtcNow };
            Save();
            return _preference;
        }
    }

    /// <summary>
    /// Whether a background refresh is due right now.
    /// </summary>
    /// <param name="fetchedAt">
    /// When the prices in hand were fetched, or null when there are none.
    /// </param>
    /// <param name="now">The current time, passed in so this can be tested.</param>
    /// <remarks>
    /// Null <paramref name="fetchedAt"/> means UEX is off, and being off is a
    /// choice this must not quietly undo: turning on automatic refresh says
    /// "keep my prices current", not "fetch prices I never asked for". So the
    /// answer is no, and it stays no until something enables UEX.
    /// </remarks>
    public bool IsDue(DateTimeOffset? fetchedAt, DateTimeOffset now)
    {
        var preference = Current;

        if (!preference.Automatic || fetchedAt is null)
            return false;

        // A recent attempt blocks a retry whether or not it worked. Checked() is
        // called either way, so a run of failures backs off to RetryAfter rather
        // than looping at the tick interval.
        if (preference.LastCheckedAt is { } attempted && now - attempted < RetryAfter)
            return false;

        return now - fetchedAt.Value >= StaleAfter;
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_path))
                _preference = JsonSerializer.Deserialize<TradeDataPreference>(File.ReadAllText(_path)) ?? new();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // A corrupt file leaves the app asking again, which is the safe way
            // to be wrong: it never turns on a fetch nobody agreed to.
            _preference = new TradeDataPreference();
        }
    }

    private void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(_preference));
    }
}
