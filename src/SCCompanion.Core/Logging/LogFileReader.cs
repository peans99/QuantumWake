using System.Text;
using SCCompanion.Core.Events;
using SCCompanion.Core.Parsing;

namespace SCCompanion.Core.Logging;

/// <summary>
/// Streams a log file and yields the events it contains.
/// </summary>
/// <remarks>
/// Always opened with <see cref="FileShare.ReadWrite"/> | <see cref="FileShare.Delete"/>:
/// Star Citizen holds an open handle on Game.log while running, and anything
/// stricter fails outright. Reading is streamed line by line - the backup set on
/// a normal install runs to hundreds of megabytes, so nothing is ever buffered
/// whole.
/// </remarks>
public static class LogFileReader
{
    /// <summary>
    /// Opens a log for shared reading, tolerating the game holding the handle.
    /// </summary>
    public static FileStream OpenShared(string path) =>
        new(path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);

    /// <summary>
    /// Reads a whole log file and yields every recognised event in order.
    /// Unparseable lines are skipped silently; that is the normal case for the
    /// overwhelming majority of lines.
    /// </summary>
    /// <param name="path">Log file to read.</param>
    /// <param name="parser">
    /// Optional parser to reuse, so callers can aggregate match statistics across
    /// several files. A fresh one is created when null.
    /// </param>
    public static IEnumerable<GameEvent> ReadEvents(string path, LogEventParser? parser = null)
    {
        parser ??= new LogEventParser();

        foreach (var entry in ReadEntries(ReadLines(path)))
        {
            if (!LogEnvelope.TryParse(entry, out var line))
                continue;

            var ev = parser.Parse(line);
            if (ev is not null)
                yield return ev;
        }
    }

    /// <summary>
    /// Joins physical lines into logical entries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some log entries embed newlines - notably HUD notifications carrying chat
    /// text - so one entry can span several physical lines:
    /// </para>
    /// <code>
    /// &lt;...285Z&gt; [Notice] &lt;SHUDEvent_OnNotification&gt; Added notification "You have joined channel 'Origin 325a : nekron'.
    /// &lt;...285Z&gt; : " [4] to queue. New queue size: 1, MissionId: [...], ObjectiveId: []
    /// </code>
    /// <para>
    /// The continuation carries its own (identical) timestamp, so a timestamp is
    /// no help in telling the two apart. The reliable signal is an odd number of
    /// double quotes in the pending entry, meaning it stopped mid-string. That
    /// holds across the whole format: complete entries always balance their
    /// quotes. On a real 400 MB backup set this recovered over a thousand
    /// notifications that were otherwise dropped.
    /// </para>
    /// </remarks>
    public static IEnumerable<string> ReadEntries(IEnumerable<string> lines)
    {
        const int maxContinuations = 32;

        string? pending = null;
        var continuations = 0;

        foreach (var raw in lines)
        {
            if (pending is not null && continuations < maxContinuations && HasUnterminatedQuote(pending))
            {
                pending = string.Concat(pending, " ", StripTimestamp(raw).Trim());
                continuations++;
                continue;
            }

            if (pending is not null)
                yield return pending;

            pending = raw;
            continuations = 0;
        }

        if (pending is not null)
            yield return pending;
    }

    /// <summary>True when a line contains an odd number of double quotes.</summary>
    internal static bool HasUnterminatedQuote(string line)
    {
        var count = 0;
        foreach (var c in line)
        {
            if (c == '"')
                count++;
        }

        return (count & 1) == 1;
    }

    /// <summary>Removes a leading <c>&lt;timestamp&gt;</c> from a continuation line.</summary>
    internal static string StripTimestamp(string line)
    {
        if (line.Length == 0 || line[0] != '<')
            return line;

        var close = line.IndexOf('>');
        return close < 0 ? line : line[(close + 1)..];
    }

    /// <summary>Streams raw lines from a log file without loading it into memory.</summary>
    public static IEnumerable<string> ReadLines(string path)
    {
        using var stream = OpenShared(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        while (reader.ReadLine() is { } line)
            yield return line;
    }

    /// <summary>
    /// Reads from a byte offset to the end of the file, returning the new offset.
    /// Used by the live tailer to resume without re-reading what it has seen.
    /// </summary>
    /// <remarks>
    /// A file shorter than the stored offset means the log rotated, so the caller
    /// should reset to zero. That is signalled by <paramref name="rotated"/>.
    /// </remarks>
    public static List<string> ReadFrom(string path, ref long offset, out bool rotated)
    {
        rotated = false;
        var lines = new List<string>();

        using var stream = OpenShared(path);

        if (stream.Length < offset)
        {
            rotated = true;
            offset = 0;
        }

        if (stream.Length == offset)
            return lines;

        stream.Seek(offset, SeekOrigin.Begin);

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false);
        while (reader.ReadLine() is { } line)
            lines.Add(line);

        offset = stream.Length;
        return lines;
    }
}
