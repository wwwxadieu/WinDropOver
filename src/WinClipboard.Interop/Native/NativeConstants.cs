namespace WinClipboard.Interop.Native;

/// <summary>Win32 constants used by the hooks, hotkey, clipboard-listener and layered-window helpers.</summary>
internal static class NativeConstants
{
    // SetWindowsHookEx hook types
    public const int WH_KEYBOARD_LL = 13;
    public const int WH_MOUSE_LL = 14;

    // Low-level mouse hook messages (wParam)
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP = 0x0205;

    // Low-level keyboard hook messages (wParam)
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    // Window messages the message-only window cares about
    public const int WM_HOTKEY = 0x0312;
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const int WM_DESTROY = 0x0002;
    public const int WM_CLOSE = 0x0010;
    public const int WM_TIMER = 0x0113;
    public const int WM_NCDESTROY = 0x0082;

    // RegisterHotKey modifier flags
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
    public const uint MOD_NOREPEAT = 0x4000;

    // Extended window styles
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_LAYERED = 0x00080000;
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_TRANSPARENT = 0x00000020;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;

    // SetLayeredWindowAttributes flags
    public const uint LWA_COLORKEY = 0x1;
    public const uint LWA_ALPHA = 0x2;

    // SetWindowPos
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    public const uint MONITOR_DEFAULTTONEAREST = 0x0002;

    // GetCursorInfo
    public const int CURSOR_SHOWING = 0x0001;

    // The complete set of cursors LoadCursor hands out for hInstance = NULL. Used to recognise
    // "the pointer is still one of the system's own" - i.e. nothing has taken it over for a drag.
    public const int IDC_ARROW = 32512;
    public const int IDC_IBEAM = 32513;
    public const int IDC_WAIT = 32514;
    public const int IDC_CROSS = 32515;
    public const int IDC_UPARROW = 32516;
    public const int IDC_SIZENWSE = 32642;
    public const int IDC_SIZENESW = 32643;
    public const int IDC_SIZEWE = 32644;
    public const int IDC_SIZENS = 32645;
    public const int IDC_SIZEALL = 32646;
    public const int IDC_NO = 32648;
    public const int IDC_HAND = 32649;
    public const int IDC_APPSTARTING = 32650;
    public const int IDC_HELP = 32651;

    // GetSystemMetrics
    public const int SM_CXVIRTUALSCREEN = 78;
    public const int SM_CYVIRTUALSCREEN = 79;
    public const int SM_XVIRTUALSCREEN = 76;
    public const int SM_YVIRTUALSCREEN = 77;

    // Virtual key codes for common modifier hold-to-trigger hotkeys
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_LSHIFT = 0xA0;
    public const int VK_RSHIFT = 0xA1;
    public const int VK_LCONTROL = 0xA2;
    public const int VK_RCONTROL = 0xA3;
    public const int VK_LMENU = 0xA4; // Alt
    public const int VK_RMENU = 0xA5;

    // BitBlt / GetDIBits
    public const uint SRCCOPY = 0x00CC0020;
    /// <summary>Includes layered windows in the copy, which is most of what floats above the desktop these days.</summary>
    public const uint CAPTUREBLT = 0x40000000;
    public const uint DIB_RGB_COLORS = 0;
    /// <summary>sizeof(BITMAPINFOHEADER). Fixed by the API, and not the size of our BITMAPINFO struct, which also carries the colour table.</summary>
    public const uint BITMAPINFOHEADER_SIZE = 40;
    public const uint BI_RGB = 0;

    // SHGetFileInfo
    public const uint SHGFI_ICON = 0x000000100;
    public const uint SHGFI_LARGEICON = 0x000000000;
    public const uint SHGFI_SMALLICON = 0x000000001;
    public const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

    // SHFileOperation
    public const uint FO_DELETE = 0x0003;
    /// <summary>The flag that makes the deletion recoverable — without it the shell deletes outright.</summary>
    public const ushort FOF_ALLOWUNDO = 0x0040;
    public const ushort FOF_NOCONFIRMATION = 0x0010;
    public const ushort FOF_SILENT = 0x0004;
    public const ushort FOF_NOERRORUI = 0x0400;
}
