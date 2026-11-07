using Quantumwake.Data;

namespace Quantumwake.Tests;

/// <summary>
/// Between the Windows engine's straightened copy of a tilted frame and the
/// frame, pinned to the two points measured against ONNX.
/// </summary>
public class ScreenGeometryTests
{
    [Theory]
    // This install's Freight Manager, 24 Oct 21:58, 3440x1440 at 10.3 degrees: ONNX 1431,559.
    [InlineData(1409, 612, 10.3, 3440, 1440, 1433, 558)]
    // The 4.10 video's confirmation at 3:42, 2560x1440 at -3.6 degrees: ONNX 887,628.
    [InlineData(892, 607, -3.6, 2560, 1440, 886, 632)]
    public void A_straightened_point_lands_where_onnx_saw_it(double x, double y, double angle, double w, double h, double frameX, double frameY)
    {
        var (fx, fy) = ScreenGeometry.OnFrame(x, y, new ScreenTilt(angle, w, h));
        Assert.Equal(frameX, fx, 1.0);
        Assert.Equal(frameY, fy, 1.0);
    }

    [Fact]
    public void Straightening_undoes_turning()
    {
        var tilt = new ScreenTilt(7.4, 3440, 1440);
        var (x, y) = ScreenGeometry.Straightened(ScreenGeometry.OnFrame(1200, 300, tilt).X, ScreenGeometry.OnFrame(1200, 300, tilt).Y, tilt);
        Assert.Equal(1200, x, 6);
        Assert.Equal(300, y, 6);
    }

    [Fact]
    public void A_patch_on_the_frame_holds_all_four_turned_corners()
    {
        var tilt = new ScreenTilt(10, 3440, 1440);
        var patch = new ScreenPatch(1400, 600, 120, 100);
        var onFrame = ScreenGeometry.OnFrame(patch, tilt);

        foreach (var (x, y) in new[] { (1400.0, 600.0), (1520.0, 600.0), (1400.0, 700.0), (1520.0, 700.0) })
        {
            var (fx, fy) = ScreenGeometry.OnFrame(x, y, tilt);
            Assert.InRange(fx, onFrame.Left - 1e-6, onFrame.Left + onFrame.Width + 1e-6);
            Assert.InRange(fy, onFrame.Top - 1e-6, onFrame.Top + onFrame.Height + 1e-6);
        }
    }

    /// <summary>
    /// A close read through the straight reader is cut where the text is on
    /// the frame, and comes back in the copy the whole read's lines are in -
    /// unless its caller wants it as it lies on the frame.
    /// </summary>
    [Fact]
    public async Task A_close_read_is_cut_on_the_frame_and_handed_back_straightened()
    {
        var tilt = new ScreenTilt(10.3, 3440, 1440);
        var inner = new Recording();
        var patch = new ScreenPatch(1380, 590, 130, 110);

        var straight = ScreenGeometry.Straight(inner, tilt);
        var back = Assert.Single(await straight.ReadAsync("frame.png", patch, new ScreenTreatment(2, 0)));

        Assert.Equal(ScreenGeometry.OnFrame(patch, tilt), inner.Asked);
        // The line the reader saw on the frame at 1433,558 is "B SCu" at 1409,612 in the copy.
        Assert.Equal(1409, back.Left, 1.0);
        Assert.Equal(612, back.Top, 1.0);

        var onFrame = ScreenGeometry.Straight(inner, tilt, linesOnFrame: true);
        var kept = Assert.Single(await onFrame.ReadAsync("frame.png", patch, new ScreenTreatment(2, 0)));
        Assert.Equal(1433, kept.Left, 1.0);
    }

    [Fact]
    public void A_frame_read_straight_is_read_by_the_reader_itself()
    {
        var inner = new Recording();
        Assert.Same(inner, ScreenGeometry.Straight(inner, null));
        Assert.Same(inner, ScreenGeometry.Straight(inner, new ScreenTilt(0, 2560, 1440)));
    }

    private sealed class Recording : IScreenReader
    {
        public ScreenPatch? Asked;

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, CancellationToken token = default) =>
            Task.FromResult<IReadOnlyList<ScreenTextLine>>([]);

        public Task<IReadOnlyList<ScreenTextLine>> ReadAsync(string imagePath, ScreenPatch patch, ScreenTreatment treatment, CancellationToken token = default)
        {
            Asked = patch;
            var (x, y) = ScreenGeometry.OnFrame(1409, 612, new ScreenTilt(10.3, 3440, 1440));
            return Task.FromResult<IReadOnlyList<ScreenTextLine>>([new("B SCu", x, y, 13)]);
        }
    }
}
