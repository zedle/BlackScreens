namespace BlackScreens.Ui;

/// <summary>Theme preference stored in settings.</summary>
public enum AppTheme
{
    System,
    Dark,
    Light
}

/// <summary>The two palettes the app actually ships.</summary>
public enum ResolvedTheme
{
    Dark,
    Light
}

public static class AppThemes
{
    public static AppTheme Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "dark" => AppTheme.Dark,
        "light" => AppTheme.Light,
        _ => AppTheme.System
    };

    public static string Format(AppTheme theme) => theme switch
    {
        AppTheme.Dark => "Dark",
        AppTheme.Light => "Light",
        _ => "System"
    };

    /// <summary>Maps a preference plus the current Windows setting onto a palette.</summary>
    public static ResolvedTheme Resolve(AppTheme preference, bool systemUsesLight) => preference switch
    {
        AppTheme.Dark => ResolvedTheme.Dark,
        AppTheme.Light => ResolvedTheme.Light,
        _ => systemUsesLight ? ResolvedTheme.Light : ResolvedTheme.Dark
    };
}
