using System.Drawing;

namespace BlackScreens;

public sealed record ConnectedMonitor(string DeviceName, Rectangle Bounds)
{
    public bool IsPrimary { get; init; }

    /// <summary>The make and model from the panel's EDID, such as "LG 27GL850", when it is readable.</summary>
    public string? HardwareName { get; init; }

    public bool IsPortrait => Bounds.Height > Bounds.Width;

    public string Label => $"{DeviceName} {Bounds.Width}x{Bounds.Height}";
}
