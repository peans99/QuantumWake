namespace Quantumwake.Data;

/// <summary>
/// Reads the mining scan panel again at several sizes, and keeps what they agree on.
/// </summary>
/// <remarks>
/// <para>
/// The panel is orange on a glow, and reading the whole frame loses most of its
/// figures. Measured on this install's first three scan frames (2026-10-03,
/// 3440 × 1440, a Golem at Daymar): the whole read of the silicon rock lost its
/// mass, mangled its instability to "21 .eø" and kept one quality of five; the
/// patch at x2, x2.5 and x3 each read the mass as 4294, and x1.5 to x3 all read
/// 21.89. The aphorite cluster's mass, 0.12, and its quality, 348, came back
/// from the patch and not from the whole frame.
/// </para>
/// <para>
/// Upright, so no shear: unlike the mobiGlas balance the HUD's face does not
/// lean. x1 is left out - it read nothing the larger sizes missed, and dropped
/// a name the others kept - and nothing above x3 is tried, because the engine
/// drops a line once it stands taller than about 55 px (see
/// <see cref="WalletSecondLook"/>) and a 17 px row at x3 is already 51.
/// </para>
/// <para>
/// Every rung is read rather than stopping at the first agreement, because the
/// panel is a dozen figures, not one: on the silicon frame x1.5 read the
/// instability and not the mass, and x3 read 572 as 72 where the other three
/// had it right. Four reads of a 440 × 510 patch.
/// What none of them read stays empty - the silicon rows' qualities, 510 and
/// 310, did not read at any size, and the page shows them as unread.
/// </para>
/// </remarks>
public static class MiningSecondLook
{
    /// <summary>The sizes tried, best first by the measurements above.</summary>
    public static readonly IReadOnlyList<ScreenTreatment> Treatments =
    [
        new(Scale: 2, Shear: 0),
        new(Scale: 2.5, Shear: 0),
        new(Scale: 3, Shear: 0),
        new(Scale: 1.5, Shear: 0),
    ];

    /// <summary>
    /// The panel as the looks agree it reads, or null when the frame is not a
    /// scan or fewer than two looks found the panel at all.
    /// </summary>
    /// <remarks>
    /// A reader that throws loses the second look only: the whole frame was
    /// already read, and the caller falls back to that reading.
    /// </remarks>
    public static async Task<MiningScanReading?> SettleAsync(
        IReadOnlyList<ScreenTextLine> lines,
        WalletSecondLook.PatchReader read,
        IReadOnlyList<string> commodityNames,
        CancellationToken token = default)
    {
        if (ScreenFrames.ScanPanel(lines) is not { } panel)
            return null;

        var looks = new List<ScreenFrames.ScanLook>();

        foreach (var treatment in Treatments)
        {
            token.ThrowIfCancellationRequested();

            IReadOnlyList<ScreenTextLine> again;

            try
            {
                again = await read(panel, treatment, token);
            }
            catch (Exception) when (!token.IsCancellationRequested)
            {
                return null;
            }

            var trimmed = again
                .Select(line => line with { Text = line.Text.Trim() })
                .Where(line => line.Text.Length > 0)
                .ToList();

            if (ScreenFrames.LookAtScan(trimmed, commodityNames) is { } look)
                looks.Add(look);
        }

        return looks.Count >= WalletSecondLook.Agreement ? ScreenFrames.Settle(looks) : null;
    }
}
