namespace Quantumwake.Data;

/// <summary>
/// Small conversions for the game's own text table.
/// </summary>
/// <remarks>
/// The game's <c>global.ini</c> is UTF-8 with a byte order mark and bare LF
/// endings, and anything written over it has to be the same or it is not the
/// same kind of file. Whether a BOM reached the text used to depend on how it
/// was read: <see cref="System.Text.Encoding.UTF8"/>'s decoder hands the mark
/// back as a character, while <c>File.ReadAllText</c> consumes it. Normalising
/// on the way in means the writer decides, once, rather than the source
/// deciding for it.
/// </remarks>
public static class GameText
{
    private const char Bom = '\uFEFF';

    /// <summary>The text without a leading byte order mark, if it had one.</summary>
    public static string WithoutBom(string text) =>
        text.Length > 0 && text[0] == Bom ? text[1..] : text;
}
