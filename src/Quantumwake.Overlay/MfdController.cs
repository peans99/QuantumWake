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
    private readonly DispatcherTimer _displays = new() { Interval = TimeSpan.FromSeconds(2) };
    private MfdMonitor[] _monitors = MfdWindow.Monitors();
    private MfdLayout _saved;
    private MfdLayout _active;
    private CougarInput? _input;
    private MfdWindow? _setup;
    private bool _preview;

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
        EnsureInput();
        _setup.Show();
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
            Apply(layout, type == "preview");
            _setup?.Send(new { type = "result", ok = true, message = type == "save"
                ? "Layout saved." : "Alignment preview is visible. Drag the areas here to adjust it." });
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
                    SendDeviceStatus();
                };
                _windows[panel.Id] = window;
                window.Show();
            }
            window.Place(panel, monitor);
            window.Send(new { type = "alignment", enabled = preview, panel = panel.Id });
        }
        if (layout.Enabled || preview || _setup is not null) EnsureInput();
        else { _input?.Dispose(); _input = null; }
        SendDeviceStatus();
    }

    public void Dispose()
    {
        _displays.Stop();
        _setup?.Close();
        _input?.Dispose();
        foreach (var window in _windows.Values) window.Close();
        _windows.Clear();
    }
}
