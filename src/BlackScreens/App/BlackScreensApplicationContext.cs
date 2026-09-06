using BlackScreens.Ui;
using BlackScreens.Updates;
using Microsoft.Win32;

namespace BlackScreens.App;

internal sealed class BlackScreensApplicationContext : ApplicationContext
{
    private readonly OverlayManager _overlays = new();
    private readonly AlwaysOnTop _onTop = new();
    private nint _lastForeground;
    private readonly SingleInstance _instance;
    private readonly AppSettings _settings;
    private readonly NotifyIcon _icon;
    private readonly ToolStripMenuItem _pauseItem;
    private readonly ToolStripMenuItem _whitelistMenu;
    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly System.Windows.Forms.Timer _updateTimer;
    private readonly UpdateService _updates;
    private readonly HotkeyWindow _hotkey;
    private readonly Control _sync = new();
    private IReadOnlyList<ConnectedMonitor> _monitors;
    private GameDetector _detector;
    private string? _screensaverPath;
    private SettingsWindow? _settingsWindow;
    private FirstRunWindow? _firstRunWindow;
    private bool _paused;
    private bool _detectorFailing;
    private bool _rebuild;
    private bool _cleaned;

    public BlackScreensApplicationContext(SingleInstance instance)
    {
        _instance = instance;
        _ = _sync.Handle;

        UpdateService.CleanUp();

        _monitors = MonitorEnumerator.CaptureAll();
        _settings = AppSettings.Load(_monitors);
        _updates = new UpdateService(_settings);
        _detector = CreateDetector();
        _screensaverPath = _settings.GetScreensaverPath();

        ThemeManager.Apply(AppThemes.Parse(_settings.Theme));

        _pauseItem = new ToolStripMenuItem("Pause", null, (_, _) => TogglePause())
        {
            CheckOnClick = false,
            Checked = false,
            ShortcutKeyDisplayString = AppSettings.FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey)
        };
        _whitelistMenu = new ToolStripMenuItem("Keep monitor clear");
        _whitelistMenu.DropDownOpening += (_, _) =>
        {
            _monitors = MonitorEnumerator.CaptureAll();
            RebuildWhitelistMenu();
        };
        RebuildWhitelistMenu();

        _menu = new ContextMenuStrip { ShowImageMargin = true };
        _menu.Items.Add(_pauseItem);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Settings...", null, (_, _) => OpenSettings()));
        _menu.Items.Add(_whitelistMenu);
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add(new ToolStripMenuItem("Quit", null, (_, _) => Quit()));
        TrayMenuTheme.Apply(_menu, ThemeManager.Current);

        _icon = new NotifyIcon
        {
            Text = "BlackScreens",
            Icon = AppIcon.LoadTray(),
            Visible = true,
            ContextMenuStrip = _menu
        };
        _icon.DoubleClick += (_, _) => OpenSettings();

        _hotkey = new HotkeyWindow(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        _hotkey.Pressed += (_, _) => TogglePause();
        if (!_hotkey.Registered)
        {
            Notify("Another app already owns that shortcut. Use the tray menu to pause.");
        }

        _timer = new System.Windows.Forms.Timer
        {
            Interval = _settings.GetPollIntervalMs()
        };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        // First look shortly after start, then every half hour the service decides whether a day
        // has passed. Nothing leaves the machine while automatic updates are off.
        _updateTimer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _updateTimer.Tick += async (_, _) => await PollForUpdateAsync().ConfigureAwait(true);
        _updateTimer.Start();

        StartupRegistration.Apply(_settings.StartWithWindows);
        UpdateTrayState();

        AskAboutUpdatesOnce();

        _instance.OnActivationRequested(() => Post(OpenSettings));
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.SessionEnding += OnSessionEnding;
        Application.ApplicationExit += (_, _) => Cleanup();
    }

    /// <summary>Re-resolves the palette and repaints the tray menu with it.</summary>
    private void ApplyThemePreference()
    {
        ThemeManager.Apply(AppThemes.Parse(_settings.Theme));
        TrayMenuTheme.Apply(_menu, ThemeManager.Current);
    }

    private GameDetector CreateDetector() =>
        new(new ProcessDenylist(_settings.GetDenylist()), _settings.ToDetectOptions());

    /// <summary>Persists the current settings and pushes them into the running detector.</summary>
    private bool SaveAndApplySettings()
    {
        if (!_settings.Save())
        {
            return false;
        }

        _detector = CreateDetector();
        _screensaverPath = _settings.GetScreensaverPath();
        _timer.Interval = _settings.GetPollIntervalMs();

        if (!_hotkey.Rebind(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey))
        {
            Notify("Another app already owns that shortcut. Use the tray menu to pause.");
        }

        StartupRegistration.Apply(_settings.StartWithWindows);
        ApplyThemePreference();
        _monitors = MonitorEnumerator.CaptureAll();
        RebuildWhitelistMenu();
        UpdateTrayState();
        _rebuild = true;
        return true;
    }

