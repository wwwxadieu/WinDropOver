using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop.Hooks;

public sealed class MouseHookEventArgs : EventArgs
{
    public required int X { get; init; }
    public required int Y { get; init; }
    public required bool IsLeftButtonDown { get; init; }
    public required bool IsLeftButtonUp { get; init; }

    /// <summary>The event's own tick count, straight from MSLLHOOKSTRUCT.time. Used by the shake detector, which needs real timing rather than when a handler happened to run.</summary>
    public required long TimestampMs { get; init; }
}

/// <summary>
/// Wraps WH_MOUSE_LL. Per the plan's risk mitigation (section 6): the callback only reads the
/// struct fields and raises a .NET event — no I/O, no allocation beyond the event args, so a
/// slow subscriber can never make Windows think the hook itself is unresponsive and silently
/// unhook it. Must be installed and pumped from the dedicated Win32 message-loop thread.
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    private readonly HookProc _hookProc;
    private IntPtr _hookHandle;

    public event EventHandler<MouseHookEventArgs>? MouseEvent;

    public LowLevelMouseHook()
    {
        // Keep the delegate alive for the lifetime of the hook — otherwise the GC can collect
        // it while Windows still holds a native pointer to it.
        _hookProc = HookCallback;
    }

    /// <summary>Call from the thread that will run the message loop pumping this hook's callbacks.</summary>
    public void Install()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }
        using var currentModule = System.Diagnostics.Process.GetCurrentProcess().MainModule!;
        var moduleHandle = NativeMethods.GetModuleHandle(currentModule.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeConstants.WH_MOUSE_LL, _hookProc, moduleHandle, 0);
        if (_hookHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException($"SetWindowsHookEx(WH_MOUSE_LL) failed: {Marshal.GetLastWin32Error()}");
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
            var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            var message = (int)wParam;
            MouseEvent?.Invoke(this, new MouseHookEventArgs
            {
                X = data.pt.X,
                Y = data.pt.Y,
                IsLeftButtonDown = message == NativeConstants.WM_LBUTTONDOWN,
                IsLeftButtonUp = message == NativeConstants.WM_LBUTTONUP,
                TimestampMs = data.time
            });
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
