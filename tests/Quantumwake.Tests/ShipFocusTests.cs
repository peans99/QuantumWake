using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// Reading intent off the ship in the hangar.
/// </summary>
/// <remarks>
/// This is the whole basis of the Now page's focus, and it is a guess: the logs
/// record no mining, no salvage and no cargo, so the retrieved ship is the only
/// statement of intent there is. The guess is worth making because it is nearly
/// always right — nobody takes a Prospector out to dogfight — and the rules
/// below are where its rightness lives.
/// </remarks>
public class ShipFocusTests
{
    /// <summary>
    /// Every career this install's own fleet resolves to, which is the set the
    /// feature has to get right before any other.
    /// </summary>
    [Theory]
    [InlineData("Transporter", "Medium Freight", "freight")]   // RSI Hermes
    [InlineData("Combat", "Medium Fighter", "combat")]         // Anvil F7C-M Mk II
    [InlineData("Combat", "Heavy Fighter", "combat")]          // Origin M80
    [InlineData("Exploration", "Expedition", "explore")]       // Drake Corsair
    [InlineData("Exploration", "Pathfinder", "explore")]       // Anvil C8X Pisces
    [InlineData("Industrial", "Light Mining", "mining")]
    public void A_career_names_the_work(string career, string role, string expected)
    {
        Assert.Equal(expected, ShipFocus.Of(career, role)?.Key);
    }

    /// <summary>
    /// The dataset files the MISC Fortune under Starter and the Prospector under
    /// Industrial, and both are out there to fill a hopper. Role is read first
    /// for exactly these two trades because a career can bury them.
    /// </summary>
    [Theory]
    [InlineData("Starter", "Starter / Light Mining")]
    [InlineData("Competition", "Light Salvage")]
    [InlineData("Support", "Heavy Salvage")]
    public void A_role_that_names_a_trade_beats_its_career(string career, string role)
    {
        Assert.Equal("mining", ShipFocus.Of(career, role)?.Key);
    }

    /// <summary>
    /// A hull with a hold is hauling whatever the career column calls it.
    /// </summary>
    [Fact]
    public void Freight_in_the_role_carries_a_career_that_says_nothing()
    {
        Assert.Equal("freight", ShipFocus.Of("Multi-Role", "Light Freight / Medium Fighter")?.Key);
    }

    /// <summary>
    /// Three careers in the dataset hold one ship each and are plainly a role
    /// filed in the wrong column. They are still combat.
    /// </summary>
    [Theory]
    [InlineData("Destroyer")]
    [InlineData("Gunship")]
    [InlineData("Snub Fighter")]
    public void A_role_filed_as_a_career_is_still_read(string career)
    {
        Assert.Equal("combat", ShipFocus.Of(career, null)?.Key);
    }

    /// <summary>
    /// The important negative. A wrong guess rearranges the page around work
    /// the pilot is not doing, so a career with nothing to say answers nothing
    /// and the page stays as they arranged it.
    /// </summary>
    [Theory]
    [InlineData("Multi-Role", "Starter / Light Fighter")]  // RSI Aurora Mk II
    [InlineData("Competition", "Racing")]
    [InlineData("Ground", "Light Tank")]
    [InlineData("Support", "Medical")]
    [InlineData(null, null)]
    public void A_ship_that_says_nothing_gets_no_focus(string? career, string? role)
    {
        Assert.Null(ShipFocus.Of(career, role));
    }

    /// <summary>
    /// A role alone is not enough for the careers that carry the fighting: the
    /// dataset gives every hull a career, so a missing one means the community
    /// dataset is switched off, and a guess from half the data is still a guess.
    /// </summary>
    [Fact]
    public void A_role_without_a_career_only_answers_for_the_trades()
    {
        Assert.Null(ShipFocus.Of(null, "Medium Fighter"));
        Assert.Equal("mining", ShipFocus.Of(null, "Light Mining")?.Key);
    }
}
