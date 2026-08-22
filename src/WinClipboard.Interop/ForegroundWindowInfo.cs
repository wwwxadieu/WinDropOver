using System.Diagnostics;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>Resolves the foreground window's owning process name, used to fill ClipboardItem.SourceApp.</summary>
public static class ForegroundWindowInfo
{
    public static string? GetForegroundProcessName()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return null;
        }

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);
        if (processId == 0)
        {
            return null;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName;
        }
        catch (ArgumentException)
        {
            // Process exited between GetWindowThreadProcessId and GetProcessById.
            return null;
        }
    }

    /// <summary>Snapshot of the foreground window, taken right before an overlay/bubble steals focus, so paste can be sent back to it afterwards.</summary>
    public static IntPtr CaptureHandle() => NativeMethods.GetForegroundWindow();

    public static void Restore(IntPtr hwnd)
    {
        if (hwnd != IntPtr.Zero)
        {
            NativeMethods.SetForegroundWindow(hwnd);
        }
    }
}
