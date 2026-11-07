namespace Quantumwake.Data;

/// <summary>
/// Decides which live scan-panel readings are kept, and under what name.
/// </summary>
/// <remarks>
/// <para>
/// A live frame arrives every couple of seconds and the panel says the same
/// thing for as long as the rock is selected - but not word for word, because
/// the engine reads a figure one frame and drops it the next. Keeping every
/// frame would fill the reading log, which keeps the newest
/// <see cref="ScreenReadingStore.Keep"/> of everything, with one rock and push
/// out the screenshots the pilot actually took.
/// </para>
/// <para>
/// So a rock gets one reading, named for when it was first seen, and a later
/// frame of the same rock replaces it only when it read more of the panel. A
/// rock is the same rock while its main mineral reads the same and its mass is
/// within 2% - a misread digit moves the mass far more than that, a second rock
/// of the same ore is rarely that close - and while scans keep coming: two
/// minutes without one and the next is a new rock.
/// </para>
/// </remarks>
public sealed class LiveRocks
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(2);

    private MiningScanReading? _rock;
    private string? _shot;
    private int _score;
    private DateTimeOffset _lastSeen;

    /// <summary>
    /// The name to keep this frame's scan under, or null when an earlier frame
    /// of the same rock already read as much.
    /// </summary>
    public string? Keep(MiningScanReading scan, DateTimeOffset at)
    {
        var score = Score(scan);
        var same = _rock is not null && at - _lastSeen <= Gap && SameRock(_rock, scan);
        _lastSeen = at;

        if (!same)
        {
            _rock = scan;
            _score = score;
            _shot = $"live-{at.ToUniversalTime():yyyyMMdd-HHmmss}";
            return _shot;
        }

        if (score <= _score) return null;
        _rock = scan;
        _score = score;
        return _shot;
    }

    private static bool SameRock(MiningScanReading a, MiningScanReading b)
    {
        var mineral = a.Primary ?? a.PrimaryRead;
        var other = b.Primary ?? b.PrimaryRead;
        if (mineral is not null && other is not null && !string.Equals(mineral, other, StringComparison.OrdinalIgnoreCase)) return false;
        if (a.MassKg is { } m && b.MassKg is { } n && m > 0 && Math.Abs(m - n) / m > 0.02) return false;
        return true;
    }

    /// <summary>How much of the panel a reading got: each figure, each share with its mineral, and the shares adding to 100.</summary>
    public static int Score(MiningScanReading scan)
    {
        var score = 0;
        if (scan.Primary is not null) score++;
        if (scan.MassKg is not null) score++;
        if (scan.ResistancePercent is not null) score++;
        if (scan.Instability is not null) score++;
        if (scan.Scu is not null) score++;
        if (scan.Difficulty is not null) score++;
        score += scan.Parts.Count(p => p.Mineral is not null && p.Percent > 0);
        if (scan.ShareTotal is { } total && Math.Abs(total - 100) < 0.05) score += 3;
        return score;
    }
}
