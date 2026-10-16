using System.IO;
using System.Text.Json;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Quantumwake.Overlay;

internal sealed class MfdController : IDisposable
{
    private readonly string _root;
    private readonly Action<string> _notify;
    private readonly string _path = Core.AppPaths.In("mfd.json");
    private readonly Dictionary<string, MfdWindow> _windows = [];
    private readonly Dictionary<string, MfdBlackout> _blackouts = [];
    private readonly DispatcherTimer _displays = new() { Interval = TimeSpan.FromSeconds(2) };
    private MfdMonitor[] _monitors = MfdWindow.Monitors();
    private MfdLayout _saved;
    private MfdLayout _active;
    private CougarInput? _input;
    private MfdWindow? _setup;
    private bool _preview;
    private bool _dirty;
    private bool _disposing;

    /// <summary>The monitor the backdrop is currently keeping clear for setup.</summary>
    private string? _busy;

    public MfdController(string root, Action<string> notify)
    {
        _root = root;
        _notify = notify;
        _saved = MfdLayout.Default(_monitors);
        try
        {
            if (File.Exists(_path)
                && JsonSerializer.Deserialize<MfdLayout>(File.ReadAllText(_path), MfdLayout.JsonOptions) is { } stored)
            {
                // Explained before it is validated: validating is what moves a
                // panel to its screen's new name, and the note is about that move.
                if (MfdLayout.Explain(stored, _monitors) is { } note) _notify(Worded(note));
                _saved = stored.Validate(_monitors);
            }
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        { _notify("MFD settings could not be read. Open MFD setup to place the displays again."); }
        _active = _saved;
        Apply(_saved, false);
        RegisterSetupHotkey();
        _displays.Tick += (_, _) =>
        {
            var monitors = MfdWindow.Monitors();
            if (_monitors.SequenceEqual(monitors)) return;
            _monitors = monitors;
            Apply(_active.Validate(_monitors), _preview);
            _setup?.Send(new { type = "monitors", monitors = _monitors });
        };
        _displays.Start();
    }

    public void OpenSetup()
    {
        if (_setup is not null) { _setup.Activate(); return; }
        _dirty = false;
        _setup = new MfdWindow(_root + "mfd-setup.html", setup: true);
        _setup.Ready += SendState;
        _setup.Message += Receive;
        _setup.Closing += (_, close) =>
        {
            if (_disposing || !_dirty) return;

            // The preview follows every edit, so a closing setup window once
            // looked like a saved one until its frames sprang back. Ask here,
            // at the moment the transient layout would actually be discarded.
            var discard = System.Windows.MessageBox.Show(
                _setup,
                DesktopText.T("Discard the unsaved MFD changes? The frames will return to the last saved layout."),
                DesktopText.T("Discard MFD changes?"),
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.No);
            if (discard != System.Windows.MessageBoxResult.Yes) close.Cancel = true;
        };
        _setup.Closed += (_, _) => { _setup = null; _dirty = false; Apply(_saved, false); };
        // Dragged onto the cockpit monitor, setup takes that monitor's backdrop
        // down with it; dragged off again, it comes back. Only on a change of
        // monitor - a drag raises this on every pixel.
        _setup.LocationChanged += (_, _) =>
        {
            if (_setup?.MonitorId(_monitors) != _busy) Backdrops(_active);
        };
        EnsureInput();
        _setup.Show();
        _setup.Activate();
        Backdrops(_active);
    }

    private const int SetupHotkeyId = 0xA21;
    private const uint VkM = 0x4D;
    private HwndSource? _hotkeys;

    /// <summary>
    /// Ctrl+Alt+M opens MFD setup from anywhere.
    /// </summary>
    /// <remarks>
    /// On a message-only window of its own rather than the overlay's, whose
    /// hotkeys exist only once the overlay has been shown - and a pilot who
    /// never uses the overlay is exactly the pilot with frames on the wrong
    /// screen and no other way in. Said out loud when another app holds the
    /// keys, since a shortcut that silently does nothing is worse than none.
    /// </remarks>
    private void RegisterSetupHotkey()
    {
        _hotkeys = new HwndSource(new HwndSourceParameters("Quantum Wake MFD hotkeys") { ParentWindow = new IntPtr(-3) });
        _hotkeys.AddHook((IntPtr _, int msg, IntPtr wParam, IntPtr _, ref bool handled) =>
        {
            if (msg == 0x0312 && wParam.ToInt32() == SetupHotkeyId) { OpenSetup(); handled = true; }
            return IntPtr.Zero;
        });
        const uint keys = NativeWindowStyles.Modifiers.Control | NativeWindowStyles.Modifiers.Alt
            | NativeWindowStyles.Modifiers.NoRepeat;
        if (!NativeWindowStyles.RegisterGlobalHotKey(_hotkeys.Handle, SetupHotkeyId, keys, VkM))
            _notify(DesktopText.T("Ctrl+Alt+M is already in use by another app; open MFD setup from the tray or the dashboard."));
    }

    /// <summary>
    /// A layout warning in the dashboard's language. MfdLayout writes it in
    /// English - the screen tests compile that file on its own - so the one
    /// with figures in it is matched here and reworded with them.
    /// </summary>
    private static string Worded(string note)
    {
        var lost = System.Text.RegularExpressions.Regex.Match(note, @"^The MFD screen \((\d+)×(\d+)\) was not found");
        return lost.Success
            ? DesktopText.T("The MFD screen ({width}×{height}) was not found, so the MFDs are off. Reconnect it, or open MFD setup (Ctrl+Alt+M) to place them again.",
                ("width", lost.Groups[1].Value), ("height", lost.Groups[2].Value))
            : DesktopText.T(note);
    }

    private void SendState() => _setup?.Send(new {
        type = "setup", monitors = _monitors, layout = _saved.Validate(_monitors), devices = _input?.Connected ?? []
    });

    private void Receive(JsonElement message)
    {
        try
        {
            var type = message.GetProperty("type").GetString();
            if (type == "dirty")
            {
                _dirty = message.TryGetProperty("dirty", out var dirty) && dirty.ValueKind == JsonValueKind.True;
                return;
            }
            if (type == "stopPreview") { Apply(_saved, false); return; }
            if (type is not ("save" or "preview")) return;
            var layout = message.GetProperty("layout").Deserialize<MfdLayout>(MfdLayout.JsonOptions)
                ?.Validate(_monitors) ?? throw new ArgumentException("The MFD layout is missing.");
            if (type == "save")
            {
                layout = layout.Stamped(_monitors);
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(layout, MfdLayout.JsonOptions));
                File.Move(_path + ".tmp", _path, overwrite: true);
                _saved = layout;
                _dirty = false;
            }
            // A save while the preview is up leaves it up. Writing the file is
            // not a reason to take the picture away, and dropping the preview
            // here is what made every change after the first save invisible
            // until the pilot saved again.
            var previewing = type == "preview" || _preview;
            Apply(layout, previewing);
            _setup?.Send(new { type = "result", ok = true, saved = type == "save", message = type switch
            {
                "save" when previewing => "Layout saved. The preview is still following the editor.",
                "save" => "Layout saved.",
                _ => "Alignment preview is visible. Save layout to keep these changes."
            } });
        }
        catch (Exception e) when (e is JsonException or ArgumentException or InvalidOperationException
            or KeyNotFoundException or IOException or UnauthorizedAccessException)
        { _setup?.Send(new { type = "result", ok = false, message = e.Message }); }
    }

