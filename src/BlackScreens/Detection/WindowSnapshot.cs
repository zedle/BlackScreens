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
    Rectangle MonitorBounds)
{
    /// <summary>
    /// Full path to the executable, or empty when it could not be read. Only needed so a denylist or
    /// on top entry can name one exact file rather than every program with the same name.
    /// </summary>
    public string ExecutablePath { get; init; } = string.Empty;
}
