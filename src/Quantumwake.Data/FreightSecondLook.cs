namespace Quantumwake.Data;

/// <summary>
/// The Freight Manager's crate tiles, read again up close for the commodity.
/// </summary>
/// <remarks>
/// <para>
/// The whole read gets the commodity under a tile most of the time, but
/// spells it badly often enough to matter: "TLJNGSTEN" for TUNGSTEN on the
/// 4.10 video. Each tile read again at twice the size by the panel reader
/// (ONNX, when its models are on disk) spelled it right. The patch is sized
/// off the size label's own height, so it is the same patch at 1440 as at
/// 2160. Only the words are handed back: the size label it also reads again
/// would otherwise be a second tile.
/// </para>
/// <para>
/// It was written for the count between the two - "11/11" in small green
/// type - and that did not work: see <see cref="FreightCrates"/>. Windows
/// read none of 15 counts, and ONNX read the 8 SCU tile's "11/11" as "1/1"
/// on about half its reads, at every scale and patch tried.
/// </para>
/// </remarks>
public static class FreightSecondLook
{
    private static readonly ScreenTreatment Closer = new(Scale: 2, Shear: 0);

    public static async Task<IReadOnlyList<ScreenTextLine>> TakeAsync(
        IReadOnlyList<ScreenTextLine> lines, WalletSecondLook.PatchReader read, CancellationToken token = default)
    {
        static string Folded(ScreenTextLine l) => ScreenInsight.Plain(l.Text).ToUpperInvariant();

        if (!lines.Any(l => Folded(l).Contains("FREIGHTMANAGER", StringComparison.Ordinal))
            || !lines.Any(l => Folded(l).Contains("TRANSACTIONCONFIRMATION", StringComparison.Ordinal)))
            return lines;

        var added = new List<ScreenTextLine>();

        // A label the whole read lost its digit from - "scu" alone, the 1 SCU
        // silicon tile on the video - is a tile too, and the close read is
        // the only way its size comes back. That size is kept; any other the
        // close read finds is the tile's own label again.
        foreach (var size in lines.Where(l => ScreenFrames.CrateSize(l.Text) is not null || Folded(l) == "SCU"))
        {
            token.ThrowIfCancellationRequested();
            var h = size.Height;
            var lost = ScreenFrames.CrateSize(size.Text) is null;
            var patch = new ScreenPatch(size.Left - 3 * h, size.Top - h, 9 * h, 8.5 * h);

            try
            {
                foreach (var line in await read(patch, Closer, token))
                {
                    if (ScreenFrames.CrateSize(line.Text) is null)
                        added.Add(line);
                    else if (lost && Math.Abs(line.Left - size.Left) <= h * 3)
                        added.Add(line);
                }
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                // One tile not read again keeps the whole read's word for it.
            }
        }

        return added.Count == 0 ? lines : [.. lines, .. added];
    }
}
