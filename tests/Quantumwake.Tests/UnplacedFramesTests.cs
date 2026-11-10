using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// The shape of a frame no reader placed (1.2.24), on frames shaped like this
/// install's own Unknown screenshots. Chat is written fresh rather than copied:
/// the real frames carry other players' handles, and those stay out of the
/// repository.
/// </summary>
public sealed class UnplacedFramesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "qw-frames-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static IReadOnlyList<ScreenTextLine> Lines(params string[] texts) =>
        [.. texts.Select((t, i) => new ScreenTextLine(t, 100, 100 + i * 24, 18))];

    [Fact]
    public void A_black_screenshot_is_no_text() =>
        Assert.Equal("no-text", UnplacedFrames.Shape(Lines()));

    [Fact]
    public void Gauges_and_glyphs_alone_are_no_text_either() =>
        Assert.Equal("no-text", UnplacedFrames.Shape(Lines("e", "12", "Ä", "0.0")));

    [Fact]
    public void The_looting_view_is_the_inventory()
    {
        // The furniture of the looting view, as the engine read it on 8 Sep 2026.
        Assert.Equal("inventory", UnplacedFrames.Shape(Lines(
            "LOOTING VIEW", "MOVE ALL", "PERSONAL/ ALL", "CAPACITY:", "TILTERS", "EQUIPMENT", "CLICK DRAG", "QUICK EQUIP", "CLOSE INVENTORY")));
    }

    [Fact]
    public void Global_chat_over_a_lobby_is_chat()
    {
        // The shape of an 11 Sep 2026 screenshot: five tagged lines of
        // thirteen, the engine's IGLOBALI and (GLOBALI for [GLOBAL], messages
        // wrapping onto untagged lines, a few HUD words. Handles invented.
        Assert.Equal("chat", UnplacedFrames.Shape(Lines(
            "has anyone else lost the ships in storage after the", "patch? nothing on the list",
            "IGLOBALI PilotOne: claim them again", "[GLOBALI no luck", "[GLOBALI PilotTwo: there's one on the pad",
            "IGLOBALI PilotOne:", "open the local tab at the top and pick the", "missing ship",
            "(GLOBALI PilotThree: try the other terminal", "CKER1226", "INTERACT (OFFSCREEN)", "ERACT MODE", "PIT MENU L-Alt +")));
    }

    [Fact]
    public void The_flight_hud_is_known_by_its_gauges() =>
        Assert.Equal("flight-hud", UnplacedFrames.Shape(Lines("H-FUEL", "Q-FUEL", "SCM", "GEAR • CPLD", "PITMAN LASER")));

    [Fact]
    public void A_screen_of_its_own_is_other_and_a_few_words_are_few()
    {
        Assert.Equal("few-words", UnplacedFrames.Shape(Lines("SEAT", "FIRE")));
        Assert.Equal("other", UnplacedFrames.Shape(Lines(
            "JOURNAL ENTRIES", "CORNERSTONE DEVELOPMENTS' GUIDE TO", "MINING FUNDAMENTALS BASIC OVERVIEW",
            "MINING FUNDAMENTALS •2: WHERE TO MINE", "A SALVAGER'S GUIDE TO MATERIAL COLLECTION", "REFUELING PROTOCOLS")));
    }

    [Fact]
    public void A_week_of_frame_logs_is_counted_by_kind_and_shape()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllLines(Path.Combine(_folder, "frames-2026-11-06.csv"),
        [
            LiveFrameLog.Header,
            "2026-11-06T21:00:00.000-05:00,read,Target,10,130,3440,1440,,",
            "2026-11-06T21:00:01.000-05:00,read,Unknown,10,900,3440,1440,,",
            "2026-11-06T21:00:02.000-05:00,waiting,,0,0,0,0,,\"Star Citizen is not in front, so nothing is read\"",
        ]);
        File.WriteAllLines(Path.Combine(_folder, "frames-2026-11-10.csv"),
        [
            LiveFrameLog.Header,
            "2026-11-10T21:00:00.000-05:00,read,Unknown/chat,10,900,3440,1440,,",
            "2026-11-10T21:00:02.000-05:00,read,Unknown/no-text,10,900,3440,1440,,",
            "2026-11-10T21:00:04.000-05:00,read,Unknown/no-text,10,900,3440,1440,,",
            "2026-11-10T21:00:06.000-05:00,read,Contracts,10,900,3440,1440,live-20261110-020000,",
        ]);

        var tally = UnplacedFrames.Tally(_folder);

        Assert.Equal(6, tally.Frames);
        Assert.Equal(4, tally.Kinds["Unknown"]);
        Assert.Equal(1, tally.Kinds["Target"]);
        Assert.Equal(1, tally.Kinds["Contracts"]);
        Assert.Equal(2, tally.Shapes["no-text"]);
        Assert.Equal(1, tally.Shapes["chat"]);
        Assert.Equal(new DateTimeOffset(2026, 11, 6, 21, 0, 0, TimeSpan.FromHours(-5)), tally.From);
    }

    [Fact]
    public void The_shapes_are_the_apps_own_words()
    {
        // The frame log promises no text from the screen: a shape is one of
        // a fixed list, whatever the frame said.
        var shapes = UnplacedFrames.Shapes.Select(s => s.Shape).ToHashSet();
        foreach (var frame in new[] { Lines(), Lines("SEAT"), Lines("[GLOBAL] Someone: hello", "[GLOBAL] Other: hi", "[GLOBAL] Third: yo") })
            Assert.Contains(UnplacedFrames.Shape(frame), shapes);
    }
}
