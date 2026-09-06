namespace BlackScreens.Overlays;

internal sealed class OverlayForm : PlacedForm
{
    /// <summary>How many times a screensaver may die before this overlay stops trying to run it.</summary>
    private const int MaxScreensaverFailures = 2;

    private readonly ScreensaverHost _screensaver = new();
    private string? _screensaverPath;
    private string? _unusableScreensaver;
    private int _failures;

    public OverlayForm(Rectangle bounds)
        : base(bounds)
    {
        BackColor = Color.Black;
        Cursor = Cursors.Default;
        // Deliberately not double buffered: the buffered blit covers the whole client area and would
        // erase a hosted screensaver's child window every repaint.
    }

    /// <summary>
    /// How solid the overlay is, as a percentage. Anything under 100 makes this a layered window,
    /// which composites fine over the desktop but not over the native child window a screensaver
    /// runs in, so the caller passes 100 whenever a screensaver is up.
    /// </summary>
    public void SetOpacityPercent(int percent)
    {
        var wanted = Math.Clamp(percent, AppSettings.MinOverlayOpacity, 100) / 100.0;
        if (Math.Abs(Opacity - wanted) > 0.001)
        {
            Opacity = wanted;
        }
    }

    /// <summary>
    /// Draws a screensaver instead of plain black. Pass null for black. The window must already be at
    /// its final size, because the screensaver reads the client rect when it starts. A screensaver
    /// that will not run in preview mode is dropped after a couple of tries and the monitor stays black.
    /// </summary>
    public void SetScreensaver(string? screensaverPath)
    {
        if (screensaverPath is not null
            && string.Equals(screensaverPath, _unusableScreensaver, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.Equals(_screensaverPath, screensaverPath, StringComparison.OrdinalIgnoreCase))
        {
            if (screensaverPath is null || _screensaver.IsRunning)
            {
                return;
            }

            // It started but did not stay up. Try again, then give up quietly.
            if (!RecordFailure(screensaverPath))
            {
                return;
            }
        }
        else
        {
            _failures = 0;
            _unusableScreensaver = null;
        }

        _screensaverPath = screensaverPath;

        if (screensaverPath is null)
        {
            _screensaver.Stop();
            return;
        }

        if (!_screensaver.Start(Handle, screensaverPath))
        {
            RecordFailure(screensaverPath);
        }
    }

    /// <summary>Returns true when another attempt is still worth making.</summary>
    private bool RecordFailure(string screensaverPath)
    {
        _failures++;
        if (_failures < MaxScreensaverFailures)
        {
            return true;
        }

        ErrorLog.Write($"Screensaver {screensaverPath} will not run in preview mode. Staying black.");
        _unusableScreensaver = screensaverPath;
        _screensaverPath = null;
        _screensaver.Stop();
        return false;
    }

    /// <summary>A screensaver sizes itself once, so a DPI move needs it started again.</summary>
    protected override void OnPlacementRestored()
    {
        if (_screensaverPath is not { } path)
        {
            return;
        }

        _screensaverPath = null;
        _failures = 0;
        SetScreensaver(path);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.Clear(Color.Black);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _screensaver.Dispose();
        }

        base.Dispose(disposing);
    }
}
