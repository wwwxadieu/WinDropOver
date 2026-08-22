using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Applies the WS_EX_LAYERED + WS_EX_TOOLWINDOW + topmost combination the plan calls for on the
/// bubble window (3.2 Overlay/Bubble Window): layered for the pill-shaped/rounded clip and
/// smooth alpha, tool-window so it never shows up in the taskbar or Alt+Tab, and topmost so it
/// stays visible while docked at the screen edge.
/// </summary>
public static class WindowStyleHelper
{
    public static void MakeLayeredToolWindow(IntPtr hwnd)
    {
        var exStyle = GetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE);
        exStyle |= NativeConstants.WS_EX_LAYERED | NativeConstants.WS_EX_TOOLWINDOW;
        SetWindowLong(hwnd, NativeConstants.GWL_EXSTYLE, exStyle);
    }

    public static void SetTopmost(IntPtr hwnd, bool topmost)
    {
        NativeMethods.SetWindowPos(
            hwnd,
            topmost ? NativeConstants.HWND_TOPMOST : NativeConstants.HWND_NOTOPMOST,
            0, 0, 0, 0,
            NativeConstants.SWP_NOMOVE | NativeConstants.SWP_NOSIZE | NativeConstants.SWP_NOACTIVATE);
    }

    private static int GetWindowLong(IntPtr hwnd, int index) =>
        IntPtr.Size == 8
            ? (int)NativeMethods.GetWindowLongPtr64(hwnd, index)
            : NativeMethods.GetWindowLong32(hwnd, index);

    private static void SetWindowLong(IntPtr hwnd, int index, int value)
    {
        if (IntPtr.Size == 8)
        {
            NativeMethods.SetWindowLongPtr64(hwnd, index, new IntPtr(value));
        }
        else
        {
            NativeMethods.SetWindowLong32(hwnd, index, value);
        }
    }
}
