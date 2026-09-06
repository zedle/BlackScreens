using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlackScreens;

public sealed class AppSettings
{
    public const int MinPollIntervalMs = 100;
    public const int MaxPollIntervalMs = 2000;
    public const int DefaultPollIntervalMs = 250;

    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int ModShift = 0x0004;
    public const int ModWin = 0x0008;

    public const int DefaultHotkeyModifiers = ModControl | ModAlt;
    public const int DefaultHotkeyVirtualKey = 0x42; // B

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public List<string> WhitelistedDeviceNames { get; set; } = [];

    public List<string> DenylistProcessNames { get; set; } = [];

    public bool BackgroundGames { get; set; }

    public bool? AlwaysClearFocusedMonitor { get; set; }

    public int PollIntervalMs { get; set; } = DefaultPollIntervalMs;

    public bool StartWithWindows { get; set; }

    public string Theme { get; set; } = "System";

    /// <summary>"Black" or "Screensaver". What a blacked out monitor shows.</summary>
    public string Blackout { get; set; } = "Black";

    /// <summary>Screensaver to run. Empty means follow the Windows personalization setting.</summary>
    public string ScreensaverPath { get; set; } = string.Empty;

    /// <summary>
    /// Off by default. When on, BlackScreens asks GitHub once a day whether there is a newer
    /// release and installs it. This is the only time the app touches the network.
    /// </summary>
    public bool AutoUpdate { get; set; }

    public DateTimeOffset? LastUpdateCheckUtc { get; set; }

    public int HotkeyModifiers { get; set; } = DefaultHotkeyModifiers;

    public int HotkeyVirtualKey { get; set; } = DefaultHotkeyVirtualKey;

    public static string DirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BlackScreens");

    public static string FilePath { get; } = Path.Combine(DirectoryPath, "settings.json");

    public bool IsWhitelisted(string deviceName) =>
        WhitelistedDeviceNames.Contains(deviceName, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> GetDenylist() =>
        DenylistProcessNames.Count == 0 ? ProcessDenylist.Defaults : DenylistProcessNames;

    public bool GetAlwaysClearFocusedMonitor() => AlwaysClearFocusedMonitor ?? true;

    public int GetPollIntervalMs() => Math.Clamp(PollIntervalMs, MinPollIntervalMs, MaxPollIntervalMs);

    public BlackoutMode GetBlackoutMode() => BlackoutModes.Parse(Blackout);

    /// <summary>
    /// The screensaver to draw on blacked out monitors, or null for plain black. Null is also the
    /// answer when screensaver mode is on but no usable .scr could be found.
    /// </summary>
    public string? GetScreensaverPath() =>
        GetBlackoutMode() == BlackoutMode.Screensaver ? Screensavers.Choose(ScreensaverPath) : null;

    public DetectOptions ToDetectOptions() => new()
    {
        BackgroundGames = BackgroundGames,
        AlwaysClearFocusedMonitor = GetAlwaysClearFocusedMonitor()
    };

    public void Toggle(string deviceName)
    {
        var existing = WhitelistedDeviceNames.FindIndex(name =>
            string.Equals(name, deviceName, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            WhitelistedDeviceNames.RemoveAt(existing);
            return;
        }

        WhitelistedDeviceNames.Add(deviceName);
    }

    /// <summary>
    /// A global hotkey needs a real key plus at least one of Ctrl, Alt or Win. Shift alone would
    /// swallow ordinary typing.
    /// </summary>
    public static bool IsUsableHotkey(int modifiers, int virtualKey)
    {
        if (virtualKey == 0)
        {
            return false;
        }

        return (modifiers & (ModControl | ModAlt | ModWin)) != 0;
    }

    public static string FormatHotkey(int modifiers, int virtualKey)
    {
        var parts = new List<string>();
        if ((modifiers & ModControl) != 0)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModAlt) != 0)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModShift) != 0)
        {
            parts.Add("Shift");
        }

        if ((modifiers & ModWin) != 0)
        {
            parts.Add("Win");
        }

        parts.Add(virtualKey == 0 ? "None" : FormatKey(virtualKey));
        return string.Join(" + ", parts);
    }

    private static string FormatKey(int virtualKey)
    {
        var key = (Keys)virtualKey;
        return key switch
        {
            >= Keys.D0 and <= Keys.D9 => ((char)('0' + (key - Keys.D0))).ToString(),
            >= Keys.NumPad0 and <= Keys.NumPad9 => $"Num {(char)('0' + (key - Keys.NumPad0))}",
            Keys.Oemtilde => "`",
            Keys.OemMinus => "-",
            Keys.Oemplus => "=",
            Keys.OemOpenBrackets => "[",
            Keys.OemCloseBrackets => "]",
            Keys.OemPipe => "\\",
            Keys.OemSemicolon => ";",
            Keys.OemQuotes => "'",
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemQuestion => "/",
            _ => key.ToString()
        };
    }

    public static string? PickTallThin(IReadOnlyList<ConnectedMonitor> monitors)
    {
        return monitors
            .Where(monitor => monitor.IsPortrait)
            .OrderByDescending(monitor => monitor.Bounds.Height / (double)Math.Max(1, monitor.Bounds.Width))
            .ThenBy(monitor => monitor.Bounds.Width)
            .Select(monitor => monitor.DeviceName)
            .FirstOrDefault();
    }

    /// <summary>Fixes up values that a hand edited or older settings file could hold.</summary>
    public AppSettings Normalize()
    {
        PollIntervalMs = GetPollIntervalMs();

        if (!IsUsableHotkey(HotkeyModifiers, HotkeyVirtualKey))
        {
            HotkeyModifiers = DefaultHotkeyModifiers;
            HotkeyVirtualKey = DefaultHotkeyVirtualKey;
        }

        WhitelistedDeviceNames = WhitelistedDeviceNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        DenylistProcessNames = DenylistProcessNames
            .Select(ProcessDenylist.Normalize)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrWhiteSpace(Theme))
        {
            Theme = "System";
        }

        Blackout = BlackoutModes.Format(GetBlackoutMode());
        ScreensaverPath = ScreensaverPath?.Trim() ?? string.Empty;

        return this;
    }

    public static AppSettings Load(IReadOnlyList<ConnectedMonitor> monitors)
    {
        if (File.Exists(FilePath))
        {
            try
            {
                var json = File.ReadAllText(FilePath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    return loaded.Normalize();
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Settings load failed: {ex}");
            }
        }

        var settings = new AppSettings();
        var tallThin = PickTallThin(monitors);
        if (tallThin is not null)
        {
            settings.WhitelistedDeviceNames.Add(tallThin);
        }

        settings.Save();
        return settings;
    }

    /// <summary>Writes through a temporary file so a crash mid write cannot leave a truncated settings file.</summary>
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            var json = JsonSerializer.Serialize(this, JsonOptions);
            var temporary = FilePath + ".tmp";
            File.WriteAllText(temporary, json);

            if (File.Exists(FilePath))
            {
                File.Replace(temporary, FilePath, destinationBackupFileName: null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(temporary, FilePath);
            }

            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Settings save failed: {ex}");
            return false;
        }
    }
}
