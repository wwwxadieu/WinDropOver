using WinClipboard.Core.Models;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// The union of every monitor's bounds, read straight from GetSystemMetrics.
///
/// This exists because the edge trigger needs the answer from inside the WH_MOUSE_LL callback,
/// on the hook thread, and the obvious source — WPF's SystemParameters — is the wrong tool
/// there. Those are WPF statics tied to the Dispatcher; touching them off the UI thread is not
/// something WPF promises to support, and an exception raised inside a low-level hook callback
/// does not surface as an ordinary error. It unwinds through a native frame, which the runtime
/// treats as fatal: the process dies on the spot, with no handler given a chance to run.
///
/// GetSystemMetrics has no such affinity, is a cheap syscall, and returns exactly the same
/// numbers.
/// </summary>
public static class VirtualScreen
{
    private static ScreenRect _cached;
    private static long _cachedAtTicks;

    /// <summary>
    /// Cached for a second: this is asked for on every mouse sample while a drag is in progress,
    /// which is up to a thousand times a second on a high-polling-rate mouse. The answer only
    /// changes when a monitor is added, removed or rearranged, and a second of staleness after
    /// that is not something a drag can notice.
    /// </summary>
    public static ScreenRect GetBounds()
    {
        var now = Environment.TickCount64;
        if (_cached.Right != 0 && now - _cachedAtTicks < 1000)
        {
            return _cached;
        }

        var left = NativeMethods.GetSystemMetrics(NativeConstants.SM_XVIRTUALSCREEN);
        var top = NativeMethods.GetSystemMetrics(NativeConstants.SM_YVIRTUALSCREEN);
        var width = NativeMethods.GetSystemMetrics(NativeConstants.SM_CXVIRTUALSCREEN);
        var height = NativeMethods.GetSystemMetrics(NativeConstants.SM_CYVIRTUALSCREEN);

        _cached = new ScreenRect(left, top, left + width, top + height);
        _cachedAtTicks = now;
        return _cached;
    }
}
