using System.Diagnostics;
using System.Xml.Linq;
using Quantumwake.Server;

namespace Quantumwake.Tests;

/// <summary>
/// The build says the version <c>Directory.Build.props</c> declares.
/// </summary>
/// <remarks>
/// The app learns what it is from <c>AssemblyVersion</c> - the About page, the
/// footer and the update check all read it. That was written out by hand
/// beside <c>Version</c> and stayed at 0.18.11 while <c>Version</c> moved on to
/// 0.18.23, so four releases called themselves 0.18.11 and offered an update
/// to themselves. The release workflow checks the tag against <c>Version</c>
/// only, which is why nothing went red; this is the check that would have.
/// </remarks>
public class VersionTests
{
    private static string Declared()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Directory.Build.props")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return XDocument.Load(Path.Combine(dir!.FullName, "Directory.Build.props"))
            .Descendants("Version").Single().Value.Trim();
    }

    [Fact]
    public void The_app_knows_itself_by_the_declared_version()
    {
        Assert.Equal(Declared(), typeof(ServerHost).Assembly.GetName().Version?.ToString(3));
    }

    [Fact]
    public void Windows_shows_the_declared_version_in_the_files_properties()
    {
        var file = FileVersionInfo.GetVersionInfo(typeof(ServerHost).Assembly.Location);

        Assert.Equal(Declared(), $"{file.FileMajorPart}.{file.FileMinorPart}.{file.FileBuildPart}");
    }
}
