namespace BlackScreens.Overlays;

public sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<Rectangle, OverlayForm> _overlays = [];

    /// <summary>Number of overlays currently shown. Exposed for diagnostics and tests.</summary>
    public int Count => _overlays.Count;

    /// <summary>
    /// Shows a black overlay on each of <paramref name="blackMonitors"/> and removes the rest.
    /// A non null <paramref name="screensaverPath"/> draws that screensaver instead of plain black.
    /// <paramref name="opacityPercent"/> is how solid the black is, and is ignored while a
    /// screensaver is running, because a partly transparent window does not composite over the
    /// native child window the screensaver draws into.
    /// </summary>
    public void Apply(
        IReadOnlyList<Rectangle> blackMonitors,
        string? screensaverPath = null,
        int opacityPercent = 100)
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
                    existing.Reassert();
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
