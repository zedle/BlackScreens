using System.Runtime.InteropServices;

namespace BlackScreens;

public static class MonitorEnumerator
{
    public static IReadOnlyList<Rectangle> Capture() =>
        CaptureAll().Select(monitor => monitor.Bounds).ToArray();

    /// <summary>The monitor a window sits on, or null when it cannot be resolved.</summary>
    public static Rectangle? BoundsForWindow(nint handle)
    {
        if (handle == 0)
        {
            return null;
        }

        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MonitorDefaultToNearest);
        if (monitor == 0)
        {
            return null;
        }

        var info = new NativeMethods.MONITORINFOEX
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
        };

        return NativeMethods.GetMonitorInfo(monitor, ref info)
            ? info.rcMonitor.ToRectangle()
            : null;
    }

    public static IReadOnlyList<ConnectedMonitor> CaptureAll()
    {
        var monitors = new List<ConnectedMonitor>();

        // Read once rather than per monitor, since it walks the registry.
        var hardware = MonitorHardware.NamesByDevice();

        NativeMethods.EnumDisplayMonitors(0, 0, (hMonitor, _, _, _) =>
        {
            if (hMonitor == 0)
            {
                return true;
            }

            var info = new NativeMethods.MONITORINFOEX
            {
                cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>()
            };

            if (!NativeMethods.GetMonitorInfo(hMonitor, ref info))
            {
                return true;
            }

            var bounds = info.rcMonitor.ToRectangle();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return true;
            }

            var name = string.IsNullOrWhiteSpace(info.szDevice)
                ? bounds.ToString()
                : info.szDevice;
            if (monitors.Any(existing => existing.DeviceName == name))
            {
                return true;
            }

            monitors.Add(new ConnectedMonitor(name, bounds)
            {
                IsPrimary = (info.dwFlags & NativeMethods.MonitorinfofPrimary) != 0,
                HardwareName = hardware.TryGetValue(name, out var model) ? model : null
            });
            return true;
        }, 0);

        return monitors;
    }
}
