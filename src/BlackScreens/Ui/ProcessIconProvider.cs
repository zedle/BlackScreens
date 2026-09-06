using System.Diagnostics;
using Microsoft.Win32;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BlackScreens.Ui;

/// <summary>
/// Finds the executable behind a denylist entry and hands back its icon. Running processes are the
/// best source; the Windows system folders cover the shell processes that are not always running.
/// </summary>
internal static class ProcessIconProvider
{
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static Dictionary<string, string>? _runningPaths;
    private static ImageSource? _fallback;

    /// <summary>Drops the cached process snapshot so a freshly opened window sees current apps.</summary>
    public static void Reset()
    {
        Cache.Clear();
        _runningPaths = null;
    }

    /// <summary>Remembers where a program lives when the user picks it with Browse.</summary>
    public static void Register(string processName, string executablePath)
    {
        var name = ProcessDenylist.Normalize(processName);
        if (name.Length == 0 || !File.Exists(executablePath))
        {
            return;
        }

        try
        {
            var icon = Load(executablePath);
            if (icon is not null)
            {
                Cache[name] = icon;
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Icon register failed for {name}: {ex}");
        }
    }

    public static ImageSource? For(string? processName)
    {
        var name = ProcessDenylist.Normalize(processName);
        if (name.Length == 0)
        {
            return Fallback();
        }

        if (Cache.TryGetValue(name, out var cached))
        {
            return cached;
        }

        ImageSource? icon = null;
        try
        {
            var path = ResolvePath(name);
            if (path is not null)
            {
                icon = Load(path);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Icon lookup failed for {name}: {ex}");
        }

        icon ??= Fallback();
        Cache[name] = icon;
        return icon;
    }

    private static string? ResolvePath(string name)
    {
        if (RunningPaths().TryGetValue(name, out var running))
        {
            return running;
        }

        var fromRegistry = AppPathsEntry(name);
        if (fromRegistry is not null)
        {
            return fromRegistry;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string[] candidates =
        [
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), name + ".exe"),
            Path.Combine(windows, name + ".exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// Installers register their executable under App Paths, which is how a browser or editor that is
    /// not currently running still gets its real icon.
    /// </summary>
    private static string? AppPathsEntry(string name)
    {
        const string subKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\";

        foreach (var root in new[] { Registry.CurrentUser, Registry.LocalMachine })
        {
            try
            {
                using var key = root.OpenSubKey(subKey + name + ".exe");
                if (key?.GetValue(null) is not string value || value.Length == 0)
                {
                    continue;
                }

                var path = value.Trim().Trim('"');
                if (File.Exists(path))
                {
                    return path;
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"App Paths lookup failed for {name}: {ex}");
            }
        }

        return null;
    }

    /// <summary>One pass over the process list, reused for every row on the page.</summary>
    private static Dictionary<string, string> RunningPaths()
    {
        if (_runningPaths is not null)
        {
            return _runningPaths;
        }

        var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (paths.ContainsKey(process.ProcessName))
                    {
                        continue;
                    }

                    var path = ProcessPath.ForId(process.Id);
                    if (path is not null)
                    {
                        paths[process.ProcessName] = path;
                    }
                }
                catch
                {
                    // Protected processes cannot be opened. Skip them.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Process scan failed: {ex}");
        }

        _runningPaths = paths;
        return paths;
    }

    private static ImageSource? Load(string path)
    {
        using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
        return icon is null ? null : ToImageSource(icon);
    }

    private static ImageSource? Fallback()
    {
        if (_fallback is not null)
        {
            return _fallback;
        }

        try
        {
            _fallback = ToImageSource(SystemIcons.Application);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Fallback icon failed: {ex}");
        }

        return _fallback;
    }

    private static ImageSource? ToImageSource(Icon icon)
    {
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        source.Freeze();
        return source;
    }
}
