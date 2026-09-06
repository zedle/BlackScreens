using System.Runtime.InteropServices;

namespace BlackScreens.Interop;

internal static class NativeMethods
{
    public const int GwlStyle = -16;
    public const int GwlExStyle = -20;
    public const int MonitorDefaultToNearest = 2;
    public const int DwmwaCloaked = 14;
    public const int WmHotkey = 0x0312;
    public const int ModAlt = 0x0001;
    public const int ModControl = 0x0002;
    public const int VkB = 0x42;
    public const int ModNoRepeat = 0x4000;
    public const int HotkeyId = 1;
    public const int MonitorinfofPrimary = 0x00000001;

    public const uint SwpNoActivate = 0x0010;
    public const uint SwpShowWindow = 0x0040;
    public const uint SwpNoSendChanging = 0x0400;

    public const int ProcessQueryLimitedInformation = 0x1000;

    public static readonly nint HwndMessage = -3;
    public static readonly nint HwndTopMost = -1;

    public delegate bool EnumWindowsProc(nint hWnd, nint lParam);

    public delegate bool EnumMonitorsProc(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, nint lParam);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(nint hWnd);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(nint hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern nint GetForegroundWindow();

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(nint hWnd, out int lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern nint MonitorFromWindow(nint hwnd, int dwFlags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFO lpmi);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX lpmi);

    [DllImport("user32.dll")]
    public static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, EnumMonitorsProc lpfnEnum, nint dwData);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint value);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int value);

    [DllImport("dwmapi.dll")]
    public static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    [DllImport("user32.dll")]
    public static extern bool RegisterHotKey(nint hWnd, int id, int fsModifiers, int vk);

    [DllImport("user32.dll")]
    public static extern bool UnregisterHotKey(nint hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern nint OpenProcess(int desiredAccess, bool inheritHandle, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(nint handle);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(nint process, int flags, System.Text.StringBuilder exeName, ref int size);

    public static nint GetWindowLong(nint hWnd, int nIndex)
    {
        return nint.Size == 8
            ? GetWindowLongPtr64(hWnd, nIndex)
            : GetWindowLong32(hWnd, nIndex);
    }

    public static nint SetWindowLong(nint hWnd, int nIndex, nint value)
    {
        return nint.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, value)
            : SetWindowLong32(hWnd, nIndex, (int)value);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public readonly Rectangle ToRectangle() => Rectangle.FromLTRB(Left, Top, Right, Bottom);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }
}
