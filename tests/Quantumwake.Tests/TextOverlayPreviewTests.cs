using Microsoft.Extensions.Logging.Abstractions;
using Quantumwake.Core.Logging;
using Quantumwake.Data;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// What the labels page previews once the labels are already installed.
/// </summary>
/// <remarks>
/// The live table is not the base when ours is the file sitting there - it is
/// this build's own output. Previewing against it showed a second set of marks
/// on every name, so the plan has to be built on what our install displaced.
/// Installing never hit this because it takes itself out first; asking what
/// would change does not, so it reads the backup instead.
/// </remarks>
public class TextOverlayPreviewTests : IDisposable
{
    private readonly string _root =
        Path.Combine(Path.GetTempPath(), $"qw-preview-{Guid.NewGuid():N}");

    private readonly string _table;
    private readonly string _backup;
    private readonly SessionStore _sessions = new(":memory:");

    public TextOverlayPreviewTests()
    {
        var english = Path.Combine(_root, "data", "localization", "english");
        Directory.CreateDirectory(english);
        _table = Path.Combine(english, "global.ini");
        _backup = Path.Combine(_root, "displaced.ini");
    }

    public void Dispose()
    {
        _sessions.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private TextOverlayStatus Status(string live, string displaced, bool record = true)
    {
        File.WriteAllText(_table, live);
        File.WriteAllText(_backup, displaced);

        var store = new TextOverlayStore(_root);
        if (record)
        {
            store.Record(new TextOverlayInstall(
                DateTimeOffset.UtcNow, _root, 1, false,
                [new InstalledFile(_table, _backup)],
                TextOverlayStore.Fingerprint(_table)));
        }

        var service = new TextOverlayService(
            new LogLibrary(_sessions), new ItemLabelStore(_root), new UexData(_root),
            store, new StarStringsStore(_root), NullLogger<TextOverlayService>.Instance);

        return service.Status(new GameInstall("LIVE", _root));
    }

    /// <summary>
    /// Three marked lines are sitting in the game folder and one unmarked line is
    /// what we displaced. A preview built on our own output would count three.
    /// </summary>
    [Fact]
    public void The_preview_is_built_on_what_our_install_displaced()
    {
        var status = Status(
            live: "item_Name_behr_rifle_ballistic_01=P4-AR Rifle [*]\n"
                + "item_Name_behr_rifle_ballistic_02=P6-LR Rifle [*]\n"
                + "item_Name_behr_rifle_ballistic_03=P8-SC Rifle [*]",
            displaced: "item_Name_behr_rifle_ballistic_01=P4-AR Rifle");

        Assert.Equal(1, status.Marked);
        Assert.Equal("the game", status.BaseSource);
    }

    /// <summary>
    /// With nothing of ours installed the base is the game's own table, read out
    /// of Data.p4k rather than off disk - so a folder without one has no plan to
    /// show, and says so instead of previewing an empty one. That is also what
    /// makes the count above meaningful: the loose file is never the base, so the
    /// single marked line can only have come from the backup.
    /// </summary>
    [Fact]
    public void Without_an_install_of_ours_the_loose_file_is_not_the_base()
    {
        var status = Status(
            live: "item_Name_behr_rifle_ballistic_01=P4-AR Rifle\n"
                + "item_Name_behr_rifle_ballistic_02=P6-LR Rifle\n"
                + "item_Name_behr_rifle_ballistic_03=P8-SC Rifle",
            displaced: "item_Name_behr_rifle_ballistic_01=P4-AR Rifle",
            record: false);

        Assert.Equal(0, status.Marked);
        Assert.Contains("data archive is not where this app expects it", status.Problem);
    }
}
