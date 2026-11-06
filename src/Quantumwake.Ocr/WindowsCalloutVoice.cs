using System.IO;
using System.Reflection;
using System.Threading.Channels;
using Quantumwake.Data;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace Quantumwake.Ocr;

/// <summary>
/// Speaks combat callouts: in the recorded voice the app carries when it has
/// the words, in the voice Windows already has when it does not.
/// </summary>
/// <remarks>
/// <para>
/// Spoken by the app itself rather than by a page, because the page may not be
/// open: the overlay is often hidden in a fight, and the MFD frames are not a
/// browser anyone is listening to. Both voices need no download and no key,
/// and play on the default output - the pilot's headset.
/// </para>
/// <para>
/// The recorded voice is embedded in this assembly (<c>Voices/af_bella</c>,
/// see <see cref="VoicePack"/>), because a single-file build has no folder
/// beside it to carry one. Its clips are AAC, which Windows plays without any
/// codec being added, joined in a playback list.
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

    /// <summary>The recorded voice embedded with the app; see <see cref="VoicePack"/>.</summary>
    private const string PackFolder = "Voices/af_bella/";

    private readonly Channel<(string Text, DateTimeOffset At)> _queue =
        Channel.CreateBounded<(string, DateTimeOffset)>(new BoundedChannelOptions(4)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
        });

    private readonly CancellationTokenSource _stop = new();
    private readonly Task _speaking;
    private readonly VoicePack? _pack = VoicePack.Load(Resource("manifest.json"));

    public WindowsCalloutVoice() => _speaking = Task.Run(SpeakAsync);

    public void Say(string text) => _queue.Writer.TryWrite((text, DateTimeOffset.UtcNow));

    public string? Trouble { get; private set; }

    /// <summary>Which voice a sentence would be said in: the pack's name, or "windows" when the pack does not cover it.</summary>
    public string VoiceFor(string text) => _pack?.Clips(text) is not null ? _pack.Voice : "windows";

    private static Stream? Resource(string file) =>
        typeof(WindowsCalloutVoice).Assembly.GetManifestResourceStream(PackFolder + file);

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
                    player ??= new MediaPlayer { AudioCategory = MediaPlayerAudioCategory.Speech };

                    IMediaPlaybackSource source;
                    if (_pack?.Clips(text) is { } clips)
                    {
                        var list = new MediaPlaybackList();
                        foreach (var clip in clips)
                        {
                            var stream = Resource(clip) ?? throw new FileNotFoundException($"voice clip {clip} is missing from the app");
                            list.Items.Add(new MediaPlaybackItem(MediaSource.CreateFromStream(stream.AsRandomAccessStream(), "audio/mp4")));
                        }
                        source = list;
                    }
                    else
                    {
                        synth ??= new SpeechSynthesizer();
                        var spoken = await synth.SynthesizeTextToStreamAsync(text);
                        source = MediaSource.CreateFromStream(spoken, spoken.ContentType);
                    }

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
                        player.Source = source;
                        player.Play();
                        // A callout is a couple of seconds; ten is a voice that never reported back.
                        await Task.WhenAny(ended.Task, Task.Delay(TimeSpan.FromSeconds(10), _stop.Token));
                    }
                    finally
                    {
                        player.MediaEnded -= Done;
                        player.MediaFailed -= Failed;
                        player.Source = null;
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
