using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Undoing a label install, including when it cannot be undone.
/// </summary>
/// <remarks>
/// Two ways this goes wrong and both leave somebody's game folder changed. A
/// restore that throws must not be followed by forgetting the record, or the
/// only thing that knew how to undo it is gone. And a file another mod has since
/// replaced must not be restored over, or removing these marks would quietly
/// uninstall theirs.
/// </remarks>
public class TextOverlayRemovalTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-remove-{Guid.NewGuid():N}");

    private readonly string _table;
    private readonly string _backup;

    public TextOverlayRemovalTests()
    {
        Directory.CreateDirectory(_directory);
        _table = Path.Combine(_directory, "global.ini");
        _backup = Path.Combine(_directory, "backup.ini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private TextOverlayStore Installed(string written, string displaced)
    {
        File.WriteAllText(_backup, displaced);
        File.WriteAllText(_table, written);

        var store = new TextOverlayStore(_directory);
        store.Record(new TextOverlayInstall(
            DateTimeOffset.UtcNow, _directory, 12, true,
            [new InstalledFile(_table, _backup)],
            TextOverlayStore.Fingerprint(_table)));

        return store;
    }

    /// <summary>
    /// The record is what remembers the backup, so a store that still claims an
    /// install is the only reason a failed removal can be retried.
    /// </summary>
    [Fact]
    public void A_record_is_what_makes_a_second_attempt_possible()
    {
        var store = Installed("marked", "original");

        Assert.NotNull(store.Current);
        Assert.True(store.StillPresent());
    }

    /// <summary>
    /// StarStrings installing over these marks replaces the file. The backup
    /// describes what was under OUR file, which is no longer under theirs.
    /// </summary>
    [Fact]
    public void A_file_another_mod_replaced_is_no_longer_ours_to_restore()
    {
        var store = Installed("marked", "original");

        File.WriteAllText(_table, "somebody else's mod");

        Assert.False(store.StillPresent());
    }
}
