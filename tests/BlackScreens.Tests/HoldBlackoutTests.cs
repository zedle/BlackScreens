using System.Drawing;
using BlackScreens.Detection;
using Xunit;

namespace BlackScreens.Tests;

/// <summary>
/// Alt tabbing from the game to a program on the On top list should not uncover the screens, when
/// the option for it is on. The game has to still be there: this holds a blackout up, it does not
/// invent one.
/// </summary>
public sealed class HoldBlackoutTests
{
    private static readonly Rectangle Monitor1 = new(0, 0, 1920, 1080);
    private static readonly Rectangle Monitor2 = new(1920, 0, 1920, 1080);
    private static readonly Rectangle[] TwoMonitors = [Monitor1, Monitor2];

    private static WindowSnapshot Game(Rectangle monitor, bool foreground) =>
        new("hl2", monitor, WindowStyles.WsPopup, 0, true, false, foreground, monitor);

    private static WindowSnapshot Obs(Rectangle monitor, bool foreground) =>
        new("obs64", new Rectangle(monitor.X + 100, monitor.Y + 100, 800, 600),
            WindowStyles.WsCaption, 0, true, false, foreground, monitor);

    private static GameDetector Detector(bool hold) =>
        new(new ProcessDenylist(["chrome"]), new DetectOptions
        {
            BackgroundGames = false,
            AlwaysClearFocusedMonitor = true,
            HoldsBlackout = hold ? new ProcessRules(["obs64"]) : null
        });

    [Fact]
    public void With_the_option_off_focusing_the_program_ends_the_blackout()
    {
        // With the option turned off: the game is no longer in front, so nothing is blacked out.
        var result = Detector(hold: false).Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)], blackoutActive: true);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void With_the_option_on_the_blackout_survives_focusing_the_program()
    {
        var result = Detector(hold: true).Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)], blackoutActive: true);

        Assert.Equal([Monitor1], result.GameMonitors);

        // And the monitor the program is on stays black, which is the whole point: the window sits
        // above the black rather than the black getting out of its way.
        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_game_still_has_to_be_running()
    {
        // Same setup with the game gone. Nothing to hold up, so nothing is blacked out.
        var result = Detector(hold: true).Decide([Obs(Monitor2, foreground: true)], blackoutActive: true);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Focusing_something_else_still_ends_the_blackout()
    {
        // Only the listed programs hold it. Anything else behaves as before.
        var other = new WindowSnapshot("notepad", new Rectangle(2000, 100, 600, 400),
            WindowStyles.WsCaption, 0, true, false, true, Monitor2);

        var result = Detector(hold: true).Decide([Game(Monitor1, foreground: false), other], blackoutActive: true);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Playing_normally_is_unaffected()
    {
        var result = Detector(hold: true).Decide([Game(Monitor1, foreground: true), Obs(Monitor2, foreground: false)]);

        Assert.Equal([Monitor1], result.GameMonitors);
        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Focusing_the_program_with_nothing_running_does_not_black_anything_out()
    {
        // The bug this release fixes. Holding is only ever allowed to keep a blackout alive; with no
        // blackout to keep, focusing the program must do nothing at all.
        var result = Detector(hold: true).Decide([Obs(Monitor2, foreground: true)], blackoutActive: false);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void A_background_window_cannot_start_a_blackout_just_because_the_program_has_focus()
    {
        // Holding used to judge every window as if background games were on, so anything fullscreen
        // sitting behind could start a blackout that would never otherwise have happened.
        var result = Detector(hold: true).Decide(
            [Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)], blackoutActive: false);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void A_program_on_the_list_is_never_itself_the_game()
    {
        // Obsidian and the like run fullscreen quite happily. Being on the on top list says the
        // program belongs over a blackout, so it must never be the thing that causes one.
        var fullscreenObs = new WindowSnapshot("obs64", Monitor2, WindowStyles.WsPopup, 0,
            true, false, true, Monitor2);

        var result = Detector(hold: true).Decide([fullscreenObs], blackoutActive: false);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));

        // Not even while a blackout is already running.
        var during = Detector(hold: true).Decide([fullscreenObs], blackoutActive: true);
        Assert.Empty(during.GameMonitors);
    }

    [Fact]
    public void An_empty_list_holds_nothing()
    {
        var detector = new GameDetector(new ProcessDenylist([]), new DetectOptions
        {
            AlwaysClearFocusedMonitor = true,
            HoldsBlackout = new ProcessRules([])
        });

        var result = detector.Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)], blackoutActive: true);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_setting_is_on_by_default_but_idle_until_something_is_listed()
    {
        var settings = new AppSettings();
        Assert.True(settings.HoldBlackoutForAboveOverlay);

        // On, but with nothing listed there is nothing to hold, so detection is unchanged.
        Assert.Null(settings.ToDetectOptions().HoldsBlackout);

        settings.AboveOverlayProcessNames = ["obs64"];
        var options = settings.ToDetectOptions();
        Assert.NotNull(options.HoldsBlackout);
        Assert.True(options.HoldsBlackout!.Matches("obs64", null));
    }

    [Fact]
    public void Turning_it_off_puts_the_old_behaviour_back()
    {
        var settings = new AppSettings
        {
            AboveOverlayProcessNames = ["obs64"],
            HoldBlackoutForAboveOverlay = false
        };

        Assert.Null(settings.ToDetectOptions().HoldsBlackout);
    }

    [Fact]
    public void Turning_it_on_with_nothing_listed_changes_nothing()
    {
        var settings = new AppSettings { HoldBlackoutForAboveOverlay = true };

        Assert.Null(settings.ToDetectOptions().HoldsBlackout);
    }
}

