using System.Runtime.InteropServices;
using Quantumwake.Data;

namespace Quantumwake.Ocr;

/// <summary>
/// Presses keys in Star Citizen for a voice command, the way VoiceAttack does:
/// <c>SendInput</c> with keyboard scan codes, while the game is the window in
/// front.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place the app sends the game input, and it is kept to
/// what a pilot's own hand would do: one action's key, pressed for the time
/// the game's own activation rule asks, once per spoken command. Nothing
/// is chained or repeated, and nothing is pressed into any other window -
/// the foreground is checked immediately before every key goes down.
/// </para>
/// <para>
/// Scan codes, not virtual keys, because the game binds by position: its
/// <c>a</c> is the key in that place whatever the keyboard layout prints on
/// it. If the game runs as administrator and the app does not, Windows drops
/// the keys without saying so (UIPI); that cannot be detected from here, so
/// the page says it as a possible reason.
/// </para>
/// </remarks>
public sealed class WindowsGameKeyboard : IGameKeyboard
{
    /// <summary>The gap between the two presses of a double tap, inside the game's 0.25 s window.</summary>
    private const int TapGapMs = 80;

    /// <summary>The pause after a modifier goes down, so the game sees it held before the key.</summary>
    private const int ModifierLeadMs = 30;

    private readonly Lock _gate = new();

    public KeyPress Press(KeyPlan plan)
    {
        if (!plan.CanPress) return KeyPress.Not(plan.Why ?? "nothing to press");
        // One command's keys at a time: two presses interleaved would hold a
        // modifier across the other's key.
        lock (_gate)
        {
            for (var tap = 0; tap < Math.Max(1, plan.Taps); tap++)
            {
                if (tap > 0) Thread.Sleep(TapGapMs);
                if (WindowsGameCapture.WhyNotInFront() is { } why) return KeyPress.Not(why);
                var down = new List<string>();
                try
                {
                    foreach (var key in plan.Keys.SkipLast(1))
                    {
                        if (!Send(key, up: false)) return KeyPress.Not("Windows refused the key press");
                        down.Add(key);
                        Thread.Sleep(ModifierLeadMs);
                    }
                    var last = plan.Keys[^1];
                    if (!Send(last, up: false)) return KeyPress.Not("Windows refused the key press");
                    down.Add(last);
                    Thread.Sleep(TimeSpan.FromSeconds(Math.Clamp(plan.HoldSeconds, 0.02, 10)));
                }
                finally
                {
                    // Up in reverse, always - a key left down in the game is a
                    // stuck throttle or a ship that will not stop firing.
                    for (var i = down.Count - 1; i >= 0; i--) Send(down[i], up: true);
                }
            }
        }
        return new KeyPress(true);
    }

    public bool IsDown(string key)
    {
        if (GameKeys.Code(key) is not { } code) return false;
        var vk = MapVirtualKey((uint)(code.Extended ? 0xE000 | code.Code : code.Code), MapvkVscToVkEx);
        return vk != 0 && (GetAsyncKeyState((int)vk) & 0x8000) != 0;
    }

    private static bool Send(string key, bool up)
    {
        var input = new Input();
        if (GameKeys.IsMouse(key))
        {
            var (downFlag, upFlag, data) = key.ToLowerInvariant() switch
            {
                "mouse1" => (MouseLeftDown, MouseLeftUp, 0u),
                "mouse2" => (MouseRightDown, MouseRightUp, 0u),
                "mouse3" => (MouseMiddleDown, MouseMiddleUp, 0u),
                "mouse4" => (MouseXDown, MouseXUp, 1u),
                _ => (MouseXDown, MouseXUp, 2u),
            };
            input.Type = InputMouse;
            input.Union.Mouse = new MouseInput { Flags = up ? upFlag : downFlag, MouseData = data };
        }
        else if (GameKeys.Code(key) is { } code)
        {
            input.Type = InputKeyboard;
            input.Union.Keyboard = new KeyboardInput
            {
                Scan = code.Code,
                Flags = KeyScanCode | (code.Extended ? KeyExtended : 0) | (up ? KeyUp : 0),
            };
        }
        else
        {
            return false;
        }
        return SendInput(1, [input], Marshal.SizeOf<Input>()) == 1;
    }

    private const uint InputMouse = 0;
    private const uint InputKeyboard = 1;
    private const uint KeyExtended = 0x0001;
    private const uint KeyUp = 0x0002;
    private const uint KeyScanCode = 0x0008;
    private const uint MouseLeftDown = 0x0002, MouseLeftUp = 0x0004;
    private const uint MouseRightDown = 0x0008, MouseRightUp = 0x0010;
    private const uint MouseMiddleDown = 0x0020, MouseMiddleUp = 0x0040;
    private const uint MouseXDown = 0x0080, MouseXUp = 0x0100;
    private const uint MapvkVscToVkEx = 3;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Union;
    }

    // The union is sized by its largest member, the mouse input; declaring
    // only the keyboard one makes the struct too small and SendInput refuses
    // every call.
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int Dx;
        public int Dy;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort Vk;
        public ushort Scan;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] private static extern uint MapVirtualKey(uint code, uint mapType);
}
