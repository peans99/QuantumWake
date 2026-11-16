namespace Quantumwake.Data;

/// <summary>A rock worked: its charge window, and the scan taken of it when there was one.</summary>
/// <param name="Fracture">The window, from first sight to gone.</param>
/// <param name="Scan">The scan panel read nearest before it, within three minutes; null when none was.</param>
/// <param name="ScannedAt">When that scan was read.</param>
public sealed record RockWorked(FractureRecord Fracture, MiningScanReading? Scan, DateTimeOffset? ScannedAt);

/// <summary>
/// Each charge window the live reader followed, kept for the Sessions debrief
/// (1.2.23): when, how long the charge sat in the window, how often it went
/// over, and whether the rock broke.
/// </summary>
/// <remarks>
/// <para>
/// Between 6 and 9 November 2026 the live reader followed the fracture bar on
/// 119 frames of this install and kept nothing of it: the mining watch held
/// the current bar in memory and said "in the window", "overcharge" and "rock
/// cracked" as they happened. The log has no line for any of it.
/// </para>
/// <para>
/// No text from the screen is in it - the bar is read by its colours alone -
/// so it is kept whenever live reading is on, like the frame log, rather than
/// behind a switch of its own. A scan of the rock is joined when the reading
/// log holds one; the scan was already kept, one per rock, since 1.1.
/// </para>
/// <para>
/// What it cannot say is what the rock refined to. A refinery order names a
/// station and its minerals, never the rocks that went into it, and over a
/// run of several rocks there is nothing to share the yield out by. That join
/// would be a guess, so it is not made.
/// </para>
/// </remarks>
public sealed class RockLog(string folder)
{
    /// <summary>How long before a window a scan still counts as the scan of that rock.</summary>
    public static readonly TimeSpan ScanReach = TimeSpan.FromMinutes(3);

    private readonly DailyJsonLines<FractureRecord> _days = new(folder, r => r.Since);

    /// <summary>
    /// The least crack a window has to show, with no time in the window and no
    /// overcharge, to be a rock worked rather than a yellow box held still.
    /// </summary>
    public const double WorkedFill = 0.05;

    /// <summary>
    /// Whether the charge did anything: cracked, filled some of the bar, sat in
    /// the window or went over it (1.2.55).
    /// </summary>
    /// <remarks>
    /// Every window this install had kept by 16 November 2026 was a phantom.
    /// 31 of the 32 never filled more than 1%, never entered the window and
    /// never went over, and the game's own logs put the pilot in a Hermes, a
    /// Pisces, a Super Hornet and an Ironclad, or on foot - at 02:51 UTC on
    /// the 16th in an Area18 elevator two minutes after spawning - and never
    /// in a mining ship. A yellow box of the bar's size had held still for
    /// three frames on screens filed Unknown, alternating with them, and each
    /// was recorded as a rock let go, so the debrief of a hauling evening said
    /// "rocks worked". (The 32nd, the Fleet Manager's bar from before 1.2.41,
    /// still reads as a crack: nothing in its record tells it apart.) A real charge that never reached the window is lost with them;
    /// it did nothing to the rock either.
    /// </remarks>
    public static bool Worked(FractureRecord window) =>
        window.Cracked || window.Filled >= WorkedFill || window.InWindowSeconds > 0 || window.Overcharges > 0;

    public void Add(FractureRecord window) => _days.Add(window);

    /// <summary>
    /// The windows kept between two times, less any that were never worked:
    /// installs from 1.2.23 to 1.2.54 recorded those, and the files are the
    /// pilot's, so they are passed over when read rather than rewritten.
    /// </summary>
    public IReadOnlyList<FractureRecord> Between(DateTimeOffset from, DateTimeOffset to) =>
        [.. _days.Between(from, to).Where(Worked)];

    public bool Any() => _days.Any();

    /// <summary>
    /// Each window with the scan read nearest before it - from three minutes
    /// before it opened until it closed, since the panel can stay up while
    /// the laser works - and each scan given to one window only, the first.
    /// A second window on the same rock, after a let-go, gets none rather than
    /// the first one's again: one scan is one rock's.
    /// </summary>
    public static IReadOnlyList<RockWorked> WithScans(IEnumerable<FractureRecord> windows, IEnumerable<ScreenSighting> readings)
    {
        var scans = readings
            .Where(r => r.Mining is not null)
            .OrderBy(r => r.ShotAt)
            .ToList();
        var used = new HashSet<string>(StringComparer.Ordinal);
        var worked = new List<RockWorked>();

        foreach (var window in windows.OrderBy(w => w.Since))
        {
            var scan = scans
                .Where(s => !used.Contains(s.Shot))
                .Where(s => s.ShotAt >= window.Since - ScanReach && s.ShotAt <= window.Until)
                .OrderBy(s => Math.Abs((s.ShotAt - window.Since).TotalSeconds))
                .FirstOrDefault();
            if (scan is not null) used.Add(scan.Shot);
            worked.Add(new RockWorked(window, scan?.Mining, scan?.ShotAt));
        }

        return worked;
    }
}
