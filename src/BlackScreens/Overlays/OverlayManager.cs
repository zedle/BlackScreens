namespace BlackScreens.Overlays;

public sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<Rectangle, OverlayForm> _overlays = [];

    /// <summary>Number of overlays currently shown. Exposed for diagnostics and tests.</summary>
    public int Count => _overlays.Count;

    /// <summary>The window handles of the overlays on screen, for whoever has to sit above them.</summary>
    public IReadOnlyCollection<nint> Handles =>
        _overlays.Values.Where(form => form.IsHandleCreated).Select(form => form.Handle).ToArray();

    /// <summary>
    /// Shows a black overlay on each of <paramref name="blackMonitors"/> and removes the rest.
    /// A non null <paramref name="screensaverPath"/> draws that screensaver instead of plain black.
    /// <paramref name="opacityPercent"/> is how solid the black is, and is ignored while a
    /// screensaver is running, because a partly transparent window does not composite over the
    /// native child window the screensaver draws into.
    /// </summary>
    /// <param name="reassertZOrder">
    /// Whether to push the overlays back to the top. Doing it on every poll is what made a window
    /// held above them flicker: the overlay went above it and was put back below it four times a
    /// second, and each of those is a repaint. The caller only asks for it when something happened
    /// that could have pushed the overlays down, which in practice means the foreground changed.
    /// </param>
    public void Apply(
        IReadOnlyList<Rectangle> blackMonitors,
        string? screensaverPath = null,
        int opacityPercent = 100,
        bool reassertZOrder = true)
    {
        var desired = new HashSet<Rectangle>(blackMonitors);
        var opacity = screensaverPath is null ? opacityPercent : 100;

        foreach (var leftover in _overlays.Keys.Where(key => !desired.Contains(key)).ToArray())
        {
            CloseOne(leftover);
        }

        foreach (var monitor in blackMonitors)
        {
            try
            {
                if (_overlays.TryGetValue(monitor, out var existing))
                {
                    if (!existing.Visible)
                    {
                        existing.Show();
                    }

                    // A game going fullscreen can push other topmost windows down the z order.
                    if (reassertZOrder)
                    {
                        existing.Reassert();
                    }

                    existing.SetOpacityPercent(opacity);
                    existing.SetScreensaver(screensaverPath);
                    continue;
                }

                var form = new OverlayForm(monitor);
                form.Show();
                form.PlaceAt(monitor);
                form.SetOpacityPercent(opacity);
                form.SetScreensaver(screensaverPath);
                _overlays[monitor] = form;
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Overlay apply failed for {monitor}: {ex}");
            }
        }
    }

    public void HideAll()
    {
        foreach (var key in _overlays.Keys.ToArray())
        {
            CloseOne(key);
        }
    }

    public void Dispose()
    {
        HideAll();
    }

    private void CloseOne(Rectangle key)
    {
        if (!_overlays.Remove(key, out var form))
        {
            return;
        }

        try
        {
            form.SetScreensaver(null);
            form.Close();
            form.Dispose();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Overlay close failed for {key}: {ex}");
        }
    }
}
