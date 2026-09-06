namespace BlackScreens;

public sealed class OverlayManager : IDisposable
{
    private readonly Dictionary<Rectangle, OverlayForm> _overlays = [];

    /// <summary>Number of overlays currently shown. Exposed for diagnostics and tests.</summary>
    public int Count => _overlays.Count;

    /// <summary>
    /// Shows a black overlay on each of <paramref name="blackMonitors"/> and removes the rest.
    /// A non null <paramref name="screensaverPath"/> draws that screensaver instead of plain black.
    /// </summary>
    public void Apply(IReadOnlyList<Rectangle> blackMonitors, string? screensaverPath = null)
    {
        var desired = new HashSet<Rectangle>(blackMonitors);

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
                    existing.SetScreensaver(screensaverPath);
                    continue;
                }

                var form = new OverlayForm(monitor);
                form.Show();
                form.PlaceAt(monitor);
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
