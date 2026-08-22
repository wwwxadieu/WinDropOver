namespace WinClipboard.Core.Abstractions;

/// <summary>Invokes Windows Shell verbs. Implemented against Win32/Shell in WinClipboard.App; swappable for tests.</summary>
public interface IShellLauncher
{
    /// <summary>Shows the Windows "Open with" picker for a single file.</summary>
    Task OpenWithDialogAsync(string filePath, CancellationToken ct = default);

    /// <summary>Shows the Windows share flyout for one or more files.</summary>
    Task ShareAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default);
}
