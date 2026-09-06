using System.Drawing;

namespace BlackScreens;

public sealed class ScanResult
{
    public ScanResult(IReadOnlyList<Rectangle> gameMonitors, Rectangle? focusClearMonitor)
    {
        GameMonitors = gameMonitors;
        FocusClearMonitor = focusClearMonitor;
    }

    public IReadOnlyList<Rectangle> GameMonitors { get; }

    public Rectangle? FocusClearMonitor { get; }

    public IReadOnlyList<Rectangle> BlackMonitors(
        IReadOnlyList<Rectangle> allMonitors,
        IReadOnlyList<Rectangle>? whitelist = null)
    {
        if (GameMonitors.Count == 0)
        {
            return [];
        }

        var black = new List<Rectangle>();
        foreach (var monitor in allMonitors)
        {
            if (GameMonitors.Contains(monitor))
            {
                continue;
            }

            if (FocusClearMonitor is { } focus && focus == monitor)
            {
                continue;
            }

            if (whitelist is not null && whitelist.Contains(monitor))
            {
                continue;
            }

            black.Add(monitor);
        }

        return black;
    }
}
