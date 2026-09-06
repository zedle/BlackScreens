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

    private readonly ProcessRules _rules;

    public ProcessDenylist()
        : this(Defaults)
    {
    }

    public ProcessDenylist(IEnumerable<string> names)
    {
        _rules = new ProcessRules(names);
    }

    /// <summary>
    /// Whether this process is denied. An entry can be a bare name, matching every copy, or the full
    /// path to one executable, so <paramref name="processPath"/> is worth passing when it is known.
    /// </summary>
    public bool Contains(string processName, string? processPath = null) =>
        _rules.Matches(processName, processPath);

    /// <summary>Trims a user typed entry. Kept for callers that predate <see cref="ProcessRules"/>.</summary>
    public static string Normalize(string? entry) => ProcessRules.Normalize(entry);
}
