using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// What a stash name has to be to tick a shopping line (1.2.45). The false
/// matches are the ones the containment rule made on this install's stash.
/// </summary>
public sealed class HeldNamesTests
{
    [Theory]
    [InlineData("Hadanite", "Hadanite")]
    [InlineData("hadanite", "Hadanite")]
    [InlineData("Hadanite (Raw)", "Hadanite")]
    [InlineData("MedPen (Hemozal)", "MedPen")]
    [InlineData("ORC-mkV Arms Red", "ORC-mkV Arms")]
    [InlineData("Beacon Undersuit White", "Beacon Undersuit")]
    public void A_stash_name_that_is_the_line_matches(string held, string wanted) =>
        Assert.True(HeldNames.Matches(held, wanted));

    [Theory]
    [InlineData("MedPen (Hemozal)", "Oza")]
    [InlineData("Fried Seanut", "Seanut")]
    [InlineData("Odyssey II Undersuit Alpha", "Odyssey")]
    [InlineData("Arrowhead Sniper Rifle", "Arrow")]
    [InlineData("ORC-mkV Armsmaster", "ORC-mkV Arms")]
    [InlineData("Hadanite", "")]
    public void A_stash_name_that_merely_contains_the_line_does_not(string held, string wanted) =>
        Assert.False(HeldNames.Matches(held, wanted));
}
