using Quantumwake.Core;

namespace Quantumwake.Tests;

/// <summary>
/// Where the data folder lands when nothing has moved it.
/// </summary>
/// <remarks>
/// For the Linux run. There .NET reads LocalApplicationData from
/// <c>XDG_DATA_HOME</c>, and pointing that at a folder nobody has made yet is
/// the fresh account whose data folder used to come out relative.
/// </remarks>
public class AppPathsTests
{
    [Fact]
    public void The_default_data_folder_is_rooted_even_before_its_parent_exists()
    {
        var missing = Path.Combine(Path.GetTempPath(), "qw-xdg-" + Guid.NewGuid().ToString("N"));
        var before = Environment.GetEnvironmentVariable("XDG_DATA_HOME");

        try
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", missing);

            var root = AppPaths.DefaultRoot();

            Assert.True(Path.IsPathRooted(root), $"'{root}' is relative");
            Assert.Equal("Quantumwake", Path.GetFileName(root));

            if (!OperatingSystem.IsWindows())
                Assert.Equal(Path.Combine(missing, "Quantumwake"), root);
        }
        finally
        {
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", before);
        }
    }
}
