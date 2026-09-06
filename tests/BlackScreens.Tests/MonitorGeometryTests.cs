using System.Drawing;
using Xunit;

namespace BlackScreens.Tests;

public sealed class MonitorGeometryTests
{
    private static readonly Rectangle Monitor1 = new(0, 0, 1920, 1080);
    private static readonly Rectangle Monitor2 = new(1920, 0, 1920, 1080);
    private static readonly Rectangle[] All = [Monitor1, Monitor2];

    [Fact]
    public void Maps_near_miss_game_rect_to_the_real_monitor()
    {
        var offset = new Rectangle(3, -1, 1918, 1079);

        Assert.Equal(Monitor1, MonitorGeometry.Map(offset, All));
    }

    [Fact]
    public void Canonicalize_prevents_blacking_every_monitor_when_rects_do_not_match()
    {
        var raw = new ScanResult([new Rectangle(3, -1, 1918, 1079)], null);
        var mapped = MonitorGeometry.Canonicalize(raw, All);

        Assert.Equal([Monitor2], mapped.BlackMonitors(All));
    }
}
