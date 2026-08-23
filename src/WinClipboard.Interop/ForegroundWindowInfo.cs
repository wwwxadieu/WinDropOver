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

    /// <summary>True when this window is the one the user is currently working in.</summary>
    public static bool IsForeground(IntPtr hwnd) => hwnd != IntPtr.Zero && NativeMethods.GetForegroundWindow() == hwnd;

    /// <summary>
    /// Brings a window to the front and gives it the keyboard, from a process that is not
    /// currently in the foreground.
    ///
    /// A plain SetForegroundWindow is not enough for that. Windows refuses foreground changes
    /// requested by a background process — the rule that stops applications stealing focus while
    /// you type — and refuses them *silently*: the call returns, the window appears, and it never
    /// becomes active. For a panel that closes when it is deactivated, never becoming active
    /// means never deactivating, so it sits on top of everything with no way to dismiss it.
    ///
    /// Attaching this thread's input queue to the current foreground thread makes the two count
    /// as one input context for the duration, which is what lets the request through. The attach
    /// is undone immediately: leaving two processes sharing an input queue would couple their
    /// message loops, so a stall in either becomes a stall in both.
    /// </summary>
    public static void ForceForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == hwnd)
        {
            return;
        }

        var ourThread = NativeMethods.GetCurrentThreadId();
        var theirThread = foreground == IntPtr.Zero ? 0 : NativeMethods.GetWindowThreadProcessId(foreground, out _);
        var attached = theirThread != 0 && theirThread != ourThread
                       && NativeMethods.AttachThreadInput(theirThread, ourThread, true);
        try
        {
            NativeMethods.BringWindowToTop(hwnd);
            NativeMethods.SetForegroundWindow(hwnd);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(theirThread, ourThread, false);
            }
        }
    }
}
