namespace BlackScreens;

/// <summary>Last line of defence. Anything that escapes a handler is logged and shown once.</summary>
internal static class CrashHandler
{
    private static bool _reported;

    public static void Install()
    {
        Application.ThreadException += (_, e) => Report(e.Exception, fatal: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Report(e.ExceptionObject as Exception, fatal: e.IsTerminating);
    }

    public static void Report(Exception? exception, bool fatal)
    {
        ErrorLog.Write($"Unhandled exception (fatal: {fatal}): {exception}");

        if (_reported)
        {
            return;
        }

        _reported = true;

        try
        {
            MessageBox.Show(
                $"BlackScreens hit an unexpected error.{Environment.NewLine}{Environment.NewLine}"
                + $"{exception?.Message}{Environment.NewLine}{Environment.NewLine}"
                + $"Details were written to{Environment.NewLine}{ErrorLog.FilePath}",
                "BlackScreens",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
