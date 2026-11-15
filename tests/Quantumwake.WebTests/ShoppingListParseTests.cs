namespace Quantumwake.WebTests;

/// <summary>
/// The new-list box: one line per thing, an amount after it if there is one.
/// </summary>
/// <remarks>
/// Before 1.2.44 the amount did not have to stand apart from the name, so any
/// name ending in digits was cut: "JS-400" was saved as 400 of "JS-". Every
/// mangled line was a line no seller lookup could ever find.
/// </remarks>
public sealed class ShoppingListParseTests
{
    private static string Parsed(string line, string known = "[]") =>
        new Page().Text($"JSON.stringify(parseJobItems({Page.Quote(line)}, {known})[0])");

    [Theory]
    [InlineData("JS-400", """{"name":"JS-400","needed":0,"unit":""}""")]
    [InlineData("FR-86", """{"name":"FR-86","needed":0,"unit":""}""")]
    [InlineData("Phoenix 2", """{"name":"Phoenix","needed":2,"unit":""}""")]
    [InlineData("Max 3", """{"name":"Max","needed":3,"unit":""}""")]
    [InlineData("JS-400 1", """{"name":"JS-400","needed":1,"unit":""}""")]
    [InlineData("Hadanite 23", """{"name":"Hadanite","needed":23,"unit":""}""")]
    [InlineData("Agricium 1.16 SCU", """{"name":"Agricium","needed":1.16,"unit":"SCU"}""")]
    [InlineData("Arrow x2", """{"name":"Arrow","needed":2,"unit":""}""")]
    [InlineData("CF-337 Panther x 2", """{"name":"CF-337 Panther","needed":2,"unit":""}""")]
    [InlineData("Medical Supplies ×3", """{"name":"Medical Supplies","needed":3,"unit":""}""")]
    [InlineData("Karna Rifle", """{"name":"Karna Rifle","needed":0,"unit":""}""")]
    public void A_line_keeps_its_whole_name_and_its_amount(string line, string expected) =>
        Assert.Equal(expected, Parsed(line));

    /// <summary>The game's blueprint lines carry a quality floor after the amount.</summary>
    [Fact]
    public void A_bracket_after_the_amount_is_a_note_not_the_name() =>
        Assert.Equal("""{"name":"Hadanite","needed":0.5,"unit":"SCU","note":"quality 500+"}""",
            Parsed("Hadanite 0.5 SCU (quality 500+)"));

    [Fact]
    public void A_bracket_with_no_amount_stays_in_the_name() =>
        Assert.Equal("""{"name":"Hadanite (Raw)","needed":0,"unit":""}""", Parsed("Hadanite (Raw)"));

    /// <summary>The picker writes these, and nine catalogue names end in a spaced number.</summary>
    [Fact]
    public void A_name_the_catalogue_knows_is_never_split()
    {
        const string known = """["Ship Ammunition - Size 3"]""";

        Assert.Equal("""{"name":"Ship Ammunition - Size 3","needed":0,"unit":""}""",
            Parsed("Ship Ammunition - Size 3", known));
        Assert.Equal("""{"name":"Ship Ammunition - Size 3","needed":10,"unit":""}""",
            Parsed("Ship Ammunition - Size 3 10", known));
    }
}
