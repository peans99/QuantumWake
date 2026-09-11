namespace Quantumwake.Data;

/// <summary>
/// What the Ledger can say about the balance: the last figure read off a
/// screen, and where the logs have moved it since.
/// </summary>
/// <param name="Balance">The figure as it was on the screen.</param>
/// <param name="MovedSince">Every movement the ledger recorded after the shot, netted. Zero when nothing has.</param>
/// <param name="MovementsSince">How many lines that was, so the estimate can say what it rests on.</param>
/// <param name="Estimate">
/// The balance plus the movement - what the wallet holds now if every aUEC
/// that moved left a line in the log. It is an estimate and is labelled one
/// on the page, because the logs miss things: insurance claims, hangar fees,
/// a trade the parser did not recognise. The next screenshot says how far off it was.
/// </param>
public sealed record WalletStanding(
    string? Shot,
    DateTimeOffset ShotAt,
    long Balance,
    decimal MovedSince,
    int MovementsSince,
    long Estimate);

public static class WalletStandings
{
    /// <summary>The standing now, or null when no screenshot has ever shown a balance that read.</summary>
    /// <remarks>
    /// The ledger is a movement record - <c>Game.log</c> never states a total -
    /// so a balance only exists once a screen has been read, and the running
    /// sum is what carries it forward from there. The whole ledger rather than
    /// a period, because the shot may be older than any period the page offers.
    /// </remarks>
    public static WalletStanding? Now(ScreenReadingStore readings, LogLibrary library)
    {
        if (readings.LastWallet() is not { } last) return null;

        var ledger = library.Ledger();

        // Newest first: the head carries the total to date, and the first
        // entry at or before the shot carries the total as it stood then.
        var now = ledger.FirstOrDefault()?.Running ?? 0m;
        var then = ledger.FirstOrDefault(e => e.At <= last.At)?.Running ?? 0m;
        var moved = now - then;
        var lines = ledger.Count(e => e.At > last.At);

        return new WalletStanding(last.Shot, last.At, last.Balance, moved, lines,
            last.Balance + (long)Math.Round(moved));
    }
}
