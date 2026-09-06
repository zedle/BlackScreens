using Microsoft.Win32;

namespace BlackScreens;

internal static class StartupRegistration
{
    private const string ValueName = "BlackScreens";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
            if (key is null)
            {
                return;
            }

            if (enabled)
            {
                var path = Environment.ProcessPath;
                if (!string.IsNullOrWhiteSpace(path))
                {
                    key.SetValue(ValueName, $"\"{path}\"");
                }

                return;
            }

            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Startup registration failed: {ex}");
        }
    }
}
