using System.Runtime.InteropServices;
using WinClipboard.Core.Abstractions;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Tells a real drag-and-drop apart from any other press-and-hold.
///
/// Windows publishes no "is a drag running on this desktop" state — the operation belongs to the
/// source application's thread, inside OLE's own modal loop. What it does publish is the evidence
/// a drag leaves lying around on screen, and there are two separate kinds of it, because there
/// are two ways an application can drag.
///
/// An application that just calls DoDragDrop gets OLE's default feedback: the pointer becomes one
/// of ole32's own copy/move/link/no-drop cursors, which is a cursor outside user32's standard set.
/// Explorer, and anything else using the shell's drag image helper, does something different — it
/// puts up a layered window (window class <c>SysDragImage</c>) carrying a picture of what is being
/// dragged, and that window draws its own pointer, so the real cursor is <b>hidden</b> for the
/// duration.
///
/// Checking only the cursor's identity therefore misses precisely the case that matters most:
/// dragging files out of Explorer, where there is no cursor to inspect at all. So this looks for
/// any of three things, and a rubber-band selection or an idly held button produces none of them:
///
///   1. The cursor went from visible to hidden while the button was down — something is drawing
///      its own pointer.
///   2. The cursor changed into something outside user32's standard set — OLE's feedback cursors.
///   3. A visible <c>SysDragImage</c> window exists — the shell drag image itself.
///
/// Ordered by cost, cheapest first, and the window scan is rate-limited: this runs inside a
/// WH_MOUSE_LL callback, where the whole machine's pointer waits on it.
///
/// The standard-cursor handles are re-read periodically rather than cached for the process
/// lifetime, because LoadCursor returns whatever the user's current pointer scheme maps each
/// cursor to, and changing scheme mid-session would otherwise leave every stale handle looking
/// like a drag cursor — turning the guard into the very bug it is here to prevent.
/// </summary>
public sealed class ShellDragProbe : IActiveDragProbe
{
    private static readonly int[] SystemCursorIds =
    [
        NativeConstants.IDC_ARROW, NativeConstants.IDC_IBEAM, NativeConstants.IDC_WAIT,
        NativeConstants.IDC_CROSS, NativeConstants.IDC_UPARROW, NativeConstants.IDC_SIZENWSE,
        NativeConstants.IDC_SIZENESW, NativeConstants.IDC_SIZEWE, NativeConstants.IDC_SIZENS,
        NativeConstants.IDC_SIZEALL, NativeConstants.IDC_NO, NativeConstants.IDC_HAND,
        NativeConstants.IDC_APPSTARTING, NativeConstants.IDC_HELP
    ];

    private static readonly int CursorInfoSize = Marshal.SizeOf<CURSORINFO>();

    /// <summary>A pointer scheme changes about as often as never; re-reading the table on every single click would be pure hook latency for nothing.</summary>
    private const int SystemCursorRefreshIntervalMs = 5_000;

    /// <summary>FindWindow walks the desktop's top-level window list, which is not something to do on every mouse sample of a high-polling-rate mouse.</summary>
    private const int DragImageScanIntervalMs = 40;

    private readonly IntPtr[] _systemCursors = new IntPtr[SystemCursorIds.Length];
    private long _systemCursorsReadAtMs;

    private IntPtr _cursorAtPointerPressed;
    private bool _cursorVisibleAtPointerPressed;

    private long _dragImageScannedAtMs;
    private bool _dragImageSeen;

    // A field rather than a local so the struct is written in place on each call instead of a
    // fresh one being zeroed for every mouse sample the hook sees.
    private CURSORINFO _cursorInfo;

    public ShellDragProbe() => ReadSystemCursorHandles();

    public void OnPointerPressed()
    {
        if (Environment.TickCount64 - _systemCursorsReadAtMs >= SystemCursorRefreshIntervalMs)
        {
            ReadSystemCursorHandles();
        }

        ReadCursor(out _cursorAtPointerPressed, out _cursorVisibleAtPointerPressed);
        _dragImageScannedAtMs = 0;
        _dragImageSeen = false;
    }

    public bool IsDragInProgress()
    {
        ReadCursor(out var current, out var visible);

        // The shell's drag image draws its own pointer and hides the real one. Only meaningful if
        // there was a cursor to begin with — an application that keeps the pointer hidden anyway
        // must not read as permanently dragging.
        if (!visible)
        {
            return _cursorVisibleAtPointerPressed;
        }

        // OLE's own feedback cursors. Unchanged since button-down means nothing has taken the
        // pointer over, whatever it looks like, which covers applications whose idle cursor is
        // custom.
        if (current != IntPtr.Zero && current != _cursorAtPointerPressed && !IsSystemCursor(current))
        {
            return true;
        }

        return HasVisibleDragImageWindow();
    }

    private bool IsSystemCursor(IntPtr cursor)
    {
        for (var i = 0; i < _systemCursors.Length; i++)
        {
            if (_systemCursors[i] == cursor)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The shell's drag image window. Present only while a drag using the drag image helper is
    /// running, and checked for visibility because a hidden one can outlive the drag that made it.
    /// </summary>
    private bool HasVisibleDragImageWindow()
    {
        var now = Environment.TickCount64;
        if (now - _dragImageScannedAtMs < DragImageScanIntervalMs)
        {
            return _dragImageSeen;
        }

        _dragImageScannedAtMs = now;

        var hwnd = NativeMethods.FindWindow("SysDragImage", null);
        _dragImageSeen = hwnd != IntPtr.Zero && NativeMethods.IsWindowVisible(hwnd);
        return _dragImageSeen;
    }

    private void ReadCursor(out IntPtr cursor, out bool visible)
    {
        _cursorInfo.cbSize = CursorInfoSize;
        if (!NativeMethods.GetCursorInfo(ref _cursorInfo))
        {
            cursor = IntPtr.Zero;
            visible = true; // Nothing was learned; do not let a failed call read as "hidden".
            return;
        }

        visible = (_cursorInfo.flags & NativeConstants.CURSOR_SHOWING) != 0;
        cursor = _cursorInfo.hCursor;
    }

    private void ReadSystemCursorHandles()
    {
        for (var i = 0; i < SystemCursorIds.Length; i++)
        {
            _systemCursors[i] = NativeMethods.LoadCursor(IntPtr.Zero, new IntPtr(SystemCursorIds[i]));
        }

        _systemCursorsReadAtMs = Environment.TickCount64;
    }
}
