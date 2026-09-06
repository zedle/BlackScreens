namespace BlackScreens;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    public event EventHandler? Pressed;

    public bool Registered { get; private set; }

    public HotkeyWindow(int modifiers, int virtualKey)
    {
        CreateHandle(new CreateParams
        {
            Caption = "BlackScreensHotkey",
            Parent = NativeMethods.HwndMessage
        });

        Rebind(modifiers, virtualKey);
    }

    /// <summary>Registers the shortcut. Returns false when another app already owns it.</summary>
    public bool Rebind(int modifiers, int virtualKey)
    {
        if (Handle != 0 && Registered)
        {
            NativeMethods.UnregisterHotKey(Handle, NativeMethods.HotkeyId);
            Registered = false;
        }

        if (Handle == 0 || !AppSettings.IsUsableHotkey(modifiers, virtualKey))
        {
            return false;
        }

        // MOD_NOREPEAT stops a held shortcut from toggling pause over and over.
        Registered = NativeMethods.RegisterHotKey(
            Handle,
            NativeMethods.HotkeyId,
            modifiers | NativeMethods.ModNoRepeat,
            virtualKey);
        return Registered;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey && m.WParam == NativeMethods.HotkeyId)
        {
            Pressed?.Invoke(this, EventArgs.Empty);
            return;
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (Handle != 0)
        {
            if (Registered)
            {
                NativeMethods.UnregisterHotKey(Handle, NativeMethods.HotkeyId);
                Registered = false;
            }

            DestroyHandle();
        }
    }
}
