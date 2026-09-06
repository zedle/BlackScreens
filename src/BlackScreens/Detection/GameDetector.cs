using System.Drawing;

namespace BlackScreens.Detection;

public sealed class GameDetector
{
    private const int SlackPx = 2;
    private readonly ProcessDenylist _denylist;
    private readonly DetectOptions _options;

    public GameDetector(ProcessDenylist denylist, DetectOptions? options = null)
    {
        _denylist = denylist;
        _options = options ?? DetectOptions.Safe;
    }

    public ScanResult Decide(IReadOnlyList<WindowSnapshot> windows)
    {
        var gameMonitors = new List<Rectangle>();
        Rectangle? focusClear = null;

        foreach (var window in windows)
        {
            if (window.IsForeground && (_denylist.Contains(window.ProcessName, window.ExecutablePath) || _options.AlwaysClearFocusedMonitor))
            {
                focusClear = window.MonitorBounds;
            }

            if (!IsFullscreen(window))
            {
                continue;
            }

            if (_denylist.Contains(window.ProcessName, window.ExecutablePath))
            {
                continue;
            }

            if (!_options.BackgroundGames && !window.IsForeground)
            {
                continue;
            }

            if (!gameMonitors.Contains(window.MonitorBounds))
            {
                gameMonitors.Add(window.MonitorBounds);
            }
        }

        return new ScanResult(gameMonitors, focusClear);
    }

    private static bool IsFullscreen(WindowSnapshot window)
    {
        if (!window.Visible || window.Cloaked)
        {
            return false;
        }

        if ((window.ExStyle & WindowStyles.WsExToolWindow) != 0)
        {
            return false;
        }

        if (!MatchesMonitor(window.Bounds, window.MonitorBounds))
        {
            return false;
        }

        var noCaption = (window.Style & WindowStyles.WsCaption) == 0;
        var popup = (window.Style & WindowStyles.WsPopup) != 0;
        return noCaption || popup;
    }

    private static bool MatchesMonitor(Rectangle bounds, Rectangle monitor)
    {
        return Math.Abs(bounds.X - monitor.X) <= SlackPx
            && Math.Abs(bounds.Y - monitor.Y) <= SlackPx
            && Math.Abs(bounds.Right - monitor.Right) <= SlackPx
            && Math.Abs(bounds.Bottom - monitor.Bottom) <= SlackPx;
    }
}
