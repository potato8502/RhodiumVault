using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace RhodiumVault.Services;

[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Alt = 1,
    Control = 2,
    Shift = 4,
    Win = 8
}

/// <summary>System-wide keyboard shortcut so the vault can be brought to front from anywhere.</summary>
public class HotkeyService
{
    [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    private HwndSource? _source;
    private HotkeyModifiers? _modifiers;
    private Key? _key;
    private Action? _onPressed;

    /// <summary>Re-attaches the hotkey to a new window (call this each time the active window changes).</summary>
    public void AttachTo(Window window)
    {
        _source?.RemoveHook(WndProc);
        if (_modifiers.HasValue) UnregisterHotKey(_source?.Handle ?? IntPtr.Zero, 1);

        var helper = new WindowInteropHelper(window);
        if (helper.Handle == IntPtr.Zero) helper.EnsureHandle(); // window may not be shown yet
        _source = HwndSource.FromHwnd(helper.Handle);
        _source?.AddHook(WndProc);

        if (_modifiers.HasValue && _key.HasValue)
            RegisterCore(_modifiers.Value, _key.Value);
    }

    public void Register(HotkeyModifiers modifiers, Key key, Action onPressed)
    {
        _modifiers = modifiers;
        _key = key;
        _onPressed = onPressed;
        RegisterCore(modifiers, key);
    }

    private void RegisterCore(HotkeyModifiers modifiers, Key key)
    {
        if (_source == null) return;
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        RegisterHotKey(_source.Handle, 1, (uint)modifiers, vk);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == 1)
        {
            _onPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }
}
