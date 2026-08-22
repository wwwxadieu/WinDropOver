using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>Per-monitor DPI lookup so bubble/overlay/panel placement stays correct across mixed-DPI multi-monitor setups (plan 6).</summary>
public static class DpiHelper
{
    private const int DefaultDpi = 96;

    /// <summary>Scale factor (1.0 = 100%) for the monitor the given window currently sits on.</summary>
    public static double GetScaleFactor(IntPtr hwnd)
    {
        var dpi = NativeMethods.GetDpiForWindow(hwnd);
        return dpi <= 0 ? 1.0 : dpi / (double)DefaultDpi;
    }
}
