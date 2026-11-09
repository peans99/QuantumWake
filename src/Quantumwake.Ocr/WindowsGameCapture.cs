using System.Runtime.InteropServices;
using System.Text;
using Quantumwake.Data;

namespace Quantumwake.Ocr;

/// <summary>
/// Copies the screen over Star Citizen's window while it is the window in front.
/// </summary>
/// <remarks>
/// <para>
/// GDI's <c>BitBlt</c> from the desktop, rather than Windows.Graphics.Capture.
/// The desktop is already composited, so a game in borderless or windowed mode
/// is on it; the copy needs no capture API, no package capability, no consent
/// prompt and no yellow border, and behaves the same in the downloaded exe and
/// in the Store's MSIX - which, with a package identity, would put
/// Windows.Graphics.Capture behind a per-app privacy switch that has been
/// reported to turn itself off across updates. The cost is that a game in
/// exclusive fullscreen never reaches the desktop, and copies as black; that
/// is detected and said.
/// </para>
/// <para>
/// What it touches is the point. The game is found by its window's title and
/// class, which any program can read off the desktop; its process is never
/// opened, its memory never read, nothing is injected or hooked and no input is
/// sent. It copies only while the game is the window in front, so what it reads
/// is what the pilot is looking at, and nothing of another program's.
/// </para>
/// </remarks>
public sealed class WindowsGameCapture : IGameScreenCapture
{
    /// <summary>The window's title, as the game sets it.</summary>
    private const string GameTitle = "Star Citizen";

    /// <summary>The engine's window class; checked as well, so a browser tab or a folder named after the game is never taken for it.</summary>
    private const string GameClass = "CryENGINE";

    public GameCapture Capture(string path)
    {
        // Physical pixels for this thread alone, whatever the host process
        // declared: a window on a 150% monitor otherwise reports two-thirds of
        // its size, and the copy would be a corner of the frame.
        var previous = SetThreadDpiAwarenessContext(PerMonitorAwareV2);
        try
        {
            return CaptureAware(path);
        }
        finally
        {
            if (previous != IntPtr.Zero) SetThreadDpiAwarenessContext(previous);
        }
    }

    private static GameCapture CaptureAware(string path)
    {
        var front = GetForegroundWindow();
        if (!IsGame(front))
        {
            // The class is the one assumption here not yet seen on a running
            // game, so a window with the right title and another class says
            // what it is rather than reading as "not running".
            if (front != IntPtr.Zero && Text(front, GetWindowText).Trim() == GameTitle)
                return GameCapture.Not($"the window in front is called {GameTitle} but its class is {Text(front, GetClassName)}, not {GameClass}");
            return GameCapture.Not(AnyGameWindow() ? "Star Citizen is not the window in front" : "Star Citizen is not running");
        }
        if (IsIconic(front)) return GameCapture.Not("Star Citizen is minimised");

        if (!GetClientRect(front, out var client)) return GameCapture.Not("Windows would not say where the game window is");
        var origin = new Point();
        if (!ClientToScreen(front, ref origin)) return GameCapture.Not("Windows would not say where the game window is");

        var width = client.Right - client.Left;
        var height = client.Bottom - client.Top;
        if (width < 320 || height < 200) return GameCapture.Not("the game window is too small to read");

        return CopyScreen(origin.X, origin.Y, width, height, path);
    }

    /// <summary>
    /// Copies a rectangle of the desktop, in physical pixels, to a BMP - the
    /// half of a capture that does not depend on the game, so it can be tested
    /// on any Windows machine.
    /// </summary>
    public static GameCapture CopyScreen(int x, int y, int width, int height, string path)
    {
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, width, height);
        var old = SelectObject(memory, bitmap);
        var selected = true;
        try
        {
            if (!BitBlt(memory, 0, 0, width, height, screen, x, y, SrcCopy))
                return GameCapture.Not("Windows would not copy the screen");

            // GetDIBits wants the bitmap out of every device context first.
            SelectObject(memory, old);
            selected = false;

            var header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = height,
                Planes = 1,
                BitCount = 24,
            };
            var pixels = new byte[BmpFile.Stride(width) * height];
            if (GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref header, 0) == 0)
                return GameCapture.Not("Windows would not hand over the copied pixels");

            if (BmpFile.LooksBlank(width, height, pixels))
                return GameCapture.Not("the copy came back black - exclusive fullscreen never reaches the desktop; borderless or windowed can be read");

            BmpFile.Write(path, width, height, pixels);
            return new GameCapture(true, null, width, height);
        }
        finally
        {
            if (selected) SelectObject(memory, old);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>
    /// Why the game cannot be handed keys now, or null when it is the window in
    /// front and not minimised: the check the voice keyboard makes before every
    /// press, so a key never lands in another program.
    /// </summary>
    internal static string? WhyNotInFront()
    {
        var front = GetForegroundWindow();
        if (!IsGame(front)) return AnyGameWindow() ? "Star Citizen is not the window in front" : "Star Citizen is not running";
        return IsIconic(front) ? "Star Citizen is minimised" : null;
    }

    private static bool IsGame(IntPtr window)
    {
        if (window == IntPtr.Zero || !IsWindowVisible(window)) return false;
        return Text(window, GetWindowText).Trim() == GameTitle
            && Text(window, GetClassName).Equals(GameClass, StringComparison.OrdinalIgnoreCase);
    }

    private static bool AnyGameWindow()
    {
        var found = false;
        EnumWindows((window, _) =>
        {
            if (IsGame(window)) { found = true; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate int TextGetter(IntPtr window, StringBuilder text, int max);

    private static string Text(IntPtr window, TextGetter get)
    {
        var text = new StringBuilder(256);
        return get(window, text, text.Capacity) > 0 ? text.ToString() : string.Empty;
    }

    private static readonly IntPtr PerMonitorAwareV2 = new(-4);
    private const uint SrcCopy = 0x00CC0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr param);

    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr param);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr gdiObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr gdiObject);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint rop);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BitmapInfoHeader info, uint usage);
}
