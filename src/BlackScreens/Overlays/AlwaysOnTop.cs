namespace BlackScreens.Overlays;

/// <summary>
/// Keeps chosen programs above the overlays, so something like a capture or chat window can stay
/// visible on a monitor that is otherwise blacked out.
/// </summary>
/// <remarks>
/// The overlays are topmost windows, so the only way another window can stay above one is to be
/// topmost as well and to have been raised more recently.
///
/// Raising it on every poll is the obvious way to do that and the wrong one: the overlay is put back
/// on top, then the window is raised over it again, four times a second, and the window visibly
/// flickers as it is covered and uncovered. So both halves only act when the z order is actually
/// wrong. In the steady state neither writes anything.
///
/// A window that was already topmost before BlackScreens touched it is left that way afterwards.
/// Everything else is put back when blackout ends, so no program is left holding a z order the user
/// never asked it for.
/// </remarks>
public sealed class AlwaysOnTop
{
    private const nint HwndTopmost = -1;
    private const nint HwndNoTopmost = -2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const int WsExTopmost = 0x00000008;

    /// <summary>Each window raised, against whether it was already topmost on its own.</summary>
    private readonly Dictionary<nint, bool> _raised = [];

    /// <summary>How many windows are currently held above the overlays.</summary>
    public int Count => _raised.Count;

    /// <summary>
    /// Raises every visible window belonging to one of <paramref name="processNames"/> that is not
    /// already above <paramref name="overlays"/>.
    /// </summary>
    public void Apply(IReadOnlyList<string> processNames, IReadOnlyCollection<nint> overlays)
    {
        var wanted = new ProcessRules(processNames);
        if (wanted.Count == 0)
        {
            ReleaseAll();
            return;
        }

        var self = Environment.ProcessId;
        var paths = new Dictionary<int, string>();
        var matches = new List<nint>();

        // EnumWindows walks the z order from the top down, so the position in this list is the
        // window's depth: a smaller index is nearer the front.
        var depth = new Dictionary<nint, int>();
        var next = 0;

        NativeMethods.EnumWindows((handle, _) =>
        {
            try
            {
                if (handle == 0 || !NativeMethods.IsWindowVisible(handle))
                {
                    return true;
                }

                depth[handle] = next++;

                if (NativeMethods.IsIconic(handle))
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(handle, out var processId);
                if (processId == 0 || processId == self)
                {
                    return true;
                }

                if (!paths.TryGetValue(processId, out var path))
                {
                    path = ProcessPath.ForId(processId) ?? string.Empty;
                    paths[processId] = path;
                }

                var name = path.Length == 0 ? string.Empty : Path.GetFileNameWithoutExtension(path);
                if (wanted.Matches(name, path))
                {
                    matches.Add(handle);
                }
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Could not examine a window above the overlay: {ex}");
            }

            return true;
        }, 0);

        // The front most overlay. Anything in front of that one is in front of all of them.
        var frontOverlay = int.MaxValue;
        foreach (var overlay in overlays)
        {
            if (depth.TryGetValue(overlay, out var at) && at < frontOverlay)
            {
                frontOverlay = at;
            }
        }

        var found = new HashSet<nint>();
        foreach (var handle in matches)
        {
            found.Add(handle);

            // Remember how it was the first time, before anything is changed.
            if (!_raised.ContainsKey(handle))
            {
                var exStyle = (int)NativeMethods.GetWindowLong(handle, NativeMethods.GwlExStyle);
                _raised[handle] = (exStyle & WsExTopmost) != 0;
            }

            // Already in front of every overlay, so there is nothing to do and nothing to repaint.
            if (depth.TryGetValue(handle, out var at) && at < frontOverlay)
            {
                continue;
            }

            try
            {
                NativeMethods.SetWindowPos(
                    handle, HwndTopmost, 0, 0, 0, 0,
                    SwpNoMove | SwpNoSize | NativeMethods.SwpNoActivate);
            }
            catch (Exception ex)
            {
                ErrorLog.Write($"Could not raise a window above the overlay: {ex}");
            }
        }

        // Anything raised before that has since closed, been hidden, or been taken off the list.
        foreach (var gone in _raised.Keys.Where(handle => !found.Contains(handle)).ToArray())
        {
            Restore(gone);
        }
    }

    /// <summary>Puts every window this raised back to where it was.</summary>
    public void ReleaseAll()
    {
        foreach (var handle in _raised.Keys.ToArray())
        {
            Restore(handle);
        }
    }

    private void Restore(nint handle)
    {
        if (!_raised.Remove(handle, out var wasAlreadyTopmost) || wasAlreadyTopmost)
        {
            return;
        }

        try
        {
            NativeMethods.SetWindowPos(
                handle, HwndNoTopmost, 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | NativeMethods.SwpNoActivate);
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Could not put a window back below the overlay: {ex}");
        }
    }
}
