using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// The icon Explorer shows for a file, straight from the shell.
///
/// The shelf used to draw the same page emoji next to every non-image item, which made a column
/// of files unreadable at a glance: a spreadsheet, an installer and a PDF looked identical, and
/// the only thing distinguishing them was a filename that is usually truncated. The icon is what
/// the user already recognises — it is the same one they were looking at in the folder they
/// dragged the file out of.
///
/// Returns a raw HICON that the caller owns and must pass back to <see cref="Destroy"/>. Icons
/// are GDI handles from a per-process pool of ten thousand; a shelf that leaked one per reload
/// would exhaust it and start failing to draw anything at all.
/// </summary>
public static class ShellIcon
{
    /// <summary>The system large icon — 32px at 100% scale. The shelf draws these at 26px, so a larger source only costs memory; the grid layout is reserved for image thumbnails and never shows one of these.</summary>
    public static IntPtr Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return IntPtr.Zero;
        }

        // Ask about the file itself first. That is the only way to get an executable's own
        // artwork, or the picture a shortcut points at — for those the icon belongs to the file,
        // not to its type.
        var icon = Query(path, NativeConstants.SHGFI_ICON | NativeConstants.SHGFI_LARGEICON, 0);
        if (icon != IntPtr.Zero)
        {
            return icon;
        }

        // The file is gone, or on a drive that is not there any more. Ask what the *type* looks
        // like instead: USEFILEATTRIBUTES tells the shell to answer from the extension alone and
        // never touch the disk. A moved file still showing a spreadsheet icon is far better than
        // a blank page — and it is the same answer the folder would have given.
        return Query(path, NativeConstants.SHGFI_ICON | NativeConstants.SHGFI_LARGEICON
                           | NativeConstants.SHGFI_USEFILEATTRIBUTES, NativeConstants.FILE_ATTRIBUTE_NORMAL);
    }

    private static IntPtr Query(string path, uint flags, uint fileAttributes)
    {
        try
        {
            var info = new SHFILEINFO();
            var result = NativeMethods.SHGetFileInfo(
                path, fileAttributes, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);

            return result == IntPtr.Zero ? IntPtr.Zero : info.hIcon;
        }
        catch
        {
            return IntPtr.Zero;
        }
    }

    public static void Destroy(IntPtr hIcon)
    {
        if (hIcon != IntPtr.Zero)
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }
}
