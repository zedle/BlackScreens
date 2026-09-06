using Microsoft.Win32;

namespace BlackScreens.Configuration;

/// <summary>One installed screensaver.</summary>
public sealed record Screensaver(string Path, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Finds the screensavers Windows has installed and which one to draw.</summary>
public static class Screensavers
{
    private const string DesktopKey = @"Control Panel\Desktop";

    /// <summary>
    /// Windows ships several savers under names nobody would recognise from the file name alone.
    /// </summary>
    private static readonly Dictionary<string, string> FriendlyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scrnsave"] = "Blank",
        ["ssText3d"] = "3D Text",
        ["ssmyst"] = "Mystify",
        ["ssstars"] = "Starfield",
        ["ssflwbox"] = "Flower Box",
        ["ssmarque"] = "Marquee",
        ["ssbezier"] = "Beziers",
        ["ssPipes"] = "3D Pipes",
        ["PhotoScreensaver"] = "Photos",
        ["Bubbles"] = "Bubbles",
        ["Mystify"] = "Mystify",
        ["Ribbons"] = "Ribbons"
    };

    /// <summary>Turns a screensaver path into something worth showing in a list.</summary>
    public static string Describe(string path)
    {
        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (name.Length == 0)
        {
            return path;
        }

        return FriendlyNames.TryGetValue(name, out var friendly) ? friendly : name;
    }

    /// <summary>
    /// Picks the screensaver to run: the one chosen in settings, otherwise whatever Windows is set to.
    /// Returns null when neither exists, which means fall back to plain black.
    /// </summary>
    public static string? Choose(string? settingsPath, string? windowsPath, Func<string, bool> exists)
    {
        if (!string.IsNullOrWhiteSpace(settingsPath) && exists(settingsPath))
        {
            return settingsPath;
        }

        if (!string.IsNullOrWhiteSpace(windowsPath) && exists(windowsPath))
        {
            return windowsPath;
        }

        return null;
    }

    public static string? Choose(string? settingsPath) =>
        Choose(settingsPath, WindowsScreensaverPath(), File.Exists);

    /// <summary>The screensaver selected in the Windows personalization settings, if any.</summary>
    public static string? WindowsScreensaverPath()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(DesktopKey);
            var value = key?.GetValue("SCRNSAVE.EXE") as string;
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().Trim('"');
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Screensaver probe failed: {ex}");
            return null;
        }
    }

    /// <summary>Every .scr the system folders hold, plus whatever Windows is currently set to.</summary>
    public static IReadOnlyList<Screensaver> Installed()
    {
        // Keyed by display name: System32 and SysWOW64 hold the same savers, and two rows called
        // "Blank" help nobody.
        var found = new Dictionary<string, Screensaver>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in SearchFolders())
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    continue;
                }

                foreach (var file in Directory.EnumerateFiles(folder, "*.scr"))
                {
                    var saver = new Screensaver(file, Describe(file));
                    found.TryAdd(saver.Name, saver);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Screensaver scan failed for {folder}: {ex}");
            }
        }

        if (WindowsScreensaverPath() is { } configured && File.Exists(configured))
        {
            var saver = new Screensaver(configured, Describe(configured));
            found.TryAdd(saver.Name, saver);
        }

        return found.Values.OrderBy(saver => saver.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<string> SearchFolders()
    {
        yield return Environment.GetFolderPath(Environment.SpecialFolder.System);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.SystemX86);
        yield return Environment.GetFolderPath(Environment.SpecialFolder.Windows);
    }
}
