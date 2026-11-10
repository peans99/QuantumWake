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
    /// A kiosk on this install at last - the buy side, 10 Sep 2026, with no
    /// hold selected - and the balance printed in full.
    /// </summary>
    /// <remarks>
    /// The Windows engine's own lines and boxes for
    /// <c>ScreenShot-2026-09-10_20-53-54-CF5.jpg</c>, dumped from the
    /// screenshot in 1.2.22. Until then this fixture was the log's text with
    /// invented boxes, and it was called the sell side because the first
    /// reader said so; the picture shows the BUY tab, and every price on it
    /// matches what UEX lists Pyro's counters asking. What the frame really
    /// says, row by row: Scrap 2,040 SCU at 3,265; Methane 18,000 at 3,312;
    /// Ship Ammunition sizes 1-4, 12,000 each, at 6,868 / 7,126 / 7,384 /
    /// 7,641; size 5 with its price below the edge of the panel. Every
    /// misreading is the engine's: SHOP OUANTITV, 18, ooo, Ä6 , 868, n 7. 126,
    /// 384 for 7,384, ", 641" for 7,641 and 43,265 for ¤3,265.
    /// </remarks>
    public static readonly ScreenTextLine[] OwnBuySide =
    [
        new("x", 2661, 151, 38),
        new("COMMODITIES", 801, 184, 37),
        new("YOUR INVENTORIES", 772, 294, 17),
        new("SELECT LOCATION", 787, 343, 14),
        new("IN DEMAND", 762, 388, 15),
        new("NO OENAND", 761, 427, 15),
        new("CANNOT SELL", 760, 465, 15),
        new("SHOP", 2107, 258, 17),
        new("BUY", 2126, 310, 18),
        new("INVENTORY", 2172, 256, 18),
        new("CURRENT BALANCE:", 2391, 148, 20),
        new("Ä2,092, 773 AUEC", 2429, 180, 19),
        new("LOCAL MARKET VALUE", 2312, 306, 18),
        new("IN STOCK", 2106, 388, 17),
        new("SCRAP", 2203, 446, 19),
        new("MX INVENTORY", 2201, 476, 14),
        new("METHANE", 2202, 562, 19),
        new("INVENTORY", 2244, 593, 14),
        new("PLEASE SELECT A VALID INVENTORY", 1470, 738, 18),
        new("TO NAKE A TRANSACTION", 1536, 764, 17),
        new("e", 2137, 804, 18),
        new("e", 2136, 929, 19),
        new("SHIP AMMUNITION -", 2202, 679, 19),
        new("SIZE 1", 2202, 710, 18),
        new("mx INVENTORY", 2200, 740, 14),
        new("SHIP AMMUNITION -", 2201, 805, 19),
        new("SIZE 2", 2201, 836, 19),
        new("\"Ax INVENTORY", 2200, 866, 15),
        new("SHIP AMMUNITION -", 2201, 931, 21),
        new("SIZE 3", 2201, 962, 19),
        new("mx INVENTORY", 2199, 992, 15),
        new("SHIP AMMUNITION -", 2200, 1058, 21),
        new("SIZE 4", 2200, 1088, 20),
        new("NAX INVENTORY", 2199, 1119, 15),
        new("SHIP AMMUNITION -", 2200, 1184, 22),
        new("SIZE 5", 2200, 1215, 19),
        new("SHOP OUANTITV", 2508, 441, 14),
        new("2,040 SCU", 2511, 463, 22),
        new("43,265/SCU", 2522, 496, 15),
        new("SHOP QUANTITY", 2508, 560, 14),
        new("18, ooo SCU", 2498, 581, 22),
        new("Ä3.312/SCU", 2521, 615, 14),
        new("SHOP OUANTITY", 2507, 678, 14),
        new("12, ooo SCU", 2498, 699, 22),
        new("Ä6 , 868/SCU", 2521, 732, 14),
        new("SHOP QUANTITY", 2507, 805, 14),
        new("12,000 SCU", 2498, 827, 22),
        new("n 7. 126/SCU", 2520, 860, 15),
        new("SHOP QUANTITY", 2507, 933, 14),
        new("12,000 SCU", 2497, 955, 21),
        new("384/SCU", 2557, 989, 15),
        new("SHOP OUANTITY", 2506, 1061, 14),
        new("12, ooo SCU", 2497, 1083, 22),
        new(", 641/SCU", 2548, 1117, 15),
        new("SHOP OUANTITV", 2506, 1189, 13),
        new("12,000 scu", 2496, 1211, 22),
    ];
}
