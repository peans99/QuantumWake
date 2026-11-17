using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A stash name that is probably a list line's thing in another colour or
/// version (1.2.60): said beside the line, never counted.
/// </summary>
public sealed class HeldAlikeTests
{
    private static readonly Dictionary<string, ItemInfo> Reference = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ORC-mkV Core Grey"] = new("Char_Armor_Torso", "Medium", 0, 1, "Clark Defense Systems"),
        ["ORC-mkV Core Red"] = new("Char_Armor_Torso", "Medium", 0, 1, "Clark Defense Systems"),
        ["ORC-mkV Arms Red"] = new("Char_Armor_Arms", "Medium", 0, 1, "Clark Defense Systems"),
        ["Arden-CL Backpack Rime"] = new("Char_Armor_Backpack", "Light", 0, 1, "Roussimoff Rehabilitation Systems"),
        ["Arden-CL Backpack"] = new("Char_Armor_Backpack", "Light", 0, 1, "Roussimoff Rehabilitation Systems"),
        ["Tailwind Flight Suit"] = new("Char_Armor_Undersuit", "", 0, 1, "Tailwind"),
        ["Tailwind Flight Helmet"] = new("Char_Armor_Helmet", "", 0, 1, "Tailwind"),
    };

    private static bool Alike(string held, string wanted) =>
        HeldNames.Alike(held, wanted, n => Reference.GetValueOrDefault(n));

    [Theory]
    // The case it was written for: Grey wanted, Red in the stash.
    [InlineData("ORC-mkV Core Red", "ORC-mkV Core Grey", true)]
    // The plain one for a coloured one.
    [InlineData("Arden-CL Backpack", "Arden-CL Backpack Rime", true)]
    // Another piece of the same range is not a version of it.
    [InlineData("ORC-mkV Arms Red", "ORC-mkV Core Grey", false)]
    [InlineData("Tailwind Flight Helmet", "Tailwind Flight Suit", false)]
    // The exact thing is a match, not a look-alike.
    [InlineData("ORC-mkV Core Grey", "ORC-mkV Core Grey", false)]
    // Nothing in the reference to tell them apart by.
    [InlineData("ORC-mkV Core Blue", "ORC-mkV Core Grey", false)]
    // Two words leave one word of stem, which is too little.
    [InlineData("P8-AR Rifle", "P8-SC SMG", false)]
    public void Another_colour_of_the_same_item_and_nothing_else(string held, string wanted, bool alike) =>
        Assert.Equal(alike, Alike(held, wanted));
}
