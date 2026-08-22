using System.Collections.Specialized;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using WinClipboard.Core.Models;
using WinClipboard.Interop;

namespace WinClipboard.App.Services;

/// <summary>
/// "Dán lại một mục bằng cú click" (plan 2, MVP): puts the chosen history item back on the
/// clipboard, restores focus to whatever window was active before the overlay opened, then
/// synthesizes Ctrl+V so it lands exactly where the user was typing.
/// </summary>
public sealed class PasteService
{
    private readonly ClipboardMonitorService _monitor;

    public PasteService(ClipboardMonitorService monitor)
    {
        _monitor = monitor;
    }

    public async Task PasteAsync(ClipboardItem item, IntPtr targetWindowHandle)
    {
        _monitor.SuppressNextChange = true;
        try
        {
            switch (item.Type)
            {
                case ContentType.Text:
                case ContentType.Link:
                    Clipboard.SetText(item.TextContent ?? string.Empty);
                    break;

                case ContentType.File:
                    if (item.FilePath is not null)
                    {
                        var files = new StringCollection { item.FilePath };
                        Clipboard.SetFileDropList(files);
                    }
                    break;

                case ContentType.Image:
                    if (item.ThumbnailPath is not null && File.Exists(item.ThumbnailPath))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.UriSource = new Uri(item.ThumbnailPath);
                        bitmap.EndInit();
                        Clipboard.SetImage(bitmap);
                    }
                    break;
            }
        }
        catch
        {
            _monitor.SuppressNextChange = false;
            throw;
        }

        ForegroundWindowInfo.Restore(targetWindowHandle);
        // Give the target window a moment to actually take focus before synthesizing the
        // keystroke, otherwise Ctrl+V can race SetForegroundWindow and land on our own window.
        await Task.Delay(50);
        KeyboardSimulator.SendCtrlV();
    }
}
