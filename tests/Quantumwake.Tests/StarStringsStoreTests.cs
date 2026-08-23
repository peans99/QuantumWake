using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The record of what was written into somebody else's game folder.
/// </summary>
/// <remarks>
/// This is the only thing in the app that writes outside its own directory, so
/// the record of it has to be exact: an install is a list of specific files
/// with specific origins, and removing has to be able to undo precisely that
/// and nothing else.
/// </remarks>
public class StarStringsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(
        Path.GetTempPath(), "qw-starstrings-" + Guid.NewGuid().ToString("N")[..8]);

    private StarStringsStore Store() => new(_folder);

    private StarStringsInstall Install(params InstalledFile[] files) =>
        new("SC LIVE Build (release-2026-07-22-efdf111)",
            DateTimeOffset.Parse("2026-07-22T07:31:01Z"),
            DateTimeOffset.UtcNow,
            @"C:\Games\StarCitizen\LIVE",
            files);

    [Fact]
    public void Nothing_is_installed_until_something_is_recorded()
    {
        Assert.Null(Store().Current);
        Assert.False(Store().StillPresent());
    }

    [Fact]
    public void An_install_survives_a_restart()
    {
        var file = Path.Combine(_folder, "global.ini");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(file, "x");

        Store().Record(Install(new InstalledFile(file, null)));

        var reopened = Store().Current;

        Assert.Equal("SC LIVE Build (release-2026-07-22-efdf111)", reopened?.Release);
        Assert.Equal(file, reopened?.Files.Single().Path);
    }

    /// <summary>
    /// A game patch can put the original localisation back without telling
    /// anyone, and the page must say "not installed" rather than believing its
    /// own record.
    /// </summary>
    [Fact]
    public void A_file_that_is_gone_is_not_installed_any_more()
    {
        var file = Path.Combine(_folder, "gone.ini");
        Directory.CreateDirectory(_folder);
        File.WriteAllText(file, "x");

        var store = Store();
        store.Record(Install(new InstalledFile(file, null)));
        Assert.True(store.StillPresent());

        File.Delete(file);

        Assert.False(store.StillPresent());
        Assert.NotNull(store.Current);
    }

    [Fact]
    public void Forgetting_leaves_no_record_behind()
    {
        var store = Store();
        store.Record(Install(new InstalledFile(Path.Combine(_folder, "a.ini"), null)));
        store.Forget();

        Assert.Null(store.Current);
        Assert.Null(Store().Current);
    }

    /// <summary>Displaced files are kept in our folder, never in the game's.</summary>
    [Fact]
    public void Backups_live_beside_our_own_data()
    {
        Assert.StartsWith(_folder, Store().BackupRoot);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
            Directory.Delete(_folder, recursive: true);
    }
}
