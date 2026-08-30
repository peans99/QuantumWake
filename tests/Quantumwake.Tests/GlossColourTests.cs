using Quantumwake.Core.GameData;
using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Colouring the gear nothing sells.
/// </summary>
/// <remarks>
/// The game's text renderer honours emphasis tags — StarStrings ships
/// <c>&lt;EM3&gt;[!]&lt;/EM3&gt;</c> on eight contraband commodities and
/// <c>&lt;EM4&gt;</c> on 958 contract titles. What is not established is
/// whether an item name renders them, since CIG never do it and StarStrings
/// uses plain prefixes there. So this is off unless asked for, and it must be
/// possible to turn back off.
/// </remarks>
public class GlossColourTests
{
    private const string Ini = "item_Name_behr_rifle_ballistic_01=P4-AR Rifle";

    private static TextOverlayPlan Built(TextOverlayOptions options) =>
        TextOverlay.Build(Ini, _ => false, new Dictionary<string, GameItem>(), options);

    [Fact]
    public void Colour_is_off_unless_it_is_asked_for()
    {
        Assert.DoesNotContain("<EM", TextOverlay.Build(Ini, _ => false).Content);
        Assert.DoesNotContain("<EM", Built(new TextOverlayOptions()).Content);
    }

    [Fact]
    public void The_chosen_level_is_the_one_written()
    {
        Assert.Contains("<EM4>P4-AR Rifle [*]</EM4>", Built(new TextOverlayOptions(true, 4)).Content);
    }

    /// <summary>
    /// Turning it off must leave the file exactly as it was without it, not
    /// merely stop adding more.
    /// </summary>
    [Fact]
    public void Turning_it_off_puts_the_name_back()
    {
        var on = Built(new TextOverlayOptions(true, 3)).Content;
        var off = Built(new TextOverlayOptions(false, 3)).Content;

        Assert.Contains("<EM3>", on);
        Assert.DoesNotContain("<EM", off);
        Assert.Equal(TextOverlay.Build(Ini, _ => false).Content, off);
    }

    /// <summary>
    /// Only five levels exist, so a number outside them is brought back rather
    /// than written into somebody's game folder as a tag that means nothing.
    /// </summary>
    [Theory]
    [InlineData(0, "<EM1>")]
    [InlineData(9, "<EM5>")]
    [InlineData(-3, "<EM1>")]
    public void A_level_that_does_not_exist_is_clamped(int asked, string expected)
    {
        Assert.Contains(expected, Built(new TextOverlayOptions(true, asked)).Content);
    }

    /// <summary>
    /// The colour marks the same thing the star does, so an item something
    /// sells must not be coloured either.
    /// </summary>
    [Fact]
    public void An_item_something_sells_is_never_coloured()
    {
        var plan = TextOverlay.Build(
            Ini, _ => true, new Dictionary<string, GameItem>(), new TextOverlayOptions(true, 3));

        Assert.DoesNotContain("<EM", plan.Content);
    }
}
