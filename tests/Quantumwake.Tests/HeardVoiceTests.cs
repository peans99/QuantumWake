using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The voice remembers what it was asked to say, so the MFD can show it
/// (1.2.28), and still passes every line on to the voice underneath.
/// </summary>
public class HeardVoiceTests
{
    private sealed class Spoken : ICalloutVoice
    {
        public List<string> Said { get; } = [];
        public void Say(string text) => Said.Add(text);
        public string? VoiceFor(string text) => "bella";
        public string? Trouble => "no audio device";
    }

    [Fact]
    public void Every_line_is_kept_newest_first_and_still_said()
    {
        var inner = new Spoken();
        var now = new DateTimeOffset(2026, 11, 11, 20, 0, 0, TimeSpan.Zero);
        var heard = new HeardVoice(inner, () => now);
        heard.Say("Target identified: Gladius");
        now = now.AddSeconds(2);
        heard.Say("Missile inbound");

        Assert.Equal(["Target identified: Gladius", "Missile inbound"], inner.Said);
        Assert.Equal(["Missile inbound", "Target identified: Gladius"], heard.Recent().Select(l => l.Text));
        Assert.Equal(now, heard.Recent()[0].At);
        // What the voice underneath knows is still asked of it.
        Assert.Equal("bella", heard.VoiceFor("x"));
        Assert.Equal("no audio device", heard.Trouble);
    }

    [Fact]
    public void Two_lines_in_the_same_instant_stay_apart()
    {
        // A frame asks for "after the last one I saw"; equal times would hide one.
        var now = new DateTimeOffset(2026, 11, 11, 20, 0, 0, TimeSpan.Zero);
        var heard = new HeardVoice(null, () => now);
        heard.Say("Objective complete");
        heard.Say("Contract complete");
        var recent = heard.Recent();
        Assert.True(recent[0].At > recent[1].At);
    }

    [Fact]
    public void Only_the_last_few_are_kept_and_blanks_not_at_all()
    {
        var heard = new HeardVoice(null);
        heard.Say("   ");
        for (var i = 0; i < 20; i++) heard.Say("line " + i);
        Assert.Equal(HeardVoice.Keep, heard.Recent().Count);
        Assert.Equal("line 19", heard.Recent()[0].Text);
    }
}
