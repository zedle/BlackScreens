namespace BlackScreens;

/// <summary>
/// A borderless window that owns its own geometry in raw screen pixels.
///
/// WinForms is per monitor DPI aware here, so when a window is moved onto a monitor with a different
/// scale factor Windows sends WM_DPICHANGED and WinForms resizes the window to match. For a full
/// screen overlay that is exactly wrong: the window shrinks to a small square in the corner of the
/// monitor. This form swallows the message and re-applies the rectangle it was told to cover.
/// </summary>
internal class PlacedForm : Form
{
    private const int WmDpiChanged = 0x02E0;

    private Rectangle _target;
    private bool _placing;

    protected PlacedForm(Rectangle bounds)
    {
        _target = bounds;
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        ControlBox = false;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
    }

    /// <summary>The rectangle this window is meant to cover, in physical screen pixels.</summary>
    protected Rectangle Target => _target;

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= WindowStyles.WsExNoActivate;
            cp.ExStyle |= WindowStyles.WsExToolWindow;
            // A hosted screensaver draws into a native child window. Without clipping, the form
            // would paint straight over it.
            cp.Style |= WindowStyles.WsClipChildren;
            return cp;
        }
    }

    public void PlaceAt(Rectangle bounds)
    {
        _target = bounds;
        ApplyPlacement();
    }

    /// <summary>Pushes the window back to the top of the z order without stealing focus.</summary>
    public void Reassert() => ApplyPlacement();

    /// <summary>Called after the window has been moved back onto its monitor rectangle.</summary>
    protected virtual void OnPlacementRestored()
    {
    }

    private void ApplyPlacement()
    {
        if (_placing || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        _placing = true;
        try
        {
            OverlayPlacement.Place(Handle, _target);
        }
        finally
        {
            _placing = false;
        }
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmDpiChanged)
        {
            // Do not let the base class rescale us. Re-apply the rectangle once the message is done.
            m.Result = 0;
            if (!_placing)
            {
                try
                {
                    BeginInvoke(() =>
                    {
                        ApplyPlacement();
                        OnPlacementRestored();
                    });
                }
                catch (Exception ex)
                {
                    ErrorLog.Write($"DPI reposition failed: {ex}");
                }
            }

            return;
        }

        base.WndProc(ref m);
    }
}
