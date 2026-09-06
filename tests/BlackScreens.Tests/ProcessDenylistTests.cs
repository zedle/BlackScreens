using Xunit;

namespace BlackScreens.Tests;

public sealed class ProcessDenylistTests
{
    private readonly ProcessDenylist _denylist = new();

    [Theory]
    [InlineData("chrome")]
    [InlineData("Chrome")]
    [InlineData("discord")]
    [InlineData("explorer")]
    [InlineData("Code")]
    public void Contains_known_names_ignoring_case(string name)
    {
        Assert.True(_denylist.Contains(name));
    }

    [Fact]
    public void Does_not_contain_a_game_process()
    {
        Assert.False(_denylist.Contains("hl2"));
    }

    [Theory]
    [InlineData("chrome", "chrome")]
    [InlineData("chrome.exe", "chrome")]
    [InlineData("  Chrome.EXE  ", "Chrome")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData(".exe", "")]
    [InlineData(null, "")]
    public void Normalize_trims_whitespace_and_the_extension(string? entry, string expected)
    {
        Assert.Equal(expected, ProcessDenylist.Normalize(entry));
    }
}
