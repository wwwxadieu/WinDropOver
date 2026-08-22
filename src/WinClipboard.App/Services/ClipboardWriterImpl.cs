using System.Collections.Specialized;
using System.Windows;
using System.Windows.Threading;
using WinClipboard.Core.Abstractions;

namespace WinClipboard.App.Services;

/// <summary>IClipboardWriter backed by System.Windows.Clipboard. All calls are marshaled onto the UI thread, the only thread this app touches the OLE clipboard from.</summary>
public sealed class ClipboardWriterImpl : IClipboardWriter
{
    private readonly Dispatcher _uiDispatcher;
    private readonly ClipboardMonitorService _monitor;

    public ClipboardWriterImpl(Dispatcher uiDispatcher, ClipboardMonitorService monitor)
    {
        _uiDispatcher = uiDispatcher;
        _monitor = monitor;
    }

    public Task WriteFilesAsync(IReadOnlyList<string> filePaths, CancellationToken ct = default) =>
        RunOnUiThread(() =>
        {
            var collection = new StringCollection();
            collection.AddRange([.. filePaths]);
            Clipboard.SetFileDropList(collection);
        });

    public Task WriteTextAsync(string text, CancellationToken ct = default) =>
        RunOnUiThread(() => Clipboard.SetText(text));

    private Task RunOnUiThread(Action action)
    {
        var tcs = new TaskCompletionSource();
        _uiDispatcher.Invoke(() =>
        {
            try
            {
                _monitor.SuppressNextChange = true;
                action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                _monitor.SuppressNextChange = false;
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }
}
