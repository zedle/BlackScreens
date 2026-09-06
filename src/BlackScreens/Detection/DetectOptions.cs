namespace BlackScreens.Detection;

public sealed record DetectOptions
{
    public bool BackgroundGames { get; init; }

    public bool AlwaysClearFocusedMonitor { get; init; }

    /// <summary>
    /// Programs that hold the blackout up while they have focus, so alt tabbing to one of them does
    /// not uncover the screens. Null or empty means nothing holds it, which is how it behaved before
    /// this existed.
    /// </summary>
    public ProcessRules? HoldsBlackout { get; init; }

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
