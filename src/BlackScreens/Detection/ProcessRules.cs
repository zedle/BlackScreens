namespace BlackScreens.Detection;

/// <summary>
/// A list of programs, where each entry is either a bare process name or the full path to one
/// executable. Used for both the denylist and the programs kept above the overlay.
/// </summary>
/// <remarks>
/// A name matches every copy of a program, which is what you want for "chrome" or "explorer". A path
/// matches exactly one file, which is what you want when two builds share a name: a game and its
/// launcher both called "game.exe", or a portable copy next to an installed one.
/// </remarks>
public sealed class ProcessRules
{
    private readonly HashSet<string> _names = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public ProcessRules(IEnumerable<string> entries)
    {
        foreach (var raw in entries ?? [])
        {
            var entry = Normalize(raw);
            if (entry.Length == 0)
            {
                continue;
            }

            if (IsPath(entry))
            {
                _paths.Add(entry);
            }
            else
            {
                _names.Add(entry);
            }
        }
    }

    public int Count => _names.Count + _paths.Count;

    /// <summary>
    /// Whether a window's process is covered. <paramref name="processPath"/> may be null: the path
    /// cannot always be read, and in that case only the name rules can match.
    /// </summary>
    public bool Matches(string? processName, string? processPath)
    {
        if (!string.IsNullOrEmpty(processName) && _names.Contains(processName))
        {
            return true;
        }

        return !string.IsNullOrEmpty(processPath) && _paths.Contains(processPath);
    }

    /// <summary>
    /// Whether an entry names one file rather than a program. Anything with a directory in it is a
    /// path; anything else is a process name.
    /// </summary>
    public static bool IsPath(string entry) =>
        entry.Contains('\\', StringComparison.Ordinal)
        || entry.Contains('/', StringComparison.Ordinal);

    /// <summary>
    /// Tidies a typed or picked entry. A path is kept whole, extension and all, because that is what
    /// it has to be compared against. A bare name loses a trailing .exe, so "obs64.exe" and "obs64"
    /// are the same rule.
    /// </summary>
    public static string Normalize(string? entry)
    {
        var text = entry?.Trim() ?? string.Empty;

        // A path typed with quotes round it, the way Explorer copies one.
        if (text.Length > 1 && text.StartsWith('"') && text.EndsWith('"'))
        {
            text = text[1..^1].Trim();
        }

        if (IsPath(text))
        {
            return text;
        }

        return text.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? text[..^4].Trim()
            : text;
    }

    /// <summary>What to show in a list. A path is long, so only the file name is worth reading.</summary>
    public static string Display(string entry) =>
        IsPath(entry) ? Path.GetFileName(entry) : entry;

    /// <summary>
    /// The second, quieter half of a list row, which is what tells the two kinds of entry apart. A
    /// path shows the folder it points at; a name says plainly that it covers every copy.
    /// </summary>
    public static string Detail(string entry)
    {
        if (!IsPath(entry))
        {
            return "any copy";
        }

        var folder = Path.GetDirectoryName(entry);
        return string.IsNullOrEmpty(folder) ? "this copy only" : folder;
    }
}
