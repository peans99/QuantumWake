using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace Quantumwake.Overlay;

/// <summary>
/// Cougar MFD buttons, read as Raw Input on a message-only window of its own.
/// </summary>
/// <remarks>
/// <para>
/// This used to poll winmm's joystick API, and plugging in a USB device could
/// take the app down. winmm answers <c>joyGetDevCapsW</c> through the legacy
/// DirectInput, which rebuilds its HID list when the device set changes, and
/// on this install that rebuild corrupted the process heap. Five crash dumps
/// between 2026-10-15 and 2026-10-19 all die in <c>dinput!DIHid_BuildHidList</c>
/// reallocating its own list - four as heap corruption, one as an access
/// violation. A fault inside Windows is not something a try/catch can stand
/// between, so the reader no longer goes near DirectInput.
/// </para>
/// <para>
/// Raw Input rather than Windows.Gaming.Input, which the Controls page uses:
/// the MFDs are pressed while the game has focus, and <c>RIDEV_INPUTSINK</c>
/// is documented to deliver then. Whether Windows.Gaming.Input keeps reading
/// for a process that is not in the foreground was never settled.
/// </para>
/// </remarks>
internal sealed class CougarInput : IDisposable
{
    private readonly HwndSource _window;
    private readonly Dictionary<nint, (int Number, CougarEdges Edges, byte[] Preparsed)> _devices = [];
    private readonly Dictionary<nint, string> _names = [];
    public event Action<int, int>? Pressed;
    public event Action<int[]>? DevicesChanged;
    public int[] Connected => _devices.Values.Select(d => d.Number).Order().ToArray();

    public CougarInput()
    {
        _window = new HwndSource(new HwndSourceParameters("Quantum Wake MFD input") { ParentWindow = new IntPtr(-3) });
        _window.AddHook(Hook);
        // Both usages: the Warthog stick reports as a joystick, but on this
        // install both Cougar MFDs report as game pads.
        Register(InputSink | DevNotify, _window.Handle);
        Rescan();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // Neither is marked handled: WM_INPUT needs DefWindowProc to release
        // the input it carries.
        if (msg == WmInputDeviceChange) Rescan();
        else if (msg == WmInput) Read(lParam);
        return IntPtr.Zero;
    }

    private void Rescan()
    {
        var controllers = GameControllers();
        foreach (var stale in _names.Keys.Except(controllers).ToArray()) _names.Remove(stale);
        var found = new Dictionary<nint, int>();
        foreach (var device in controllers)
        {
            if (!_names.TryGetValue(device, out var name)) _names[device] = name = ProductName(device);
            var match = Regex.Match(name, @"\bF16 MFD ([1-8])\b", RegexOptions.IgnoreCase);
            if (match.Success) found[device] = int.Parse(match.Groups[1].Value);
        }

        var before = string.Join(",", Connected);
        foreach (var (device, entry) in _devices.ToArray())
            if (!found.TryGetValue(device, out var number) || number != entry.Number) _devices.Remove(device);
        foreach (var (device, number) in found)
        {
            if (_devices.ContainsKey(device) || PreparsedData(device) is not { } preparsed) continue;
            // Raw Input only speaks when a button changes, so waiting for a
            // first report to learn the resting state would spend the first
            // press on it.
            var edges = new CougarEdges();
            edges.Read(0);
            _devices[device] = (number, edges, preparsed);
        }
        if (before != string.Join(",", Connected)) DevicesChanged?.Invoke(Connected);
    }

    private void Read(IntPtr input)
    {
        uint size = 0;
        if (GetRawInputData(input, RidInput, null, ref size, HeaderSize) != 0 || size == 0) return;
        var buffer = new byte[size];
        if (GetRawInputData(input, RidInput, buffer, ref size, HeaderSize) == uint.MaxValue) return;

        // RAWINPUTHEADER: type, size, then the device handle.
        var device = (nint)(IntPtr.Size == 8 ? BitConverter.ToInt64(buffer, 8) : BitConverter.ToInt32(buffer, 8));
        if (!_devices.TryGetValue(device, out var entry)) return;
        // Duplicate firmware numbers are ambiguous; the setup tester still shows the devices.
        if (_devices.Values.Count(d => d.Number == entry.Number) != 1) return;

        var reportSize = BitConverter.ToInt32(buffer, HeaderSize);
        var count = BitConverter.ToInt32(buffer, HeaderSize + 4);
        var usages = new ushort[128];
        for (var i = 0; i < count; i++)
        {
            var report = buffer.AsSpan(HeaderSize + 8 + i * reportSize, reportSize).ToArray();
            var length = (uint)usages.Length;
            // A report with no buttons in it says nothing about them, so it
            // must not read as every button released.
            if (HidP_GetUsages(HidPInput, ButtonPage, 0, usages, ref length, entry.Preparsed, report, (uint)report.Length) != HidPSuccess)
                continue;
            foreach (var button in entry.Edges.Read(CougarEdges.Buttons(usages.AsSpan(0, (int)length))))
                Pressed?.Invoke(entry.Number, button);
        }
    }

