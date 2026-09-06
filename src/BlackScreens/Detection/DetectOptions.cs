namespace BlackScreens.Detection;

public sealed record DetectOptions
{
    public bool BackgroundGames { get; init; }

    public bool AlwaysClearFocusedMonitor { get; init; }

    public static DetectOptions Classic { get; } = new()
    {
        BackgroundGames = true,
        AlwaysClearFocusedMonitor = false
    };

    public static DetectOptions Safe { get; } = new()
    {
        BackgroundGames = false,
        AlwaysClearFocusedMonitor = true
    };
}
