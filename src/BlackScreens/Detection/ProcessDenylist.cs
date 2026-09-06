namespace BlackScreens.Detection;

public sealed class ProcessDenylist
{
    public static IReadOnlyList<string> Defaults { get; } =
    [
        "explorer", "SearchHost", "ShellExperienceHost", "ApplicationFrameHost",
        "TextInputHost", "dwm", "LockApp", "StartMenuExperienceHost", "SearchApp",
        "ShellHost", "GameBar", "XboxGameBar", "Widgets", "WidgetBoard",
        "SystemSettings", "Taskmgr", "chrome", "msedge", "firefox", "brave",
        "opera", "discord", "slack", "spotify", "Code", "devenv", "WindowsTerminal",
        "vlc", "mpc-hc64", "NVIDIA Share"
    ];

    private readonly HashSet<string> _names;

    public ProcessDenylist()
        : this(Defaults)
    {
    }

    public ProcessDenylist(IEnumerable<string> names)
    {
        _names = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    public bool Contains(string processName) => _names.Contains(processName);

    /// <summary>Trims a user typed entry down to a bare process name.</summary>
    public static string Normalize(string? entry)
    {
        var name = entry?.Trim() ?? string.Empty;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^4].Trim();
        }

        return name;
    }
}
