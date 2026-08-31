using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Names the game has not written yet.
/// </summary>
/// <remarks>
/// A localisation key can be present in the string table and still say nothing:
/// 8,149 of this install's 26,028 items resolve to "&lt;= PLACEHOLDER =&gt;".
/// Passing that through filled a third of the Parts catalogue with identical
/// unreadable rows, so it counts as the missing entry it stands for and callers
/// fall back to the class name.
/// </remarks>
public class PlaceholderNameTests
{
    [Theory]
    [InlineData("<= PLACEHOLDER =>")]
    [InlineData("  <= PLACEHOLDER =>  ")]
    [InlineData("<-=MISSING=->")]
    [InlineData("<TBD>")]
    public void Bracket_wrapped_text_is_not_a_name(string text)
    {
        Assert.True(GameItems.Unwritten(text));
    }

    [Theory]
    [InlineData("7MA 'Lorica'")]
    [InlineData("Arctic")]
    [InlineData("P4-AR Rifle")]
    [InlineData("<Unfinished")]
    [InlineData("Unfinished>")]
    public void A_real_name_survives(string text)
    {
        Assert.False(GameItems.Unwritten(text));
    }

    /// <summary>
    /// A lone angle bracket is punctuation, not a wrapper, and must not take a
    /// name with it.
    /// </summary>
    [Theory]
    [InlineData("<")]
    [InlineData(">")]
    [InlineData("")]
    [InlineData(null)]
    public void Nothing_at_all_is_not_a_placeholder(string? text)
    {
        Assert.False(GameItems.Unwritten(text));
    }
}
