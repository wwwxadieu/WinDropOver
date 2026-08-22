using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;
using WinClipboard.Core.Utils;
using WinClipboard.Interop;

namespace WinClipboard.App.Services;

/// <summary>
/// Reacts to WM_CLIPBOARDUPDATE (relayed from <see cref="Win32MessageWindow"/>) by reading the
/// current clipboard contents and recording them (plan 3.2 Clipboard Monitor / use case A).
/// Must run its clipboard reads on the WPF UI thread — <see cref="System.Windows.Clipboard"/>
/// only works reliably from an STA thread that owns a window, which the UI thread is and the
/// Win32 message-loop thread, despite also being STA, is not (it never creates a WPF window).
/// </summary>
public sealed class ClipboardMonitorService
{
    private readonly IClipboardRepository _repository;
    private readonly string _thumbnailDirectory;

    /// <summary>
    /// Set by <see cref="ClipboardWriterImpl"/> immediately before it writes to the clipboard
    /// (e.g. "copy to clipboard" quick action, or the history overlay re-posting an item to
    /// paste it) so this service's own write doesn't re-enter itself as a "new" copy — the
    /// loop-avoidance the plan calls out for the Clipboard Monitor.
    /// </summary>
    public bool SuppressNextChange { get; set; }

    public event EventHandler<ClipboardItem>? ItemAdded;

    public ClipboardMonitorService(IClipboardRepository repository, string thumbnailDirectory)
    {
        _repository = repository;
        _thumbnailDirectory = thumbnailDirectory;
        Directory.CreateDirectory(_thumbnailDirectory);
    }

    /// <summary>Call on the UI thread in response to Win32MessageWindow.ClipboardChanged.</summary>
    public async void OnClipboardChanged()
    {
        if (SuppressNextChange)
        {
            SuppressNextChange = false;
            return;
        }

        try
        {
            var item = ReadClipboard();
            if (item is null)
            {
                return;
            }

            var existing = await _repository.FindByHashAsync(item.HashDedup);
            if (existing is not null)
            {
                return; // Same content already at (or near) the top of history — nothing to add.
            }

            await _repository.AddAsync(item);
            ItemAdded?.Invoke(this, item);
        }
        catch (Exception)
        {
            // A transient clipboard read failure (another app briefly holds the clipboard open)
            // should never take the monitor down; the next change will simply be picked up.
        }
    }

    private ClipboardItem? ReadClipboard()
    {
        var sourceApp = ForegroundWindowInfo.GetForegroundProcessName();
        var now = DateTimeOffset.UtcNow;

        if (Clipboard.ContainsFileDropList())
        {
            var files = Clipboard.GetFileDropList();
            // One row per file: the data model (plan 3.5) gives ClipboardItem a single FilePath,
            // so a multi-file copy is recorded as that many independent, individually-dedupable rows.
            foreach (var path in files)
            {
                if (string.IsNullOrEmpty(path))
                {
                    continue;
                }
                return new ClipboardItem
                {
                    Type = ContentType.File,
                    CreatedAt = now,
                    FilePath = path,
                    SourceApp = sourceApp,
                    HashDedup = ContentHasher.HashPath(ContentType.File, path)
                };
            }
            return null;
        }

        if (Clipboard.ContainsImage())
        {
            var image = Clipboard.GetImage();
            if (image is null)
            {
                return null;
            }
            var (thumbnailPath, bytes) = SaveThumbnail(image);
            return new ClipboardItem
            {
                Type = ContentType.Image,
                CreatedAt = now,
                ThumbnailPath = thumbnailPath,
                SourceApp = sourceApp,
                HashDedup = ContentHasher.HashBytes(ContentType.Image, bytes)
            };
        }

        if (Clipboard.ContainsText())
        {
            var text = Clipboard.GetText();
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }
            var isLink = Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri)
                         && (uri.Scheme is "http" or "https" or "ftp" or "mailto");
            var type = isLink ? ContentType.Link : ContentType.Text;
            return new ClipboardItem
            {
                Type = type,
                CreatedAt = now,
                TextContent = text,
                SourceApp = sourceApp,
                HashDedup = ContentHasher.HashText(text)
            };
        }

        return null;
    }

    private (string path, byte[] bytes) SaveThumbnail(BitmapSource image)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        var bytes = stream.ToArray();

        var fileName = $"{Guid.NewGuid():N}.png";
        var path = Path.Combine(_thumbnailDirectory, fileName);
        File.WriteAllBytes(path, bytes);
        return (path, bytes);
    }
}
