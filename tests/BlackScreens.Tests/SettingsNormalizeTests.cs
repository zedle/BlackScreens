using Xunit;

namespace BlackScreens.Tests;

public sealed class SettingsNormalizeTests
{
    [Fact]
    public void Poll_interval_is_clamped_to_the_supported_range()
    {
        Assert.Equal(AppSettings.MinPollIntervalMs, new AppSettings { PollIntervalMs = 1 }.Normalize().PollIntervalMs);
        Assert.Equal(AppSettings.MaxPollIntervalMs, new AppSettings { PollIntervalMs = 99999 }.Normalize().PollIntervalMs);
        Assert.Equal(500, new AppSettings { PollIntervalMs = 500 }.Normalize().PollIntervalMs);
    }

    [Fact]
    public void An_unusable_hotkey_falls_back_to_the_default()
    {
        var settings = new AppSettings { HotkeyModifiers = AppSettings.ModShift, HotkeyVirtualKey = 0 }.Normalize();

        Assert.Equal(AppSettings.DefaultHotkeyModifiers, settings.HotkeyModifiers);
        Assert.Equal(AppSettings.DefaultHotkeyVirtualKey, settings.HotkeyVirtualKey);
    }

    [Fact]
    public void Duplicate_and_blank_entries_are_dropped()
    {
        var settings = new AppSettings
        {
            WhitelistedDeviceNames = [@"\\.\DISPLAY1", @"\\.\display1", "  ", ""],
            DenylistProcessNames = ["chrome.exe", "CHROME", " notepad ", "", "   "]
        }.Normalize();

        Assert.Equal([@"\\.\DISPLAY1"], settings.WhitelistedDeviceNames);
        Assert.Equal(["chrome", "notepad"], settings.DenylistProcessNames);
    }

    [Fact]
    public void A_blank_theme_becomes_system()
    {
        Assert.Equal("System", new AppSettings { Theme = "   " }.Normalize().Theme);
        Assert.Equal("Dark", new AppSettings { Theme = "Dark" }.Normalize().Theme);
    }

    [Fact]
    public void An_empty_denylist_falls_back_to_the_defaults()
    {
        Assert.Equal(ProcessDenylist.Defaults, new AppSettings().GetDenylist());
    }

    [Fact]
    public void Toggle_adds_then_removes_a_monitor()
    {
        var settings = new AppSettings();

        settings.Toggle(@"\\.\DISPLAY2");
        Assert.True(settings.IsWhitelisted(@"\\.\display2"));

        settings.Toggle(@"\\.\DISPLAY2");
        Assert.False(settings.IsWhitelisted(@"\\.\DISPLAY2"));
    }
}
