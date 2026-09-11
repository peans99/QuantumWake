using System.Windows;
using Quantumwake.Data;

namespace Quantumwake.Ocr;

/// <summary>
/// Reads the desktop clipboard, from the thread that is allowed to.
/// </summary>
/// <remarks>
/// <para>
/// The clipboard is single-threaded apartment only, and the server's request
/// threads are not it. So the call is marshalled onto the UI dispatcher the
/// overlay hands over at startup.
/// </para>
/// <para>
/// It is also genuinely flaky: another process can hold it open, and
/// <c>Clipboard.GetText</c> throws rather than waiting. A pilot pressing a
/// button gets "nothing to read" and presses it again, which is a better
/// answer than a stack trace.
/// </para>
/// </remarks>
public sealed class WindowsClipboardReader(Func<Func<string?>, Task<string?>> onUiThread)
    : IClipboardReader
{
    public Task<string?> ReadTextAsync(CancellationToken token = default) =>
        onUiThread(() =>
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                return null;
            }
        });
}
