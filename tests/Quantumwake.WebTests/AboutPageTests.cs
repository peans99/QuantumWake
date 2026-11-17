namespace Quantumwake.WebTests;

/// <summary>The About page's facts have to fit the columns they sit in.</summary>
public class AboutPageTests
{
    [Theory]
    // The Store package: short commit, then the full hash SourceLink appends.
    // Left whole, it ran across Game install and Dashboard beside it.
    [InlineData("1.2.41+190a243.190a2435b80b8a0fb780a2fe4eb202ba582c8e5a", "1.2.41+190a243")]
    // A local build carries only the full hash.
    [InlineData("1.2.61+fb359ba2cf74092f9dcc5c2fb69894e6bbd18449", "1.2.61+fb359ba")]
    [InlineData("1.2.61", "1.2.61")]
    public void The_build_is_cut_to_seven_characters_of_commit(string build, string expected)
    {
        var page = new Page();

        Assert.Equal(expected, page.Text($"shortBuild('{build}')"));
    }
}