    private void OpenSettings()
    {
        try
        {
            if (_settingsWindow is { } existing)
            {
                if (existing.WindowState == System.Windows.WindowState.Minimized)
                {
                    existing.WindowState = System.Windows.WindowState.Normal;
                }

                existing.Activate();
                return;
            }

            // Overlays are topmost, so they would sit on top of the settings window. Stand them down
            // while the window is open and pick the blackout back up on close.
            _overlays.HideAll();
            _onTop.ReleaseAll();

            _monitors = MonitorEnumerator.CaptureAll();
            var window = new SettingsWindow(_settings, _monitors, _updates, SaveAndApplySettings);
            window.Closed += (_, _) =>
            {
                _settingsWindow = null;
                ApplyThemePreference();
                _rebuild = true;
            };

            _settingsWindow = window;
            ShowWpfWindow(window);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Opening settings failed: {ex}");
            Notify("Settings could not be opened. See the error log.");
        }
    }

    /// <summary>
    /// Shows a WPF window from this WinForms application context.
    /// </summary>
    /// <remarks>
    /// The message loop here is WinForms', and it does not know how to hand a keystroke to a
    /// modeless WPF window: the window paints, and the mouse works, but nothing typed ever arrives.
    /// EnableModelessKeyboardInterop adds the message filter that forwards keyboard messages to the
    /// window's HwndSource, which is what makes text boxes, tab order and access keys work at all.
    /// </remarks>
    private static void ShowWpfWindow(System.Windows.Window window)
    {
        System.Windows.Forms.Integration.ElementHost.EnableModelessKeyboardInterop(window);
        window.Show();
        window.Activate();
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Post(() =>
    {
        _monitors = MonitorEnumerator.CaptureAll();
        _rebuild = true;
    });

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category is not (UserPreferenceCategory.General or UserPreferenceCategory.Color))
        {
            return;
        }

