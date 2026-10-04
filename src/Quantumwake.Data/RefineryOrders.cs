using Quantumwake.Core.State;

namespace Quantumwake.Data;

/// <summary>
/// A refinery work order, put together from the terminal's screenshots and the
/// game's word that it finished.
/// </summary>
/// <param name="Station">Where, as the terminal's title read.</param>
/// <param name="SeenAt">The first screenshot of it.</param>
/// <param name="Shot">The screenshot the due time is counted from.</param>
/// <param name="Basis">
/// <c>running</c> when the due time comes from the PROCESSING screen's time
/// remaining - the order was certainly placed. <c>quote</c> when it comes from
/// the setup screen's processing time: the pilot was shown the price and may
/// not have confirmed it, and everything built on it says "if confirmed".
/// </param>
/// <param name="DueAt">When the terminal's own countdown runs out: the shot's time plus the time it printed.</param>
/// <param name="InCscu">What went in, from the quote's TO REFINE.</param>
/// <param name="OutCscu">What the terminal said would come back - summed over the lots whose yield read.</param>
/// <param name="CompletedAt">When the game's log said this order completed; null until it does.</param>
public sealed record RefineryOrder(
    string Station,
    DateTimeOffset SeenAt,
    string Shot,
    string Basis,
    DateTimeOffset? DueAt,
    string? Method,
    decimal? Cost,
    int? InCscu,
    int? OutCscu,
    IReadOnlyList<RefineryLot> Lots,
    DateTimeOffset? CompletedAt = null);

/// <summary>
/// One lot's yield as a refinery quoted it: what went in and what it said would
/// come back, under one method at one station.
/// </summary>
/// <remarks>
/// The number the game's files do not hold and nobody publishes. Each is one
/// quote on one evening at one station's load; the page shows them as that.
/// </remarks>
public sealed record RefineryYield(
    DateTimeOffset At,
    string Station,
    string Method,
    string Mineral,
    int? Quality,
    int InCscu,
    int OutCscu);

/// <summary>Everything known about refining, from screenshots and the log.</summary>
/// <param name="Unmatched">
/// Completions the log reported with no screenshot of the order: real orders,
/// but nothing is known of them except where and when they finished.
/// </param>
public sealed record RefineryPicture(
    IReadOnlyList<RefineryOrder> Orders,
    IReadOnlyList<RefineryCompletion> Unmatched,
    IReadOnlyList<RefineryYield> Measured);

/// <summary>
/// Joins refinery screenshots into orders and orders to the log's completions.
/// </summary>
/// <remarks>
/// <para>
/// The two sources answer different halves. A screenshot says what is in an
/// order, what it cost and how long the terminal expected it to take; the log
/// says when it finished, and nothing else. Neither knows the other exists, so
/// they are joined here by station and time.
/// </para>
/// <para>
/// Measured on the one order this install has both halves of: the PROCESSING
/// screen at 23:10:19 local read 6 m 26 s remaining, so due at 23:16:45; the
/// log's completion is stamped 23:16:54. Nine seconds - the terminal's clock
/// is a good timer and the log is the confirmation. That is what
/// <see cref="Grace"/> is sized against.
/// </para>
/// </remarks>
public static class RefineryOrders
{
    /// <summary>
    /// How long past the terminal's due time the log is given to say so before
    /// the page says it instead.
    /// </summary>
    /// <remarks>
    /// The one measured completion came nine seconds after the countdown; a
    /// minute is several times that, and short enough that the timer still
    /// arrives when it is useful.
    /// </remarks>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    /// <summary>
    /// How long after a quote another frame of the same quote is the same
    /// order rather than a second one.
    /// </summary>
    private static readonly TimeSpan SameQuote = TimeSpan.FromMinutes(15);

