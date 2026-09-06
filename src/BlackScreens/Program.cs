namespace BlackScreens;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var instance = new SingleInstance();
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
