namespace Quantumwake.Server;

/// <summary>
/// Lets the web dashboard show and hide the in-game overlay.
/// </summary>
/// <remarks>
/// <para>
/// The server runs inside QuantumWake.exe, so the overlay window lives in the
/// same process as the HTTP endpoints - this bridge is just the seam between
/// them. The WPF shell attaches at startup with a callback that marshals onto
/// its dispatcher; the endpoints call <see cref="TrySet"/> from request
/// threads and never touch the window directly.
/// </para>
/// <para>
/// Under the bare server there is no overlay to control: nothing attaches,
/// <see cref="Available"/> stays false, and the UI hides the control instead
/// of offering a button that cannot work.
/// </para>
/// </remarks>
public sealed class OverlayBridge
{
    private readonly Lock _lock = new();
    private Action<bool>? _apply;

    public bool Available { get; private set; }
    public bool Visible { get; private set; }

    /// <summary>Called once by the overlay shell when it owns a window.</summary>
    public void Attach(bool initiallyVisible, Action<bool> apply)
    {
        lock (_lock)
        {
            _apply = apply;
            Available = true;
            Visible = initiallyVisible;
        }
    }

    /// <summary>
    /// The shell reporting a change it made itself - the tray menu, mostly - so
    /// the dashboard's toggle stays truthful.
    /// </summary>
    public void Report(bool visible)
    {
        lock (_lock)
            Visible = visible;
    }

    /// <summary>Shows or hides the overlay. False when nothing is attached.</summary>
    public bool TrySet(bool visible)
    {
        Action<bool>? apply;

        lock (_lock)
        {
            if (_apply is null)
                return false;

            apply = _apply;
            Visible = visible;
        }

        apply(visible);
        return true;
    }
}