    private void EnsureInput()
    {
        if (_input is not null) return;
        _input = new CougarInput();
        _input.DevicesChanged += devices =>
        {
            _setup?.Send(new { type = "devices", devices });
            SendDeviceStatus();
        };
        _input.Pressed += (cougar, button) =>
        {
            _setup?.Send(new { type = "button", cougar, button });
            foreach (var panel in _active.Panels.Where(p => p.Cougar == cougar))
                if (_windows.TryGetValue(panel.Id, out var window)) window.Send(new { type = "button", button });
        };
    }

    /// <summary>Everything a display draws itself from: its map and its screen.</summary>
    private static object Display(MfdLayout layout) => new
    {
        type = "display", buttons = layout.Buttons,
        brightness = layout.Brightness, textScale = layout.TextScale,
        sleepAfterMinutes = layout.SleepAfterMinutes
    };

    private void SendDeviceStatus()
    {
        foreach (var p in _active.Panels)
            if (_windows.TryGetValue(p.Id, out var window))
                window.Send(new { type = "device", cougar = p.Cougar,
                    connected = (_input?.Connected.Count(n => n == p.Cougar) ?? 0) == 1 });
    }

    private void Apply(MfdLayout layout, bool preview)
    {
        _active = layout;
        _preview = preview;
        foreach (var panel in layout.Panels)
        {
            var monitor = MfdLayout.Locate(panel, _monitors);
            if ((!layout.Enabled && !preview) || monitor is null)
            {
                if (_windows.Remove(panel.Id, out var old)) old.Close();
                continue;
            }
            if (!_windows.TryGetValue(panel.Id, out var window))
            {
                window = new MfdWindow(_root + "mfd.html?panel=" + panel.Id);
                var created = window;
                window.Ready += () => {
                    created.Send(new { type = "alignment", enabled = _preview, panel = panel.Id });
                    created.Send(new { type = "inset", inset = _active.Panels.FirstOrDefault(p => p.Id == panel.Id)?.Inset });
                    // On Ready as well as on every Apply: a window that has just
                    // finished navigating missed the send that placed it, and a
                    // frame drawing the shipped captions over a custom profile
                    // is a frame whose labels lie.
                    created.Send(Display(_active));
                    SendDeviceStatus();
                };
                _windows[panel.Id] = window;
                window.Show();
            }
            window.Place(panel, monitor);
            window.Send(new { type = "alignment", enabled = preview, panel = panel.Id });
            window.Send(new { type = "inset", inset = panel.Inset });
            window.Send(Display(layout));
        }
        Backdrops(layout);
        if (layout.Enabled || preview || _setup is not null) EnsureInput();
        else { _input?.Dispose(); _input = null; }
        SendDeviceStatus();
    }

