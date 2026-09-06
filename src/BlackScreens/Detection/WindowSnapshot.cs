using System.Drawing;

namespace BlackScreens.Detection;

public sealed record WindowSnapshot(
    string ProcessName,
    Rectangle Bounds,
    int Style,
    int ExStyle,
    bool Visible,
    bool Cloaked,
    bool IsForeground,
    Rectangle MonitorBounds);