/// <summary>
/// Holding Alt Tab used to end a blackout: the switcher takes the foreground, so the game was no
/// longer in front and the screens lit up behind the switcher itself. It is not a real change of
/// foreground, and this holds whatever the settings say.
/// </summary>
public sealed class TaskSwitcherTests
{
    private static readonly Rectangle Monitor1 = new(0, 0, 1920, 1080);
    private static readonly Rectangle Monitor2 = new(1920, 0, 1920, 1080);
    private static readonly Rectangle[] TwoMonitors = [Monitor1, Monitor2];

    private static WindowSnapshot Game(Rectangle monitor, bool foreground) =>
        new("hl2", monitor, WindowStyles.WsPopup, 0, true, false, foreground, monitor);

    private static WindowSnapshot Switcher(Rectangle monitor, string className) =>
        new("explorer", new Rectangle(monitor.X + 200, monitor.Y + 300, 1200, 400),
            WindowStyles.WsPopup, 0, true, false, true, monitor)
        {
            ClassName = className
        };

    /// <summary>The strictest settings: nothing in the background counts, focused monitor cleared.</summary>
    private static GameDetector Strict() =>
        new(new ProcessDenylist([]), new DetectOptions
        {
            BackgroundGames = false,
            AlwaysClearFocusedMonitor = true
        });

    [Theory]
    [InlineData("XamlExplorerHostIslandWindow")]
    [InlineData("MultitaskingViewFrame")]
    [InlineData("TaskSwitcherWnd")]
    [InlineData("TaskSwitcherOverlayWnd")]
    public void The_switcher_does_not_end_a_blackout(string className)
    {
        var result = Strict().Decide([Game(Monitor1, foreground: false), Switcher(Monitor1, className)], blackoutActive: true);

        Assert.Equal([Monitor1], result.GameMonitors);
        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_monitor_the_switcher_is_on_is_not_cleared()
    {
        // The switcher appears over the blacked out screen, and clearing that monitor would show the
        // desktop behind it, which is the flash this is meant to stop.
        var result = Strict().Decide([Game(Monitor1, foreground: false), Switcher(Monitor2, "TaskSwitcherWnd")], blackoutActive: true);

        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void It_still_needs_a_game()
    {
        var result = Strict().Decide([Switcher(Monitor1, "TaskSwitcherWnd")], blackoutActive: true);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_switcher_cannot_start_a_blackout_either()
    {
        // Alt tabbing around with nothing running must not black the screens out.
        var result = Strict().Decide(
            [Game(Monitor1, foreground: false), Switcher(Monitor1, "TaskSwitcherWnd")], blackoutActive: false);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void An_ordinary_window_of_the_same_process_is_not_the_switcher()
    {
        // explorer.exe also owns the taskbar, the desktop and every File Explorer window, so this
        // cannot be matched by process.
        var explorer = new WindowSnapshot("explorer", new Rectangle(2000, 100, 900, 600),
            WindowStyles.WsCaption, 0, true, false, true, Monitor2)
        {
            ClassName = "CabinetWClass"
        };

        var result = Strict().Decide([Game(Monitor1, foreground: false), explorer], blackoutActive: true);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Theory]
    [InlineData("CabinetWClass")]
    [InlineData("Shell_TrayWnd")]
    [InlineData("Progman")]
    [InlineData("")]
    [InlineData(null)]
    public void Only_the_switcher_classes_count(string? className)
    {
        Assert.False(TaskSwitcher.Is(className));
    }
}
