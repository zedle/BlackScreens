namespace BlackScreens.Ui;

/// <summary>Flashes a number on every connected screen so the whitelist rows can be matched to hardware.</summary>
internal static class MonitorIdentifier
{
    private const int VisibleMs = 2000;

    public static void Flash()
    {
        try
        {
            var monitors = MonitorEnumerator.CaptureAll();
            if (monitors.Count == 0)
            {
                return;
            }

            var forms = new List<Form>(monitors.Count);
            for (var index = 0; index < monitors.Count; index++)
            {
                forms.Add(Create(monitors[index], index + 1));
            }

            var timer = new System.Windows.Forms.Timer { Interval = VisibleMs };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                timer.Dispose();
                foreach (var form in forms)
                {
                    try
                    {
                        form.Close();
                        form.Dispose();
                    }
                    catch (Exception ex)
                    {
                        ErrorLog.Write($"Identify close failed: {ex}");
                    }
                }
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            ErrorLog.Write($"Identify failed: {ex}");
        }
    }

    private static Form Create(ConnectedMonitor monitor, int number)
    {
        var family = SystemFonts.MessageBoxFont?.FontFamily ?? SystemFonts.DefaultFont.FontFamily;

        var form = new IdentifyForm(monitor.Bounds)
        {
            BackColor = Color.Black,
            Opacity = 0.85
        };

        var label = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.White,
            Font = new Font(family, 160f, FontStyle.Bold),
            Text = number.ToString()
        };

        var caption = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 60,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gainsboro,
            Font = new Font(family, 14f),
            Text = $"{MonitorChoice.FriendlyName(monitor.DeviceName)}   {monitor.Bounds.Width} x {monitor.Bounds.Height}"
        };

        form.Controls.Add(label);
        form.Controls.Add(caption);
        form.Show();
        form.PlaceAt(monitor.Bounds);
        return form;
    }

    private sealed class IdentifyForm(Rectangle bounds) : PlacedForm(bounds);
}
