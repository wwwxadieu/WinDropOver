using WinClipboard.Core.Abstractions;

namespace WinClipboard.Core.Tests.Fakes;

public sealed class FakeShellLauncher : IShellLauncher
{
    public List<string> OpenWithCalls { get; } = [];
    public List<IReadOnlyList<string>> ShareCalls { get; } = [];

    public Task OpenWithDialogAsync(string filePath, CancellationToken ct = default)
    {
        OpenWithCalls.Add(filePath);
        return Task.CompletedTask;
    }

    public Task ShareAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default)
    {
        ShareCalls.Add(filePaths);
        return Task.CompletedTask;
    }
}
