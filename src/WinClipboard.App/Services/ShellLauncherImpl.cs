using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using WinClipboard.Core.Abstractions;

namespace WinClipboard.App.Services;

public sealed class ShellLauncherImpl : IShellLauncher
{
    private readonly Dispatcher _uiDispatcher;
    private readonly Func<IntPtr> _getOwnerWindowHandle;

    public ShellLauncherImpl(Dispatcher uiDispatcher, Func<IntPtr> getOwnerWindowHandle)
    {
        _uiDispatcher = uiDispatcher;
        _getOwnerWindowHandle = getOwnerWindowHandle;
    }

    public Task OpenWithDialogAsync(string filePath, CancellationToken ct = default)
    {
        // The classic, still-supported rundll32 trick for showing the shell's "Open with" picker
        // without writing a SHOpenWithDialog COM wrapper for a "nice to have" action.
        Process.Start(new ProcessStartInfo
        {
            FileName = "rundll32.exe",
            Arguments = $"shell32.dll,OpenAs_RunDLL \"{filePath}\"",
            UseShellExecute = true
        });
        return Task.CompletedTask;
    }

    public Task ShareAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource();
        _uiDispatcher.InvokeAsync(async () =>
        {
            try
            {
                await ShareUI.ShowAsync(_getOwnerWindowHandle(), filePaths);
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    /// <summary>
    /// Off the UI thread: SHFileOperation is synchronous and talks to the shell, which for a
    /// large folder or a file on a network share can take seconds. Blocking the dispatcher here
    /// would freeze the card mid-drop.
    /// </summary>
    public Task RecycleAsync(string path, CancellationToken ct = default) => Task.Run(() =>
    {
        if (!WinClipboard.Interop.RecycleBin.Send(path))
        {
            // The engine turns a throw into that item's failure message; returning quietly would
            // report a delete that never happened as a success.
            throw new IOException($"Không đưa được vào thùng rác: {path}");
        }
    }, ct);
}
