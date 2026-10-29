using Quantumwake.Core.GameData;

namespace Quantumwake.Tests;

/// <summary>
/// Naming an archive entry without asking the platform how paths are split.
/// </summary>
/// <remarks>
/// On Windows <see cref="Path.GetFileName(string)"/> gives the same answers, so
/// these would pass there with either; they are here for the Linux run, where
/// it does not split on '\' and three tests lost their liveries and icons.
/// </remarks>
public class P4kEntryNameTests
{
    [Theory]
    [InlineData(@"Data\UI\SharedAssets\PaintColorLogos\paint_clipper_default.dds", "paint_clipper_default.dds")]
    [InlineData("Data/UI/Textures/EA/VehicleIcons/VehicleIcon_ANVL_Arrow.tif", "VehicleIcon_ANVL_Arrow.tif")]
    [InlineData(@"Data\Libs/Config\Mappings/layout_t16000m.xml", "layout_t16000m.xml")]
    [InlineData("defaultProfile.xml", "defaultProfile.xml")]
    [InlineData(@"Data\Libs\Config\", "")]
    public void The_file_name_is_after_the_last_separator_of_either_kind(string entry, string expected)
    {
        Assert.Equal(expected, P4kArchive.EntryFileName(entry));
    }

    [Theory]
    [InlineData(@"Data\UI\SharedAssets\ManufacturerLogos\AEGS_Logo.tif", "AEGS_Logo")]
    [InlineData("Data/UI/a.b/c.dds", "c")]
    [InlineData(@"Data\UI\icon.tar.dds", "icon.tar")]
    [InlineData(@"Data\UI\noextension", "noextension")]
    [InlineData(@"Data\UI\.dds", "")]
    public void The_stem_drops_only_the_last_extension(string entry, string expected)
    {
        Assert.Equal(expected, P4kArchive.EntryStem(entry));
    }
}
