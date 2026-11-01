using System.Text.RegularExpressions;

namespace Quantumwake.WebTests;

/// <summary>
/// Boxes that take an amount the pilot reads off the game take any amount.
/// </summary>
/// <remarks>
/// A number input's <c>step</c> is also a validity rule: with step 1000 the
/// browser refused to submit a savings goal of 250,500 aUEC, and a rock of
/// 5,123 kg failed a step of 100. The step had only been there to make the spin
/// arrows jump in round numbers.
/// </remarks>
public class FormControlTests
{
    private static readonly string Markup =
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"));

    [Theory]
    [InlineData("now-goal-target")]
    [InlineData("routes-capital")]
    [InlineData("crack-mass")]
    public void An_amount_from_the_game_is_never_refused_for_its_step(string id)
    {
        var input = Regex.Match(Markup, $"<input[^>]*id=\"{id}\"[^>]*>");
        Assert.True(input.Success, $"no input {id}");
        Assert.Contains("step=\"any\"", input.Value);
    }
}
