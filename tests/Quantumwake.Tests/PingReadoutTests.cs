using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The mining radar's signature readout, read and decoded. The figures are
/// ONNX's reads of RisboTheBlade's Riccite video (4.7, 2560x1440); the rows
/// are this install's, the ones those figures decode against.
/// </summary>
public sealed class PingReadoutTests
{
    private static ScreenTextLine L(double left, double top, string text) => new(text, left, top, 18);

    private static readonly List<RadarRow> Rows =
    [
        new(3385, "ship", "Riccite", "Epic", ["Riccite"], true, true),
        new(3570, "ship", "Borase", "Rare", ["Borase"], true, true),
        new(4270, "ship", "Iron", "Common", ["Iron"], true, true),
        new(4000, "asteroid", "I-type", null, ["Ice"], false, true),
        new(4000, "ground", "Ground-vehicle deposit", null, ["Beradom"], true, false),
        new(3000, "hand", "Hand-mining deposit", null, ["Aphorite"], true, false),
    ];

    [Theory]
    [InlineData("10,155", 10155)]
    [InlineData("12.810", 12810)]
    [InlineData("96000", 96000)]
    [InlineData("3,385", 3385)]
    public void The_figure_in_the_box_is_read_with_either_separator(string text, double value) =>
        Assert.Equal(value, PingReadout.Value([L(1283, 500, text), L(1258, 528, "240"), L(1325, 529, "260")]));

    [Fact]
    public void The_compass_headings_under_it_are_not_a_ping() =>
        Assert.Null(PingReadout.Value([L(1258, 528, "240"), L(1325, 529, "260"), L(1341, 529, "780")]));

    [Theory]
    [InlineData(10155, "Three Riccite")]
    [InlineData(17080, "Four Iron")]
    [InlineData(3570, "One Borase")]
    [InlineData(12000, "Three I-type asteroid or Three Ground-vehicle deposit")]
    public void A_ping_is_said_as_its_fewest_rocks(double ping, string said) =>
        Assert.Equal(said, PingReadout.Sentence(new PingReading(ping, PingReadout.Decode(ping, Rows))));

    [Theory]
    [InlineData(47385)]   // no whole number of any one rock
    [InlineData(3386)]    // 3,385 misread by one digit: exact only
    [InlineData(96000)]   // 24 of 4,000 - past the twenty the Radar page reads to
    public void A_figure_that_is_no_whole_number_of_one_rock_says_nothing(double ping) =>
        Assert.Null(PingReadout.Sentence(new PingReading(ping, PingReadout.Decode(ping, Rows))));

    [Fact]
    public void The_patch_sits_over_the_box_at_2560()
    {
        var patch = PingReadout.Patch(2560, 1440);
        Assert.True(patch.Left < 1270 && patch.Left + patch.Width > 1420 && patch.Top < 490 && patch.Top + patch.Height > 515);
    }

    // ---- what is said ----

    private static readonly DateTimeOffset T0 = new(2026, 11, 6, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_ping_is_said_once_two_reads_agree_and_not_again_while_it_stays()
    {
        var watch = new MiningWatch();
        var said = new List<string>();
        foreach (var (v, i) in new double?[] { 10155, 10155, 10155, null, 10155, 10155, 17080, 17080 }.Select((v, i) => (v, i)))
            said.AddRange(watch.SeePing(v, Rows, T0.AddSeconds(i * 2)).Select(c => c.Text));

        Assert.Equal(["Three Riccite", "Four Iron"], said);
        Assert.Equal(17080, watch.Picture().Ping!.Value);
    }

    [Fact]
    public void A_misread_figure_on_one_frame_is_never_said()
    {
        var watch = new MiningWatch();
        var said = new[] { 3385d, 3386, 3385 }.SelectMany((v, i) => watch.SeePing(v, Rows, T0.AddSeconds(i * 2))).ToList();
        Assert.Empty(said);
    }
}
