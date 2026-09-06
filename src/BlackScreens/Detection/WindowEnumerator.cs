using System.Runtime.InteropServices;
using System.Text;

namespace BlackScreens.Detection;

public static class WindowEnumerator
{
    public static IReadOnlyList<WindowSnapshot> Capture()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        var self = Environment.ProcessId;
        var windows = new List<WindowSnapshot>();
        var processPaths = new Dictionary<int, string>();

        NativeMethods.EnumWindows((hWnd, _) =>
        {
            try
            {
                if (hWnd == 0 || !NativeMethods.IsWindowVisible(hWnd) || NativeMethods.IsIconic(hWnd))
                {
                    return true;
                }

                NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
                if (processId == 0 || processId == self)
                {
                    return true;
                }

                if (!NativeMethods.GetWindowRect(hWnd, out var rect))
                {
                    return true;
                }

                var bounds = rect.ToRectangle();
                if (bounds.Width <= 0 || bounds.Height <= 0)
                {
                    return true;
                }

                var style = (int)NativeMethods.GetWindowLong(hWnd, NativeMethods.GwlStyle);
                var exStyle = (int)NativeMethods.GetWindowLong(hWnd, NativeMethods.GwlExStyle);

                var cloaked = false;
                if (NativeMethods.DwmGetWindowAttribute(hWnd, NativeMethods.DwmwaCloaked, out var cloakedValue, sizeof(int)) == 0)
                {
                    cloaked = cloakedValue != 0;
                }

                var monitor = NativeMethods.MonitorFromWindow(hWnd, NativeMethods.MonitorDefaultToNearest);
                if (monitor == 0)
                {
                    return true;
                }

                var monitorInfo = new NativeMethods.MONITORINFOEX
                {
                    cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
                };
                if (!NativeMethods.GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return true;
                }

                var executablePath = ResolveProcessPath(processId, processPaths);
                var processName = executablePath.Length == 0
                    ? string.Empty
                    : Path.GetFileNameWithoutExtension(executablePath);
                if (processName.Length == 0)
                {
                    return true;
                }

                windows.Add(new WindowSnapshot(
                    processName,
                    bounds,
                    style,
                    exStyle,
                    Visible: true,
                    cloaked,
                    hWnd == foreground,
                    monitorInfo.rcMonitor.ToRectangle())
                {
                    ExecutablePath = executablePath,
                    ClassName = ReadClassName(hWnd)
                });
            }
            catch
            {
            }

            return true;
        }, 0);

        return windows;
    }

    /// <summary>
    /// Resolves a process name once per scan. Cheaper than a <see cref="System.Diagnostics.Process"/>
    /// lookup for every window, which the 250 ms poll would otherwise repeat dozens of times a second.
    /// </summary>
    private static string ReadClassName(nint handle)
    {
        var buffer = new StringBuilder(96);
        var length = NativeMethods.GetClassName(handle, buffer, buffer.Capacity);
        return length <= 0 ? string.Empty : buffer.ToString(0, length);
    }

    private static string ResolveProcessPath(int processId, Dictionary<int, string> cache)
    {
        if (cache.TryGetValue(processId, out var cached))
        {
            return cached;
        }

        var path = ProcessPath.ForId(processId) ?? string.Empty;
        cache[processId] = path;
        return path;
    }
}
