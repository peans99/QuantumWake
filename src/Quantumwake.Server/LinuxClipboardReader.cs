using System.Diagnostics;
using Quantumwake.Data;

namespace Quantumwake.Server;

/// <summary>
/// Reads the desktop clipboard on Linux by asking the tool the session has.
/// </summary>
/// <remarks>
/// <para>
/// <c>wl-paste</c> on Wayland, <c>xclip</c> or <c>xsel</c> on X11; whichever
/// is on the path, Wayland's first because a Wayland session usually has an
/// X bridge and the X tools then read a clipboard the game never wrote to.
/// When none is installed <see cref="Available"/> is false, the reader is not
/// registered, and the panel says the clipboard cannot be read rather than
/// showing nothing.
/// </para>
/// <para>
/// Measured 2026-10-30 on Fedora with KDE on Wayland: <c>wl-paste</c> is
/// there, the X tools are not. Whether Wine hands what the game copied to
/// that clipboard is not measured yet; it needs the game running
/// (docs/linux.md).
/// </para>
/// </remarks>
internal sealed class LinuxClipboardReader : IClipboardReader
{
    private static readonly (string Command, string[] Arguments)[] Tools =
    [
        // Text only: with an image on the clipboard wl-paste would hand over
        // the picture's bytes.
        ("wl-paste", ["--no-newline", "--type", "text"]),
        ("xclip", ["-o", "-selection", "clipboard"]),
        ("xsel", ["--output", "--clipboard"]),
    ];

    private static readonly Lazy<(string Command, string[] Arguments)?> Tool = new(() =>
    {
        if (!OperatingSystem.IsLinux()) return null;

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var tool in Tools)
            if (path.Split(':').Any(dir => dir.Length > 0 && File.Exists(Path.Combine(dir, tool.Command))))
                return tool;

        return null;
    });

    public static bool Available => Tool.Value is not null;

    public async Task<string?> ReadTextAsync(CancellationToken token = default)
    {
        if (Tool.Value is not { } tool) return null;

        var start = new ProcessStartInfo(tool.Command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in tool.Arguments) start.ArgumentList.Add(argument);

        // The tools wait on whoever owns the clipboard, which here is Wine and
        // the game; one that has stalled would hold the request for ever and
        // leave a process behind at every press.
        using var patience = CancellationTokenSource.CreateLinkedTokenSource(token);
        patience.CancelAfter(Patience);

        Process? process = null;

        try
        {
            process = Process.Start(start);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(patience.Token);
            await process.WaitForExitAsync(patience.Token);

            // wl-paste exits 1 with "Nothing is copied" when the clipboard is
            // empty, which is an answer and not a failure.
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException)
        {
            return null;
        }
        finally
        {
            if (process is not null)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    // Gone already.
                }

                process.Dispose();
            }
        }
    }

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(2);
}
