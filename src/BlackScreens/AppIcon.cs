namespace BlackScreens;

internal static class AppIcon
{
    public static Icon LoadTray()
    {
        using var stream = typeof(AppIcon).Assembly.GetManifestResourceStream("BlackScreens.Assets.app.ico");
        if (stream is not null)
        {
            return new Icon(stream, 16, 16);
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
