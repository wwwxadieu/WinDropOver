using System.Runtime.InteropServices;
using WinClipboard.Core.Abstractions;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Tells a real drag-and-drop apart from any other press-and-hold, by watching the mouse cursor.
///
/// Windows offers no way to ask "is a drag running somewhere on this desktop?" — the operation
/// belongs to the source application's thread, inside OLE's own modal loop, and nothing about it
/// is published system-wide. The cursor, however, is: while DoDragDrop runs, the cursor is
/// whatever the operation set it to — one of OLE's own copy/move/link/no-drop cursors, or the
/// picture-of-the-file cursor the shell's drag image helper builds — and none of those is a
/// cursor from user32's standard set. A rubber-band selection, a scrollbar drag, or a hand
/// resting on the button all leave the ordinary arrow in place.
///
/// So the test is: the cursor changed after the button went down, and what it changed to is not
/// one of the system's own cursors. Two GetCursorInfo calls and a walk of a 14-entry array, which
/// is what running inside a WH_MOUSE_LL callback allows.
///
/// The standard-cursor handles are re-read periodically rather than cached for the process
/// lifetime, because LoadCursor returns whatever the user's current pointer scheme maps each
/// cursor to, and changing scheme mid-session would otherwise leave every stale handle looking
/// like a drag cursor — turning the guard into the very bug it is here to prevent.
/// </summary>
public sealed class OleDragCursorProbe : IActiveDragProbe
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

    private readonly IntPtr[] _systemCursors = new IntPtr[SystemCursorIds.Length];
    private IntPtr _cursorAtPointerPressed;
    private long _systemCursorsReadAtMs;

    // A field rather than a local so the struct is written in place on each call instead of a
    // fresh one being zeroed for every mouse sample the hook sees.
    private CURSORINFO _cursorInfo;

    public OleDragCursorProbe() => ReadSystemCursorHandles();

    public void OnPointerPressed()
    {
        if (Environment.TickCount64 - _systemCursorsReadAtMs >= SystemCursorRefreshIntervalMs)
        {
            ReadSystemCursorHandles();
        }

        _cursorAtPointerPressed = ReadCurrentCursor();
    }

    public bool IsDragInProgress()
    {
        var current = ReadCurrentCursor();

        // Unchanged since the button went down means nothing has taken over the pointer, whatever
        // it happens to look like — which also covers applications that use a custom cursor as
        // their idle one.
        if (current == IntPtr.Zero || current == _cursorAtPointerPressed)
        {
            return false;
        }

        for (var i = 0; i < _systemCursors.Length; i++)
        {
            if (_systemCursors[i] == current)
            {
                return false;
            }
        }

        return true;
    }

    private void ReadSystemCursorHandles()
    {
        for (var i = 0; i < SystemCursorIds.Length; i++)
        {
            _systemCursors[i] = NativeMethods.LoadCursor(IntPtr.Zero, new IntPtr(SystemCursorIds[i]));
        }

        _systemCursorsReadAtMs = Environment.TickCount64;
    }

    private IntPtr ReadCurrentCursor()
    {
        _cursorInfo.cbSize = CursorInfoSize;
        if (!NativeMethods.GetCursorInfo(ref _cursorInfo))
        {
            return IntPtr.Zero;
        }

        return (_cursorInfo.flags & NativeConstants.CURSOR_SHOWING) == 0
            ? IntPtr.Zero
            : _cursorInfo.hCursor;
    }
}
