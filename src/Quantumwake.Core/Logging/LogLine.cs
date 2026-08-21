using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Quantumwake.Core.Logging;

/// <summary>
/// One parsed line of Game.log, split into its envelope parts.
/// </summary>
/// <param name="Timestamp">UTC timestamp from the leading <c>&lt;...&gt;</c> block.</param>
/// <param name="Severity">First bracket tag, e.g. <c>Notice</c>, <c>Error</c>. Null when absent.</param>
/// <param name="Tag">Angle-bracket event tag, e.g. <c>Legacy login response</c>. Null when absent.</param>
/// <param name="Body">Everything after the tag.</param>
/// <param name="IsSpam">True when a <c>[SPAM nnn]</c> tag was present.</param>
/// <param name="Raw">The original unmodified line.</param>
public sealed record LogLine(
    DateTimeOffset Timestamp,
    string? Severity,
    string? Tag,
    string Body,
    bool IsSpam,
    string Raw);

/// <summary>
/// Parses the common envelope shared by every Game.log line.
/// </summary>
/// <remarks>
/// Three shapes occur in real logs and all three must parse:
/// <code>
/// &lt;2026-08-20T01:28:42.748Z&gt; Log started on Thu Aug 20 01:28:42 2026
/// &lt;2026-08-20T01:28:55.402Z&gt; [Notice] &lt;Legacy login response&gt; [CIG-net] ...
/// &lt;2026-04-27T01:53:07.044Z&gt; [SPAM 299][Notice] &lt;CObjectiveMarker...&gt; ...
/// </code>
/// Note the second form's severity tag is optional (header lines and
/// "Loading screen for ..." carry none), and the third stacks multiple bracket
/// groups. Assuming <c>[Notice]</c> is always present is a common parsing bug.
/// </remarks>
public static partial class LogEnvelope
{
    [GeneratedRegex(
        @"^<(?<ts>[^>]+)>\s*(?<tags>(?:\[[^\]]*\]\s*)*)(?:<(?<tag>[^>]+)>)?\s*(?<body>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex EnvelopeRegex { get; }

    [GeneratedRegex(@"\[([^\]]*)\]", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex BracketRegex { get; }

    /// <summary>
    /// Attempts to parse a raw log line. Returns false for blank lines and any
    /// line lacking a leading timestamp, which callers should skip rather than
    /// treat as an error.
    /// </summary>
    public static bool TryParse(string raw, [NotNullWhen(true)] out LogLine? line)
    {
        line = null;
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var match = EnvelopeRegex.Match(raw);
        if (!match.Success)
            return false;

        if (!DateTimeOffset.TryParse(
                match.Groups["ts"].ValueSpan,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                out var timestamp))
        {
            return false;
        }

        string? severity = null;
        var isSpam = false;

        var tags = match.Groups["tags"].Value;
        if (tags.Length > 0)
        {
            foreach (var bracket in BracketRegex.Matches(tags).Cast<Match>())
            {
                var value = bracket.Groups[1].Value;
                if (value.StartsWith("SPAM", StringComparison.OrdinalIgnoreCase))
                    isSpam = true;
                else
                    severity ??= value;
            }
        }

        var tag = match.Groups["tag"].Success ? match.Groups["tag"].Value : null;

        line = new LogLine(
            timestamp,
            severity,
            tag,
            match.Groups["body"].Value,
            isSpam,
            raw);

        return true;
    }
}
