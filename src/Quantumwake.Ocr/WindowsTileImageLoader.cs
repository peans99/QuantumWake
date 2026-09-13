using Quantumwake.Data;
using Windows.Graphics.Imaging;
using Windows.Storage;

namespace Quantumwake.Ocr;

/// <summary>
/// Turns a screenshot into brightness, with the decoder Windows already has.
/// </summary>
/// <remarks>
/// <para>
/// The same seam and the same reasoning as <see cref="WindowsScreenReader"/>:
/// no package, no codec to ship, and it lives here so that one Windows target
/// framework does not spread to the server and the parser tests.
/// </para>
/// <para>
/// Grey is computed with the usual luminance weights rather than by averaging
/// the channels. It matters more here than it looks: the inventory panels are
/// blue-white line art over whatever colour the hangar behind them happens to
/// be, and a flat average lets a red wall count for as much as the tile.
/// </para>
/// </remarks>
public sealed class WindowsTileImageLoader : ITileImageLoader
{
    public async Task<GreyImage> LoadAsync(string imagePath, CancellationToken token = default)
    {
        var file = await StorageFile.GetFileFromPathAsync(imagePath).AsTask(token);
        using var stream = await file.OpenAsync(FileAccessMode.Read).AsTask(token);

        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask(token);

        // Pixels straight out of the decoder rather than through a
        // SoftwareBitmap: the buffer of one cannot be reached from .NET
        // without unsafe COM interop, and this wants none of that to move
        // bytes it is only going to average.
        //
        // Bgra8 asked for explicitly so there is one shape to handle whatever
        // the file was - the game writes JPEG, a pilot may well drop in a PNG.
        // Orientation is ignored rather than respected so that the size below
        // always agrees with the pixels; screenshots carry no EXIF rotation,
        // and colour management is off so the same file always reads the same.
        var pixelData = await decoder.GetPixelDataAsync(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Ignore,
            new BitmapTransform(),
            ExifOrientationMode.IgnoreExifOrientation,
            ColorManagementMode.DoNotColorManage).AsTask(token);

        var bytes = pixelData.DetachPixelData();

        var width = (int)decoder.PixelWidth;
        var height = (int)decoder.PixelHeight;

        var pixels = new float[width * height];
        for (var i = 0; i < pixels.Length; i++)
        {
            var at = i * 4;
            pixels[i] = 0.114f * bytes[at] + 0.587f * bytes[at + 1] + 0.299f * bytes[at + 2];
        }

        return new GreyImage(width, height, pixels);
    }
}
