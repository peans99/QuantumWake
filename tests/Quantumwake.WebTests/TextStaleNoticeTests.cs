namespace Quantumwake.WebTests;

/// <summary>
/// The notice for a loose text file that has fallen behind the game.
/// </summary>
/// <remarks>
/// What fixes it depends on whose file it is - reinstalling the labels fills
/// our own, a StarStrings file needs a StarStrings release, and another mod's
/// is not ours to touch - so the notice is tested for saying the right one,
/// and for not nagging once dismissed until the gap changes.
/// </remarks>
public class TextStaleNoticeTests
{
    private static Page Checked(string freshness)
    {
        var page = new Page();
        page.Serve("/api/labels/freshness", freshness);
        page.Do("localStorage.removeItem('qw-text-stale-dismissed'); await checkTextFreshness();");
        return page;
    }

    private const string OursBehind = """{"present":true,"owner":"overlay+StarStrings","missing":521,"sample":["iasi_Patrol_E_title,P"]}""";

    [Fact]
    public void Our_file_behind_the_game_says_how_many_strings_and_that_reinstalling_fixes_it()
    {
        var page = Checked(OursBehind);

        Assert.False(page.Truth("__dom.node('#text-stale').hidden"));
        var detail = page.NodeText("#text-stale-detail");
        Assert.Contains("521 of the game's strings are missing from it", detail);
        Assert.Contains("Reinstalling the item labels adds them back", detail);
    }

    [Fact]
    public void A_starstrings_file_behind_the_game_points_at_starstrings_not_at_the_labels()
    {
        var detail = Checked(OursBehind.Replace("overlay+StarStrings", "StarStrings")).NodeText("#text-stale-detail");

        Assert.Contains("StarStrings release made for this patch", detail);
        Assert.DoesNotContain("Reinstalling the item labels", detail);
    }

    [Fact]
    public void Another_mods_file_is_named_as_not_ours_to_fix()
    {
        Assert.Contains("text mod Quantum Wake did not install",
            Checked(OursBehind.Replace("overlay+StarStrings", "unknown")).NodeText("#text-stale-detail"));
    }

    [Fact]
    public void A_file_that_is_complete_or_absent_raises_nothing()
    {
        Assert.True(Checked("""{"present":true,"owner":"overlay","missing":0,"sample":[]}""").Truth("__dom.node('#text-stale').hidden"));
        Assert.True(Checked("""{"present":false,"owner":"unknown","missing":0,"sample":[]}""").Truth("__dom.node('#text-stale').hidden"));
    }

    /// <summary>"Not now" holds for this gap; a new patch with a different gap is news again.</summary>
    [Fact]
    public void Not_now_holds_until_the_gap_changes()
    {
        var page = Checked(OursBehind);
        page.Do("__dom.node('#text-stale-dismiss').click();");
        Assert.True(page.Truth("__dom.node('#text-stale').hidden"));

        page.Do("await checkTextFreshness();");
        Assert.True(page.Truth("__dom.node('#text-stale').hidden"));

        page.Serve("/api/labels/freshness", OursBehind.Replace("521", "640"));
        page.Do("await checkTextFreshness();");
        Assert.False(page.Truth("__dom.node('#text-stale').hidden"));
    }
}
