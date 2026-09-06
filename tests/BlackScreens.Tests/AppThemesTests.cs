using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

public sealed class AppThemesTests
{
    [Theory]
    [InlineData("Dark", AppTheme.Dark)]
    [InlineData("dark", AppTheme.Dark)]
    [InlineData(" LIGHT ", AppTheme.Light)]
    [InlineData("System", AppTheme.System)]
    [InlineData("", AppTheme.System)]
    [InlineData(null, AppTheme.System)]
    [InlineData("nonsense", AppTheme.System)]
    public void Parse_is_forgiving(string? value, AppTheme expected)
    {
        Assert.Equal(expected, AppThemes.Parse(value));
    }

    [Fact]
    public void Format_round_trips_through_parse()
    {
        foreach (var theme in new[] { AppTheme.System, AppTheme.Dark, AppTheme.Light })
        {
            Assert.Equal(theme, AppThemes.Parse(AppThemes.Format(theme)));
        }
    }

    [Theory]
    [InlineData(AppTheme.Dark, true, ResolvedTheme.Dark)]
    [InlineData(AppTheme.Dark, false, ResolvedTheme.Dark)]
    [InlineData(AppTheme.Light, true, ResolvedTheme.Light)]
    [InlineData(AppTheme.Light, false, ResolvedTheme.Light)]
    [InlineData(AppTheme.System, true, ResolvedTheme.Light)]
    [InlineData(AppTheme.System, false, ResolvedTheme.Dark)]
    public void Resolve_follows_windows_only_for_the_system_preference(
        AppTheme preference,
        bool systemUsesLight,
        ResolvedTheme expected)
    {
        Assert.Equal(expected, AppThemes.Resolve(preference, systemUsesLight));
    }
}
