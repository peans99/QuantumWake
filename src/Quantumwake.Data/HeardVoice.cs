namespace Quantumwake.Data;

/// <summary>One line the voice was asked to say, and when.</summary>
public sealed record HeardLine(DateTimeOffset At, string Text);

/// <summary>
/// The voice, remembering the last few lines it was asked to say, so the MFD
/// can put each one up on the glass as it is spoken (1.2.28).
/// </summary>
/// <remarks>
/// Kept here, at the one place every sentence passes - the screen watches'
/// callouts, the mission toasts, the copilot's replies, a "Hear it" press -
/// rather than in each of them, so nothing spoken can miss the frame by
/// coming from a speaker nobody thought to wire up. A line is kept when it is
/// asked for, not when it plays: the voice drops a line it is too busy for,
/// and the frame showing it anyway is the better half of that miss. With no
/// voice underneath (the server on its own) it keeps nothing, because nothing
/// asks it to say anything.
/// </remarks>
public sealed class HeardVoice(ICalloutVoice? inner, Func<DateTimeOffset>? clock = null) : ICalloutVoice
{
    /// <summary>How many lines are kept. A frame asks every second and a half, so a few covers a burst.</summary>
    public const int Keep = 8;

    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);
    private readonly Lock _gate = new();
    private readonly LinkedList<HeardLine> _lines = new();

    /// <summary>The voice underneath, or null when this copy has none.</summary>
    public ICalloutVoice? Inner => inner;

    /// <inheritdoc/>
    public void Say(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            lock (_gate)
            {
                // Two lines in the same tick would read as one to a frame that
                // asks for "after the last one I saw": keep them apart.
                var at = _clock();
                if (_lines.First is { } last && at <= last.Value.At) at = last.Value.At.AddMilliseconds(1);
                _lines.AddFirst(new HeardLine(at, text.Trim()));
                while (_lines.Count > Keep) _lines.RemoveLast();
            }
        }
        inner?.Say(text);
    }

    /// <inheritdoc/>
    public string? VoiceFor(string text) => inner?.VoiceFor(text);

    /// <inheritdoc/>
    public string? Trouble => inner?.Trouble;

    /// <summary>The last few lines asked for, newest first.</summary>
    public IReadOnlyList<HeardLine> Recent()
    {
        lock (_gate) return [.. _lines];
    }
}
