using WinClipboard.Core.Models;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>Maps the platform-neutral <see cref="ModifierHoldKey"/> setting to the Win32 virtual-key code the low-level keyboard hook compares against.</summary>
public static class ModifierHoldKeyMap
{
    public static int? ToVirtualKeyCode(ModifierHoldKey key) => key switch
    {
        ModifierHoldKey.None => null,
        ModifierHoldKey.RightShift => NativeConstants.VK_RSHIFT,
        ModifierHoldKey.LeftShift => NativeConstants.VK_LSHIFT,
        ModifierHoldKey.RightControl => NativeConstants.VK_RCONTROL,
        ModifierHoldKey.LeftControl => NativeConstants.VK_LCONTROL,
        ModifierHoldKey.RightAlt => NativeConstants.VK_RMENU,
        ModifierHoldKey.LeftAlt => NativeConstants.VK_LMENU,
        _ => null
    };
}
