using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// A commodity kiosk, buy side, as the engine read it.
/// </summary>
/// <remarks>
/// The one fixture here that is <b>not</b> from this install: it is a
/// screenshot of somebody else's terminal, shared publicly, used because no
/// kiosk on this machine has been photographed. So it defends the shape of
/// the screen and nothing about this pilot's data. Every misreading in it is
/// real - "usc HULL A" for the MISC Hull A, a "Ton" left on the Titanium row
/// by the ship rendered behind the glass, and the float noise in
/// 2.19700002K that the game itself printed.
/// </remarks>
internal static class ScreenKioskFixtures
{
    public static readonly ScreenTextLine[] BuySide =
    [
        new("CURRENT BALANCE;", 2592, 68, 32),
        new("Ä1.583n AUEC", 2698, 114, 27),
        new("COMMODITIES", 338, 118, 54),
        new("SHOP INVENTORY", 2191, 223, 25),
        new("YOUR INVENTORIES", 297, 275, 24),
        new("LOCAL MARKET VALUE", 2481, 293, 27),
        new("BUY", 2219, 299, 26),
        new("usc HULL A", 317, 344, 19),
        new("SHOP QUANTITY", 2758, 421, 22),
        new("CARGO GRIO", 316, 425, 20),
        new("HEPHAESTANITE", 2326, 428, 28),
        new("5,000 SCU", 2762, 451, 32),
        new("INVENTORY", 2386, 472, 20),
        new("n 2.19700002K/UNITS", 2653, 499, 21),
        new("0/ 64 SCU", 850, 508, 22),
        new("CARGO CAPACITY", 272, 509, 19),
        new("AVAILABLE CARGO SIZE CSCIJ)", 2593, 551, 20),
        new("IN DENAND", 279, 624, 21),
        new("NO DENAND", 278, 679, 20),
        new("EST. LOADING TIME 00:01:12", 2492, 695, 26),
        new("X.", 1812, 730, 18),
        new("CANNOT SELL", 277, 733, 21),
        new("0 Units", 2793, 777, 30),
        new("TOTAL: O SCU", 2221, 864, 21),
        new("BUY", 2825, 891, 26),
        new("CORUNDUM", 2324, 1020, 28),
        new("SHOP QUANTITY", 2757, 1021, 19),
        new("6,000 SCU", 2761, 1051, 32),
        new("INVENTORY", 2384, 1064, 20),
        new("Ä296/SCU", 2807, 1099, 22),
        new("AVAILABLE", 2592, 1148, 19),
        new("CARGO SIZE cscuj", 2728, 1149, 22),
        new("0.", 1375, 1286, 11),
        new("Ton", 2261, 1286, 14),
        new("TITANIUM", 2322, 1298, 30),
        new("SHOP QUANTITY", 2757, 1303, 21),
        new("6,000 SCU", 2761, 1333, 33),
        new("NAX INVENTORY", 2323, 1342, 21),
        new("AVAILABLE CARGO SIZE tscu)", 2592, 1429, 23),
        new("I CARBON", 2299, 1567, 41),
        new("SHOP ouANTtrv", 2757, 1585, 23),
    ];

    /// <summary>
    /// The sell side, written by hand from a photograph of a screen.
    /// </summary>
    /// <remarks>
    /// Not an engine reading: the only sell-side frame anyone has produced is
    /// 640 pixels wide and the engine refused it outright. So these lines are
    /// transcribed from what is legible in the picture, with plausible boxes,
    /// and they defend one thing only - that a sell-side kiosk is filed as a
    /// kiosk rather than as an unknown screen, and that it claims no rows it
    /// cannot read.
    /// </remarks>
    public static readonly ScreenTextLine[] SellSide =
    [
        new("COMMODITIES", 100, 40, 54),
        new("YOUR INVENTORIES", 90, 155, 24),
        new("DRAKE CATERPILLAR", 110, 209, 20),
        new("SELECT SUB-CATEGORY", 110, 271, 20),
        new("CARGO CAPACITY", 88, 334, 19),
        new("67 / 576 SCU", 460, 334, 19),
        new("DYMANTIUM", 160, 473, 20),
        new("MAX INVENTORY", 160, 496, 16),
        new("8 SCU", 470, 473, 20),
        new("1/SCU", 490, 500, 14),
    ];

    /// <summary>
    /// A kiosk on this install at last - the sell side, 10 Sep 2026, with the
    /// pilot's ship out of the frame - and the balance printed in full.
    /// </summary>
    /// <remarks>
    /// The text is the engine's own, line for line, from
    /// <c>ScreenShot-2026-09-10_20-53-54-CF5.jpg</c> as it sits in the
    /// readings log: <c>NO OENAND</c>, <c>TO NAKE A TRANSACTION</c> and the
    /// padded <c>2,092, 773</c> are all real. The boxes are not - the log keeps
    /// text and not geometry - so they are plausible, and the two things they
    /// have to get right are what the reading itself proved: the figure sits
    /// under its label as on the other kiosk, and the close button's <c>x</c>
    /// sits beside the label, which is what the first reader took for the
    /// balance.
    /// </remarks>
    public static readonly ScreenTextLine[] OwnSellSide =
    [
        new("x", 3000, 70, 24),
        new("COMMODITIES", 100, 40, 54),
        new("YOUR INVENTORIES", 90, 155, 24),
        new("SELECT LOCATION", 110, 209, 20),
        new("IN DEMAND", 90, 624, 21),
        new("NO OENAND", 90, 679, 20),
        new("CANNOT SELL", 90, 734, 20),
        new("SHOP", 1500, 160, 24),
        new("BUY", 1600, 160, 24),
        new("INVENTORY", 1700, 160, 24),
        new("CURRENT BALANCE:", 2592, 68, 32),
        new("Ä2,092, 773 AUEC", 2698, 114, 27),
        new("LOCAL MARKET VALUE", 2481, 293, 27),
        new("IN STOCK", 2500, 350, 20),
        new("SCRAP", 1600, 473, 20),
        new("MX INVENTORY", 1600, 496, 16),
        new("METHANE", 1600, 560, 20),
        new("INVENTORY", 1600, 583, 16),
        new("PLEASE SELECT A VALID INVENTORY", 1500, 900, 22),
        new("TO NAKE A TRANSACTION", 1500, 930, 22),
        new("e", 2900, 1000, 16),
        new("e", 2950, 1000, 16),
    ];
}
