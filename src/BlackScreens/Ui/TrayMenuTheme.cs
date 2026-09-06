namespace BlackScreens.Ui;

/// <summary>
/// Paints the tray menu with the same palette as the settings window. WinForms menus are light by
/// default, which looks wrong next to a dark shell.
/// </summary>
internal static class TrayMenuTheme
{
    public static void Apply(ContextMenuStrip menu, ResolvedTheme theme)
    {
        var palette = TrayPalette.For(theme);

        menu.RenderMode = ToolStripRenderMode.Professional;
        menu.Renderer = new ToolStripProfessionalRenderer(new TrayColorTable(palette))
        {
            RoundedEdges = false
        };
        menu.BackColor = palette.Background;
        menu.ForeColor = palette.Text;
        menu.ShowImageMargin = true;

        PaintItems(menu.Items, palette);
    }

    private static void PaintItems(ToolStripItemCollection items, TrayPalette palette)
    {
        foreach (ToolStripItem item in items)
        {
            item.BackColor = palette.Background;
            item.ForeColor = palette.Text;

            if (item is ToolStripMenuItem menuItem && menuItem.HasDropDownItems)
            {
                menuItem.DropDown.BackColor = palette.Background;
                menuItem.DropDown.ForeColor = palette.Text;
                PaintItems(menuItem.DropDownItems, palette);
            }
        }
    }

    /// <summary>Repaints a submenu that was rebuilt after the menu was themed.</summary>
    public static void Refresh(ToolStripMenuItem item, ResolvedTheme theme)
    {
        var palette = TrayPalette.For(theme);
        item.DropDown.BackColor = palette.Background;
        item.DropDown.ForeColor = palette.Text;
        PaintItems(item.DropDownItems, palette);
    }

    private sealed record TrayPalette(
        Color Background,
        Color Text,
        Color Hover,
        Color Border,
        Color Separator,
        Color Accent)
    {
        public static TrayPalette For(ResolvedTheme theme) => theme == ResolvedTheme.Dark
            ? new TrayPalette(
                Color.FromArgb(26, 29, 37),
                Color.FromArgb(231, 234, 241),
                Color.FromArgb(45, 51, 65),
                Color.FromArgb(43, 48, 59),
                // Separators need more contrast than the menu border or they vanish on dark.
                Color.FromArgb(88, 97, 115),
                Color.FromArgb(76, 141, 255))
            : new TrayPalette(
                Color.FromArgb(255, 255, 255),
                Color.FromArgb(24, 27, 34),
                Color.FromArgb(237, 240, 245),
                Color.FromArgb(221, 225, 232),
                Color.FromArgb(199, 205, 214),
                Color.FromArgb(37, 99, 235));
    }

    private sealed class TrayColorTable(TrayPalette palette) : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => palette.Background;

        public override Color MenuItemSelected => palette.Hover;

        public override Color MenuItemSelectedGradientBegin => palette.Hover;

        public override Color MenuItemSelectedGradientEnd => palette.Hover;

        public override Color MenuItemBorder => palette.Hover;

        public override Color MenuBorder => palette.Border;

        public override Color ImageMarginGradientBegin => palette.Background;

        public override Color ImageMarginGradientMiddle => palette.Background;

        public override Color ImageMarginGradientEnd => palette.Background;

        public override Color SeparatorDark => palette.Separator;

        // The renderer draws a second line just below the first. Matching the background keeps it
        // to one crisp rule instead of a fuzzy double line.
        public override Color SeparatorLight => palette.Background;

        public override Color CheckBackground => palette.Accent;

        public override Color CheckSelectedBackground => palette.Accent;

        public override Color CheckPressedBackground => palette.Accent;

        public override Color ButtonSelectedBorder => palette.Hover;
    }
}
