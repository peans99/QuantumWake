using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The parts of an entity card that are the server's own wording.
/// </summary>
/// <remarks>
/// One component described two ways in two places is exactly the disagreement
/// the shared panel exists to end, so where the drawer formats something the
/// page already formats, the two have to agree.
/// </remarks>
public class EntityCardTests
{
    /// <summary>
    /// Grade 1 is grade A, as the Parts table has always printed it. The drawer
    /// shipped showing the raw ordinal, which is one component described two
    /// ways on two surfaces.
    /// </summary>
    [Theory]
    [InlineData(1, "A")]
    [InlineData(2, "B")]
    [InlineData(3, "C")]
    [InlineData(4, "D")]
    public void A_grade_reads_as_the_letter_the_rest_of_the_app_uses(int grade, string expected)
    {
        Assert.Equal(expected, EntityCards.GradeLetter(grade));
    }

    /// <summary>
    /// Past D the game has no letter and the number has to stand, rather than
    /// running off the end of the alphabet into something invented.
    /// </summary>
    [Fact]
    public void A_grade_the_alphabet_does_not_cover_keeps_its_number()
    {
        Assert.Equal("5", EntityCards.GradeLetter(5));
    }

    /// <summary>
    /// Nothing is not zero. An item the install grades at 0 has no grade, and
    /// the em dash says so where "grade 0" would read as a real answer.
    /// </summary>
    [Fact]
    public void No_grade_is_shown_as_nothing_rather_than_as_a_zero()
    {
        Assert.Equal("—", EntityCards.GradeLetter(0));
    }
}
