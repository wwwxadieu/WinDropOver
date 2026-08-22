using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop.Hooks;

public sealed class KeyboardHookEventArgs : EventArgs
{
    public required int VirtualKeyCode { get; init; }
    public required bool IsKeyDown { get; init; }
}

/// <summary>
/// Wraps WH_KEYBOARD_LL, used only to detect whether the configured "hold while dragging" key
/// is currently down. Like <see cref="LowLevelMouseHook"/>, the callback stays minimal by
/// design — it just forwards the key state as an event.
/// </summary>
public sealed class LowLevelKeyboardHook : IDisposable
{
    private readonly HookProc _hookProc;
    private IntPtr _hookHandle;

    public event EventHandler<KeyboardHookEventArgs>? KeyEvent;

    public LowLevelKeyboardHook()
    {
        _hookProc = HookCallback;
    }

    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }
        using var currentModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeConstants.WH_KEYBOARD_LL, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SetWindowsHookEx(WH_KEYBOARD_LL) failed: {Marshal.GetLastWin32Error()}");
        }
    }

    public void Uninstall()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }
        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            var message = (int)wParam;
            var isDown = message is NativeConstants.WM_KEYDOWN or NativeConstants.WM_SYSKEYDOWN;
            var isUp = message is NativeConstants.WM_KEYUP or NativeConstants.WM_SYSKEYUP;
            if (isDown || isUp)
            {
                KeyEvent?.Invoke(this, new KeyboardHookEventArgs
                {
                    VirtualKeyCode = (int)data.vkCode,
                    IsKeyDown = isDown
                });
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
