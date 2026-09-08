using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Whether the marks written into the game's text are still there.
/// </summary>
/// <remarks>
/// Both this and StarStrings write the same file, so the second one installed
/// wins. Checking that the path exists cannot tell the difference, because the
/// path exists either way — and the app would go on reporting marks that had
/// been overwritten, which nobody notices until a column stops filling in.
/// </remarks>
public class TextOverlayPresenceTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"qw-present-{Guid.NewGuid():N}");

    private readonly string _table;

    public TextOverlayPresenceTests()
    {
        Directory.CreateDirectory(_directory);
        _table = Path.Combine(_directory, "global.ini");
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
        GC.SuppressFinalize(this);
    }

    private TextOverlayStore Installed(string contents)
    {
        File.WriteAllText(_table, contents);

        var store = new TextOverlayStore(_directory);
        store.Record(new TextOverlayInstall(
            DateTimeOffset.UtcNow, _directory, 12, false,
            [new InstalledFile(_table, null)],
            TextOverlayStore.Fingerprint(_table)));

        return store;
    }

    [Fact]
    public void An_untouched_table_is_still_installed()
    {
        Assert.True(Installed("item_Name_x=Thing [*]").StillPresent());
    }

    /// <summary>
    /// StarStrings installing afterwards writes this very path. The file is
    /// still there and the marks are not.
    /// </summary>
    [Fact]
    public void A_table_another_mod_overwrote_is_not_installed()
    {
        var store = Installed("item_Name_x=Thing [*]");

        File.WriteAllText(_table, "item_Name_x=Thing");

        Assert.True(File.Exists(_table));
        Assert.False(store.StillPresent());
    }

    [Fact]
    public void A_deleted_table_is_not_installed()
    {
        var store = Installed("item_Name_x=Thing [*]");
        File.Delete(_table);

        Assert.False(store.StillPresent());
    }

    /// <summary>
    /// An install recorded by an older build carries no fingerprint. It is
    /// taken at its word rather than declared missing, or upgrading would look
    /// like every install had vanished.
    /// </summary>
    [Fact]
    public void An_install_from_before_fingerprints_is_believed()
    {
        File.WriteAllText(_table, "item_Name_x=Thing [*]");

        var store = new TextOverlayStore(_directory);
        store.Record(new TextOverlayInstall(
            DateTimeOffset.UtcNow, _directory, 12, false, [new InstalledFile(_table, null)]));

        Assert.True(store.StillPresent());
    }
}
