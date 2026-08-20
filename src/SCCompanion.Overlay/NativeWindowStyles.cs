using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SCCompanion.Overlay;

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
    /// Turns mouse click-through on or off. On means the overlay is purely
    /// informational and every click reaches the game.
    /// </summary>
    public static void SetClickThrough(Window window, bool enabled)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero)
            return;

        var style = GetWindowLong(handle, GWL_EXSTYLE);

        SetWindowLong(handle, GWL_EXSTYLE, enabled
            ? style | WS_EX_TRANSPARENT
            : style & ~WS_EX_TRANSPARENT);
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
