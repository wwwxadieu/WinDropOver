namespace WinClipboard.Core.Abstractions;

/// <summary>Invokes Windows Shell verbs. Implemented against Win32/Shell in WinClipboard.App; swappable for tests.</summary>
public interface IShellLauncher
{
    /// <summary>Shows the Windows "Open with" picker for a single file.</summary>
    Task OpenWithDialogAsync(string filePath, CancellationToken ct = default);

    /// <summary>Shows the Windows share flyout for one or more files.</summary>
    Task ShareAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default);

    /// <summary>
    /// Sends a file or folder to the Recycle Bin.
    ///
    /// A shell operation rather than a File.Delete, because the Recycle Bin is not a directory
    /// you can move something into — the shell maintains the record that makes the deletion
    /// undoable, and only its own delete verb writes that record.
    /// </summary>
    Task RecycleAsync(string path, CancellationToken ct = default);
}
