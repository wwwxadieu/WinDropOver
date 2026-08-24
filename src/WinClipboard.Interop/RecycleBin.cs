using System.Runtime.InteropServices;
using WinClipboard.Interop.Native;

namespace WinClipboard.Interop;

/// <summary>
/// Sends files and folders to the Recycle Bin.
///
/// Not File.Delete, and not a move: the Recycle Bin is not a folder you can put something into.
/// It is a shell facility, and the record that makes a deletion undoable — where the file came
/// from, what it was called, when it went — is written by the shell's own delete verb and by
/// nothing else. Move the file into $Recycle.Bin by hand and you get a file nobody can restore.
/// </summary>
public static class RecycleBin
{
    /// <summary>Returns false when the shell declined or the operation was aborted; the caller reports that as the item's failure.</summary>
    public static bool Send(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // SHFileOperation takes a list, not a path: the buffer is a run of null-terminated
        // strings closed by one more null. One entry still needs both terminators, and a plain
        // marshalled string would supply only the first — leaving the shell reading past the end
        // of the buffer for a filename that is not there.
        var from = path + '\0' + '\0';

        var operation = new SHFILEOPSTRUCT
        {
            wFunc = NativeConstants.FO_DELETE,
            pFrom = from,
            fFlags = NativeConstants.FOF_ALLOWUNDO
                     | NativeConstants.FOF_NOCONFIRMATION
                     | NativeConstants.FOF_NOERRORUI
                     | NativeConstants.FOF_SILENT
        };

        try
        {
            // Zero is success. fAnyOperationsAborted covers the case where the shell returns
            // success having quietly done nothing — a file the user declined to delete, or one
            // the bin refused to take.
            return NativeMethods.SHFileOperation(ref operation) == 0 && !operation.fAnyOperationsAborted;
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            return false;
        }
    }
}
