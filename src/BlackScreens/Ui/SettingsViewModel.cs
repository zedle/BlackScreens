using System.Collections.ObjectModel;
using System.Drawing;
using System.Globalization;

namespace BlackScreens.Ui;

/// <summary>One row on the Monitors page.</summary>
public sealed class MonitorChoice : ObservableObject
{
    private bool _isWhitelisted;

    public MonitorChoice(ConnectedMonitor monitor, bool isWhitelisted)
    {
        DeviceName = monitor.DeviceName;
        Title = Headline(monitor);
        Detail = Describe(monitor);
        Bounds = monitor.Bounds;
        Number = FriendlyName(monitor.DeviceName).Replace("Display ", string.Empty, StringComparison.Ordinal);
        Size = string.Create(CultureInfo.InvariantCulture, $"{monitor.Bounds.Width} x {monitor.Bounds.Height}");
        _isWhitelisted = isWhitelisted;
    }

    public string DeviceName { get; }

    public string Title { get; }

    public string Detail { get; }

    /// <summary>Where the monitor sits on the desktop, which is also where it sits on the map.</summary>
    public Rectangle Bounds { get; }

    /// <summary>Just the number, the one the Identify monitors button flashes on screen.</summary>
    public string Number { get; }

    public string Size { get; }

    public double MapLeft { get; private set; }

    public double MapTop { get; private set; }

    public double MapWidth { get; private set; }

    public double MapHeight { get; private set; }

    public double MapCorner { get; private set; }

    public double MapStroke { get; private set; }

    public double NumberFontSize { get; private set; }

    public double SizeFontSize { get; private set; }

    /// <summary>Whether the tile is wide enough to print the resolution across it.</summary>
    public bool SizeFits { get; private set; }

    /// <summary>
    /// Shifts the monitor into a map whose origin is the top left of <paramref name="union"/>, and
    /// works out the tile's trimmings.
    /// </summary>
    /// <remarks>
    /// The text and the stroke are sized against the whole desktop rather than against this one
    /// monitor. The window scales the finished map down to fit, by roughly the height of the map
    /// area over the height of the desktop, so sizing against the desktop is what makes a number
    /// come out the same number of pixels whether the desktop is 1080 tall or 4000.
    /// </remarks>
    public void PlaceOnMap(Rectangle union)
    {
        var span = Math.Max(1, union.Height);

        // A small gap around each monitor, so two that touch still read as two.
        var inset = span * 0.006;

        MapLeft = Bounds.X - union.X + inset;
        MapTop = Bounds.Y - union.Y + inset;
        MapWidth = Math.Max(1, Bounds.Width - (2 * inset));
        MapHeight = Math.Max(1, Bounds.Height - (2 * inset));

        MapCorner = span * 0.012;
        MapStroke = span * 0.005;
        NumberFontSize = span * 0.13;
        SizeFontSize = span * 0.05;

        // A tall narrow monitor has no room for "682 x 2560" across it, and a clipped label looks
        // like a bug. Digits in this font run to about half an em, so this is close enough to tell
        // the difference between fits and does not.
        SizeFits = MapWidth > SizeFontSize * 0.55 * Size.Length * 1.1;

        Raise(nameof(MapLeft));
        Raise(nameof(MapTop));
        Raise(nameof(MapWidth));
        Raise(nameof(MapHeight));
    }

    public bool IsWhitelisted
    {
        get => _isWhitelisted;
        set => Set(ref _isWhitelisted, value);
    }

    /// <summary>
    /// The line that identifies the row. The display number comes first, because that is what the
    /// Identify monitors button flashes on screen, followed by the make and model when the panel
    /// reports one.
    /// </summary>
    public static string Headline(ConnectedMonitor monitor)
    {
        var display = FriendlyName(monitor.DeviceName);
        return string.IsNullOrWhiteSpace(monitor.HardwareName)
            ? display
            : $"{display}  ·  {monitor.HardwareName}";
    }

    /// <summary>Turns <c>\\.\DISPLAY2</c> into <c>Display 2</c>.</summary>
    public static string FriendlyName(string deviceName)
    {
        var trimmed = deviceName.Replace(@"\\.\", string.Empty, StringComparison.Ordinal);
        if (trimmed.StartsWith("DISPLAY", StringComparison.OrdinalIgnoreCase))
        {
            var number = trimmed[7..];
            if (number.Length > 0)
            {
                return $"Display {number}";
            }
        }

        return trimmed.Length == 0 ? deviceName : trimmed;
    }

    public static string Describe(ConnectedMonitor monitor)
    {
        var shape = monitor.IsPortrait ? "portrait" : "landscape";
        var primary = monitor.IsPrimary ? ", primary" : string.Empty;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{monitor.Bounds.Width} x {monitor.Bounds.Height} {shape}{primary}  at {monitor.Bounds.X}, {monitor.Bounds.Y}");
    }
}

/// <summary>Backs the settings window. Holds an editable copy until Save is pressed.</summary>
public sealed class SettingsViewModel : ObservableObject
{
    private bool _startWithWindows;
    private bool _autoUpdate;
    private AppTheme _theme;
    private bool _backgroundGames;
    private bool _alwaysClearFocusedMonitor;
    private int _pollIntervalMs;
    private int _hotkeyModifiers;
    private int _hotkeyVirtualKey;
    private BlackoutMode _blackout;
    private Screensaver _selectedScreensaver;
    private string _newDenylistEntry = string.Empty;
    private string? _selectedDenylistEntry;
    private bool _isDirty;

