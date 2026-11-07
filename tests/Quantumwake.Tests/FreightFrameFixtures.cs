using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The Freight Manager terminal as the Windows engine read it, from zirHaze
/// Gaming's "Hauling in Star Citizen 4.10 - What Has Changed?" (YouTube
/// 2VUGRubmnP0, 27 August 2026, 2560x1440), and two from this install. Lines are
/// exactly what the engine returned, misreadings included - and positions as
/// it returned them before 1.1.25, in its de-rotated space on a slanted frame.
/// </summary>
internal static class FreightFrameFixtures
{
    /// <summary>1:32 - the list after loading one contract: 94/1120 SCU on the elevator, the warehouse capacity misread "n Isooo".</summary>
    public static readonly ScreenTextLine[] ListOneContract =
    [
        new("\"f FREIGHT MANAGER", 327, 205, 31),
        new("Search", 1754, 197, 14),
        new("(ELEVATOR) (WAREHOUSE)", 1057, 266, 38),
        new("SELECTED: O", 282, 345, 16),
        new("CAPACITY: 94/1120 SCU", 281, 367, 15),
        new("V: LOWERED", 564, 1186, 20),
        new("SELECT ALL SHOWN", 833, 347, 15),
        new("EXPAND am", 1103, 343, 23),
        new("UNSELECT ALL SHOWN", 1482, 334, 16),
        new("CLAIM MOOE", 2066, 267, 13),
        new("SELECTED: a", 1895, 315, 15),
        new("CAPACITY: n Isooo scu", 1897, 338, 16),
        new("CLEAR", 1906, 402, 13),
        new("MOVE TO WAREHOUSE", 956, 1176, 19),
        new("FILTERS", 1901, 420, 13),
        new("COVALEX INDEPENDENT CONTRACTORS", 1306, 463, 15),
        new("ROOKIE RANK - DIRECT MEDIUM CARGO HAUL", 1308, 477, 21),
        new("PERSONAL INVENTORY -", 1317, 762, 15),
        new("LOCATION", 1319, 785, 23),
        new("Argo ATLS", 1476, 957, 18),
        new("Registered To You", 1505, 989, 18),
        new("MOVE TO ELEVATOR", 1380, 1168, 18),
    ];

    /// <summary>4:12 - the list with two contracts' cargo in the warehouse and an empty elevator, 0/896 SCU.</summary>
    public static readonly ScreenTextLine[] ListTwoContracts =
    [
        new("\"f FREIGHT MANAGER", 403, 195, 30),
        new("Search", 1786, 193, 14),
        new("(ELEVATOR)", 1098, 264, 37),
        new("EXPAND", 1140, 341, 13),
        new("(WAREHOUSE)", 1314, 262, 37),
        new("SELECTED: O", 358, 332, 15),
        new("CAPACITY: 0/896 SCU", 355, 355, 15),
        new("i LOWERED", 576, 1164, 26),
        new("SELECT ALL SHOWN", 880, 340, 15),
        new("EXPAND", 1375, 337, 14),
        new("SELECT ALL SH&NN", 1511, 326, 23),
        new("SELECTED: O", 1920, 316, 15),
        new("CAPACITY: O/SOOO SCu", 1922, 340, 16),
        new("CLEAR", 1927, 405, 13),
        new("FILTERS", 1921, 424, 13),
        new("MOVE TO WAREHOUSE", 967, 1174, 16),
        new("COVALEX INDEPENDENT CONTRACTORS", 1332, 564, 13),
        new("ROOKIE RANK - DIRECT SMALL CARGO HAUL", 1333, 583, 21),
        new("COVALEX INDEPENDENT CONTRACTORS", 1330, 872, 14),
        new("JUNIOR RANK - DIRECT MEDIUM CARGO HAUL", 1331, 893, 22),
        new("ELEVATOR", 1733, 1382, 20),
    ];

    /// <summary>3:42 - the drop's confirmation for one contract: 8, 4 and 2 SCU of Stims, the clock misread "01:sa", every 8 read "B".</summary>
    public static readonly ScreenTextLine[] ConfirmOneContract =
    [
        new("FREIGHT MANAGER", 435, 209, 28),
        new("TRANSACTION CONFIRMATION", 707, 366, 18),
        new("CONTRACT ORDERS", 1286, 448, 14),
        new("ROOKIE RANK - DIRECT MEDIUM CARGO HAUL •", 865, 501, 12),
        new("Time Remaining: 01:sa", 865, 536, 18),
        new("This contract Will be completed after you deliver the goods to the warehouse.", 865, 555, 14),
        new("B SCU", 892, 607, 15),
        new("STIMS", 900, 671, 11),
        new("4 scu", 1025, 607, 14),
        new("2 scu", 1157, 606, 14),
        new("STIMS", 1167, 669, 11),
        new("CONFIRM", 1222, 976, 15),
        new("Search", 1693, 224, 13),
        new("ELEVATOR", 1708, 1306, 22),
    ];

