namespace Quantumwake.Data;

/// <summary>
/// How damaged the locked ship looks, from the colour of its hologram.
/// </summary>
/// <param name="Damage">
/// 0 for a white hologram, 1 for an all-red one: orange counts half and red
/// whole, over every lit pixel of the ship. An estimate read off colours, not
/// the game's hit points, and said as one.
/// </param>
/// <param name="White">Lit pixels drawn white or pale: hull still sound.</param>
/// <param name="Orange">Pixels drawn orange: damaged.</param>
/// <param name="Red">Pixels drawn red: critical.</param>
public sealed record HologramReading(double Damage, int White, int Orange, int Red)
{
    public int Lit => White + Orange + Red;
}

/// <summary>
/// Reads the target panel's hologram for damage.
/// </summary>
/// <remarks>
/// <para>
/// The game draws the locked ship white while it is whole, turns the parts
/// that are hurt orange and then red, and a ship at 0 m/s - disabled - is red
/// from nose to tail. That is visible in 240 frames of a 2026-11-05 Tactical
/// Strike Group fight at 3440×1440: every target seen at speed starts white,
/// goes orange in patches, and the ones that stop are solid red; see
/// docs/screen-insight.md, "The hologram's colour (1.1.13)".
/// </para>
/// <para>
/// Pixels, not text, so it costs nothing beside the panel read that found
/// where the hologram is. The hologram floats over whatever is behind the
/// cockpit glass, so a lit pixel has to be one of the three colours and bright
/// - a teal nebula is neither white enough nor red - and a hologram that comes
/// to fewer than <see cref="MinimumLit"/> pixels is not called at all.
/// </para>
/// </remarks>
public static class HologramDamage
{
    /// <summary>Fewer lit pixels than this, at 1440 lines, and the hologram is too small or gone to judge.</summary>
    public const int MinimumLit = 400;

    /// <summary>
    /// Where the hologram sits under the panel's text, in frame pixels: the same
    /// cut the MFD shows, so what the pilot sees there is what was judged.
    /// </summary>
    public static ScreenPatch Area(ScreenPatch panel, int frameHeight)
    {
        var u = frameHeight / 1440.0;
        return new ScreenPatch(panel.Left + 120 * u, panel.Top + 230 * u, 420 * u, 190 * u);
    }

    public static HologramReading? Read(ColourPatch pixels, int frameHeight)
    {
        int white = 0, orange = 0, red = 0;
        for (var y = pixels.Top; y < pixels.Top + pixels.Height; y++)
        for (var x = pixels.Left; x < pixels.Left + pixels.Width; x++)
        {
            switch (Kind(pixels.At(x, y)))
            {
                case 1: white++; break;
                case 2: orange++; break;
                case 3: red++; break;
            }
        }

        var lit = white + orange + red;
        var scale = frameHeight / 1440.0;
        if (lit < MinimumLit * scale * scale) return null;
        return new HologramReading((orange * 0.5 + red) / lit, white, orange, red);
    }

    /// <summary>0 unlit or background, 1 white, 2 orange, 3 red.</summary>
    internal static int Kind((byte R, byte G, byte B) c)
    {
        int r = c.R, g = c.G, b = c.B;
        var max = Math.Max(r, Math.Max(g, b));
        var min = Math.Min(r, Math.Min(g, b));

        // Red and orange first: both are r well above b, which no teal or
        // navy behind the glass ever is.
        if (r >= 120 && b < r * 0.55)
        {
            if (g < r * 0.45) return 3;
            if (g < r * 0.85) return 2;
        }

        // White: bright and nearly grey. The nebula is bright too, but tinted
        // green-blue well past this spread.
        if (min >= 150 && max - min <= 40) return 1;
        return 0;
    }
}
