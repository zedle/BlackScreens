namespace BlackScreens.Ui;

/// <summary>
/// Runs the chosen screensaver on the monitors a real blackout would cover, so the user can see what
/// it will look like and still reach the Close button.
/// </summary>
internal static class ScreensaverPreview
{
    private const int VisibleMs = 6000;

    private static OverlayManager? _overlays;
    private static System.Windows.Forms.Timer? _timer;

    /// <summary>
    /// Previews on every monitor that blackout would actually cover: not the one holding the settings
    /// window, and not the monitors the user has chosen to keep clear.
    /// </summary>
    public static bool Flash(string? screensaverPath, IReadOnlyCollection<Rectangle> exclude)
    {
        Stop();

        if (string.IsNullOrWhiteSpace(screensaverPath) || !File.Exists(screensaverPath))
        {
            return false;
        }

        var targets = MonitorEnumerator.CaptureAll()
            .Select(monitor => monitor.Bounds)
            .Where(bounds => !exclude.Contains(bounds))
            .ToArray();

        if (targets.Length == 0)
        {
            return false;
        }

        _overlays = new OverlayManager();
        _overlays.Apply(targets, screensaverPath);

        _timer = new System.Windows.Forms.Timer { Interval = VisibleMs };
        _timer.Tick += (_, _) => Stop();
        _timer.Start();
        return true;
    }

    public static void Stop()
    {
        try
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            _overlays?.Dispose();
            _overlays = null;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Screensaver preview cleanup failed: {ex}");
        }
    }
}
