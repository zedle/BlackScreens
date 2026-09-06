using Xunit;

namespace BlackScreens.Tests;

public sealed class BlackoutModeTests
{
    [Theory]
    [InlineData("Screensaver", BlackoutMode.Screensaver)]
    [InlineData("screensaver", BlackoutMode.Screensaver)]
    [InlineData("  SCREENSAVER ", BlackoutMode.Screensaver)]
    [InlineData("Black", BlackoutMode.Black)]
    [InlineData("", BlackoutMode.Black)]
    [InlineData(null, BlackoutMode.Black)]
    [InlineData("nonsense", BlackoutMode.Black)]
    public void Parse_defaults_to_black(string? value, BlackoutMode expected)
    {
        Assert.Equal(expected, BlackoutModes.Parse(value));
    }

    [Fact]
    public void Format_round_trips()
    {
        Assert.Equal(BlackoutMode.Black, BlackoutModes.Parse(BlackoutModes.Format(BlackoutMode.Black)));
        Assert.Equal(
            BlackoutMode.Screensaver,
            BlackoutModes.Parse(BlackoutModes.Format(BlackoutMode.Screensaver)));
    }

    [Fact]
    public void Black_mode_never_asks_for_a_screensaver()
    {
        var settings = new AppSettings { Blackout = "Black", ScreensaverPath = @"C:\Windows\System32\Mystify.scr" };
        Assert.Null(settings.GetScreensaverPath());
    }
}

public sealed class ScreensaversTests
{
    [Theory]
    [InlineData(@"C:\Windows\System32\scrnsave.scr", "Blank")]
    [InlineData(@"C:\Windows\System32\ssText3d.scr", "3D Text")]
    [InlineData(@"C:\Windows\System32\Bubbles.scr", "Bubbles")]
    [InlineData(@"G:\Downloads\Flurry\Flurry.scr", "Flurry")]
    public void Describe_prefers_a_friendly_name(string path, string expected)
    {
        Assert.Equal(expected, Screensavers.Describe(path));
    }

    [Fact]
    public void Choose_prefers_the_setting_over_the_windows_default()
    {
        var chosen = Screensavers.Choose(
            @"C:\saver\Chosen.scr",
            @"C:\saver\Windows.scr",
            _ => true);

        Assert.Equal(@"C:\saver\Chosen.scr", chosen);
    }

    [Fact]
    public void Choose_falls_back_to_the_windows_setting()
    {
        var chosen = Screensavers.Choose(
            string.Empty,
            @"C:\saver\Windows.scr",
            _ => true);

        Assert.Equal(@"C:\saver\Windows.scr", chosen);
    }

    [Fact]
    public void Choose_skips_a_saver_that_is_no_longer_installed()
    {
        var chosen = Screensavers.Choose(
            @"C:\saver\Gone.scr",
            @"C:\saver\Windows.scr",
            path => path == @"C:\saver\Windows.scr");

        Assert.Equal(@"C:\saver\Windows.scr", chosen);
    }

    [Fact]
    public void Choose_returns_null_when_nothing_is_available()
    {
        Assert.Null(Screensavers.Choose(string.Empty, null, _ => true));
        Assert.Null(Screensavers.Choose(@"C:\saver\Gone.scr", @"C:\saver\AlsoGone.scr", _ => false));
    }
}