    /// <summary>4:31 - two contracts on one confirmation, the 1 SCU silicon tile's digit lost ("scu"), Tungsten read "TLJNGSTEN".</summary>
    public static readonly ScreenTextLine[] ConfirmTwoContracts =
    [
        new("FREIGHT MANAGER", 435, 204, 29),
        new("TRANSACTION CONFIRMATION", 707, 363, 19),
        new("CONTRACT ORDERS", 1290, 447, 14),
        new("ROOKIE RANK • DIRECT EXTRA SMALL CARGO HAUL", 865, 500, 13),
        new("Time Remaining: 07:17", 865, 536, 18),
        new("This contract will be completed after you deliver the goads to the warehouse.", 865, 556, 15),
        new("1 scu", 895, 608, 14),
        new("tono", 903, 628, 13),
        new("TLJNGSTEN", 881, 672, 11),
        new("JUNIOR RANK - DIRECT MEDIUM CARGO HAUL", 862, 720, 13),
        new("Time Remaining: 12:17", 861, 759, 19),
        new("This contract wall be completed after you deliver the goods to the warehouse.", 862, 777, 16),
        new("62%", 50, 1241, 19),
        new("B SCU", 890, 835, 15),
        new("SILICON", 890, 902, 13),
        new("88%", 168, 1240, 18),
        new("2 scu", 1027, 834, 15),
        new("scu", 1180, 833, 15),
        new("SILICON", 1166, 900, 12),
        new("CONFIRM", 1222, 982, 15),
        new("o", 1665, 1324, 14),
        new("Search", 1703, 221, 13),
        new("ELEVATOR", 1709, 1320, 21),
    ];

    /// <summary>This install, 24 Oct 2026 21:58 at Ruin Station, 3440x1440, the terminal seen at 10.3 degrees: two Red Wind Carbon hauls, "00:00" on both clocks, the rep ladder read as a lone "R", no commodity read under any tile.</summary>
    public static readonly ScreenTextLine[] OwnCarbon =
    [
        new("\"t FREIGHT MANAGER", 1053, 269, 27),
        new("TRANSACTION CONFIRMATION", 1288, 396, 19),
        new("CONTRACT ORDERS", 1740, 462, 13),
        new("MEMBER 1 STELLAR MEDIUM HAUL 1 TO RUIN STATION R", 1397, 516, 14),
        new("Time Remaining: 00:00", 1394, 551, 15),
        new("This contract will be completed after you deliver the goods to the warehouse.", 1392, 566, 14),
        new("B SCu", 1409, 612, 13),
        new("0/27", 1417, 630, 9),
        new("MEMBER 1 STELLAR MEDIUM HAUL 1 TO RUIN STATION", 1377, 706, 14),
        new("Time Remaining: OO:OO", 1374, 737, 16),
        new("This contract will be completed after you deliver the goads to the warehouse.", 1372, 755, 14),
        new("B SCU", 1389, 800, 13),
        new("0128", 1397, 819, 9),
        new("4 scu", 1495, 801, 14),
        new("1 scu", 1606, 802, 14),
        new("CONFIRM", 1639, 921, 13),
    ];

    /// <summary>This install, 24 Oct 2026 22:14, at -5.3 degrees: two Waste hauls, the second title's rep ladder read as "tsan00/2001250tsoonoaoæoeo", an 8 read "e", the 1 SCU tile's digit lost.</summary>
    public static readonly ScreenTextLine[] OwnWaste =
    [
        new("— FREIGHT MANAGER", 849, 214, 28),
        new("TRANSACTION CONFIRMATION", 1178, 369, 20),
        new("CONTRACT ORDERS", 1729, 453, 14),
        new("MEMBER 1 STELLAR MEDIUM HAUL 1 FROM RUIN STATION", 1330, 502, 15),
        new("Time Remaining: 00:00", 1329, 536, 17),
        new("This contract will be completed after you deliver the goods to the warehouse.", 1329, 556, 14),
        new("B SCU", 1356, 605, 14),
        new("WASTE", 1361, 666, 12),
        new("2 scu", 1483, 605, 14),
        new("WASTE", 1488, 666, 11),
        new("scu", 1623, 605, 14),
        new("on", 1627, 625, 11),
        new("WASTE", 1614, 666, 11),
        new("MEMBER 1 STELLAR MEDIUM HAUL 1 FROM RUIN STATION tsan00/2001250tsoonoaoæoeo,•", 1329, 711, 16),
        new("Time Remaining: 00:00", 1328, 749, 18),
        new("This contract will be completed after you deliver the goods to the warehouse.", 1329, 767, 16),
        new("e scu", 1356, 822, 15),
        new("2 scu", 1486, 821, 15),
        new("WASTE", 1491, 885, 12),
        new("CONFIRM", 1670, 961, 15),
    ];
}
