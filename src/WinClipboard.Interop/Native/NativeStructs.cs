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
