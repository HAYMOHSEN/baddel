using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace Baddel.Services;

/// <summary>Registers the global shortcut with Windows. No keyboard hook: Baddel never sees other keystrokes.</summary>
internal sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xB0D1;
    private readonly HwndSource _window;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("Baddel.Hotkeys") { Width = 0, Height = 0, WindowStyle = 0 };
        _window = new HwndSource(parameters);
        _window.AddHook(WndProc);
    }

    public event EventHandler? Pressed;

    public HotkeyGesture? Current { get; private set; }

    public bool Register(HotkeyGesture gesture)
    {
        Unregister();
        uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);
        if (!Native.RegisterHotKey(_window.Handle, HotkeyId, gesture.NativeModifiers | Native.MOD_NOREPEAT, virtualKey))
        {
            Logger.Info($"RegisterHotKey failed for {gesture} (error {Marshal.GetLastWin32Error()}).");
            return false;
        }
        Current = gesture;
        return true;
    }

    public void Unregister()
    {
        if (Current is null) return;
        Native.UnregisterHotKey(_window.Handle, HotkeyId);
        Current = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt64() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Unregister();
        _window.RemoveHook(WndProc);
        _window.Dispose();
    }
}
