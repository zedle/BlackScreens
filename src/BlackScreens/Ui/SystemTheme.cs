using System.Globalization;
using Microsoft.Win32;

namespace BlackScreens.Ui;

/// <summary>Reads the Windows personalization settings that the app follows.</summary>
internal static class SystemTheme
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string DwmKey = @"Software\Microsoft\Windows\DWM";

    /// <summary>True when Windows apps are set to light mode. Defaults to dark when unreadable.</summary>
    public static bool UsesLightApps()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Theme probe failed: {ex}");
            return false;
        }
    }

    /// <summary>The Windows accent color as #RRGGBB, or null when it cannot be read.</summary>
    public static string? AccentColor()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DwmKey);
            if (key?.GetValue("AccentColor") is not int packed)
            {
                return null;
            }

            // DWM stores the accent as 0xAABBGGRR.
            var r = packed & 0xFF;
            var g = (packed >> 8) & 0xFF;
            var b = (packed >> 16) & 0xFF;
            return string.Create(CultureInfo.InvariantCulture, $"#{r:X2}{g:X2}{b:X2}");
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Accent probe failed: {ex}");
            return null;
        }
    }
}
