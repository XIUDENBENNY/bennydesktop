namespace DesktopAssistantLite.App.Shell;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    public event EventHandler<int>? HotkeyPressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams());
    }

    public bool Register(int id, Keys modifiers, Keys key)
    {
        var nativeModifiers = 0u;
        if (modifiers.HasFlag(Keys.Alt))
        {
            nativeModifiers |= NativeMethods.ModAlt;
        }

        if (modifiers.HasFlag(Keys.Control))
        {
            nativeModifiers |= NativeMethods.ModControl;
        }

        if (modifiers.HasFlag(Keys.Shift))
        {
            nativeModifiers |= NativeMethods.ModShift;
        }

        if (modifiers.HasFlag(Keys.LWin) || modifiers.HasFlag(Keys.RWin))
        {
            nativeModifiers |= NativeMethods.ModWin;
        }

        return NativeMethods.RegisterHotKey(Handle, id, nativeModifiers, (uint)key);
    }

    public void Unregister(int id)
    {
        NativeMethods.UnregisterHotKey(Handle, id);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotKey)
        {
            HotkeyPressed?.Invoke(this, m.WParam.ToInt32());
        }

        base.WndProc(ref m);
    }

    public void Dispose()
    {
        DestroyHandle();
    }
}
