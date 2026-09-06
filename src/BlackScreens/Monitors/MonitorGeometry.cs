using System.Drawing;

namespace BlackScreens.Monitors;

public static class MonitorGeometry
{
    public static Rectangle? Map(Rectangle candidate, IReadOnlyList<Rectangle> monitors)
    {
        foreach (var monitor in monitors)
        {
            if (monitor == candidate)
            {
                return monitor;
            }
        }

        var center = new Point(
            candidate.X + Math.Max(0, candidate.Width / 2),
            candidate.Y + Math.Max(0, candidate.Height / 2));

        foreach (var monitor in monitors)
        {
            if (monitor.Contains(center))
            {
                return monitor;
            }
        }

        Rectangle? best = null;
        var bestArea = 0;
        foreach (var monitor in monitors)
        {
            var overlap = Rectangle.Intersect(monitor, candidate);
            if (overlap.Width <= 0 || overlap.Height <= 0)
            {
                continue;
            }

            var area = overlap.Width * overlap.Height;
            if (area > bestArea)
            {
                bestArea = area;
                best = monitor;
            }
        }

        return best;
    }

    public static ScanResult Canonicalize(ScanResult result, IReadOnlyList<Rectangle> monitors)
    {
        var games = new List<Rectangle>();
        foreach (var game in result.GameMonitors)
        {
            var mapped = Map(game, monitors);
            if (mapped is { } monitor && !games.Contains(monitor))
            {
                games.Add(monitor);
            }
        }

        Rectangle? focus = null;
        if (result.FocusClearMonitor is { } rawFocus)
        {
            focus = Map(rawFocus, monitors);
        }

        return new ScanResult(games, focus);
    }
}
