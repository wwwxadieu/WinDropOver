using WinClipboard.Core.Abstractions;

namespace WinClipboard.Core.Tests.Fakes;

public sealed class FakeClipboardWriter : IClipboardWriter
{
    public List<string> WrittenText { get; } = [];
    public List<IReadOnlyList<string>> WrittenFileBatches { get; } = [];

    public Task WriteFilesAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default)
    {
        WrittenFileBatches.Add(filePaths);
        return Task.CompletedTask;
    }

    public Task WriteTextAsync(string text, CancellationToken ct = default)
    {
        WrittenText.Add(text);
        return Task.CompletedTask;
    }
}
