namespace WinClipboard.Core.Abstractions;

/// <summary>Writes to the OS clipboard. Implemented against Win32 in WinClipboard.App; swappable for tests.</summary>
public interface IClipboardWriter
{
    Task WriteFilesAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default);

    Task WriteTextAsync(string text, CancellationToken ct = default);
}
