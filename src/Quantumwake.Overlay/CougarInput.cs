using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Quantumwake.Overlay;

internal sealed class CougarInput : IDisposable
{
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly Dictionary<uint, (int Number, CougarEdges Edges)> _devices = [];
    private DateTime _nextScan;
    public event Action<int, int>? Pressed;
    public event Action<int[]>? DevicesChanged;
    public int[] Connected => _devices.Values.Select(d => d.Number).Order().ToArray();

    public CougarInput()
    {
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void Poll()
    {
        if (DateTime.UtcNow >= _nextScan)
        {
            _nextScan = DateTime.UtcNow.AddSeconds(2);
            var found = new Dictionary<uint, int>();
            for (uint id = 0; id < Math.Min(JoyGetNumDevs(), 16); id++)
            {
                if (JoyGetDevCaps(id, out var caps, (uint)Marshal.SizeOf<JoyCaps>()) != 0) continue;
                var match = Regex.Match(DeviceName(id, caps), @"\bF16 MFD ([1-8])\b", RegexOptions.IgnoreCase);
                if (match.Success && TryRead(id, out _)) found[id] = int.Parse(match.Groups[1].Value);
            }
            var before = string.Join(",", Connected);
            foreach (var id in _devices.Keys.ToArray())
                if (!found.TryGetValue(id, out var number) || number != _devices[id].Number) _devices.Remove(id);
            foreach (var (id, number) in found)
                _devices.TryAdd(id, (number, new CougarEdges()));
            if (before != string.Join(",", Connected)) DevicesChanged?.Invoke(Connected);
        }
        foreach (var (id, device) in _devices.ToArray())
        {
            if (!TryRead(id, out var buttons))
            {
                device.Edges.Disconnect();
                _devices.Remove(id);
                DevicesChanged?.Invoke(Connected);
                continue;
            }
            // Duplicate firmware numbers are ambiguous; the setup tester still shows the devices.
            if (_devices.Values.Count(d => d.Number == device.Number) != 1) continue;
            foreach (var button in device.Edges.Read(buttons)) Pressed?.Invoke(device.Number, button);
        }
    }

    private static bool TryRead(uint id, out uint buttons)
    {
        var state = new JoyState { Size = (uint)Marshal.SizeOf<JoyState>(), Flags = 0x80 };
        var result = JoyGetPosEx(id, ref state);
        buttons = state.Buttons;
        return result == 0;
    }

    public void Dispose() => _timer.Stop();

    private static string DeviceName(uint id, JoyCaps caps)
    {
        // On this install every szPname is "Microsoft PC-joystick driver".
        // The slot's OEM entry carries the actual F16 MFD number, unlike the driver name.
        foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var slots = hive.OpenSubKey(@"System\CurrentControlSet\Control\MediaResources\Joystick\"
                    + caps.RegistryKey + @"\CurrentJoystickSettings");
                if (slots?.GetValue($"Joystick{id + 1}OEMName") is not string oem) continue;
                foreach (var namesHive in new[] { Registry.CurrentUser, Registry.LocalMachine })
                {
                    using var names = namesHive.OpenSubKey(
                        @"System\CurrentControlSet\Control\MediaProperties\PrivateProperties\Joystick\OEM\" + oem);
                    if (names?.GetValue("OEMName") is string name && name.Length > 0) return name;
                }
            }
            catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
            { /* Fall back to the name supplied by the driver when OEM metadata is inaccessible. */ }
        }
        return caps.Name ?? "";
    }

    [DllImport("winmm.dll", EntryPoint = "joyGetNumDevs")]
    private static extern uint JoyGetNumDevs();
    [DllImport("winmm.dll", EntryPoint = "joyGetDevCapsW", CharSet = CharSet.Unicode)]
    private static extern uint JoyGetDevCaps(nuint id, out JoyCaps caps, uint size);
    [DllImport("winmm.dll", EntryPoint = "joyGetPosEx")]
    private static extern uint JoyGetPosEx(uint id, ref JoyState state);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoyCaps
    {
        public ushort Manufacturer, Product;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public uint XMin, XMax, YMin, YMax, ZMin, ZMax, Buttons, PeriodMin, PeriodMax;
        public uint RMin, RMax, UMin, UMax, VMin, VMax, Caps, MaxAxes, Axes, MaxButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegistryKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Oem;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct JoyState
    {
        public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2;
    }
}
