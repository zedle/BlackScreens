namespace BlackScreens.App;

internal static class AppIcon
{
    public static Icon LoadTray()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("BlackScreens.Assets.app.ico");
        if (stream is not null)
        {
            // The notification area wants 16 pixels at 100 per cent scaling but 20, 24 or 32 as the
            // display scaling goes up. Asking for a fixed 16 made Windows stretch the smallest frame
            // on a scaled display, which looked soft. app.ico carries a frame for each of those.
            return new Icon(stream, SystemInformation.SmallIconSize);
        }

        var exe = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(exe))
        {
            var extracted = Icon.ExtractAssociatedIcon(exe);
            if (extracted is not null)
            {
                return extracted;
            }
        }

        return SystemIcons.Application;
    }
}
