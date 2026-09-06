using System.Diagnostics;

namespace BlackScreens.Updates;

/// <summary>Puts a downloaded update in place. Returns true when the app has to exit for it.</summary>
internal static class UpdateInstaller
{
    private const string BackupSuffix = ".old";

    public static string Folder { get; } = Path.Combine(AppSettings.DirectoryPath, "updates");

    public static string PathFor(UpdateInfo update) => Path.Combine(Folder, update.AssetName);

    public static bool Apply(string downloadedFile, InstallKind kind)
    {
        if (!File.Exists(downloadedFile))
        {
            return false;
        }

        return kind == InstallKind.Installed
            ? RunInstaller(downloadedFile)
            : ReplaceSelf(downloadedFile);
    }

    /// <summary>
    /// The installer stops the running app, swaps the files and, with /RESTART, starts the new build
    /// again. /S keeps it silent.
    /// </summary>
    private static bool RunInstaller(string installer)
    {
        try
        {
            Process.Start(new ProcessStartInfo(installer)
            {
                Arguments = "/S /RESTART",
                UseShellExecute = false
            })?.Dispose();

            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update installer would not start: {ex}");
            return false;
        }
    }

    /// <summary>
    /// Windows lets a running exe be renamed but not overwritten, so the old build is moved aside,
    /// the new one takes its place, and the leftover is deleted on the next start.
    /// </summary>
    private static bool ReplaceSelf(string newExe)
    {
        var current = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(current))
        {
            return false;
        }

        var backup = current + BackupSuffix;

        try
        {
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }

            File.Move(current, backup);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update could not move the running build aside: {ex}");
            return false;
        }

        try
        {
            File.Copy(newExe, current, overwrite: true);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update could not write the new build, rolling back: {ex}");
            TryRollBack(backup, current);
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo(current)
            {
                Arguments = Program.UpdatedArgument,
                UseShellExecute = true
            })?.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Updated build would not start, rolling back: {ex}");
            TryRollBack(backup, current);
            return false;
        }
    }

    private static void TryRollBack(string backup, string current)
    {
        try
        {
            if (File.Exists(current))
            {
                File.Delete(current);
            }

            File.Move(backup, current);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Update rollback failed: {ex}");
        }
    }

    /// <summary>
    /// Deletes the build this one replaced. The new build starts before the old one has finished
    /// exiting, so the first attempt can find the file still locked and this gets tried again later.
    /// </summary>
    public static void RemovePreviousBuild()
    {
        try
        {
            var current = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(current) && File.Exists(current + BackupSuffix))
            {
                File.Delete(current + BackupSuffix);
            }
        }
        catch (IOException)
        {
            // Still running. The next attempt will get it.
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Could not remove the previous build: {ex.Message}");
        }
    }

    /// <summary>Clears the previous build and any downloads left over from an earlier update.</summary>
    public static void CleanUp()
    {
        RemovePreviousBuild();

        try
        {
            if (Directory.Exists(Folder))
            {
                Directory.Delete(Folder, recursive: true);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Could not clear the update folder: {ex.Message}");
        }
    }
}
