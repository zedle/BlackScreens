using System.Globalization;

namespace BlackScreens.App;

public static class ErrorLog
{
    private const long MaxBytes = 512 * 1024;

    private static readonly object Gate = new();

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BlackScreens",
        "error.log");

    /// <summary>Raised on the writing thread after a message is logged. Used to surface failures in the tray.</summary>
    public static event EventHandler<string>? Logged;

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(FilePath);
                if (directory is not null)
                {
                    Directory.CreateDirectory(directory);
                }

                Rotate();

                var line = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}");
                File.AppendAllText(FilePath, line);
            }
        }
        catch
        {
            // Logging must never take the app down.
        }

        try
        {
            Logged?.Invoke(null, message);
        }
        catch
        {
        }
    }

    /// <summary>Keeps one previous log around so a long running session cannot fill the disk.</summary>
    private static void Rotate()
    {
        try
        {
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length < MaxBytes)
            {
                return;
            }

            var previous = FilePath + ".1";
            if (File.Exists(previous))
            {
                File.Delete(previous);
            }

            File.Move(FilePath, previous);
        }
        catch
        {
        }
    }
}
