using System.Drawing;
using Xunit;

namespace BlackScreens.Tests;

public sealed class GameDetectorTests
{
    private static readonly Rectangle Monitor1 = new(0, 0, 1920, 1080);
    private static readonly Rectangle Monitor2 = new(1920, 0, 1920, 1080);
    private static readonly Rectangle Monitor3 = new(3840, 0, 2560, 1440);
    private static readonly Rectangle[] TwoMonitors = [Monitor1, Monitor2];
    private static readonly Rectangle[] ThreeMonitors = [Monitor1, Monitor2, Monitor3];

    private readonly GameDetector _detector = new(new ProcessDenylist(), DetectOptions.Classic);

    [Fact]
    public void Borderless_game_on_monitor1_blacks_empty_monitor2()
    {
        var result = _detector.Decide([GameOn(Monitor1)]);

        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Focused_discord_on_monitor2_keeps_both_clear()
    {
        var result = _detector.Decide(
        [
            GameOn(Monitor1),
            DiscordOn(Monitor2, foreground: true)
        ]);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Chrome_fullscreen_only_does_not_start_blackout()
    {
        var result = _detector.Decide([ChromeFullscreen(Monitor1, foreground: true)]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Maximized_captioned_notepad_does_not_start_blackout()
    {
        var result = _detector.Decide([MaximizedNotepad(Monitor1)]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Two_games_leave_only_the_third_monitor_black()
    {
        var result = _detector.Decide(
        [
            GameOn(Monitor1, "hl2"),
            GameOn(Monitor2, "eldenring")
        ]);

        Assert.Equal([Monitor3], result.BlackMonitors(ThreeMonitors));
    }

    [Fact]
    public void Invisible_cloaked_or_tool_window_is_not_a_game()
    {
        var invisible = GameOn(Monitor1) with { Visible = false };
        var cloaked = GameOn(Monitor1) with { Cloaked = true };
        var tool = GameOn(Monitor1) with { ExStyle = WindowStyles.WsExToolWindow };

        Assert.Empty(_detector.Decide([invisible]).GameMonitors);
        Assert.Empty(_detector.Decide([cloaked]).GameMonitors);
        Assert.Empty(_detector.Decide([tool]).GameMonitors);
    }

    [Fact]
    public void Window_3px_short_of_monitor_is_not_fullscreen()
    {
        var shortWindow = GameOn(Monitor1) with
        {
            Bounds = new Rectangle(0, 0, 1920, 1077)
        };

        Assert.Empty(_detector.Decide([shortWindow]).GameMonitors);
    }

    [Fact]
    public void Window_1px_off_monitor_still_counts_as_fullscreen()
    {
        var slack = GameOn(Monitor1) with
        {
            Bounds = new Rectangle(1, 0, 1920, 1080)
        };

        Assert.Equal([Monitor1], _detector.Decide([slack]).GameMonitors);
    }

    [Fact]
    public void Caption_plus_popup_full_rect_is_a_game()
    {
        var window = GameOn(Monitor1) with
        {
            Style = WindowStyles.WsCaption | WindowStyles.WsPopup
        };

        Assert.Equal([Monitor1], _detector.Decide([window]).GameMonitors);
    }

    [Fact]
    public void Denylist_focus_without_a_game_does_not_start_blackout()
    {
        var result = _detector.Decide([DiscordOn(Monitor2, foreground: true)]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    private static WindowSnapshot GameOn(Rectangle monitor, string name = "hl2", bool foreground = false) =>
        new(name, monitor, WindowStyles.WsPopup, 0, true, false, foreground, monitor);

    private static WindowSnapshot DiscordOn(Rectangle monitor, bool foreground) =>
        new("discord", new Rectangle(monitor.X + 100, monitor.Y + 100, 800, 600),
            WindowStyles.WsCaption, 0, true, false, foreground, monitor);

    private static WindowSnapshot ChromeFullscreen(Rectangle monitor, bool foreground) =>
        new("chrome", monitor, WindowStyles.WsPopup, 0, true, false, foreground, monitor);

    private static WindowSnapshot MaximizedNotepad(Rectangle monitor) =>
        new("notepad", new Rectangle(monitor.X, monitor.Y, monitor.Width, monitor.Height - 40),
            WindowStyles.WsCaption, 0, true, false, true, monitor);
}
