using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// An engine that cannot read at all leaves the screenshots unread.
/// </summary>
/// <remarks>
/// The ONNX reader fetches its models on first use, so switching screenshots
/// on while offline is a failure of the engine and not of any frame. Filed
/// as a per-frame refusal, every archive shot it touched was marked
/// unreadable and never offered again.
/// </remarks>
public sealed class ScreenEngineUnavailableTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "qw-engine-" + Guid.NewGuid().ToString("N"));
    private readonly string _install;
    private readonly SessionStore _store = new(":memory:");
    private readonly LogLibrary _library;
    private readonly ScreenReadingStore _readings;
    private readonly ScreenInsightService _insight;

    public ScreenEngineUnavailableTests()
    {
        _install = Path.Combine(_root, "LIVE");
        var folder = Screenshots.FolderFor(_install);
        Directory.CreateDirectory(folder);

        foreach (var name in new[] { "ScreenShot-a.jpg", "ScreenShot-b.jpg" })
        {
            var shot = Path.Combine(folder, name);
            File.WriteAllBytes(shot, [0xFF, 0xD8, 0xFF]);
            File.SetLastWriteTimeUtc(shot, DateTime.UtcNow.AddHours(-1));
        }

        _library = new LogLibrary(_store);
        _readings = new ScreenReadingStore(Path.Combine(_root, "data"));
        _insight = new ScreenInsightService(_library, _readings, new Offline());
    }

    public void Dispose()
    {
        _store.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { }
    }

    [Fact]
    public async Task Reading_the_archive_stores_nothing_and_says_why()
    {
        Assert.Equal(2, _insight.Unread(_install).Count);

        await Assert.ThrowsAsync<ScreenEngineUnavailableException>(() => _insight.ReadOlderAsync(_install, 40));

        Assert.Empty(_readings.All());
        Assert.Equal(2, _insight.Unread(_install).Count);
    }

    [Fact]
    public async Task A_scan_answers_with_the_reason_and_names_no_shot()
    {
        var sighting = await _insight.ScanNewestAsync(_install);

        Assert.Equal("", sighting.Shot);
        Assert.Equal(Offline.Why, sighting.Summary);
        Assert.Empty(_readings.All());
    }

    [Fact]
    public async Task One_shot_read_lets_the_reason_through()
    {
        var shot = Path.Combine(Screenshots.FolderFor(_install), "ScreenShot-a.jpg");

        await Assert.ThrowsAsync<ScreenEngineUnavailableException>(() => _insight.ReadShotAsync(shot));

        Assert.Empty(_readings.All());
    }

    private sealed class Offline : IScreenReader
    {
        public const string Why = "models could not be downloaded";

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default) =>
            throw new ScreenEngineUnavailableException(Why);

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(
            string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default) =>
            throw new ScreenEngineUnavailableException(Why);
    }
}
