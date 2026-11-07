using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>The recorded callout voice: a sentence is said from clips only when every word of it was recorded.</summary>
public sealed class VoicePackTests
{
    private static VoicePack Pack() => VoicePack.Load(new MemoryStream(Encoding.UTF8.GetBytes("""
        {
          "voice": "af_bella",
          "phrases": {
            "Hostile target identified": "p_hti.m4a", "Target identified": "p_ti.m4a", "Target disabled": "p_td.m4a",
            "Hostile contact, likely": "p_hcl.m4a", "Unknown contact, likely": "p_ucl.m4a",
            "Hostile contact": "p_hc.m4a", "Unknown contact": "p_uc.m4a", "Hostile": "p_h.m4a", "likely": "p_l.m4a", "or": "p_or.m4a"
          },
          "names": { "Vanduul Blade": "n_vb.m4a", "Anvil Paladin": "n_ap.m4a", "Aegis Idris-P": "n_ip.m4a", "RSI Perseus": "n_rp.m4a" }
        }
        """)))!;

    [Theory]
    [InlineData("Target identified: Vanduul Blade", "p_ti.m4a n_vb.m4a")]
    [InlineData("Hostile contact, likely Anvil Paladin", "p_hcl.m4a n_ap.m4a")]
    [InlineData("Unknown contact, Anvil Paladin or RSI Perseus", "p_uc.m4a n_ap.m4a p_or.m4a n_rp.m4a")]
    [InlineData("Hostile target identified: Aegis Idris-P", "p_hti.m4a n_ip.m4a")]
    [InlineData("Hostile Vanduul Blade", "p_h.m4a n_vb.m4a")]
    [InlineData("Target disabled", "p_td.m4a")]
    public void Every_callout_the_watch_makes_is_said_from_its_clips(string sentence, string clips) =>
        Assert.Equal(clips.Split(' '), Pack().Clips(sentence));

    [Fact]
    public void A_hull_the_pack_never_recorded_leaves_the_whole_sentence_to_windows()
    {
        Assert.Null(Pack().Clips("Target identified: Some Future Ship"));
        Assert.Equal("af_bella", Pack().Voice);
    }

    [Fact]
    public void No_manifest_or_a_broken_one_is_no_pack()
    {
        Assert.Null(VoicePack.Load(null));
        Assert.Null(VoicePack.Load(new MemoryStream(Encoding.UTF8.GetBytes("not json"))));
    }
}
