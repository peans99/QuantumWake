namespace Quantumwake.WebTests;

/// <summary>
/// Component grades, shown the way the game shows them.
/// </summary>
/// <remarks>
/// The files store an ordinal and the game shows a letter. Printing the ordinal
/// asks the reader to know a mapping nobody published, and "G3" beside "S2"
/// reads like a second size while giving no clue that lower is better. The
/// mapping was checked against StarStrings, which calls the AEGS coolers A to D
/// exactly where the install numbers them 1 to 4.
/// </remarks>
public class GradeLetterTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(2, "B")]
    [InlineData(3, "C")]
    [InlineData(4, "D")]
    public void The_four_grades_the_game_shows_become_letters(int ordinal, string letter)
    {
        Assert.Equal(letter, new Page().Text($"gradeLetter({ordinal})"));
    }

    /// <summary>
    /// Nothing has grade nought. A dash says so, where an "A" would promote it.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void No_grade_is_a_dash(int ordinal)
    {
        Assert.Equal("—", new Page().Text($"gradeLetter({ordinal})"));
    }

    /// <summary>
    /// A few items carry 5 and above, which is outside anything the game shows.
    /// They keep their number rather than being given a letter that means
    /// nothing.
    /// </summary>
    [Fact]
    public void An_ordinal_past_the_scale_keeps_its_number()
    {
        Assert.Equal("7", new Page().Text("gradeLetter(7)"));
    }
}