    /// <summary>The picker entry that defers to the screensaver chosen in Windows.</summary>
    public static Screensaver FollowWindows { get; } = new(string.Empty, "Follow the Windows setting");

    public SettingsViewModel(
        AppSettings settings,
        IReadOnlyList<ConnectedMonitor> connected,
        IReadOnlyList<Screensaver>? screensavers = null)
    {
        _startWithWindows = settings.StartWithWindows;
        _autoUpdate = settings.AutoUpdate;
        _theme = AppThemes.Parse(settings.Theme);
        _backgroundGames = settings.BackgroundGames;
        _alwaysClearFocusedMonitor = settings.GetAlwaysClearFocusedMonitor();
        _pollIntervalMs = Math.Clamp(settings.PollIntervalMs, AppSettings.MinPollIntervalMs, AppSettings.MaxPollIntervalMs);
        _hotkeyModifiers = settings.HotkeyModifiers;
        _hotkeyVirtualKey = settings.HotkeyVirtualKey;
        _blackout = settings.GetBlackoutMode();

        ScreensaverOptions = [FollowWindows, .. screensavers ?? Screensavers.Installed()];
        _selectedScreensaver = ScreensaverOptions.FirstOrDefault(
            option => option.Path.Length > 0
                && string.Equals(option.Path, settings.ScreensaverPath, StringComparison.OrdinalIgnoreCase))
            ?? FollowWindows;

        Monitors = new ObservableCollection<MonitorChoice>(
            connected.Select(monitor => new MonitorChoice(monitor, settings.IsWhitelisted(monitor.DeviceName))));

        // The map is drawn in desktop coordinates and scaled to fit by the window, so the monitors
        // only need shifting so the top left of the desktop is the origin.
        var union = MonitorLayout.Union(connected);
        MapWidth = union.Width;
        MapHeight = union.Height;
        foreach (var monitor in Monitors)
        {
            monitor.PlaceOnMap(union);
            monitor.PropertyChanged += (_, _) => IsDirty = true;
        }

        Denylist = new ObservableCollection<string>(
            settings.GetDenylist().OrderBy(name => name, StringComparer.OrdinalIgnoreCase));
    }

    public ObservableCollection<MonitorChoice> Monitors { get; }

    public ObservableCollection<string> Denylist { get; }

    public ObservableCollection<Screensaver> ScreensaverOptions { get; }

    public bool BlackoutIsBlack
    {
        get => _blackout == BlackoutMode.Black;
        set
        {
            if (value)
            {
                Blackout = BlackoutMode.Black;
            }
        }
    }

    public bool BlackoutIsScreensaver
    {
        get => _blackout == BlackoutMode.Screensaver;
        set
        {
            if (value)
            {
                Blackout = BlackoutMode.Screensaver;
            }
        }
    }

    public BlackoutMode Blackout
    {
        get => _blackout;
        set
        {
            if (Track(ref _blackout, value))
            {
                Raise(nameof(BlackoutIsBlack));
                Raise(nameof(BlackoutIsScreensaver));
            }
        }
    }

    public Screensaver SelectedScreensaver
    {
        get => _selectedScreensaver;
        set
        {
            if (value is null)
            {
                return;
            }

            if (Track(ref _selectedScreensaver, value))
            {
                Raise(nameof(ScreensaverPath));
            }
        }
    }

    /// <summary>The saver the user picked, or empty when Windows decides.</summary>
    public string ScreensaverPath => _selectedScreensaver.Path;

    public bool HasMonitors => Monitors.Count > 0;

    /// <summary>The size of the whole desktop, which is the coordinate space the map is drawn in.</summary>
    public double MapWidth { get; }

    public double MapHeight { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set => Set(ref _isDirty, value);
    }

    public bool StartWithWindows
    {
        get => _startWithWindows;
        set => Track(ref _startWithWindows, value);
    }

    public bool AutoUpdate
    {
        get => _autoUpdate;
        set => Track(ref _autoUpdate, value);
    }

    public bool BackgroundGames
    {
        get => _backgroundGames;
        set => Track(ref _backgroundGames, value);
    }

    public bool AlwaysClearFocusedMonitor
    {
        get => _alwaysClearFocusedMonitor;
        set => Track(ref _alwaysClearFocusedMonitor, value);
    }

    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set
        {
            var clamped = Math.Clamp(value, AppSettings.MinPollIntervalMs, AppSettings.MaxPollIntervalMs);
            if (Track(ref _pollIntervalMs, clamped))
            {
                Raise(nameof(PollIntervalText));
            }
        }
    }

