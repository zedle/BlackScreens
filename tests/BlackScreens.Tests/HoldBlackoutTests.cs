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
        // The old behaviour, and still the default: the game is no longer in front, so nothing is
        // blacked out.
        var result = Detector(hold: false).Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void With_the_option_on_the_blackout_survives_focusing_the_program()
    {
        var result = Detector(hold: true).Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)]);

        Assert.Equal([Monitor1], result.GameMonitors);

        // And the monitor the program is on stays black, which is the whole point: the window sits
        // above the black rather than the black getting out of its way.
        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_game_still_has_to_be_running()
    {
        // Same setup with the game gone. Nothing to hold up, so nothing is blacked out.
        var result = Detector(hold: true).Decide([Obs(Monitor2, foreground: true)]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Focusing_something_else_still_ends_the_blackout()
    {
        // Only the listed programs hold it. Anything else behaves as before.
        var other = new WindowSnapshot("notepad", new Rectangle(2000, 100, 600, 400),
            WindowStyles.WsCaption, 0, true, false, true, Monitor2);

        var result = Detector(hold: true).Decide([Game(Monitor1, foreground: false), other]);

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
    public void An_empty_list_holds_nothing()
    {
        var detector = new GameDetector(new ProcessDenylist([]), new DetectOptions
        {
            AlwaysClearFocusedMonitor = true,
            HoldsBlackout = new ProcessRules([])
        });

        var result = detector.Decide([Game(Monitor1, foreground: false), Obs(Monitor2, foreground: true)]);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void The_setting_is_off_on_a_new_install_and_survives_a_save()
    {
        var settings = new AppSettings();
        Assert.False(settings.HoldBlackoutForAboveOverlay);
        Assert.Null(settings.ToDetectOptions().HoldsBlackout);

        settings.AboveOverlayProcessNames = ["obs64"];
        settings.HoldBlackoutForAboveOverlay = true;

        var options = settings.ToDetectOptions();
        Assert.NotNull(options.HoldsBlackout);
        Assert.True(options.HoldsBlackout!.Matches("obs64", null));
    }

    [Fact]
    public void Turning_it_on_with_nothing_listed_changes_nothing()
    {
        var settings = new AppSettings { HoldBlackoutForAboveOverlay = true };

        Assert.Null(settings.ToDetectOptions().HoldsBlackout);
    }
}
