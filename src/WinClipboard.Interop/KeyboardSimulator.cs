using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Simulates Ctrl+V via SendInput. The history overlay uses this to "paste" a selected item:
/// it puts the item back on the clipboard, restores focus to whatever window was active before
/// the overlay opened, then synthesizes the keystroke so the target app receives an ordinary paste.
/// </summary>
public static class KeyboardSimulator
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventFKeyUp = 0x0002;
    private const ushort VkControl = 0x11;
    private const ushort VkV = 0x56;

    public static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyDown(VkControl),
            KeyDown(VkV),
            KeyUp(VkV),
            KeyUp(VkControl)
        };

        NativeMethods.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyDown(ushort vk) => new()
    {
        type = InputKeyboard,
        ki = new KEYBDINPUT { wVk = vk, dwFlags = 0 }
    };

    private static INPUT KeyUp(ushort vk) => new()
    {
        type = InputKeyboard,
        ki = new KEYBDINPUT { wVk = vk, dwFlags = KeyEventFKeyUp }
    };
}
