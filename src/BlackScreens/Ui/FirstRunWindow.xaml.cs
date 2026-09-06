using System.Windows;

namespace BlackScreens.Ui;

/// <summary>
/// Asked once, the first time BlackScreens runs. Updates stay off unless the answer is yes, so a
/// user who never sees this window never has anything leave their machine.
/// </summary>
public partial class FirstRunWindow : Window
{
    public FirstRunWindow()
    {
        InitializeComponent();
    }

    /// <summary>True when the user asked for automatic updates.</summary>
    public bool AutoUpdateChosen { get; private set; }

    /// <summary>Raised once the user has answered, whichever way.</summary>
    public event EventHandler? Answered;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeManager.ApplyWindowChrome(this);
    }

    private void AcceptClick(object sender, RoutedEventArgs e) => Answer(true);

    private void DeclineClick(object sender, RoutedEventArgs e) => Answer(false);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);

        // Closing the window with the title bar counts as no, and still counts as answered so the
        // question is not asked again.
        Answered?.Invoke(this, EventArgs.Empty);
    }

    private void Answer(bool autoUpdate)
    {
        AutoUpdateChosen = autoUpdate;
        Close();
    }
}
