using System.IO;
using System.Text.Json;
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

    /// <summary>The monitor the backdrop is currently keeping clear for setup.</summary>
    private string? _busy;

    public MfdController(string root, Action<string> notify)
    {
        _root = root;
        _notify = notify;
        _saved = MfdLayout.Default(_monitors);
        try
        {
            if (File.Exists(_path))
                _saved = (JsonSerializer.Deserialize<MfdLayout>(File.ReadAllText(_path), MfdLayout.JsonOptions)
                    ?? _saved).Validate(_monitors);
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException)
        { _notify("MFD settings could not be read. Open MFD setup to place the displays again."); }
        _active = _saved;
        Apply(_saved, false);
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
        _setup = new MfdWindow(_root + "mfd-setup.html", setup: true);
        _setup.Ready += SendState;
        _setup.Message += Receive;
        _setup.Closed += (_, _) => { _setup = null; Apply(_saved, false); };
        // Dragged onto the cockpit monitor, setup takes that monitor's backdrop
        // down with it; dragged off again, it comes back. Only on a change of
        // monitor - a drag raises this on every pixel.
        _setup.LocationChanged += (_, _) =>
        {
            if (_setup?.MonitorId(_monitors) != _busy) Backdrops(_active);
        };
        EnsureInput();
        _setup.Show();
        Backdrops(_active);
    }

    private void SendState() => _setup?.Send(new {
        type = "setup", monitors = _monitors, layout = _saved.Validate(_monitors), devices = _input?.Connected ?? []
    });

    private void Receive(JsonElement message)
    {
        try
        {
            var type = message.GetProperty("type").GetString();
            if (type == "stopPreview") { Apply(_saved, false); return; }
            if (type is not ("save" or "preview")) return;
            var layout = message.GetProperty("layout").Deserialize<MfdLayout>(MfdLayout.JsonOptions)
                ?.Validate(_monitors) ?? throw new ArgumentException("The MFD layout is missing.");
            if (type == "save")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path + ".tmp", JsonSerializer.Serialize(layout, MfdLayout.JsonOptions));
                File.Move(_path + ".tmp", _path, overwrite: true);
                _saved = layout;
            }
            // A save while the preview is up leaves it up. Writing the file is
            // not a reason to take the picture away, and dropping the preview
            // here is what made every change after the first save invisible
            // until the pilot saved again.
            var previewing = type == "preview" || _preview;
            Apply(layout, previewing);
            _setup?.Send(new { type = "result", ok = true, message = type switch
            {
                "save" when previewing => "Layout saved. The preview is still following the editor.",
                "save" => "Layout saved.",
                _ => "Alignment preview is visible. It follows the editor as you drag."
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
        brightness = layout.Brightness, textScale = layout.TextScale
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
            var monitor = _monitors.FirstOrDefault(m => m.Id == panel.Monitor);
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
                .Where(p => _windows.ContainsKey(p.Id) && p.Monitor != busy)
                .GroupBy(p => p.Monitor)
                .Where(g => _monitors.Any(m => m.Id == g.Key))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<MfdPanel>)[.. g])
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
        _displays.Stop();
        _setup?.Close();
        _input?.Dispose();
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
        foreach (var backdrop in _blackouts.Values) backdrop.Close();
        _blackouts.Clear();
    }
}
