using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What a downloaded archive is allowed to write into a game install.
/// </summary>
/// <remarks>
/// The one place in this app where a file arrives from the internet and lands
/// in a folder that is not ours. The rule is a whitelist rather than a
/// blacklist, and these tests exist to keep it that way: a release that grows
/// a new file should fail loudly here, not quietly overwrite something.
/// </remarks>
public class StarStringsArchiveTests
{
    private const string Root = @"C:\Games\StarCitizen\LIVE";

    private static string? Target(string entry) => StarStringsArchive.TargetFor(entry, Root);

    [Theory]
    [InlineData("USER.cfg")]
    [InlineData("user.cfg")]
    [InlineData("Data/Localization/english/global.ini")]
    [InlineData("data/localization/english/global.ini")]
    [InlineData(@"Data\Localization\english\global.ini")]
    public void The_two_things_the_mod_is_are_allowed(string entry)
    {
        Assert.NotNull(Target(entry));
        Assert.StartsWith(Root, Target(entry)!);
    }

    [Theory]
    [InlineData("Bin64/StarCitizen.exe")]
    [InlineData("EasyAntiCheat/settings.json")]
    [InlineData("Data/Game.pak")]
    [InlineData("readme.txt")]
    [InlineData("user.cfg.bak")]
    public void Anything_else_is_refused(string entry)
    {
        Assert.Null(Target(entry));
    }

    /// <summary>
    /// The oldest trick in archives: an entry that climbs out of the folder it
    /// is unpacked into. Refused even when it starts inside an allowed prefix.
    /// </summary>
    [Theory]
    [InlineData("../../Windows/System32/evil.dll")]
    [InlineData("Data/Localization/../../../../Windows/System32/evil.dll")]
    [InlineData("Data/Localization/../../Bin64/StarCitizen.exe")]
    [InlineData(@"C:\Windows\System32\evil.dll")]
    [InlineData("/etc/passwd")]
    public void Nothing_escapes_the_game_folder(string entry)
    {
        Assert.Null(Target(entry));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Nothing_at_all_is_not_a_file(string entry)
    {
        Assert.Null(Target(entry));
    }

    [Fact]
    public void A_missing_game_folder_can_never_be_written_to()
    {
        Assert.Null(StarStringsArchive.TargetFor("user.cfg", ""));
    }
}
