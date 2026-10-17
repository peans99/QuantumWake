using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Which text table the game reads, from <c>g_language</c> in <c>user.cfg</c>.
/// </summary>
/// <remarks>
/// The first case is this install's file as StarStrings left it, byte for byte.
/// </remarks>
public class GameLanguageTests
{
    [Theory]
    [InlineData("g_language = english", "english")]
    [InlineData("g_language = french_(france)\r\n", "french_(france)")]
    [InlineData("r_DisplayInfo = 0\ng_language=german_(germany)\n", "german_(germany)")]
    [InlineData("-- g_language = french_(france)\n", "english")]
    [InlineData("", "english")]
    [InlineData(null, "english")]
    [InlineData("g_language = french_(france)\ng_language = english", "english")]
    public void The_last_uncommented_setting_wins_and_english_is_the_default(string? cfg, string expected)
    {
        Assert.Equal(expected, GameLanguage.Parse(cfg));
    }

    [Fact]
    public void A_missing_file_or_root_reads_as_english()
    {
        Assert.Equal("english", GameLanguage.Read(null));
        Assert.Equal("english", GameLanguage.Read(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
    }

    [Fact]
    public void The_file_in_the_install_root_is_read()
    {
        var root = Directory.CreateTempSubdirectory("qw-lang").FullName;
        try
        {
            File.WriteAllText(Path.Combine(root, "user.cfg"), "g_language = french_(france)");
            Assert.Equal("french_(france)", GameLanguage.Read(root));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
