using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The first three bytes of the file the game reads at startup.
/// </summary>
/// <remarks>
/// <para>
/// The game's own <c>Data\Localization\english\global.ini</c> is UTF-8 with a
/// byte order mark - so is MrKraken's StarStrings, which is built from it - and
/// the overlay replaces that file wholesale. Whether ours carried the mark used
/// to depend on where the base table was read from, which is not a decision the
/// source should be making: <c>Encoding.UTF8.GetString</c> hands the mark back
/// as a character and it rode through to the output, while
/// <c>File.ReadAllText</c> consumes it and the output had none. The first path
/// is a fresh install with no text mod; the second is every install where
/// StarStrings is present or ours is being re-applied over its own backup.
/// </para>
/// <para>
/// The symptom is not subtle. A localisation file the game will not take is a
/// game with no localised text: labels fall back to the engine identifiers
/// underneath them across the whole UI, the size and grade marks this feature
/// exists to add among them.
/// </para>
/// </remarks>
public class GameTextBomTests
{
    private const string Ini =
        "item_Name_behr_rifle_ballistic_01=P4-AR Rifle\n"
        + "item_Name_gmni_lmg_ballistic_01=F55 LMG";

    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    [Fact]
    public void A_marked_table_is_written_with_the_mark_the_game_writes()
    {
        var plan = TextOverlay.Build(GameText.WithoutBom(Ini), _ => false);
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            File.WriteAllText(path, plan.Content, new UTF8Encoding(true));

            Assert.Equal(Bom, File.ReadAllBytes(path)[..3]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// The p4k path: the decoder hands the mark back as a character, and it must
    /// not become a second one in the output.
    /// </summary>
    [Fact]
    public void A_base_read_out_of_the_archive_does_not_contribute_a_second_mark()
    {
        var raw = Bom.Concat(Encoding.UTF8.GetBytes(Ini)).ToArray();

        var text = GameText.WithoutBom(Encoding.UTF8.GetString(raw));

        Assert.StartsWith("item_Name_behr", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\uFEFF', text);
    }

    /// <summary>
    /// The StarStrings and re-install path: the reader has already eaten the
    /// mark, so normalising has nothing to do and must not damage the text.
    /// </summary>
    [Fact]
    public void A_base_read_through_a_stream_reader_is_left_alone()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        try
        {
            File.WriteAllText(path, Ini, new UTF8Encoding(true));

            var text = GameText.WithoutBom(File.ReadAllText(path));

            Assert.Equal(Ini, text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Both bases now produce the same bytes, which is the whole point: the
    /// file the game gets cannot depend on whether a text mod was installed.
    /// </summary>
    [Fact]
    public void Both_bases_produce_the_same_file()
    {
        var fromArchive = GameText.WithoutBom(
            Encoding.UTF8.GetString(Bom.Concat(Encoding.UTF8.GetBytes(Ini)).ToArray()));

        var fromReader = GameText.WithoutBom(Ini);

        var a = new UTF8Encoding(true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(TextOverlay.Build(fromArchive, _ => false).Content));

        var b = new UTF8Encoding(true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(TextOverlay.Build(fromReader, _ => false).Content));

        Assert.Equal(a, b);
    }

    [Fact]
    public void Text_with_no_mark_is_returned_unchanged()
    {
        Assert.Equal("abc", GameText.WithoutBom("abc"));
        Assert.Equal(string.Empty, GameText.WithoutBom(string.Empty));
    }
}
