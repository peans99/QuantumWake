using System.Threading.Channels;
using Quantumwake.Data;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;

namespace Quantumwake.Ocr;

/// <summary>
/// Speaks combat callouts with the voice Windows already has.
/// </summary>
/// <remarks>
/// <para>
/// Spoken by the app itself rather than by a page, because the page may not be
/// open: the overlay is often hidden in a fight, and the MFD frames are not a
/// browser anyone is listening to. Windows' own speech needs no download and
/// no key, and plays on the default output - the pilot's headset.
/// </para>
/// <para>
/// One at a time, and a callout that has waited more than three seconds
/// behind another is dropped: "hostile" said after the fight moved on is worse
/// than not said.
/// </para>
/// </remarks>
public sealed class WindowsCalloutVoice : ICalloutVoice, IDisposable
{
    private static readonly TimeSpan Stale = TimeSpan.FromSeconds(3);

    private readonly Channel<(string Text, DateTimeOffset At)> _queue =
        Channel.CreateBounded<(string, DateTimeOffset)>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly CancellationTokenSource _stop = new();
    private readonly Task _speaking;

    public WindowsCalloutVoice() => _speaking = Task.Run(SpeakAsync);

    public void Say(string text) => _queue.Writer.TryWrite((text, DateTimeOffset.UtcNow));

    public string? Trouble { get; private set; }

    private async Task SpeakAsync()
    {
        SpeechSynthesizer? synth = null;
        MediaPlayer? player = null;

        try
        {
            await foreach (var (text, at) in _queue.Reader.ReadAllAsync(_stop.Token))
            {
                if (DateTimeOffset.UtcNow - at > Stale) continue;

                try
                {
                    synth ??= new SpeechSynthesizer();
                    player ??= new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Speech };

                    using var stream = await synth.SynthesizeTextToStreamAsync(text);
                    var ended = new TaskCompletionSource();
                    void Done(MediaPlayer p, object o)
                    {
                        Trouble = null;
                        ended.TrySetResult();
                    }
                    void Failed(MediaPlayer p, MediaPlayerFailedEventArgs e)
                    {
                        Trouble = $"the last callout could not be played ({e.Error}: {e.ErrorMessage})";
                        ended.TrySetResult();
                    }

                    player.MediaEnded += Done;
                    player.MediaFailed += Failed;
                    try
                    {
                        player.Source = MediaSource.CreateFromStream(stream, stream.ContentType);
                        player.Play();
                        // A callout is a couple of seconds; ten is a voice that never reported back.
                        await Task.WhenAny(ended.Task, Task.Delay(TimeSpan.FromSeconds(10), _stop.Token));
                    }
                    finally
                    {
                        player.MediaEnded -= Done;
                        player.MediaFailed -= Failed;
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    // A machine with no voice installed, or no audio device: the
                    // callout is still on the page, which is where it is kept.
                    Trouble = $"the last callout was not spoken ({e.GetType().Name}: {e.Message})";
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
        finally
        {
            player?.Dispose();
            synth?.Dispose();
        }
    }

    public void Dispose()
    {
        _stop.Cancel();
        _queue.Writer.TryComplete();
        try { _speaking.Wait(TimeSpan.FromSeconds(2)); } catch (AggregateException) { }
        _stop.Dispose();
    }
}
