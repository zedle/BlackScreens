using System.ComponentModel;
using BlackScreens.Updates;
using System.Windows;
using System.Windows.Input;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MessageBox = System.Windows.MessageBox;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace BlackScreens.Ui;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly IReadOnlyList<ConnectedMonitor> _connected;
    private readonly UpdateService _updates;
    private readonly Func<bool> _onSaved;
    private readonly SettingsViewModel _model;
    private readonly AppTheme _themeOnOpen;
    private bool _savedOnce;
    private bool _closingConfirmed;

    public SettingsWindow(
        AppSettings settings,
        IReadOnlyList<ConnectedMonitor> connected,
        UpdateService updates,
        Func<bool> onSaved)
    {
        _settings = settings;
        _connected = connected;
        _updates = updates;
        _onSaved = onSaved;
        _model = new SettingsViewModel(settings, connected);
        _themeOnOpen = AppThemes.Parse(settings.Theme);

        // Rescan running programs so denylist icons match what is installed right now.
        ProcessIconProvider.Reset();

        InitializeComponent();

        ShowPage(Nav.SelectedIndex);
        DataContext = _model;
        _model.PropertyChanged += OnModelPropertyChanged;
        _model.ThemeChanged += (_, _) => ApplyTheme(_model.Theme);

        VersionText.Text = $"BlackScreens {AppInfo.Version}";
        SignatureText.Text = AppInfo.SignatureSummary();
        SettingsPathText.Text = $"Settings: {AppSettings.FilePath}";
        ShowPendingUpdate();
        LogPathText.Text = $"Error log: {ErrorLog.FilePath}";
        UpdateStatus();
    }

    private void NavSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) =>
        ShowPage(Nav.SelectedIndex);

    private void ShowPage(int index)
    {
        // SelectedIndex is applied while the XAML is still being loaded, before the page fields exist.
        if (PageGeneral is null)
        {
            return;
        }

        UIElement[] pages = [PageGeneral, PageDetection, PageBlackout, PageMonitors, PageDenylist, PageOnTop, PageAbout];
        for (var i = 0; i < pages.Length; i++)
        {
            pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyWindowChrome(this);
        ThemeManager.DisableMaximize(this);
    }

    private void OnModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SettingsViewModel.IsDirty))
        {
            UpdateStatus();
        }
    }

    private void UpdateStatus() => StatusText.Text = _model.IsDirty
        ? "Unsaved changes"
        : _savedOnce
            ? "Saved"
            : "Blackout is on hold while this window is open.";

    private void ApplyTheme(AppTheme theme)
    {
        ThemeManager.Apply(theme);
        ThemeManager.ApplyWindowChrome(this);
    }

    private void HotkeyBoxPreviewKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl
            or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift
            or Key.LWin or Key.RWin
            or Key.System or Key.None)
        {
            return;
        }

        if (key is Key.Tab or Key.Escape)
        {
            return;
        }

        var modifiers = 0;
        var pressed = Keyboard.Modifiers;
        if (pressed.HasFlag(ModifierKeys.Control))
        {
            modifiers |= AppSettings.ModControl;
        }

        if (pressed.HasFlag(ModifierKeys.Alt))
        {
            modifiers |= AppSettings.ModAlt;
        }

        if (pressed.HasFlag(ModifierKeys.Shift))
        {
            modifiers |= AppSettings.ModShift;
        }

        if (pressed.HasFlag(ModifierKeys.Windows))
        {
            modifiers |= AppSettings.ModWin;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(key);
        if (!_model.SetHotkey(modifiers, virtualKey))
        {
            StatusText.Text = "That shortcut needs Ctrl, Alt or Win.";
        }
    }

    private void DenylistEntryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        _model.AddDenylistEntry();
    }

    private void AddDenylistClick(object sender, RoutedEventArgs e)
    {
        _model.AddDenylistEntry();
        DenylistEntryBox.Focus();
    }

    private void BrowseDenylistClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick a program to keep off the game list",
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(dialog.FileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // Browse picks one exact executable, so add it as a path. Typing a bare name is how you
        // cover every copy of a program.
        ProcessIconProvider.Register(name, dialog.FileName);
        _model.NewDenylistEntry = dialog.FileName;
        _model.AddDenylistEntry();
    }

    private void RemoveDenylistClick(object sender, RoutedEventArgs e) => _model.RemoveSelectedDenylistEntry();

    private void AboveOverlayEntryKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        _model.AddAboveOverlayEntry();
    }

    private void AddAboveOverlayClick(object sender, RoutedEventArgs e)
    {
        _model.AddAboveOverlayEntry();
        AboveOverlayEntryBox.Focus();
    }

    private void BrowseAboveOverlayClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick a program to keep above the overlay",
            Filter = "Programs (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var name = Path.GetFileNameWithoutExtension(dialog.FileName);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        // Browse picks one exact executable, so add it as a path. Typing a bare name is how you
        // cover every copy of a program.
        ProcessIconProvider.Register(name, dialog.FileName);
        _model.NewAboveOverlayEntry = dialog.FileName;
        _model.AddAboveOverlayEntry();
    }

    private void RemoveAboveOverlayClick(object sender, RoutedEventArgs e) =>
        _model.RemoveSelectedAboveOverlayEntry();

    private void RestoreDenylistClick(object sender, RoutedEventArgs e) => _model.RestoreDefaultDenylist();

    private void ConfigureScreensaverClick(object sender, RoutedEventArgs e)
    {
        var path = Screensavers.Choose(_model.ScreensaverPath);
        if (path is null)
        {
            StatusText.Text = "No screensaver is installed or selected in Windows.";
            return;
        }

        try
        {
            // /c is the settings dialog every screensaver implements.
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
            {
                Arguments = "/c",
                UseShellExecute = false
            });
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Screensaver configure failed: {ex}");
            StatusText.Text = "That screensaver has no settings dialog.";
        }
    }

    private void TestScreensaverClick(object sender, RoutedEventArgs e)
    {
        var path = Screensavers.Choose(_model.ScreensaverPath);
        if (path is null)
        {
            StatusText.Text = "No screensaver is installed or selected in Windows.";
            return;
        }

        StatusText.Text = ScreensaverPreview.Flash(path, MonitorsToSkip())
            ? $"Running {Screensavers.Describe(path)} on the covered monitors for six seconds."
            : "Every other monitor is set to stay clear, so there is nothing to preview.";
    }

    /// <summary>The monitors a preview must leave alone: this window's, plus the whitelisted ones.</summary>
    private HashSet<Rectangle> MonitorsToSkip()
    {
        var skip = new HashSet<Rectangle>();

        if (MonitorEnumerator.BoundsForWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle) is { } here)
        {
            skip.Add(here);
        }

        foreach (var choice in _model.Monitors.Where(monitor => monitor.IsWhitelisted))
        {
            var match = _connected.FirstOrDefault(monitor =>
                string.Equals(monitor.DeviceName, choice.DeviceName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                skip.Add(match.Bounds);
            }
        }

        return skip;
    }

    private void IdentifyMonitorsClick(object sender, RoutedEventArgs e) => MonitorIdentifier.Flash();

    private void ShowPendingUpdate()
    {
        if (_updates.Pending is not { } update)
        {
            return;
        }

        InstallUpdateButton.Content = $"Restart and install {update.Version}";
        InstallUpdateButton.Visibility = Visibility.Visible;
        UpdateStatusText.Text = UpdateService.Describe(UpdateResult.Ready, update);
    }

    private async void CheckForUpdatesClick(object sender, RoutedEventArgs e)
    {
        CheckUpdateButton.IsEnabled = false;
        UpdateStatusText.Text = "Checking...";

        try
        {
            var result = await _updates.CheckAsync(manual: true);
            UpdateStatusText.Text = UpdateService.Describe(result, _updates.Pending);
            ShowPendingUpdate();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Manual update check failed: {ex}");
            UpdateStatusText.Text = "The update check did not work. See the error log.";
        }
        finally
        {
            CheckUpdateButton.IsEnabled = true;
        }
    }

    private void InstallUpdateClick(object sender, RoutedEventArgs e)
    {
        if (_model.IsDirty && !Save())
        {
            return;
        }

        UpdateStatusText.Text = "Installing...";
        if (_updates.Apply())
        {
            // Ends the tray app's message loop, which releases the single instance mutex before the
            // replacement build asks for it.
            System.Windows.Forms.Application.Exit();
        }
        else
        {
            UpdateStatusText.Text = "The update could not be installed. See the error log.";
        }
    }

    private void OpenDataFolderClick(object sender, RoutedEventArgs e) => AppInfo.OpenDataFolder();

    private void OpenErrorLogClick(object sender, RoutedEventArgs e) => AppInfo.OpenErrorLog();

    private void SaveClick(object sender, RoutedEventArgs e) => Save();

    private void CloseClick(object sender, RoutedEventArgs e) => Close();

    private bool Save()
    {
        _model.ApplyTo(_settings);
        if (!_onSaved())
        {
            MessageBox.Show(
                this,
                $"Settings could not be written to {AppSettings.FilePath}. See the error log for details.",
                "BlackScreens",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        _model.MarkSaved();
        _savedOnce = true;
        UpdateStatus();
        return true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_closingConfirmed && _model.IsDirty)
        {
            var answer = MessageBox.Show(
                this,
                "Save your changes before closing?",
                "BlackScreens",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);

            switch (answer)
            {
                case MessageBoxResult.Cancel:
                    e.Cancel = true;
                    return;
                case MessageBoxResult.Yes when !Save():
                    e.Cancel = true;
                    return;
                case MessageBoxResult.No:
                    ApplyTheme(_themeOnOpen);
                    break;
            }
        }

        ScreensaverPreview.Stop();
        _closingConfirmed = true;
        base.OnClosing(e);
    }
}
