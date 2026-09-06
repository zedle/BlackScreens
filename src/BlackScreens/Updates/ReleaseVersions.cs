namespace BlackScreens.Updates;

/// <summary>Reads the version out of a release tag and compares it with the running build.</summary>
public static class ReleaseVersions
{
    /// <summary>
    /// Accepts the shapes a tag or an assembly version actually turns up in: "v1.2.3", "1.2.3",
    /// "1.2.3.0", "1.2.3-beta.1" and "1.2.3+abc123". Returns null when there is no version in there.
    /// </summary>
    public static Version? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim();
        if (value.StartsWith('v') || value.StartsWith('V'))
        {
            value = value[1..];
        }

        var cut = value.IndexOfAny(['-', '+', ' ']);
        if (cut >= 0)
        {
            value = value[..cut];
        }

        if (!Version.TryParse(value, out var parsed))
        {
            return null;
        }

        // Version leaves unspecified parts at -1, and a four part build number must not make
        // 1.2.3.0 look different from 1.2.3.
        return new Version(parsed.Major, Math.Max(0, parsed.Minor), Math.Max(0, parsed.Build));
    }

    /// <summary>True when <paramref name="candidate"/> is a later version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string? current, string? candidate)
    {
        var running = TryParse(current);
        var offered = TryParse(candidate);

        return running is not null && offered is not null && offered > running;
    }
}
