using Xunit;

namespace BlackScreens.Tests;

public sealed class HotkeySettingsTests
{
    [Fact]
    public void A_shortcut_needs_ctrl_alt_or_win()
    {
        Assert.True(AppSettings.IsUsableHotkey(AppSettings.ModControl, 0x42));
        Assert.True(AppSettings.IsUsableHotkey(AppSettings.ModAlt, 0x42));
        Assert.True(AppSettings.IsUsableHotkey(AppSettings.ModWin, 0x42));
        Assert.False(AppSettings.IsUsableHotkey(AppSettings.ModShift, 0x42));
        Assert.False(AppSettings.IsUsableHotkey(0, 0x42));
    }

    [Fact]
    public void A_shortcut_needs_a_key()
    {
        Assert.False(AppSettings.IsUsableHotkey(AppSettings.ModControl | AppSettings.ModAlt, 0));
    }

    [Fact]
    public void Format_lists_modifiers_in_a_stable_order()
    {
        var text = AppSettings.FormatHotkey(
            AppSettings.ModShift | AppSettings.ModWin | AppSettings.ModAlt | AppSettings.ModControl,
            0x42);

        Assert.Equal("Ctrl + Alt + Shift + Win + B", text);
    }

    [Fact]
    public void Format_spells_out_digits_and_punctuation()
    {
        Assert.Equal("Ctrl + Alt + 1", AppSettings.FormatHotkey(AppSettings.DefaultHotkeyModifiers, (int)Keys.D1));
        Assert.Equal("Ctrl + Alt + `", AppSettings.FormatHotkey(AppSettings.DefaultHotkeyModifiers, (int)Keys.Oemtilde));
        Assert.Equal("Ctrl + Alt + Num 5", AppSettings.FormatHotkey(AppSettings.DefaultHotkeyModifiers, (int)Keys.NumPad5));
    }

    [Fact]
    public void Format_says_none_when_no_key_is_bound()
    {
        Assert.Equal("Ctrl + None", AppSettings.FormatHotkey(AppSettings.ModControl, 0));
    }
}