        Post(ApplyThemePreference);
    }

    private void OnSessionEnding(object? sender, SessionEndingEventArgs e) => Cleanup();

    /// <summary>
    /// Puts the update question in front of the user once. Saying nothing leaves updates off, which
    /// is also what closing the window does.
    /// </summary>
    private void AskAboutUpdatesOnce()
    {
        if (_settings.AskedAboutUpdates)
        {
            return;
        }

        try
        {
            var window = new FirstRunWindow();
            _firstRunWindow = window;
            window.Answered += (_, _) =>
            {
                _firstRunWindow = null;
                _settings.AskedAboutUpdates = true;
                _settings.AutoUpdate = window.AutoUpdateChosen;
                _settings.Save();
            };

            ShowWpfWindow(window);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"First run question failed: {ex}");
            _firstRunWindow = null;
            _settings.AskedAboutUpdates = true;
            _settings.Save();
        }
    }

    /// <summary>Checks for a new release, and installs it when nothing is in the way.</summary>
    private async Task PollForUpdateAsync()
    {
        _updateTimer.Interval = 30 * 60_000;

        // The build this one replaced is usually still locked at startup, so try again now.
        UpdateService.RemovePreviousBuild();

        if (_cleaned || !_settings.AutoUpdate)
        {
            return;
        }

        try
        {
            if (_updates.Pending is null && await _updates.CheckAsync(manual: false).ConfigureAwait(true)
                != UpdateResult.Ready)
            {
                return;
            }

            InstallPendingUpdateIfIdle();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update poll failed: {ex}");
        }
    }

    /// <summary>
    /// Restarting mid game would be the worst possible moment, so an update waits until no monitor
    /// is blacked out and the settings window is closed.
    /// </summary>
    private void InstallPendingUpdateIfIdle()
    {
        if (_updates.Pending is not { } update || _overlays.Count > 0 || _settingsWindow is not null)
        {
            return;
        }

        Notify($"Updating BlackScreens to {update.Version}.", ToolTipIcon.Info);

        if (_updates.Apply())
        {
            Quit();
        }
    }

    private void Post(Action action)
    {
        try
        {
            if (_sync.IsDisposed)
            {
                return;
            }

            _sync.BeginInvoke(action);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Marshal to UI thread failed: {ex}");
        }
    }

    private void Tick()
    {
        try
        {
            if (_rebuild)
            {
                _overlays.HideAll();
                _onTop.ReleaseAll();
                _rebuild = false;
            }

            if (_paused || _settingsWindow is not null)
            {
                _overlays.HideAll();
                _onTop.ReleaseAll();
                return;
            }

            ScanResult result;
            try
            {
                result = _detector.Decide(WindowEnumerator.Capture());
                _detectorFailing = false;
            }
            catch (Exception ex)
            {
                if (!_detectorFailing)
                {
                    ErrorLog.Write($"Detector failed: {ex}");
                    _detectorFailing = true;
                }

                return;
            }

            var connected = _monitors;
            var monitors = connected.Select(monitor => monitor.Bounds).ToArray();
            result = MonitorGeometry.Canonicalize(result, monitors);
            var whitelist = connected
                .Where(monitor => _settings.IsWhitelisted(monitor.DeviceName))
                .Select(monitor => monitor.Bounds)
                .ToArray();
            var black = result.BlackMonitors(monitors, whitelist);

            // Only push the overlays back to the front when something could have pushed them down.
            // A game taking over the screen changes the foreground, and doing it on every poll makes
            // any window held above them flicker, because it is covered and uncovered each time.
            var foreground = NativeMethods.GetForegroundWindow();
            var foregroundMoved = foreground != _lastForeground;
            _lastForeground = foreground;

            _overlays.Apply(black, _screensaverPath, _settings.GetOverlayOpacity(), foregroundMoved);

            if (black.Count > 0)
            {
                _onTop.Apply(_settings.GetAboveOverlay(), _overlays.Handles);
            }
            else
            {
                _onTop.ReleaseAll();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Tick failed: {ex}");
        }
    }

    private void RebuildWhitelistMenu()
    {
        _whitelistMenu.DropDownItems.Clear();
        foreach (var monitor in _monitors)
        {
            var item = new ToolStripMenuItem(MonitorLabel(monitor))
            {
                Checked = _settings.IsWhitelisted(monitor.DeviceName),
                CheckOnClick = true,
                Tag = monitor.DeviceName
            };
            item.Click += (_, _) => ToggleWhitelist(monitor.DeviceName);
            _whitelistMenu.DropDownItems.Add(item);
        }

        if (_whitelistMenu.DropDownItems.Count == 0)
        {
            _whitelistMenu.DropDownItems.Add(new ToolStripMenuItem("No monitors found") { Enabled = false });
        }

        TrayMenuTheme.Refresh(_whitelistMenu, ThemeManager.Current);
    }

    private void ToggleWhitelist(string deviceName)
    {
        _settings.Toggle(deviceName);
        if (!_settings.Save())
        {
            Notify("Settings could not be saved. See the error log.");
        }

        _rebuild = true;
    }

    private static string MonitorLabel(ConnectedMonitor monitor)
    {
        var shape = monitor.IsPortrait ? "portrait" : "landscape";
        var primary = monitor.IsPrimary ? ", primary" : string.Empty;
        return $"{MonitorChoice.FriendlyName(monitor.DeviceName)}  {monitor.Bounds.Width} x {monitor.Bounds.Height} {shape}{primary}";
    }

    private void TogglePause()
    {
        _paused = !_paused;
        if (_paused)
        {
            _overlays.HideAll();
            _onTop.ReleaseAll();
        }

        UpdateTrayState();
    }

    private void UpdateTrayState()
    {
        _pauseItem.Checked = _paused;
        _pauseItem.ShortcutKeyDisplayString =
            AppSettings.FormatHotkey(_settings.HotkeyModifiers, _settings.HotkeyVirtualKey);
        // NotifyIcon.Text is capped at 63 characters by the shell.
        _icon.Text = _paused ? "BlackScreens - paused" : "BlackScreens - watching for games";
    }

    private void Notify(string message, ToolTipIcon icon = ToolTipIcon.Warning)
    {
        try
        {
            _icon.BalloonTipTitle = "BlackScreens";
            _icon.BalloonTipText = message;
            _icon.BalloonTipIcon = icon;
            _icon.ShowBalloonTip(5000);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Balloon failed: {ex}");
        }
    }

    private void Quit()
    {
        Cleanup();
        ExitThread();
    }

    private void Cleanup()
    {
        if (_cleaned)
        {
            return;
        }

        _cleaned = true;

        Try(() => SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged);
        Try(() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged);
        Try(() => SystemEvents.SessionEnding -= OnSessionEnding);

        Try(() =>
        {
            _timer.Stop();
            _timer.Dispose();
            _updateTimer.Stop();
            _updateTimer.Dispose();
        });

        Try(() =>
        {
            if (_settingsWindow is { } window)
            {
                _settingsWindow = null;
                window.Close();
            }
        });

        Try(() =>
        {
            if (_firstRunWindow is { } prompt)
            {
                _firstRunWindow = null;
                prompt.Close();
            }
        });

        // Nothing may be left pinned in front of everything else once the app is gone.
        Try(_onTop.ReleaseAll);
        Try(_overlays.Dispose);

        Try(() =>
        {
            _icon.Visible = false;
            _icon.Dispose();
        });

        Try(_hotkey.Dispose);
        Try(_sync.Dispose);
    }

    private static void Try(Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Shutdown step failed: {ex}");
        }
    }
}
