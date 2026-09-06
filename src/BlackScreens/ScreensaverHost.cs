using System.Diagnostics;
using System.Globalization;

namespace BlackScreens;

/// <summary>
/// Runs a screensaver inside one overlay window. Screensavers accept <c>/p HWND</c>, the preview mode
/// the Windows personalization dialog uses, and draw into the window handle they are given.
/// </summary>
internal sealed class ScreensaverHost : IDisposable
{
    private Process? _process;

    public bool IsRunning => _process is { HasExited: false };

    /// <summary>Starts the screensaver in the given window. Returns false when it will not run.</summary>
    public bool Start(nint parentHandle, string screensaverPath)
    {
        Stop();

        if (parentHandle == 0 || !File.Exists(screensaverPath))
        {
            return false;
        }

        try
        {
            var start = new ProcessStartInfo(screensaverPath)
            {
                Arguments = string.Create(CultureInfo.InvariantCulture, $"/p {parentHandle}"),
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(screensaverPath) ?? string.Empty
            };

            // Deliberately no WaitForExit here: this runs on the UI thread while a game is going
            // fullscreen, and a quarter second stall per monitor would be felt. A saver that refuses
            // preview mode is caught by IsRunning on a later poll.
            _process = Process.Start(start);
            return _process is not null;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Screensaver start failed for {screensaverPath}: {ex}");
            _process = null;
            return false;
        }
    }

    public void Stop()
    {
        if (_process is null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                _process.WaitForExit(1000);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Screensaver stop failed: {ex}");
        }
        finally
        {
            _process.Dispose();
            _process = null;
        }
    }

    public void Dispose() => Stop();
}
