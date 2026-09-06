namespace BlackScreens.Detection;

/// <summary>
/// Recognises the windows the shell puts up while you are switching between programs: the Alt Tab
/// switcher and Task View.
/// </summary>
/// <remarks>
/// These take the foreground while they are on screen, which used to end a blackout: the game was no
/// longer in front, so nothing was blacked out, and the screens lit up behind the switcher you were
/// looking at. Holding Alt Tab was enough to do it.
///
/// They are not a real change of foreground. Nobody is using the window behind them yet, and one is
/// about to be chosen, so a scan that sees one of these leaves the blackout exactly as it was and
/// waits for the choice.
///
/// Matched by window class rather than by title, because the titles are translated, and rather than
/// by process, because explorer.exe owns the taskbar and the desktop as well.
/// </remarks>
public static class TaskSwitcher
{
    private static readonly HashSet<string> Classes = new(StringComparer.OrdinalIgnoreCase)
    {
        // Windows 11 Alt Tab and Task View, both XAML islands hosted by explorer.
        "XamlExplorerHostIslandWindow",

        // Windows 10 and earlier.
        "MultitaskingViewFrame",
        "TaskSwitcherWnd",
        "TaskSwitcherOverlayWnd"
    };

    public static bool Is(string? className) =>
        !string.IsNullOrEmpty(className) && Classes.Contains(className);

    /// <summary>Whether the switcher is the window currently in front.</summary>
    public static bool IsShowing(IReadOnlyList<WindowSnapshot> windows)
    {
        foreach (var window in windows)
        {
            if (window.IsForeground && Is(window.ClassName))
            {
                return true;
            }
        }

        return false;
    }
}
