using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Brushes = System.Windows.Media.Brushes;

namespace Quantumwake.Overlay;

/// <summary>
/// Fills a monitor with black, with the MFD openings cut out of it.
/// </summary>
/// <remarks>
/// <para>
/// A Cougar frame is a bezel with a square hole in it, screwed over part of a
/// monitor. Everything the frame does not cover still glows: wallpaper, the
/// taskbar, whatever window happens to be there. In a dark cockpit that light
/// leaks around the edge of the frame and washes out the instrument inside it.
/// This is the backdrop that stops it.
/// </para>
/// <para>
/// The openings are cut out with a window region rather than left to z-order.
/// Two topmost windows have no guaranteed order between them, and if the
/// backdrop ever won that race the pilot would get a black square where the
/// instrument should be - a failure that looks exactly like a crash. A window
/// with holes in it cannot lose a race it is not in.
/// </para>
/// </remarks>
internal sealed class MfdBlackout : Window
{
    public MfdBlackout()
    {
        Title = "Quantum Wake · MFD backdrop";
        Background = Brushes.Black;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = true;
        Width = Height = 1;
        SourceInitialized += (_, _) => NativeWindowStyles.ApplyBackdropStyles(this);
    }

    /// <summary>The geometry currently cut, so an unchanged re-apply costs nothing.</summary>
    private string _shape = "";

    /// <summary>Covers one monitor, less the openings its panels occupy.</summary>
    public void Cover(MfdMonitor monitor, IReadOnlyList<MfdPanel> panels)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        // Dragging a panel in setup re-applies the whole layout every frame, and
        // recutting a full-screen region repaints a full-screen window. Skip the
        // backdrops that did not move; only the monitor being dragged on pays.
        var shape = string.Join(";", panels.Select(p => $"{p.X},{p.Y},{p.Width},{p.Height}"))
            + $"|{monitor.X},{monitor.Y},{monitor.Width},{monitor.Height}";
        if (shape == _shape) return;
        _shape = shape;

        // Physical pixels, the same way MfdWindow.Place works, so the holes and
        // the panels are measured in one coordinate system rather than two.
        var previous = SetThreadDpiAwarenessContext(new IntPtr(-4));
        try
        {
            SetWindowPos(handle, new IntPtr(-1),
                monitor.X, monitor.Y, monitor.Width, monitor.Height, 0x10);

            // Region coordinates are relative to the window's own top-left, and
            // a panel's X and Y are already relative to the monitor this now
            // covers exactly - so they are the same numbers.
            var region = CreateRectRgn(0, 0, monitor.Width, monitor.Height);
            foreach (var panel in panels)
            {
                var opening = CreateRectRgn(
                    panel.X, panel.Y, panel.X + panel.Width, panel.Y + panel.Height);
                CombineRgn(region, region, opening, RgnDiff);
                DeleteObject(opening);
            }

            // The window owns the region from here; deleting it would be a
            // double free the moment the window is destroyed.
            SetWindowRgn(handle, region, true);
        }
        finally { if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous); }
    }

    /// <summary>The region actually in force, for tests. Empty before it is placed.</summary>
    public Rect RegionBox()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || GetWindowRgnBox(handle, out var box) == 0) return Rect.Empty;
        return new Rect(box.Left, box.Top, box.Right - box.Left, box.Bottom - box.Top);
    }

    private const int RgnDiff = 4;

    [DllImport("user32.dll")]
    private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr hwnd, IntPtr region, [MarshalAs(UnmanagedType.Bool)] bool redraw);
    [DllImport("user32.dll")]
    private static extern int GetWindowRgnBox(IntPtr hwnd, out NativeRect box);
    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr a, IntPtr b, int mode);
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }
}
