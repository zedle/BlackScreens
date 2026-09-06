namespace BlackScreens;

/// <summary>
/// Positions borderless windows in raw screen pixels. WinForms scales <c>Bounds</c> by the DPI of the
/// monitor a form was created on, which lands the overlay in the wrong place on a mixed DPI desktop.
/// </summary>
internal static class OverlayPlacement
{
    public static bool Place(nint handle, Rectangle bounds)
    {
        if (handle == 0 || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return false;
        }

        return NativeMethods.SetWindowPos(
            handle,
            NativeMethods.HwndTopMost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow | NativeMethods.SwpNoSendChanging);
    }
}
