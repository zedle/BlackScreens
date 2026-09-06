using System.Drawing;
using Xunit;

namespace BlackScreens.Tests;

public sealed class ScanResultTests
{
    private static readonly Rectangle Wide = new(0, 0, 1920, 1080);
    private static readonly Rectangle TallThin = new(1920, 0, 682, 2560);
    private static readonly Rectangle[] All = [Wide, TallThin];

    [Fact]
    public void Whitelisted_monitor_is_not_blacked()
    {
        var result = new ScanResult([Wide], focusClearMonitor: null);

        Assert.Empty(result.BlackMonitors(All, [TallThin]));
    }

    [Fact]
    public void Without_whitelist_empty_monitor_is_blacked()
    {
        var result = new ScanResult([Wide], focusClearMonitor: null);

        Assert.Equal([TallThin], result.BlackMonitors(All));
    }
}
