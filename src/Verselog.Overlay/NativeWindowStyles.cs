using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Verselog.Overlay;

/// <summary>
/// Window-style helpers for a safe, OS-level game overlay.
/// </summary>
/// <remarks>
/// <para>
/// Everything here uses documented Win32 window styles only. The overlay never
/// injects a DLL into the game and never hooks DirectX or WinAPI functions -
/// those techniques are what get tools flagged by Easy Anti-Cheat, and they are
/// permanently out of scope for this project.
/// </para>
/// <para>
/// One consequence worth surfacing to users: an always-on-top window is not
/// composited over an <i>exclusive fullscreen</i> Direct3D swap chain. Star
/// Citizen must run in Borderless Windowed for the overlay to be visible. This
/// is a limitation of doing things the safe way, not a bug.
/// </para>
/// </remarks>
internal static partial class NativeWindowStyles
{
    private const int GWL_EXSTYLE = -20;

    /// <summary>Mouse input passes through to the window beneath.</summary>
    private const int WS_EX_TRANSPARENT = 0x00000020;

    /// <summary>Keeps the window out of the Alt+Tab list.</summary>
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    /// <summary>The window never takes focus from the game.</summary>
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private const int WS_EX_LAYERED = 0x00080000;

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static partial int GetWindowLong(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static partial int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int LWA_ALPHA = 0x2;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte alpha, uint flags);

    /// <summary>
    /// Sets uniform window translucency at the HWND level.
    /// </summary>
    /// <remarks>
    /// Used instead of WPF's <c>AllowsTransparency</c>, which is not supported
    /// with WebView2: in that mode WPF renders the window as a layered surface
    /// and the OS hit-tests it from the alpha WPF painted. WebView2 draws itself
    /// through child HWNDs, so the region behind it stays alpha 0, reads as "no
    /// window here", and every click is dropped before the browser sees it -
    /// producing an overlay that looks interactive but ignores input.
    /// A uniform layered alpha keeps hit-testing intact.
    /// </remarks>
    /// <param name="alpha">0 fully transparent, 255 fully opaque.</param>
    public static void SetWindowAlpha(Window window, byte alpha)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        SetLayeredWindowAttributes(handle, 0, alpha, LWA_ALPHA);
    }

    /// <summary>Applies the base overlay styles: tool window, never activated.</summary>
    public static void ApplyOverlayStyles(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var style = GetWindowLong(handle, GWL_EXSTYLE);
        SetWindowLong(handle, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED);
    }

    /// <summary>
    /// Turns mouse click-through on or off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="WS_EX_NOACTIVATE"/> is cleared alongside
    /// <see cref="WS_EX_TRANSPARENT"/>, and this matters: a window that cannot
    /// activate never takes focus, and the hosted WebView2 will not route mouse
    /// input to the page without it. Clearing only WS_EX_TRANSPARENT produces an
    /// overlay that looks interactive but silently ignores every click.
    /// </para>
    /// <para>
    /// Both flags go back on when click-through is restored, so the informational
    /// mode still never steals focus from the game.
    /// </para>
    /// </remarks>
    public static void SetClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var style = GetWindowLong(handle, GWL_EXSTYLE);

        SetWindowLong(handle, GWL_EXSTYLE, enabled
            ? style | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE
            : style & ~WS_EX_TRANSPARENT & ~WS_EX_NOACTIVATE);
    }

    /// <summary>Registers a system-wide hotkey. Returns false if it is already taken.</summary>
    public static bool RegisterGlobalHotKey(Window window, int id, uint modifiers, uint virtualKey) =>
        RegisterHotKey(new WindowInteropHelper(window).Handle, id, modifiers, virtualKey);

    public static void UnregisterGlobalHotKey(Window window, int id) =>
        UnregisterHotKey(new WindowInteropHelper(window).Handle, id);

    public static class Modifiers
    {
        public const uint Alt = 0x0001;
        public const uint Control = 0x0002;
        public const uint Shift = 0x0004;

        /// <summary>Stops the hotkey auto-repeating while held.</summary>
        public const uint NoRepeat = 0x4000;
    }
}
