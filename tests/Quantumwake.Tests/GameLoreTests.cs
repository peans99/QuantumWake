using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Deciding whether the star map actually said anything about a place.
/// </summary>
/// <remarks>
/// The install describes 1,344 places against the community download's 1,361,
/// and 1,251 of the 1,294 both know are word for word. Most of what separates
/// them is thrown away here rather than missing: a great many map objects carry
/// a description that is only their own name again, or a key nobody filled in.
/// </remarks>
public class GameLoreTests
{
    [Fact]
    public void A_real_description_is_kept()
    {
        Assert.False(GameLore.Worthless(
            "Derelict", "The ruined remains of an unfortunate vehicle."));
    }

    /// <summary>
    /// A card that opens to show the name it was already showing is worse than
    /// one that says it has nothing.
    /// </summary>
    [Fact]
    public void A_description_that_is_only_the_name_is_not_lore()
    {
        Assert.True(GameLore.Worthless("Downded Relay AC-652", "Downded Relay AC-652"));
        Assert.True(GameLore.Worthless("Downded Relay AC-652", "downded relay ac-652"));
    }

    [Fact]
    public void An_unfilled_key_is_not_lore()
    {
        Assert.True(GameLore.Worthless("Somewhere", "<= UNINITIALIZED =>"));
        Assert.True(GameLore.Worthless("Somewhere", "PLACEHOLDER text for this location"));
    }

    /// <summary>
    /// Below about twenty characters these are labels rather than sentences.
    /// </summary>
    [Fact]
    public void A_label_is_not_a_paragraph()
    {
        Assert.True(GameLore.Worthless("Somewhere", "A moon."));
        Assert.False(GameLore.Worthless("Somewhere", "A moon-sized asteroid hidden in a cluster."));
    }
}
