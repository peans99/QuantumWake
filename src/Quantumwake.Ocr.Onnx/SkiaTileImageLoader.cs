using Quantumwake.Data;

namespace Quantumwake.Ocr.Onnx;

/// <summary>
/// Turns a screenshot into brightness with SkiaSharp: the
/// <c>WindowsTileImageLoader</c> for machines without the Windows decoder.
/// </summary>
/// <remarks>
/// The same luminance weights as the Windows one, for the same reason: the
/// inventory panels are blue-white line art over whatever colour the hangar
/// behind them happens to be, and a flat average lets a red wall count for
/// as much as the tile.
/// </remarks>
public sealed class SkiaTileImageLoader : ITileImageLoader
{
    public Task<GreyImage> LoadAsync(string imagePath, CancellationToken token = default) =>
        Task.Run(() =>
        {
            using var frame = Frame.Load(imagePath);
            var bytes = frame.Pixels;
            var pixels = new float[frame.Width * frame.Height];

            for (var i = 0; i < pixels.Length; i++)
            {
                var at = i * 4;
                pixels[i] = 0.114f * bytes[at] + 0.587f * bytes[at + 1] + 0.299f * bytes[at + 2];
            }

            return new GreyImage(frame.Width, frame.Height, pixels);
        }, token);
}
