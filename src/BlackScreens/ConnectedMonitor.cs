using System.Drawing;

namespace BlackScreens;

public sealed record ConnectedMonitor(string DeviceName, Rectangle Bounds)
{
    public bool IsPrimary { get; init; }

    public bool IsPortrait => Bounds.Height > Bounds.Width;

    public string Label => $"{DeviceName} {Bounds.Width}x{Bounds.Height}";
}
