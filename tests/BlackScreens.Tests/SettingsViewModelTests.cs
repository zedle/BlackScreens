using System.Drawing;
using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

public sealed class SettingsViewModelTests
{
    private static readonly ConnectedMonitor[] Monitors =
    [
        new(@"\\.\DISPLAY1", new Rectangle(0, 0, 3840, 2160)) { IsPrimary = true },
        new(@"\\.\DISPLAY2", new Rectangle(3840, 0, 1080, 1920))
    ];

    private static readonly Screensaver[] Savers =
    [
        new(@"C:\saver\Mystify.scr", "Mystify"),
        new(@"C:\saver\Bubbles.scr", "Bubbles")
    ];

    private static SettingsViewModel Create(AppSettings? settings = null) =>
        new(settings ?? new AppSettings(), Monitors, Savers);

    [Fact]
    public void A_fresh_model_is_not_dirty()
    {
        Assert.False(Create().IsDirty);
    }

    [Fact]
    public void Changing_a_value_marks_the_model_dirty()
    {
        var model = Create();
        model.BackgroundGames = true;
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void Setting_the_same_value_leaves_the_model_clean()
    {
        var model = Create();
        model.BackgroundGames = model.BackgroundGames;
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void Checking_a_monitor_marks_the_model_dirty()
    {
        var model = Create();
        model.Monitors[0].IsWhitelisted = true;
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void Poll_interval_is_clamped()
    {
        var model = Create();

        model.PollIntervalMs = 10;
        Assert.Equal(AppSettings.MinPollIntervalMs, model.PollIntervalMs);

        model.PollIntervalMs = 9000;
        Assert.Equal(AppSettings.MaxPollIntervalMs, model.PollIntervalMs);
        Assert.Equal("2000 ms", model.PollIntervalText);
    }

    [Fact]
    public void An_unusable_shortcut_is_rejected_and_the_old_one_stays()
    {
        var model = Create();
        var before = model.HotkeyText;

        Assert.False(model.SetHotkey(AppSettings.ModShift, (int)Keys.B));
        Assert.Equal(before, model.HotkeyText);
        Assert.False(model.IsDirty);

        Assert.True(model.SetHotkey(AppSettings.ModControl | AppSettings.ModAlt, (int)Keys.K));
        Assert.Equal("Ctrl + Alt + K", model.HotkeyText);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void Theme_flags_stay_in_sync()
    {
        var model = Create();
        Assert.True(model.ThemeIsSystem);

        model.ThemeIsDark = true;
        Assert.Equal(AppTheme.Dark, model.Theme);
        Assert.False(model.ThemeIsSystem);
        Assert.False(model.ThemeIsLight);
    }

    [Fact]
    public void Adding_a_denylist_entry_strips_exe_and_sorts()
    {
        var model = Create(new AppSettings { DenylistProcessNames = ["alpha", "zulu"] });

        model.NewDenylistEntry = "Mike.exe";
        Assert.True(model.CanAddDenylistEntry);
        Assert.True(model.AddDenylistEntry());

        Assert.Equal(["alpha", "Mike", "zulu"], model.Denylist);
        Assert.Equal(string.Empty, model.NewDenylistEntry);
    }

    [Fact]
    public void Adding_a_duplicate_selects_the_existing_row_instead()
    {
        var model = Create(new AppSettings { DenylistProcessNames = ["chrome"] });

        model.NewDenylistEntry = "CHROME.exe";
        Assert.False(model.AddDenylistEntry());

        Assert.Single(model.Denylist);
        Assert.Equal("chrome", model.SelectedDenylistEntry);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void Blank_entries_cannot_be_added()
    {
        var model = Create();

        model.NewDenylistEntry = "  .exe ";
        Assert.False(model.CanAddDenylistEntry);
        Assert.False(model.AddDenylistEntry());
    }

    [Fact]
    public void Remove_needs_a_selection()
    {
        var model = Create(new AppSettings { DenylistProcessNames = ["chrome"] });

        Assert.False(model.CanRemoveDenylistEntry);
        Assert.False(model.RemoveSelectedDenylistEntry());

        model.SelectedDenylistEntry = "chrome";
        Assert.True(model.CanRemoveDenylistEntry);
        Assert.True(model.RemoveSelectedDenylistEntry());
        Assert.Empty(model.Denylist);
    }

    [Fact]
    public void Restore_defaults_puts_the_shipped_list_back()
    {
        var model = Create(new AppSettings { DenylistProcessNames = ["only"] });

        model.RestoreDefaultDenylist();

        Assert.Equal(ProcessDenylist.Defaults.Count, model.Denylist.Count);
        Assert.Contains("explorer", model.Denylist);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void ApplyTo_copies_every_edited_value()
    {
        var settings = new AppSettings();
        var model = Create(settings);

        model.StartWithWindows = true;
        model.BackgroundGames = true;
        model.AlwaysClearFocusedMonitor = false;
        model.PollIntervalMs = 400;
        model.ThemeIsLight = true;
        model.SetHotkey(AppSettings.ModControl, (int)Keys.F8);
        model.Monitors[1].IsWhitelisted = true;
        model.NewDenylistEntry = "obs64";
        model.AddDenylistEntry();

        model.ApplyTo(settings);

        Assert.True(settings.StartWithWindows);
        Assert.True(settings.BackgroundGames);
        Assert.False(settings.GetAlwaysClearFocusedMonitor());
        Assert.Equal(400, settings.PollIntervalMs);
        Assert.Equal("Light", settings.Theme);
        Assert.Equal(AppSettings.ModControl, settings.HotkeyModifiers);
        Assert.Equal((int)Keys.F8, settings.HotkeyVirtualKey);
        Assert.Equal([@"\\.\DISPLAY2"], settings.WhitelistedDeviceNames);
        Assert.Contains("obs64", settings.DenylistProcessNames);
    }

    [Fact]
    public void Blackout_starts_on_black_and_switches_cleanly()
    {
        var model = Create();

        Assert.True(model.BlackoutIsBlack);
        Assert.False(model.BlackoutIsScreensaver);

        model.BlackoutIsScreensaver = true;

        Assert.Equal(BlackoutMode.Screensaver, model.Blackout);
        Assert.False(model.BlackoutIsBlack);
        Assert.True(model.IsDirty);
    }

    [Fact]
    public void The_screensaver_list_starts_with_follow_windows()
    {
        var model = Create();

        Assert.Equal(SettingsViewModel.FollowWindows, model.ScreensaverOptions[0]);
        Assert.Equal(3, model.ScreensaverOptions.Count);
        Assert.Equal(SettingsViewModel.FollowWindows, model.SelectedScreensaver);
        Assert.Equal(string.Empty, model.ScreensaverPath);
    }

    [Fact]
    public void A_saved_screensaver_is_reselected()
    {
        var model = Create(new AppSettings { ScreensaverPath = @"C:\saver\Bubbles.scr" });

        Assert.Equal("Bubbles", model.SelectedScreensaver.Name);
        Assert.False(model.IsDirty);
    }

    [Fact]
    public void A_missing_screensaver_falls_back_to_follow_windows()
    {
        var model = Create(new AppSettings { ScreensaverPath = @"C:\saver\Uninstalled.scr" });

        Assert.Equal(SettingsViewModel.FollowWindows, model.SelectedScreensaver);
    }

    [Fact]
    public void ApplyTo_writes_the_blackout_choice()
    {
        var settings = new AppSettings();
        var model = Create(settings);

        model.BlackoutIsScreensaver = true;
        model.SelectedScreensaver = model.ScreensaverOptions.First(option => option.Name == "Mystify");
        model.ApplyTo(settings);

        Assert.Equal("Screensaver", settings.Blackout);
        Assert.Equal(@"C:\saver\Mystify.scr", settings.ScreensaverPath);
        Assert.Equal(BlackoutMode.Screensaver, settings.GetBlackoutMode());
    }

    [Fact]
    public void MarkSaved_clears_the_dirty_flag()
    {
        var model = Create();
        model.BackgroundGames = true;
        model.MarkSaved();
        Assert.False(model.IsDirty);
    }
}
