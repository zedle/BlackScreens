namespace BlackScreens.Updates;

/// <summary>How this copy of BlackScreens got onto the machine, which decides how it updates.</summary>
public enum InstallKind
{
    /// <summary>Put there by the installer, so the installer can replace it.</summary>
    Installed,

    /// <summary>A loose exe the user downloaded, so the exe replaces itself.</summary>
    Portable
}

/// <summary>Works out which release asset this copy should update itself with.</summary>
public static class UpdateTarget
{
    /// <summary>The repository releases are published from.</summary>
    public const string Repository = "zedle/BlackScreens";

    public static string LatestReleaseApi { get; } =
        $"https://api.github.com/repos/{Repository}/releases/latest";

    public static string ReleasesPage { get; } = $"https://github.com/{Repository}/releases";

    /// <summary>The installer leaves an uninstaller beside the exe. Nothing else does.</summary>
    public static InstallKind Detect(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return InstallKind.Portable;
        }

        var folder = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrEmpty(folder))
        {
            return InstallKind.Portable;
        }

        return File.Exists(Path.Combine(folder, "Uninstall.exe"))
            ? InstallKind.Installed
            : InstallKind.Portable;
    }

    public static InstallKind Detect() => Detect(Environment.ProcessPath);

    /// <summary>
    /// Picks the asset to download. An installed copy takes the installer. A portable copy always
    /// takes the self contained exe, so a machine without the .NET runtime cannot end up with a
    /// build it can no longer start.
    /// </summary>
    public static string? PickAsset(InstallKind kind, IEnumerable<string> assetNames)
    {
        var names = assetNames.Where(name => !string.IsNullOrWhiteSpace(name)).ToArray();

        return kind == InstallKind.Installed
            ? names.FirstOrDefault(name => name.EndsWith("-setup.exe", StringComparison.OrdinalIgnoreCase))
            : names.FirstOrDefault(name => name.EndsWith("-win-x64.exe", StringComparison.OrdinalIgnoreCase));
    }
}