    public string PollIntervalText => string.Create(CultureInfo.InvariantCulture, $"{_pollIntervalMs} ms");

    public AppTheme Theme
    {
        get => _theme;
        set
        {
            if (Track(ref _theme, value))
            {
                Raise(nameof(ThemeIsSystem));
                Raise(nameof(ThemeIsDark));
                Raise(nameof(ThemeIsLight));
                ThemeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? ThemeChanged;

    public bool ThemeIsSystem
    {
        get => _theme == AppTheme.System;
        set
        {
            if (value)
            {
                Theme = AppTheme.System;
            }
        }
    }

    public bool ThemeIsDark
    {
        get => _theme == AppTheme.Dark;
        set
        {
            if (value)
            {
                Theme = AppTheme.Dark;
            }
        }
    }

    public bool ThemeIsLight
    {
        get => _theme == AppTheme.Light;
        set
        {
            if (value)
            {
                Theme = AppTheme.Light;
            }
        }
    }

    public int HotkeyModifiers => _hotkeyModifiers;

    public int HotkeyVirtualKey => _hotkeyVirtualKey;

    public string HotkeyText => AppSettings.FormatHotkey(_hotkeyModifiers, _hotkeyVirtualKey);

    public string NewDenylistEntry
    {
        get => _newDenylistEntry;
        set
        {
            if (Set(ref _newDenylistEntry, value))
            {
                Raise(nameof(CanAddDenylistEntry));
            }
        }
    }

    public bool CanAddDenylistEntry => ProcessDenylist.Normalize(_newDenylistEntry).Length > 0;

    public string? SelectedDenylistEntry
    {
        get => _selectedDenylistEntry;
        set
        {
            if (Set(ref _selectedDenylistEntry, value))
            {
                Raise(nameof(CanRemoveDenylistEntry));
            }
        }
    }

    public bool CanRemoveDenylistEntry => _selectedDenylistEntry is not null;

    /// <summary>Accepts a shortcut. Returns false when the combination is not usable as a global hotkey.</summary>
    public bool SetHotkey(int modifiers, int virtualKey)
    {
        if (!AppSettings.IsUsableHotkey(modifiers, virtualKey))
        {
            return false;
        }

        _hotkeyModifiers = modifiers;
        _hotkeyVirtualKey = virtualKey;
        IsDirty = true;
        Raise(nameof(HotkeyText));
        return true;
    }

    public bool AddDenylistEntry()
    {
        var name = ProcessDenylist.Normalize(NewDenylistEntry);
        if (name.Length == 0)
        {
            return false;
        }

        if (Denylist.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewDenylistEntry = string.Empty;
            SelectedDenylistEntry = Denylist.First(existing =>
                string.Equals(existing, name, StringComparison.OrdinalIgnoreCase));
            return false;
        }

        var index = 0;
        while (index < Denylist.Count
            && string.Compare(Denylist[index], name, StringComparison.OrdinalIgnoreCase) < 0)
        {
            index++;
        }

        Denylist.Insert(index, name);
        NewDenylistEntry = string.Empty;
        SelectedDenylistEntry = name;
        IsDirty = true;
        return true;
    }

    public bool RemoveSelectedDenylistEntry()
    {
        if (_selectedDenylistEntry is not { } selected)
        {
            return false;
        }

        var removed = Denylist.Remove(selected);
        if (removed)
        {
            SelectedDenylistEntry = null;
            IsDirty = true;
        }

        return removed;
    }

    public void RestoreDefaultDenylist()
    {
        Denylist.Clear();
        foreach (var name in ProcessDenylist.Defaults.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
        {
            Denylist.Add(name);
        }

        SelectedDenylistEntry = null;
        IsDirty = true;
    }

    /// <summary>Copies the edited values onto the live settings object.</summary>
    public void ApplyTo(AppSettings settings)
    {
        settings.StartWithWindows = StartWithWindows;
        settings.AutoUpdate = AutoUpdate;
        settings.Theme = AppThemes.Format(Theme);
        settings.BackgroundGames = BackgroundGames;
        settings.AlwaysClearFocusedMonitor = AlwaysClearFocusedMonitor;
        settings.PollIntervalMs = PollIntervalMs;
        settings.HotkeyModifiers = HotkeyModifiers;
        settings.HotkeyVirtualKey = HotkeyVirtualKey;
        settings.WhitelistedDeviceNames = Monitors
            .Where(monitor => monitor.IsWhitelisted)
            .Select(monitor => monitor.DeviceName)
            .ToList();
        settings.DenylistProcessNames = Denylist.ToList();
        settings.Blackout = BlackoutModes.Format(Blackout);
        settings.ScreensaverPath = ScreensaverPath;
    }

    public void MarkSaved() => IsDirty = false;

    private bool Track<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (!Set(ref field, value, propertyName))
        {
            return false;
        }

        IsDirty = true;
        return true;
    }
}
