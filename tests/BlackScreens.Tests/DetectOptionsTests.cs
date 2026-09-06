using System.Drawing;
using Xunit;

namespace BlackScreens.Tests;

public sealed class DetectOptionsTests
{
    private static readonly Rectangle Monitor1 = new(0, 0, 1920, 1080);
    private static readonly Rectangle Monitor2 = new(1920, 0, 1920, 1080);
    private static readonly Rectangle[] TwoMonitors = [Monitor1, Monitor2];

    [Fact]
    public void Safe_mode_ignores_background_fullscreen_process()
    {
        var detector = new GameDetector(new ProcessDenylist(), DetectOptions.Safe);
        var result = detector.Decide(
        [
            new WindowSnapshot("hl2", Monitor1, WindowStyles.WsPopup, 0, true, false, false, Monitor1)
        ]);

        Assert.Empty(result.GameMonitors);
        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Safe_mode_clears_focused_monitor_even_if_not_denylisted()
    {
        var detector = new GameDetector(new ProcessDenylist(), DetectOptions.Safe);
        var result = detector.Decide(
        [
            new WindowSnapshot("hl2", Monitor1, WindowStyles.WsPopup, 0, true, false, true, Monitor1),
            new WindowSnapshot("notepad", new Rectangle(2000, 100, 800, 600), WindowStyles.WsCaption, 0, true, false, false, Monitor2)
        ]);

        Assert.Equal([Monitor2], result.BlackMonitors(TwoMonitors));
    }

    [Fact]
    public void Always_clear_focus_keeps_notepad_monitor_visible_while_background_game_runs()
    {
        var detector = new GameDetector(
            new ProcessDenylist(),
            new DetectOptions { BackgroundGames = true, AlwaysClearFocusedMonitor = true });

        var result = detector.Decide(
        [
            new WindowSnapshot("hl2", Monitor1, WindowStyles.WsPopup, 0, true, false, false, Monitor1),
            new WindowSnapshot("notepad", new Rectangle(2000, 100, 800, 600), WindowStyles.WsCaption, 0, true, false, true, Monitor2)
        ]);

        Assert.Empty(result.BlackMonitors(TwoMonitors));
    }
}
