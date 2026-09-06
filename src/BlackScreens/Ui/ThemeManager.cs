using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace BlackScreens.Ui;

/// <summary>Owns the WPF application shim and swaps the palette dictionaries.</summary>
internal static class ThemeManager
{
    private const int DwmwaUseImmersiveDarkMode = 20;

    private static readonly Uri DarkPalette = new("/BlackScreens;component/Themes/Dark.xaml", UriKind.Relative);
    private static readonly Uri LightPalette = new("/BlackScreens;component/Themes/Light.xaml", UriKind.Relative);
    private static readonly Uri ControlStyles = new("/BlackScreens;component/Themes/Controls.xaml", UriKind.Relative);

    private static ResourceDictionary? _palette;
    private static ResourceDictionary? _controls;

    public static ResolvedTheme Current { get; private set; } = ResolvedTheme.Dark;

    /// <summary>Creates the WPF <see cref="Application"/> the settings window needs. Safe to call twice.</summary>
    public static void EnsureApplication()
    {
        if (Application.Current is not null)
        {
            return;
        }

        _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
    }

    public static void Apply(AppTheme preference)
    {
        EnsureApplication();

        var resolved = AppThemes.Resolve(preference, SystemTheme.UsesLightApps());
        var app = Application.Current;
        if (app is null)
        {
            return;
        }

        var next = new ResourceDictionary
        {
            Source = resolved == ResolvedTheme.Dark ? DarkPalette : LightPalette
        };

        ApplySystemAccent(next, resolved);

        if (_palette is not null)
        {
            app.Resources.MergedDictionaries.Remove(_palette);
        }

        app.Resources.MergedDictionaries.Insert(0, next);
        _palette = next;

        if (_controls is null)
        {
            _controls = new ResourceDictionary { Source = ControlStyles };
            app.Resources.MergedDictionaries.Add(_controls);
        }

        Current = resolved;
    }

    /// <summary>Paints the native title bar to match the palette instead of hand rolling window chrome.</summary>
    public static void ApplyWindowChrome(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == 0)
            {
                return;
            }

            var dark = Current == ResolvedTheme.Dark ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, DwmwaUseImmersiveDarkMode, ref dark, sizeof(int));
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Window chrome theming failed: {ex}");
        }
    }

    /// <summary>
    /// Drops the maximize box. The settings pages are laid out for a dialog sized window and look
    /// stretched and empty at full screen, and double clicking the title bar would maximize too.
    /// </summary>
    public static void DisableMaximize(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == 0)
            {
                return;
            }

            var style = NativeMethods.GetWindowLong(handle, NativeMethods.GwlStyle);
            NativeMethods.SetWindowLong(handle, NativeMethods.GwlStyle, style & ~WindowStyles.WsMaximizeBox);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Disabling maximize failed: {ex}");
        }
    }

    private static void ApplySystemAccent(ResourceDictionary palette, ResolvedTheme resolved)
    {
        var accent = SystemTheme.AccentColor();
        if (accent is null)
        {
            return;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(accent);
            palette["AccentBrush"] = new SolidColorBrush(Readable(color, resolved));
            palette["AccentTextBrush"] = new SolidColorBrush(Luminance(color) > 0.6 ? Colors.Black : Colors.White);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Accent apply failed: {ex}");
        }
    }

    /// <summary>Nudges very dark or very light accents so text and fills stay legible on both palettes.</summary>
    private static Color Readable(Color color, ResolvedTheme resolved)
    {
        var luminance = Luminance(color);
        if (resolved == ResolvedTheme.Dark && luminance < 0.18)
        {
            return Blend(color, Colors.White, 0.35);
        }

        if (resolved == ResolvedTheme.Light && luminance > 0.82)
        {
            return Blend(color, Colors.Black, 0.35);
        }

        return color;
    }

    private static double Luminance(Color color) =>
        ((0.2126 * color.R) + (0.7152 * color.G) + (0.0722 * color.B)) / 255.0;

    private static Color Blend(Color from, Color to, double amount) => Color.FromRgb(
        (byte)Math.Round((from.R * (1 - amount)) + (to.R * amount)),
        (byte)Math.Round((from.G * (1 - amount)) + (to.G * amount)),
        (byte)Math.Round((from.B * (1 - amount)) + (to.B * amount)));

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
