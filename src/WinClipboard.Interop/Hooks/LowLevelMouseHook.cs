using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop.Hooks;

/// <summary>
/// A struct, not a class, because one of these is produced for every single mouse message the
/// system sees - including every WM_MOUSEMOVE, at up to a thousand a second on a high-polling-rate
/// mouse, whether or not this application is doing anything with them. As a class that was a heap
/// allocation per mouse movement for the whole time the app was running, which is a background
/// cost the user pays for nothing. EventHandler&lt;T&gt; has not required T : EventArgs since .NET 4.5.
/// </summary>
public readonly struct MouseHookEventArgs
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
/// struct fields and raises a .NET event — no I/O and no allocation at all.
///
/// That is only half the guarantee, and the half this class can enforce on its own. Subscribers
/// run *inside* the callback, on the hook thread, so a subscriber that blocks blocks the hook -
/// and Windows responds to a hook that overruns LowLevelHooksTimeout by removing it silently.
/// Anything a subscriber wants the UI thread to do must therefore be posted, never waited on.
///
/// Must be installed and pumped from the dedicated Win32 message-loop thread.
/// </summary>
public sealed class LowLevelMouseHook : IDisposable
{
    private readonly HookProc _hookProc;
    private IntPtr _hookHandle;

    public event EventHandler<MouseHookEventArgs>? MouseEvent;

    /// <summary>Raised when a subscriber threw inside the callback. Reported rather than rethrown, because rethrowing here kills the process.</summary>
    public event EventHandler<Exception>? CallbackFailed;

    /// <summary>
    /// Environment.TickCount64 at the last callback, or 0 if there has not been one. Windows
    /// removes a hook that overruns its timeout without saying so, and offers no way to ask
    /// whether a hook is still live — going quiet is the only symptom there is.
    /// </summary>
    public long LastCallbackTicks { get; private set; }

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
        // Count "alive" from installation, not from the first callback: the watchdog compares
        // this against Environment.TickCount64, which starts at the machine's uptime, so leaving
        // it at zero made every freshly installed hook look like it had been silent for days.
        LastCallbackTicks = Environment.TickCount64;
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
        // Nothing here may block or do I/O. Windows delivers this synchronously ahead of the
        // mouse event reaching any application, so time spent here is time the whole machine's
        // pointer is stalled - and a callback that overruns LowLevelHooksTimeout (300ms by
        // default) gets the hook silently removed, with no notification and no way back.
        LastCallbackTicks = Environment.TickCount64;

        var handler = MouseEvent;
        if (nCode >= 0 && handler is not null)
        {
            // Subscribers run here, inside the native callback. An exception escaping this frame
            // does not become an ordinary unhandled exception — it unwinds through native code,
            // which the runtime treats as fatal, so the process dies with no handler run and
            // nothing logged. Swallow it: dropping one mouse sample is always better than
            // killing the application, and CallNextHookEx below must run either way or every
            // other hook in the system stops seeing input.
            try
            {
                var data = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
                var message = (int)wParam;
                handler(this, new MouseHookEventArgs
                {
                    X = data.pt.X,
                    Y = data.pt.Y,
                    IsLeftButtonDown = message == NativeConstants.WM_LBUTTONDOWN,
                    IsLeftButtonUp = message == NativeConstants.WM_LBUTTONUP,
                    TimestampMs = data.time
                });
            }
            catch (Exception ex)
            {
                CallbackFailed?.Invoke(this, ex);
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    /// <summary>Uninstall and install again, to recover a hook Windows has silently dropped. Must be called on the thread that owns the hook.</summary>
    public void Reinstall()
    {
        Uninstall();
        Install();
        LastCallbackTicks = Environment.TickCount64;
    }

    public void Dispose() => Uninstall();
}
