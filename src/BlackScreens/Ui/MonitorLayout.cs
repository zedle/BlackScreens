using System.Drawing;

namespace BlackScreens.Ui;

/// <summary>
/// Works out where each monitor sits on the little map at the top of the Monitors page, the same
/// idea as the arrangement diagram in the Windows display settings.
/// </summary>
/// <remarks>
/// There is no scaling to a pixel size here. The map is laid out in desktop coordinates and the
/// window scales the whole thing to fit, so this only has to move the origin to zero. Monitors can
/// sit at negative coordinates when one is placed above or to the left of the primary.
/// </remarks>
public static class MonitorLayout
{
    /// <summary>The smallest rectangle holding every monitor, or empty when there are none.</summary>
    public static Rectangle Union(IEnumerable<ConnectedMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);

        var union = Rectangle.Empty;
        foreach (var monitor in monitors)
        {
            union = union.IsEmpty ? monitor.Bounds : Rectangle.Union(union, monitor.Bounds);
        }

        return union;
    }
}
