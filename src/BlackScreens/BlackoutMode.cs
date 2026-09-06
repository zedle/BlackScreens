namespace BlackScreens;

/// <summary>What a blacked out monitor actually shows.</summary>
public enum BlackoutMode
{
    /// <summary>Plain black. The default, and the cheapest.</summary>
    Black,

    /// <summary>The Windows screensaver, drawn into the overlay.</summary>
    Screensaver
}

public static class BlackoutModes
{
    public static BlackoutMode Parse(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "screensaver" => BlackoutMode.Screensaver,
        _ => BlackoutMode.Black
    };

    public static string Format(BlackoutMode mode) =>
        mode == BlackoutMode.Screensaver ? "Screensaver" : "Black";
}