    /// <summary>
    /// One black backdrop per monitor showing a panel, with the openings cut
    /// out of it. See <see cref="MfdBlackout"/> for why the frames need one.
    /// </summary>
    /// <remarks>
    /// The monitor the setup window is on is left alone while setup is open.
    /// Somebody placing a panel on the monitor they are working on would
    /// otherwise cover their own setup window with the thing they just switched
    /// on, and the way back out would be underneath it.
    /// </remarks>
    private void Backdrops(MfdLayout layout)
    {
        var busy = _busy = _setup?.MonitorId(_monitors);
        var wanted = layout.Blackout
            ? layout.Panels
                .Where(p => _windows.ContainsKey(p.Id))
                .Select(p => (Panel: p, Monitor: MfdLayout.Locate(p, _monitors)))
                .Where(x => x.Monitor is not null && x.Monitor.Id != busy && MfdLayout.MayBlackOut(x.Monitor))
                .GroupBy(x => x.Monitor!.Id)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<MfdPanel>)[.. g.Select(x => x.Panel)])
            : [];

        foreach (var id in _blackouts.Keys.ToArray())
            if (!wanted.ContainsKey(id) && _blackouts.Remove(id, out var gone)) gone.Close();

        foreach (var (id, panels) in wanted)
        {
            if (!_blackouts.TryGetValue(id, out var backdrop))
            {
                _blackouts[id] = backdrop = new MfdBlackout();
                backdrop.Show();
            }
            backdrop.Cover(_monitors.First(m => m.Id == id), panels);
        }
    }

    public void Dispose()
    {
        _disposing = true;
        _displays.Stop();
        if (_hotkeys is not null)
        {
            NativeWindowStyles.UnregisterGlobalHotKey(_hotkeys.Handle, SetupHotkeyId);
            _hotkeys.Dispose();
        }
        _setup?.Close();
        _input?.Dispose();
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
        foreach (var backdrop in _blackouts.Values) backdrop.Close();
        _blackouts.Clear();
    }
}
