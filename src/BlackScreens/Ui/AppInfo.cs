using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace BlackScreens.Ui;

/// <summary>Version, signature and data folder details shown on the About page.</summary>
internal static class AppInfo
{
    public static string Version { get; } = ReadVersion();

    /// <summary>Reports the Authenticode subject embedded in the running exe, if there is one.</summary>
    public static string SignatureSummary()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exe))
        {
            return "Signature: unknown";
        }

        try
        {
            // X509CertificateLoader has no replacement for reading an Authenticode signature
            // out of a PE file, so the obsolete helper is still the only way to do this.
#pragma warning disable SYSLIB0057
            var subject = X509Certificate.CreateFromSignedFile(exe).Subject;
#pragma warning restore SYSLIB0057
            var name = CommonName(subject);
            return string.IsNullOrWhiteSpace(name)
                ? "Signed"
                : $"Signed by {name}";
        }
        catch
        {
            return "Not code signed. Windows may warn the first time this build runs.";
        }
    }

    /// <summary>Pulls CN out of a distinguished name such as <c>CN=Example, O=Example, C=US</c>.</summary>
    internal static string CommonName(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            return string.Empty;
        }

        foreach (var part in subject.Split(','))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("CN=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed[3..].Trim().Trim('"');
            }
        }

        return subject.Trim();
    }

    public static void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(AppSettings.DirectoryPath);
            Start(AppSettings.DirectoryPath);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Open data folder failed: {ex}");
        }
    }

    public static void OpenErrorLog()
    {
        try
        {
            if (!File.Exists(ErrorLog.FilePath))
            {
                OpenDataFolder();
                return;
            }

            Start(ErrorLog.FilePath);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Open error log failed: {ex}");
        }
    }

    private static void Start(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    /// <summary>
    /// Drops the source control metadata the SDK appends to the product version, so About shows
    /// "1.0.0" rather than "1.0.0+3f9a...".
    /// </summary>
    internal static string TrimVersion(string? productVersion)
    {
        var value = productVersion?.Trim() ?? string.Empty;
        var plus = value.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? value : value[..plus];
    }

    private static string ReadVersion()
    {
        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            try
            {
                var info = FileVersionInfo.GetVersionInfo(exe);
                if (!string.IsNullOrWhiteSpace(info.ProductVersion))
                {
                    return TrimVersion(info.ProductVersion);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Version read failed: {ex}");
            }
        }

        return typeof(AppInfo).Assembly.GetName().Version?.ToString() ?? "unknown";
    }
}
