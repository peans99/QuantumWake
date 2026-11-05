using System.IO;
using Quantumwake.Ocr;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Quantumwake.OcrTests;

/// <summary>
/// The half of live capture that does not need the game: a rectangle of the
/// desktop, copied and written, opens in the decoder the text engine uses.
/// </summary>
/// <remarks>
/// It needs no OCR engine, so unlike the rest of this suite it does not stand
/// down for want of one. What it cannot know is what the desktop shows - on a
/// build agent it may be black, which the capture would refuse as exclusive
/// fullscreen - so a refusal for that reason is accepted and nothing else is.
/// </remarks>
public class ScreenCopyTests
{
    [Fact]
    public async Task A_copied_patch_of_the_desktop_opens_at_its_own_size()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qw-copy-{Guid.NewGuid():N}.bmp");
        try
        {
            var taken = WindowsGameCapture.CopyScreen(0, 0, 333, 217, path);
            if (!taken.Taken)
            {
                Assert.Contains("came back black", taken.Why);
                return;
            }

            Assert.Equal(333, taken.Width);
            Assert.Equal(217, taken.Height);

            var file = await StorageFile.GetFileFromPathAsync(path);
            using var stream = await file.OpenAsync(FileAccessMode.Read);
            var decoder = await BitmapDecoder.CreateAsync(stream);
            Assert.Equal(333u, decoder.PixelWidth);
            Assert.Equal(217u, decoder.PixelHeight);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
