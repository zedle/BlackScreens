using System.Drawing;
using BlackScreens.Detection;
using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

/// <summary>
/// The two things 1.1 added to the overlay: how solid it is, and which programs stay above it.
/// </summary>
public sealed class OverlayOptionsTests
{
    private static AppSettings Settings() => new();

    private static SettingsViewModel Model(AppSettings settings) =>
        new(settings, [new ConnectedMonitor(@"\\.\DISPLAY1", new Rectangle(0, 0, 1920, 1080))], []);

    [Fact]
    public void A_new_install_has_an_opaque_overlay_and_nothing_on_top()
    {
        var settings = Settings();

        Assert.Equal(100, settings.GetOverlayOpacity());
        Assert.Empty(settings.GetAboveOverlay());
    }

    [Theory]
    [InlineData(100, 100)]
    [InlineData(55, 55)]
    [InlineData(20, 20)]
    // Below the minimum the overlay stops doing its job, and above 100 means nothing.
    [InlineData(0, 20)]
    [InlineData(-40, 20)]
    [InlineData(140, 100)]
    public void Opacity_is_held_between_the_minimum_and_opaque(int stored, int expected)
    {
        Assert.Equal(expected, new AppSettings { OverlayOpacity = stored }.GetOverlayOpacity());
        Assert.Equal(expected, new AppSettings { OverlayOpacity = stored }.Normalize().OverlayOpacity);
    }

    [Fact]
    public void A_settings_file_from_before_1_1_reads_as_opaque()
    {
        // The property is simply absent from an older file, so it takes its default.
        var settings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
            """{ "PollIntervalMs": 250, "Theme": "Dark" }""");

        Assert.NotNull(settings);
        Assert.Equal(100, settings!.GetOverlayOpacity());
        Assert.Empty(settings.GetAboveOverlay());
    }

    [Theory]
    [InlineData("obs64.exe")]
    [InlineData("  OBS64  ")]
    [InlineData("obs64")]
    public void A_program_is_matched_however_the_name_was_typed(string entry)
    {
        var rules = new ProcessRules([entry]);

        Assert.True(rules.Matches("obs64", @"C:\Program Files\obs-studioin4bit\obs64.exe"));
        Assert.True(rules.Matches("OBS64", null));
        Assert.False(rules.Matches("obs", null));
    }

    [Fact]
    public void Blank_entries_are_not_something_to_look_for()
    {
        Assert.Equal(0, new ProcessRules(["", "   ", ".exe"]).Count);
        Assert.Equal(0, new ProcessRules([]).Count);
    }

    [Fact]
    public void The_same_program_twice_is_one_entry()
    {
        Assert.Equal(1, new ProcessRules(["obs64", "OBS64.exe"]).Count);
    }

    [Fact]
    public void Opacity_and_the_on_top_list_survive_a_save()
    {
        var settings = Settings();
        var model = Model(settings);

        model.OverlayOpacity = 70;
        model.NewAboveOverlayEntry = "obs64.exe";
        Assert.True(model.AddAboveOverlayEntry());

        model.ApplyTo(settings);

        Assert.Equal(70, settings.GetOverlayOpacity());
        Assert.Equal(["obs64"], settings.GetAboveOverlay());
    }

    [Fact]
    public void The_list_stays_in_order_and_holds_each_program_once()
    {
        var model = Model(Settings());

        foreach (var name in new[] { "obs64", "streamlabs", "Discord", "obs64.exe" })
        {
            model.NewAboveOverlayEntry = name;
            model.AddAboveOverlayEntry();
        }

        Assert.Equal(["Discord", "obs64", "streamlabs"], model.AboveOverlay);
    }

    [Fact]
    public void Adding_and_removing_marks_the_settings_dirty()
    {
        var model = Model(Settings());
        Assert.False(model.IsDirty);

        model.NewAboveOverlayEntry = "obs64";
        model.AddAboveOverlayEntry();
        Assert.True(model.IsDirty);

        model.SelectedAboveOverlayEntry = "obs64";
        Assert.True(model.RemoveSelectedAboveOverlayEntry());
        Assert.Empty(model.AboveOverlay);
    }

    [Fact]
    public void Moving_the_opacity_slider_marks_the_settings_dirty()
    {
        var model = Model(Settings());

        model.OverlayOpacity = 60;

        Assert.True(model.IsDirty);
        Assert.Equal("60%", model.OverlayOpacityText);
    }

    [Fact]
    public void Full_opacity_reads_as_opaque_rather_than_a_number()
    {
        var model = Model(Settings());

        Assert.Equal("Opaque", model.OverlayOpacityText);
    }
}
