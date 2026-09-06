using System.Runtime.InteropServices;

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
    public static string? PickAsset(InstallKind kind, IEnumerable<string> assetNames) =>
        PickAsset(kind, assetNames, RuntimeInformation.ProcessArchitecture);

    /// <summary>
    /// The same, for a stated architecture. A release carries a build per architecture, so the
    /// suffix has to match both, or an Arm64 machine would be handed an x64 build and the other way
    /// round.
    /// </summary>
    /// <remarks>
    /// The architecture used is the one this process is running as, not the one the machine could
    /// run. An x64 build running under emulation on an Arm64 machine therefore stays on x64 rather
    /// than swapping architecture underneath itself during an update. Moving to the native build is
    /// a download away, and is the user's decision to make.
    /// </remarks>
    public static string? PickAsset(InstallKind kind, IEnumerable<string> assetNames, Architecture architecture)
    {
        var suffix = AssetSuffix(kind, architecture);
        if (suffix is null)
        {
            return null;
        }

        return assetNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .FirstOrDefault(name => name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// What the asset for this architecture is called, or null for an architecture with no build.
    /// </summary>
    /// <remarks>
    /// The x64 names are the ones 1.0.0 and 1.0.1 shipped looking for, so they cannot change. That
    /// is also why the Arm64 installer is "-setup-arm64.exe" rather than "-arm64-setup.exe": those
    /// releases match the installer by the "-setup.exe" ending and take the first asset that fits,
    /// so a name ending that way would have been handed to x64 machines.
    /// </remarks>
    public static string? AssetSuffix(InstallKind kind, Architecture architecture) => architecture switch
    {
        Architecture.X64 => kind == InstallKind.Installed ? "-setup.exe" : "-win-x64.exe",
        Architecture.Arm64 => kind == InstallKind.Installed ? "-setup-arm64.exe" : "-win-arm64.exe",
        _ => null
    };
}
