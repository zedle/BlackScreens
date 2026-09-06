namespace BlackScreens;

internal static class Program
{
    /// <summary>Passed to the new build when an update restarts the app.</summary>
    internal const string UpdatedArgument = "--updated";

    [STAThread]
    private static void Main(string[] args)
    {
        var restarted = args.Contains(UpdatedArgument, StringComparer.OrdinalIgnoreCase);
        using var instance = new SingleInstance(restarted ? TimeSpan.FromSeconds(20) : TimeSpan.Zero);
        if (!instance.IsFirst)
        {
            // Launching again is the natural way to ask for the settings window.
            instance.SignalRunningInstance();
            return;
        }

        // DPI awareness comes from app.manifest so both WinForms overlays and the WPF window agree.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);

        CrashHandler.Install();

        try
        {
            Application.Run(new BlackScreensApplicationContext(instance));
        }
        catch (Exception ex)
        {
            CrashHandler.Report(ex, fatal: true);
        }
    }
}
