using BlackScreens.Detection;
using Xunit;

namespace BlackScreens.Tests;

/// <summary>
/// An entry in the denylist or the on top list is either a bare process name, covering every copy of
/// a program, or the full path to one executable.
/// </summary>
public sealed class ProcessRulesTests
{
    private const string ObsPath = @"C:\Program Files\obs-studio\bin\64bit\obs64.exe";
    private const string PortableObs = @"D:\portable\obs\obs64.exe";

    [Theory]
    // A bare name, with or without the extension, and however it was spaced.
    [InlineData("obs64", "obs64")]
    [InlineData("obs64.exe", "obs64")]
    [InlineData("  obs64.exe  ", "obs64")]
    [InlineData("OBS64.EXE", "OBS64")]
    // A path keeps its extension, because that is what it is compared against.
    [InlineData(@"C:\games\game.exe", @"C:\games\game.exe")]
    [InlineData(@"  C:\games\game.exe  ", @"C:\games\game.exe")]
    // Explorer copies a path with quotes round it.
    [InlineData(@"""C:\games\game.exe""", @"C:\games\game.exe")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void An_entry_is_tidied_without_losing_what_it_means(string? entry, string expected)
    {
        Assert.Equal(expected, ProcessRules.Normalize(entry));
    }

    [Theory]
    [InlineData(@"C:\games\game.exe", true)]
    [InlineData(@"C:/games/game.exe", true)]
    [InlineData(@"..\game.exe", true)]
    [InlineData("game.exe", false)]
    [InlineData("game", false)]
    public void Anything_with_a_directory_in_it_is_a_path(string entry, bool expected)
    {
        Assert.Equal(expected, ProcessRules.IsPath(entry));
    }

    [Fact]
    public void A_name_covers_every_copy_of_the_program()
    {
        var rules = new ProcessRules(["obs64"]);

        Assert.True(rules.Matches("obs64", ObsPath));
        Assert.True(rules.Matches("obs64", PortableObs));
        Assert.True(rules.Matches("obs64", null));
    }

    [Fact]
    public void A_path_covers_only_that_one_file()
    {
        var rules = new ProcessRules([ObsPath]);

        Assert.True(rules.Matches("obs64", ObsPath));
        Assert.False(rules.Matches("obs64", PortableObs));

        // Same program, but nothing to compare the path against.
        Assert.False(rules.Matches("obs64", null));
    }

    [Fact]
    public void A_path_is_matched_whatever_case_it_was_stored_in()
    {
        var rules = new ProcessRules([ObsPath.ToUpperInvariant()]);

        Assert.True(rules.Matches("obs64", ObsPath.ToLowerInvariant()));
    }

    [Fact]
    public void The_two_kinds_of_entry_live_side_by_side()
    {
        var rules = new ProcessRules(["chrome", PortableObs]);

        Assert.Equal(2, rules.Count);
        Assert.True(rules.Matches("chrome", @"C:\Program Files\Google\Chrome\chrome.exe"));
        Assert.True(rules.Matches("obs64", PortableObs));
        Assert.False(rules.Matches("obs64", ObsPath));
    }

    [Fact]
    public void Nothing_matches_an_empty_list()
    {
        var rules = new ProcessRules([]);

        Assert.Equal(0, rules.Count);
        Assert.False(rules.Matches("obs64", ObsPath));
    }

    [Fact]
    public void A_row_shows_the_file_name_rather_than_the_whole_path()
    {
        Assert.Equal("obs64.exe", ProcessRules.Display(ObsPath));
        Assert.Equal("chrome", ProcessRules.Display("chrome"));
    }

    [Fact]
    public void A_row_says_which_kind_of_entry_it_is()
    {
        // Without this the two kinds are told apart only by a ".exe", which is far too subtle.
        Assert.Equal(@"C:\Program Files\obs-studio\bin\64bit", ProcessRules.Detail(ObsPath));
        Assert.Equal("any copy", ProcessRules.Detail("chrome"));
        Assert.Equal("any copy", ProcessRules.Detail("obs64"));
    }

    [Fact]
    public void The_denylist_takes_a_path_too()
    {
        var denylist = new ProcessDenylist([PortableObs, "chrome"]);

        Assert.True(denylist.Contains("obs64", PortableObs));
        Assert.False(denylist.Contains("obs64", ObsPath));
        Assert.True(denylist.Contains("chrome", null));
    }

    [Fact]
    public void The_defaults_are_still_plain_names()
    {
        var denylist = new ProcessDenylist();

        Assert.True(denylist.Contains("explorer", @"C:\Windows\explorer.exe"));
        Assert.True(denylist.Contains("chrome", null));
        Assert.False(denylist.Contains("someGame", @"C:\games\someGame.exe"));
    }
}
