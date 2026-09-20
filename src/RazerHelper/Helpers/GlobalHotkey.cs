using System.Runtime.InteropServices;

namespace RazerHelper.Helpers;

/// <summary>
/// The shortcut that opens the popup from any program, including a game in the
/// foreground. On the Blade, Fn+Del sends the Insert key (there is no separate
/// Insert key), so that is what it listens for. Windows delivers it to a hidden
/// window of ours, so nothing polls the keyboard and nothing is watched: when the
/// key is not pressed the cost is zero. Registration fails when another program
/// already owns the key.
/// </summary>
internal sealed class GlobalHotkey : NativeWindow, IDisposable
{
    /// <summary>What the shortcut is called in the interface.</summary>
    public const string Text = "Fn+Del";

    private const int WmHotkey = 0x0312;

    // Holding the keys down would otherwise fire it again and again.
    private const uint ModNoRepeat = 0x4000;

    private const int HotkeyId = 1;
    private static readonly IntPtr MessageOnlyParent = new(-3);

    private bool _registered;

    /// <summary>Raised when the shortcut is pressed, on the UI thread.</summary>
    public event EventHandler? Pressed;

    /// <summary>Starts listening. False when another program already uses the key.</summary>
    public bool TryRegister()
    {
        if (_registered)
            return true;

        if (Handle == IntPtr.Zero)
            CreateHandle(new CreateParams { Parent = MessageOnlyParent });

        _registered = RegisterHotKey(Handle, HotkeyId, ModNoRepeat, (uint)Keys.Insert);
        return _registered;
    }

    public void Unregister()
    {
        if (!_registered)
            return;

        UnregisterHotKey(Handle, HotkeyId);
        _registered = false;
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == WmHotkey && message.WParam == HotkeyId)
            Pressed?.Invoke(this, EventArgs.Empty);
        else
            base.WndProc(ref message);
    }

    public void Dispose()
    {
        Unregister();

        if (Handle != IntPtr.Zero)
            DestroyHandle();
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr window, int id);
}
