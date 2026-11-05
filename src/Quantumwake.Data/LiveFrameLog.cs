using System.Globalization;
using System.Text;

namespace Quantumwake.Data;

/// <summary>
/// One line per live frame, so a play session leaves numbers behind: when a
/// frame was taken, what kind of screen it was, and how long copying and
/// reading it took.
/// </summary>
/// <remarks>
/// <para>
/// The status line on the Log page lives in memory and is gone when the app
/// closes; deciding how combat could be read needs an evening's worth of it -
/// how long a read takes at full resolution, how often each kind of screen
/// turns up, how long the game sits in front. This is that, as a CSV a
/// spreadsheet or a script opens.
/// </para>
/// <para>
/// No text read off the screen is written, and no summary: a frame can carry
/// other players' names, and the privacy policy promises that the only thing
/// kept from a live frame is a rock scan. Kinds, times and the app's own
/// waiting reasons are its words, not the screen's.
/// </para>
/// <para>
/// A frame read is a line; a wait is a line only when its reason changes, so
/// an hour alt-tabbed is one line rather than eighteen hundred. A file a day,
/// <c>frames-yyyy-MM-dd.csv</c> in local time - the evening a pilot played is
/// the file they look for - and a week of them kept.
/// </para>
/// </remarks>
public sealed class LiveFrameLog(string folder, Func<DateTimeOffset>? clock = null)
{
    public const string Header = "at,state,kind,capture_ms,read_ms,width,height,kept,why";

    private static readonly TimeSpan KeepFor = TimeSpan.FromDays(7);

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.Now);
    private readonly Lock _gate = new();
    private string? _lastWhy;
    private string? _pruned;

    /// <summary>A frame that was copied and read.</summary>
    public void Read(string kind, long captureMs, long readMs, int width, int height, string? kept)
    {
        lock (_gate)
        {
            _lastWhy = null;
            _active = true;
            Write("read", kind, captureMs, readMs, width, height, kept, null);
        }
    }

    /// <summary>A tick that took no frame, and why; written only when the reason is new.</summary>
    public void Waiting(string? why)
    {
        lock (_gate)
        {
            var reason = why ?? "";
            if (reason == _lastWhy) return;
            _lastWhy = reason;
            _active = true;
            Write("waiting", "", 0, 0, 0, 0, null, reason);
        }
    }

    /// <summary>
    /// Reading was switched off. A line only when it had been going: a pilot
    /// who said no never gets a file at all.
    /// </summary>
    public void Stopped()
    {
        lock (_gate)
        {
            if (!_active) return;
            _active = false;
            _lastWhy = null;
            Write("off", "", 0, 0, 0, 0, null, null);
        }
    }

    /// <summary>Whether anything has been written since reading last stopped.</summary>
    private bool _active;

    /// <summary>The file for the day it is now.</summary>
    public string PathFor(DateTimeOffset at) =>
        Path.Combine(folder, $"frames-{at.ToLocalTime():yyyy-MM-dd}.csv");

    private void Write(string state, string kind, long captureMs, long readMs, int width, int height, string? kept, string? why)
    {
        var now = _clock();
        var path = PathFor(now);

        try
        {
            Directory.CreateDirectory(folder);
            var fresh = !File.Exists(path);
            var line = string.Join(',',
                now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
                state,
                Csv(kind),
                captureMs.ToString(CultureInfo.InvariantCulture),
                readMs.ToString(CultureInfo.InvariantCulture),
                width.ToString(CultureInfo.InvariantCulture),
                height.ToString(CultureInfo.InvariantCulture),
                Csv(kept ?? ""),
                Csv(why ?? ""));
            File.AppendAllText(path, (fresh ? Header + "\n" : "") + line + "\n", Encoding.UTF8);
            Prune(now);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // A line lost is a gap in a diagnostic, never a reason to stop reading.
        }
    }

    /// <summary>Files older than a week go, checked once a day.</summary>
    private void Prune(DateTimeOffset now)
    {
        var today = now.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (_pruned == today) return;
        _pruned = today;

        foreach (var file in Directory.EnumerateFiles(folder, "frames-*.csv"))
        {
            var stem = Path.GetFileNameWithoutExtension(file)["frames-".Length..];
            if (DateTime.TryParseExact(stem, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                && now.ToLocalTime().Date - day.Date > KeepFor)
            {
                try { File.Delete(file); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
            }
        }
    }

    /// <summary>Quoted when it holds a comma, a quote or a line break - a waiting reason can hold all three.</summary>
    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
}
