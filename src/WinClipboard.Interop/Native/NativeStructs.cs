using System.Runtime.InteropServices;

namespace WinClipboard.Interop.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public IntPtr hwnd;
    public uint message;
    public IntPtr wParam;
    public IntPtr lParam;
    public uint time;
    public POINT pt;
}

/// <summary>Payload delivered to a WH_MOUSE_LL hook procedure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT
{
    public POINT pt;
    public uint mouseData;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

/// <summary>Payload delivered to a WH_KEYBOARD_LL hook procedure.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT
{
    public uint vkCode;
    public uint scanCode;
    public uint flags;
    public uint time;
    public IntPtr dwExtraInfo;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WNDCLASSEX
{
    public int cbSize;
    public int style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
    public IntPtr hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal struct KEYBDINPUT
{
    public ushort wVk;
    public ushort wScan;
    public uint dwFlags;
    public uint time;
    public IntPtr dwExtraInfo;
}

/// <summary>
/// Matches the real Win32 INPUT union's size (40 bytes on x64/ARM64 — both use 8-byte pointers)
/// via an explicit layout, since SendInput rejects calls whose cbSize doesn't match sizeof(INPUT)
/// exactly. Only the keyboard variant is populated; this app never sends synthetic mouse input.
/// Note: this size is wrong for 32-bit x86 (28 bytes there) — WinClipboard only ships x64/ARM64.
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 40)]
internal struct INPUT
{
    [FieldOffset(0)] public uint type;
    [FieldOffset(8)] public KEYBDINPUT ki;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MONITORINFO
{
    public int cbSize;
    public RECT rcMonitor;
    public RECT rcWork;
    public uint dwFlags;
}

/// <summary>
/// BITMAPINFO: the header followed by room for a colour table.
///
/// The table is carried even though these calls ask for 32-bit BI_RGB, where documentation says
/// it goes unwritten. GDI receives a bare pointer and decides for itself how far past the header
/// to write — for 32bpp it may lay down channel masks — and this struct is a managed object, so
/// a single byte written past its end corrupts the managed heap. The damage does not surface at
/// the call; it surfaces later, at whatever unrelated allocation lands on the wreckage, as a
/// process that vanishes without an exception to catch or a stack to log.
///
/// A kilobyte of slack on a stack-allocated struct is not worth reasoning about; being wrong
/// about how much GDI writes is. Note that biSize must still be the size of the *header* alone,
/// hence BITMAPINFOHEADER_SIZE rather than Marshal.SizeOf on this type.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct BITMAPINFO
{
    public uint biSize;
    public int biWidth;
    /// <summary>Negative for a top-down bitmap — the row order WPF's BitmapSource expects, which saves flipping every row by hand.</summary>
    public int biHeight;
    public ushort biPlanes;
    public ushort biBitCount;
    public uint biCompression;
    public uint biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public uint biClrUsed;
    public uint biClrImportant;
    /// <summary>The 256-entry colour table a BITMAPINFO is allowed to carry. Never read here; it exists so GDI cannot write outside the struct.</summary>
    public fixed uint bmiColors[256];
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct SHFILEINFO
{
    public IntPtr hIcon;
    public int iIcon;
    public uint dwAttributes;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
}

/// <summary>
/// SHFILEOPSTRUCT. CharSet.Unicode is load-bearing: the ANSI entry point truncates any path the
/// user's language can spell but Windows-1252 cannot, which for this app's users is most of them.
/// </summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
internal struct SHFILEOPSTRUCT
{
    public IntPtr hwnd;
    public uint wFunc;
    [MarshalAs(UnmanagedType.LPWStr)] public string pFrom;
    [MarshalAs(UnmanagedType.LPWStr)] public string? pTo;
    public ushort fFlags;
    [MarshalAs(UnmanagedType.Bool)] public bool fAnyOperationsAborted;
    public IntPtr hNameMappings;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszProgressTitle;
}

