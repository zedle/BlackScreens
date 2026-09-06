namespace BlackScreens;

public static class WindowStyles
{
    public const int WsCaption = 0x00C00000;
    public const int WsPopup = unchecked((int)0x80000000);
    public const int WsExToolWindow = 0x00000080;
    public const int WsExNoActivate = 0x08000000;
    public const int WsMaximizeBox = 0x00010000;
    public const int WsClipChildren = 0x02000000;
}