    public void Dispose()
    {
        Register(Remove, IntPtr.Zero);
        _window.Dispose();
    }

    private static void Register(uint flags, IntPtr target)
    {
        RawInputDevice[] devices =
        [
            new() { UsagePage = 1, Usage = Joystick, Flags = flags, Target = target },
            new() { UsagePage = 1, Usage = GamePad, Flags = flags, Target = target },
        ];
        RegisterRawInputDevices(devices, (uint)devices.Length, (uint)Marshal.SizeOf<RawInputDevice>());
    }

    private static List<nint> GameControllers()
    {
        var entrySize = (uint)Marshal.SizeOf<RawInputDeviceList>();
        uint count = 0;
        if (GetRawInputDeviceList(null, ref count, entrySize) == uint.MaxValue || count == 0) return [];
        var list = new RawInputDeviceList[count];
        var filled = GetRawInputDeviceList(list, ref count, entrySize);
        if (filled == uint.MaxValue) return [];

        var controllers = new List<nint>();
        foreach (var entry in list.Take((int)filled))
        {
            if (entry.Type != RimTypeHid) continue;
            // RID_DEVICE_INFO is 32 bytes, sized by its keyboard member; the
            // call refuses any other cbSize. The HID usage sits at 20 and 22.
            var info = new byte[32];
            BitConverter.TryWriteBytes(info, (uint)info.Length);
            var infoSize = (uint)info.Length;
            if (GetRawInputDeviceInfoBytes(entry.Device, RidiDeviceInfo, info, ref infoSize) == uint.MaxValue) continue;
            if (BitConverter.ToUInt16(info, 20) == 1 && BitConverter.ToUInt16(info, 22) is Joystick or GamePad)
                controllers.Add(entry.Device);
        }
        return controllers;
    }

    private static string ProductName(nint device)
    {
        // The HID product string is what Windows copies into the joystick OEM
        // registry entry the winmm reader resolved: "F16 MFD 1".
        uint length = 0;
        GetRawInputDeviceInfoChars(device, RidiDeviceName, null, ref length);
        if (length == 0) return "";
        var path = new char[length];
        if (GetRawInputDeviceInfoChars(device, RidiDeviceName, path, ref length) == uint.MaxValue) return "";

        // No access rights asked for: enough to read a HID device's strings,
        // and it still opens a device another program holds.
        using var handle = CreateFile(new string(path).TrimEnd('\0'), 0, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
        if (handle.IsInvalid) return "";
        var name = new char[127];
        return HidD_GetProductString(handle, name, (uint)name.Length * 2) ? new string(name).TrimEnd('\0') : "";
    }

    private static byte[]? PreparsedData(nint device)
    {
        uint size = 0;
        if (GetRawInputDeviceInfoBytes(device, RidiPreparsedData, null, ref size) != 0 || size == 0) return null;
        var data = new byte[size];
        return GetRawInputDeviceInfoBytes(device, RidiPreparsedData, data, ref size) == uint.MaxValue ? null : data;
    }

    private const ushort Joystick = 4, GamePad = 5, ButtonPage = 9;
    private const int WmInputDeviceChange = 0x00FE, WmInput = 0x00FF;
    private const uint InputSink = 0x100, DevNotify = 0x2000, Remove = 0x1;
    private const uint RimTypeHid = 2, RidInput = 0x10000003;
    private const uint RidiPreparsedData = 0x20000005, RidiDeviceName = 0x20000007, RidiDeviceInfo = 0x2000000b;
    private const uint FileShareReadWrite = 3, OpenExisting = 3;
    private const int HidPInput = 0, HidPSuccess = 0x00110000;
    private static readonly int HeaderSize = 8 + 2 * IntPtr.Size;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterRawInputDevices(RawInputDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputDeviceList([In, Out] RawInputDeviceList[]? list, ref uint count, uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoBytes(nint device, uint command, [In, Out] byte[]? data, ref uint size);
    [DllImport("user32.dll", EntryPoint = "GetRawInputDeviceInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetRawInputDeviceInfoChars(nint device, uint command, [Out] char[]? data, ref uint size);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetRawInputData(IntPtr input, uint command, [Out] byte[]? data, ref uint size, int headerSize);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.U1)]
    private static extern bool HidD_GetProductString(SafeFileHandle device, [Out] char[] buffer, uint bytes);
    [DllImport("hid.dll")]
    private static extern int HidP_GetUsages(int reportType, ushort usagePage, ushort linkCollection,
        [Out] ushort[] usages, ref uint length, byte[] preparsed, byte[] report, uint reportLength);

    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDevice
    {
        public ushort UsagePage, Usage;
        public uint Flags;
        public IntPtr Target;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct RawInputDeviceList
    {
        public nint Device;
        public uint Type;
    }
}