    public static RefineryPicture Build(IEnumerable<ScreenSighting> readings, IEnumerable<RefineryCompletion> completions)
    {
        var frames = readings
            .Where(r => !r.Dismissed && r.Refinery is { Station: not null, Seconds: > 0 } refinery
                && (refinery.Stage == "processing" || (refinery.Stage == "setup" && (refinery.Method ?? refinery.MethodRead) is not null)))
            .OrderBy(r => r.ShotAt)
            .ToList();

        var orders = new List<RefineryOrder>();

        foreach (var frame in frames)
        {
            var refinery = frame.Refinery!;
            var due = frame.ShotAt.AddSeconds(refinery.Seconds!.Value);
            var running = refinery.Stage == "processing";
            var lots = running ? refinery.Lots : [.. refinery.Lots.Where(l => l.Yield is > 0)];
            var outCscu = lots.Sum(l => l.Yield ?? 0) is > 0 and var sum ? sum : (int?)null;

            // A running frame belongs to the order whose countdown it falls
            // inside - the quote just before it, or an earlier frame of the same
            // run. A quote only joins a quote: a second one priced while the
            // first runs is a second order.
            var index = orders.FindLastIndex(o => SameStation(o.Station, refinery.Station!)
                && o.CompletedAt is null
                && (running
                    ? frame.ShotAt >= o.SeenAt && frame.ShotAt <= (o.DueAt ?? o.SeenAt) + Grace
                    : o.Basis == "quote" && frame.ShotAt - o.SeenAt <= SameQuote
                        && o.Method == refinery.Method && o.Cost == refinery.Cost));

            if (index < 0)
            {
                orders.Add(new RefineryOrder(
                    refinery.Station!, frame.ShotAt, frame.Shot, running ? "running" : "quote", due,
                    refinery.Method ?? refinery.MethodRead, refinery.Cost, refinery.ToRefine, outCscu, lots));
                continue;
            }

            var order = orders[index];

            orders[index] = running
                ? order with
                {
                    Shot = frame.Shot,
                    Basis = "running",
                    DueAt = due,

                    // The running screen lists every lot's yield where the quote
                    // can miss one - it read 15 cSCU of agricium the quote did not.
                    OutCscu = Math.Max(order.OutCscu ?? 0, outCscu ?? 0) is > 0 and var most ? most : null,
                    Lots = order.Lots.Count > 0 ? order.Lots : lots,
                }
                : order with { Shot = frame.Shot, DueAt = due };
        }

        // Each completion closes the open order at its station whose due time
        // it lies nearest, among those seen before it finished.
        var unmatched = new List<RefineryCompletion>();

        foreach (var done in completions.OrderBy(c => c.At))
        {
            var index = orders
                .Select((order, i) => (order, i))
                .Where(x => x.order.CompletedAt is null && x.order.SeenAt <= done.At && SameStation(x.order.Station, done.Station))
                .OrderBy(x => Math.Abs(((x.order.DueAt ?? x.order.SeenAt) - done.At).Ticks))
                .Select(x => (int?)x.i)
                .FirstOrDefault();

            if (index is { } i) orders[i] = orders[i] with { CompletedAt = done.At };
            else unmatched.Add(done);
        }

        var measured = frames
            .Where(f => f.Refinery!.Stage == "setup" && f.Refinery.Method is not null)
            .SelectMany(f => f.Refinery!.Lots
                .Where(l => l.Quantity is > 0 && l.Yield is > 0)
                .Select(l => new RefineryYield(f.ShotAt, f.Refinery.Station!, f.Refinery.Method!,
                    l.Mineral ?? l.Read, l.Quality, l.Quantity!.Value, l.Yield!.Value)))
            .GroupBy(y => (y.Station, y.Method, y.Mineral, y.Quality, y.InCscu, y.OutCscu))
            .Select(g => g.First())
            .ToList();

        return new RefineryPicture(
            [.. orders.OrderByDescending(o => o.SeenAt)],
            [.. unmatched.OrderByDescending(c => c.At)],
            measured);
    }

    /// <summary>
    /// The terminal's title and the log's toast name a station the same way on
    /// the one pair measured; one containing the other also takes a title the
    /// engine read short.
    /// </summary>
    internal static bool SameStation(string a, string b)
    {
        var x = ScreenInsight.Fold(a);
        var y = ScreenInsight.Fold(b);
        return x.Length > 0 && y.Length > 0 && (x.Contains(y, StringComparison.Ordinal) || y.Contains(x, StringComparison.Ordinal));
    }
}
