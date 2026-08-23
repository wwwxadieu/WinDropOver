using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Places a window at an exact screen point, in physical pixels.
///
/// Deliberately not WPF's Window.Left/Top. Those are device-independent units, while the
/// coordinates that matter here come from WH_MOUSE_LL, which reports physical pixels — so on a
/// scaled display the two disagree, and a shelf meant to appear under the cursor lands somewhere
/// else entirely. SetWindowPos speaks the same units the hook does, which removes the conversion
/// rather than trying to get it right.
/// </summary>
public static class WindowPlacement
{
    /// <summary>
    /// Centres the window on a screen point, clamped to the work area of whichever monitor that
    /// point is on, so it can never open half off-screen or under the taskbar.
    /// </summary>
    public static void CentreOnScreenPoint(IntPtr hwnd, int screenX, int screenY)
    {
        if (hwnd == IntPtr.Zero || !NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return;
        }

        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;

        var left = screenX - width / 2;
        var top = screenY - height / 2;

        var monitor = NativeMethods.MonitorFromPoint(
            new POINT { X = screenX, Y = screenY }, NativeConstants.MONITOR_DEFAULTTONEAREST);

        var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
        if (NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            var work = info.rcWork;
            // Math.Max guards the degenerate case of a window wider than the monitor, where the
            // clamp range would otherwise be inverted and Math.Clamp would throw.
            left = Math.Clamp(left, work.Left, Math.Max(work.Left, work.Right - width));
            top = Math.Clamp(top, work.Top, Math.Max(work.Top, work.Bottom - height));
        }

        NativeMethods.SetWindowPos(
            hwnd, NativeConstants.HWND_TOPMOST, left, top, 0, 0,
            NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }

    /// <summary>The window's bounds in physical screen pixels, or null if it has no handle yet.</summary>
    public static Core.Models.ScreenRect? GetWindowScreenRect(IntPtr hwnd) =>
        hwnd != IntPtr.Zero && NativeMethods.GetWindowRect(hwnd, out var rect)
            ? new Core.Models.ScreenRect(rect.Left, rect.Top, rect.Right, rect.Bottom)
            : null;

    /// <summary>Where the pointer is now, in the same physical pixels the hooks report. Falls back to (0,0), which the caller's clamp then pulls onto the nearest monitor.</summary>
    public static (int X, int Y) GetCursorPosition() =>
        NativeMethods.GetCursorPos(out var point) ? (point.X, point.Y) : (0, 0);
}
