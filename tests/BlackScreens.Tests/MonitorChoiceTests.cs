using System.Drawing;
using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

public sealed class MonitorChoiceTests
{
    [Theory]
    [InlineData(@"\\.\DISPLAY1", "Display 1")]
    [InlineData(@"\\.\DISPLAY12", "Display 12")]
    [InlineData("DISPLAY3", "Display 3")]
    [InlineData("something-else", "something-else")]
    public void FriendlyName_reads_like_the_windows_display_settings(string deviceName, string expected)
    {
        Assert.Equal(expected, MonitorChoice.FriendlyName(deviceName));
    }

    [Fact]
    public void Describe_calls_out_orientation_and_the_primary_monitor()
    {
        var primary = new ConnectedMonitor(@"\\.\DISPLAY1", new Rectangle(0, 0, 3840, 2160)) { IsPrimary = true };
        var portrait = new ConnectedMonitor(@"\\.\DISPLAY2", new Rectangle(3840, -100, 1080, 1920));

        Assert.Equal("3840 x 2160 landscape, primary  at 0, 0", MonitorChoice.Describe(primary));
        Assert.Equal("1080 x 1920 portrait  at 3840, -100", MonitorChoice.Describe(portrait));
    }

    [Fact]
    public void A_row_starts_checked_when_the_monitor_is_whitelisted()
    {
        var monitor = new ConnectedMonitor(@"\\.\DISPLAY2", new Rectangle(0, 0, 1080, 1920));

        Assert.True(new MonitorChoice(monitor, isWhitelisted: true).IsWhitelisted);
        Assert.False(new MonitorChoice(monitor, isWhitelisted: false).IsWhitelisted);
    }

    [Fact]
    public void The_headline_adds_the_make_and_model_when_the_panel_reports_one()
    {
        var plain = new ConnectedMonitor(@"\\.\DISPLAY2", new Rectangle(0, 0, 2560, 1440));
        var known = plain with { HardwareName = "LG 27GL850" };

        Assert.Equal("Display 2", MonitorChoice.Headline(plain));
        Assert.Equal("Display 2  ·  LG 27GL850", MonitorChoice.Headline(known));
    }
}
