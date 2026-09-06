using System.Drawing;
using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

public sealed class MonitorLayoutTests
{
    private static ConnectedMonitor At(int x, int y, int width, int height) =>
        new($@"\\.\DISPLAY{x}", new Rectangle(x, y, width, height));

    [Fact]
    public void One_monitor_is_its_own_map()
    {
        Assert.Equal(new Rectangle(0, 0, 3840, 2160), MonitorLayout.Union([At(0, 0, 3840, 2160)]));
    }

    [Fact]
    public void Two_side_by_side_make_a_map_as_wide_as_both()
    {
        var union = MonitorLayout.Union([At(0, 0, 3840, 2160), At(3840, 0, 2560, 1440)]);

        Assert.Equal(new Rectangle(0, 0, 6400, 2160), union);
    }

    [Fact]
    public void A_monitor_above_and_left_of_the_primary_moves_the_origin_negative()
    {
        // Windows puts the primary at 0,0 and everything else around it, so both are common.
        var union = MonitorLayout.Union([At(0, 0, 1920, 1080), At(-1200, -1600, 1200, 1600)]);

        Assert.Equal(new Rectangle(-1200, -1600, 3120, 2680), union);
    }

    [Fact]
    public void No_monitors_is_an_empty_map_rather_than_a_crash()
    {
        Assert.Equal(Rectangle.Empty, MonitorLayout.Union([]));
    }

    [Fact]
    public void Placing_a_monitor_shifts_it_so_the_map_starts_at_zero()
    {
        var monitor = At(-1200, -1600, 1200, 1600);
        var union = MonitorLayout.Union([monitor, At(0, 0, 1920, 1080)]);
        var choice = new MonitorChoice(monitor, isWhitelisted: false);

        choice.PlaceOnMap(union);

        // The gap that keeps two touching monitors apart is the only thing between the tile and the
        // corner of the map, so the offsets are small and positive rather than zero.
        Assert.InRange(choice.MapLeft, 0, union.Height * 0.01);
        Assert.InRange(choice.MapTop, 0, union.Height * 0.01);
        Assert.InRange(choice.MapWidth, 1150, 1200);
        Assert.InRange(choice.MapHeight, 1550, 1600);
    }

    [Fact]
    public void A_tall_narrow_monitor_drops_the_resolution_label()
    {
        var narrow = At(0, 0, 682, 2560);
        var wide = At(700, 0, 3840, 2160);
        var union = MonitorLayout.Union([narrow, wide]);

        var narrowChoice = new MonitorChoice(narrow, isWhitelisted: false);
        var wideChoice = new MonitorChoice(wide, isWhitelisted: false);
        narrowChoice.PlaceOnMap(union);
        wideChoice.PlaceOnMap(union);

        Assert.False(narrowChoice.SizeFits);
        Assert.True(wideChoice.SizeFits);
    }

    [Fact]
    public void Tiles_are_sized_against_the_desktop_so_the_numbers_come_out_the_same_size()
    {
        // Two setups of very different pixel counts. The window scales each map to the same box, so
        // the number has to be the same fraction of the desktop height in both.
        var small = At(0, 0, 1920, 1080);
        var large = At(0, 0, 5120, 2160);

        var smallChoice = new MonitorChoice(small, isWhitelisted: false);
        var largeChoice = new MonitorChoice(large, isWhitelisted: false);
        smallChoice.PlaceOnMap(MonitorLayout.Union([small]));
        largeChoice.PlaceOnMap(MonitorLayout.Union([large]));

        Assert.Equal(smallChoice.NumberFontSize / 1080, largeChoice.NumberFontSize / 2160, 6);
    }
}
