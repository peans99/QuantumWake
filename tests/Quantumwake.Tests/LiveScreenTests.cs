using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Live screen reading: asked once, off until a yes, nothing of the screen kept
/// but one reading a rock.
/// </summary>
public sealed class LiveScreenTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-live-" + Guid.NewGuid().ToString("N"));
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;
    private readonly ScreenReadingStore _readings;

    public LiveScreenTests()
    {
        Directory.CreateDirectory(_root);
        _library = new LogLibrary(_store);
        _readings = new ScreenReadingStore(Path.Combine(_root, "data"));
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    // ---- the answer ----

    [Fact]
    public void A_new_install_has_not_been_asked_and_is_off()
    {
        var live = new LiveScreenStore(_root).Current;
        Assert.False(live.Asked);
        Assert.False(live.Enabled);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Either_answer_is_kept_and_counts_as_asked(bool yes)
    {
        new LiveScreenStore(_root).Answer(yes);

        var again = new LiveScreenStore(_root).Current;
        Assert.True(again.Asked);
        Assert.Equal(yes, again.Enabled);
        Assert.NotNull(again.AnsweredAt);
    }

    [Fact]
    public void An_answer_saved_before_the_kinds_existed_speaks_both()
    {
        File.WriteAllText(Path.Combine(_root, "live-screen.json"), """{"Asked":true,"Enabled":true,"Voice":true}""");
        var live = new LiveScreenStore(_root).Current;
        Assert.True(live.SpeaksCombat);
        Assert.True(live.SpeaksMining);
    }

    [Fact]
    public void Mining_can_be_quieted_without_quieting_combat()
    {
        var store = new LiveScreenStore(_root);
        store.Answer(true);
        store.SetVoice(true, mining: false);

        var again = new LiveScreenStore(_root).Current;
        Assert.True(again.SpeaksCombat);
        Assert.False(again.SpeaksMining);

        // Off and on again keeps each kind as it was.
        store.SetVoice(false);
        Assert.False(store.Current.SpeaksCombat);
        store.SetVoice(true);
        Assert.True(store.Current.SpeaksCombat);
        Assert.False(store.Current.SpeaksMining);
    }

    /// <summary>A file that says on without saying it was asked is not an answer anybody gave.</summary>
    [Fact]
    public void On_without_asked_reads_as_off()
    {
        File.WriteAllText(Path.Combine(_root, "live-screen.json"), """{"Asked":false,"Enabled":true}""");
        var live = new LiveScreenStore(_root).Current;
        Assert.False(live.Enabled);
        Assert.False(live.Asked);
    }

    [Fact]
    public void An_unreadable_answer_is_asked_again()
    {
        File.WriteAllText(Path.Combine(_root, "live-screen.json"), "{not json");
        Assert.False(new LiveScreenStore(_root).Current.Asked);
    }

    // ---- when the dashboard asks ----

    [Fact]
    public void The_question_is_due_where_the_screen_can_be_read_and_nobody_has_answered()
    {
        var store = new LiveScreenStore(_root);
        var service = Service(store, new FakeCapture(), new Lines());

        var state = service.Describe(store);
        Assert.True(state.Available);
        Assert.True(state.Ask);

        store.Answer(false);
        Assert.False(service.Describe(store).Ask);
    }

    /// <summary>The server on its own, or on Linux: no question, and the switch says why.</summary>
    [Fact]
    public void A_copy_that_cannot_capture_never_asks()
    {
        var store = new LiveScreenStore(_root);
        var state = Service(store, null, new Lines()).Describe(store);

        Assert.False(state.Available);
        Assert.False(state.Ask);
        Assert.Contains("Windows app", state.Unavailable);
    }

    [Fact]
    public void A_copy_with_no_text_engine_never_asks()
    {
        var store = new LiveScreenStore(_root);
        var state = Service(store, new FakeCapture(), null).Describe(store);

        Assert.False(state.Ask);
        Assert.Contains("text engine", state.Unavailable);
    }

    private LiveScreenService Service(LiveScreenStore store, IGameScreenCapture? capture, IScreenReader? reader) =>
        new(store, new ScreenInsightService(_library, _readings, reader), _readings, NullLogger<LiveScreenService>.Instance, capture);

    // ---- reading a frame ----

    /// <summary>The live loop decides what is kept; reading a frame keeps nothing by itself.</summary>
    [Fact]
    public async Task Reading_a_live_frame_stores_nothing()
    {
        var frame = Path.Combine(_root, "frame.bmp");
        File.WriteAllBytes(frame, [0x42, 0x4D]);
        var insight = new ScreenInsightService(_library, _readings, new Lines("JUST SOME TEXT"));

        var sighting = await insight.ReadLiveAsync(frame, "live-20261105-120000", DateTimeOffset.UtcNow);

        Assert.Equal("live-20261105-120000", sighting.Shot);
        Assert.Empty(_readings.All());
    }

    /// <summary>
    /// A screenshot the engine could not open is still not stored, so the
    /// watcher gives a file the game is still writing another look.
    /// </summary>
    [Fact]
    public async Task A_screenshot_the_engine_could_not_open_is_still_not_stored()
    {
        var shot = Path.Combine(_root, "ScreenShot-x.jpg");
        File.WriteAllBytes(shot, [0xFF, 0xD8]);
        var insight = new ScreenInsightService(_library, _readings, new Broken());

        var sighting = await insight.ReadShotAsync(shot);

        Assert.Contains("could not read", sighting.Summary);
        Assert.Empty(_readings.All());
    }

    [Fact]
    public async Task A_screenshot_that_read_is_stored_as_before()
    {
        var shot = Path.Combine(_root, "ScreenShot-y.jpg");
        File.WriteAllBytes(shot, [0xFF, 0xD8]);
        var insight = new ScreenInsightService(_library, _readings, new Lines("JUST SOME TEXT"));

        await insight.ReadShotAsync(shot);

        Assert.Single(_readings.All());
        Assert.True(_readings.Has("ScreenShot-y.jpg"));
    }

    // ---- one reading a rock ----

    private static MiningScanReading Rock(string mineral, double mass, int figures, bool sharesAddUp = false)
    {
        var parts = sharesAddUp
            ? new List<MiningScanPart> { new(mineral, mineral, 60, 500), new("Inert", "Inert Materials", 40, null) }
            : new List<MiningScanPart> { new(mineral, mineral, 60, 500) };
        return new MiningScanReading(mineral, mineral, mass,
            figures > 0 ? 12 : null, figures > 1 ? 30 : null, figures > 2 ? 4.2 : null, figures > 3 ? "Easy" : null, parts);
    }

    [Fact]
    public void The_first_frame_of_a_rock_is_kept_under_when_it_was_seen()
    {
        var at = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("live-20261105-120000", new LiveRocks().Keep(Rock("Quantanium", 8000, 2), at));
    }

    [Fact]
    public void The_same_rock_again_is_not_kept_unless_it_read_more()
    {
        var rocks = new LiveRocks();
        var at = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var name = rocks.Keep(Rock("Quantanium", 8000, 2), at);

        Assert.Null(rocks.Keep(Rock("Quantanium", 8000, 2), at.AddSeconds(2)));
        Assert.Null(rocks.Keep(Rock("Quantanium", 8001, 1), at.AddSeconds(4)));
        Assert.Equal(name, rocks.Keep(Rock("Quantanium", 8000, 4, sharesAddUp: true), at.AddSeconds(6)));
    }

    [Fact]
    public void Another_mineral_or_a_mass_far_off_is_another_rock()
    {
        var rocks = new LiveRocks();
        var at = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var first = rocks.Keep(Rock("Quantanium", 8000, 2), at);

        var second = rocks.Keep(Rock("Agricium", 8000, 2), at.AddSeconds(2));
        Assert.NotNull(second);
        Assert.NotEqual(first, second);

        var third = rocks.Keep(Rock("Agricium", 9000, 2), at.AddSeconds(4));
        Assert.NotNull(third);
        Assert.NotEqual(second, third);
    }

    [Fact]
    public void Two_minutes_without_a_scan_and_the_next_is_another_rock()
    {
        var rocks = new LiveRocks();
        var at = new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero);
        var first = rocks.Keep(Rock("Quantanium", 8000, 2), at);
        var later = rocks.Keep(Rock("Quantanium", 8000, 2), at.AddMinutes(3));

        Assert.NotNull(later);
        Assert.NotEqual(first, later);
    }

    // ---- the frame on disk ----

    [Fact]
    public void A_bitmap_has_the_header_every_reader_expects_and_padded_rows()
    {
        var path = Path.Combine(_root, "f.bmp");
        const int width = 3, height = 2;
        Assert.Equal(12, BmpFile.Stride(width));

        var pixels = new byte[BmpFile.Stride(width) * height];
        pixels[0] = 255;
        BmpFile.Write(path, width, height, pixels);

        var bytes = File.ReadAllBytes(path);
        Assert.Equal(54 + 24, bytes.Length);
        Assert.Equal((byte)'B', bytes[0]);
        Assert.Equal((byte)'M', bytes[1]);
        Assert.Equal(bytes.Length, BitConverter.ToInt32(bytes, 2));
        Assert.Equal(54, BitConverter.ToInt32(bytes, 10));
        Assert.Equal(width, BitConverter.ToInt32(bytes, 18));
        Assert.Equal(height, BitConverter.ToInt32(bytes, 22));
        Assert.Equal(24, BitConverter.ToInt16(bytes, 28));
        Assert.Equal(255, bytes[54]);
        Assert.False(File.Exists(path + ".part"));
    }

    /// <summary>Exclusive fullscreen copies as pure black; a dark sky with a lit panel does not.</summary>
    [Fact]
    public void Only_a_frame_with_nothing_drawn_is_blank()
    {
        var pixels = new byte[BmpFile.Stride(640) * 360];
        Assert.True(BmpFile.LooksBlank(640, 360, pixels));

        pixels[BmpFile.Stride(640) * 200 + 300 * 3] = 3;
        Assert.False(BmpFile.LooksBlank(640, 360, pixels));
    }

    private sealed class FakeCapture : IGameScreenCapture
    {
        public GameCapture Capture(string path) => GameCapture.Not("Star Citizen is not running");
    }

    private sealed class Lines(params string[] text) : IScreenReader
    {
        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<ScreenTextLine>>(
                [.. text.Select((t, i) => new ScreenTextLine(t, 10, 10 + i * 20, 16))]);

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
            string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<ScreenTextLine>>([]);
    }

    private sealed class Broken : IScreenReader
    {
        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default) =>
            throw new IOException("half written");

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
            string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default) =>
            throw new IOException("half written");
    }
}
