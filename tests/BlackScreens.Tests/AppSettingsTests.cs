using System.Drawing;
using Xunit;

namespace BlackScreens.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void PickTallThin_selects_the_highest_aspect_portrait()
    {
        var monitors = new ConnectedMonitor[]
        {
            new(@"\\.\DISPLAY1", new Rectangle(0, 0, 4096, 1728)),
            new(@"\\.\DISPLAY5", new Rectangle(8960, 0, 682, 2560)),
            new(@"\\.\DISPLAY6", new Rectangle(7599, -1440, 3440, 1440)),
            new(@"\\.\DISPLAY2", new Rectangle(5120, 0, 3072, 1728)),
        };

        Assert.Equal(@"\\.\DISPLAY5", AppSettings.PickTallThin(monitors));
    }

    [Fact]
    public void PickTallThin_returns_null_when_no_portrait()
    {
        var monitors = new ConnectedMonitor[]
        {
            new(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080)),
        };

        Assert.Null(AppSettings.PickTallThin(monitors));
    }
}
