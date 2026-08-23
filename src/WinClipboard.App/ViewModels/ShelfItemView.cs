using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WinClipboard.App.Services;
using WinClipboard.Core.Models;

namespace WinClipboard.App.ViewModels;

/// <summary>Display-ready wrapper around a <see cref="ShelfItem"/> for the shelf card and panel.</summary>
public sealed class ShelfItemView
{
    /// <summary>
    /// What counts as an image, and so gets a preview instead of a generic page glyph. Extension
    /// rather than content sniffing: this runs for every item every time the shelf reloads, and
    /// opening each file to inspect its header would be real I/O for a guess that the decoder is
    /// about to make properly anyway — if the extension lies, decoding simply fails and the item
    /// falls back to the glyph.
    /// </summary>
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".tif", ".tiff", ".ico"
    };

    public required ShelfItem Model { get; init; }

    /// <summary>Null when the item is not an image, or when decoding it failed.</summary>
    public ImageSource? Thumbnail { get; init; }

    /// <summary>
    /// The icon Explorer shows for this file. Null for text and links, which are not files, and
    /// for a file the shell had no icon for.
    /// </summary>
    public ImageSource? Icon { get; init; }

    /// <summary>
    /// The second line under the name: what the item is and how big it is.
    ///
    /// A filename alone does not answer the questions a collecting shelf raises — is this the
    /// 4 MB export or the 400 KB draft, is this the folder or the zip of it — and the shelf is
    /// narrow enough that names are usually truncated before the extension is even visible.
    /// </summary>
    public string? Details { get; init; }

    public Visibility DetailsVisibility => string.IsNullOrEmpty(Details) ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>
    /// Everything known about the item, for hovering: name, what it is and how big, and — for a
    /// file — where it actually lives. The path is the one thing no row has room to show and the
    /// thing most worth asking about, since two files collected from different folders can have
    /// the same name and the shelf gives no other way to tell them apart.
    /// </summary>
    public string ToolTipText
    {
        get
        {
            var lines = new List<string> { DisplayName };
            if (!string.IsNullOrEmpty(Details))
            {
                lines.Add(Details);
            }
            if (Model.Type == ShelfItemType.File && !string.IsNullOrEmpty(Model.FilePath))
            {
                lines.Add(Model.FilePath);
            }
            return string.Join("\n", lines);
        }
    }

    public bool IsImage =>
        Model.Type == ShelfItemType.File &&
        Model.FilePath is not null &&
        ImageExtensions.Contains(Path.GetExtension(Model.FilePath));

    // Three ways an item can be shown, in order of how much they say about it: a preview of the
    // file itself, the icon of the program that owns it, then a shape standing in for its kind.
    // Each falls through to the next only when the one above is unavailable.
    public Visibility ThumbnailVisibility => Thumbnail is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility IconVisibility => Thumbnail is null && Icon is not null ? Visibility.Visible : Visibility.Collapsed;

    public Visibility GlyphVisibility => Thumbnail is null && Icon is null ? Visibility.Visible : Visibility.Collapsed;

    public string Glyph => Model.Type switch
    {
        ShelfItemType.File => "\U0001F4C4",
        ShelfItemType.Text => "\U0001F4DD",
        ShelfItemType.Link => "\U0001F517",
        _ => ""
    };

    public string DisplayName => Model.Type switch
    {
        ShelfItemType.File => Path.GetFileName(Model.FilePath ?? string.Empty),
        ShelfItemType.Text or ShelfItemType.Link => Truncate(Model.TextContent ?? string.Empty, 60),
        _ => string.Empty
    };

    public static ShelfItemView From(ShelfItem item) => new() { Model = item };

    /// <summary>
    /// Builds the views for a shelf, decoding image previews off the UI thread.
    ///
    /// Thumbnails are loaded once here rather than bound and filled in later, which is what keeps
    /// this a plain immutable object with no change notification: the list is only ever handed to
    /// the UI complete.
    /// </summary>
    public static async Task<List<ShelfItemView>> BuildAsync(IReadOnlyList<ShelfItem> items, int thumbnailPixelWidth)
    {
        // One hop to the pool for every filesystem question the list raises, rather than one per
        // item: each answer is a metadata call that is nothing locally and can stall for seconds
        // on a network share or a drive that has been unplugged.
        var details = await Task.Run(() => items.Select(Describe).ToList());

        var views = new List<ShelfItemView>(items.Count);
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var view = From(item);
            if (view.IsImage)
            {
                views.Add(new ShelfItemView
                {
                    Model = item,
                    Details = details[i],
                    Thumbnail = await LoadThumbnailAsync(item.FilePath!, thumbnailPixelWidth)
                });
            }
            else if (item.Type == ShelfItemType.File && item.FilePath is not null)
            {
                views.Add(new ShelfItemView
                {
                    Model = item,
                    Details = details[i],
                    Icon = await ShellIconCache.GetAsync(item.FilePath)
                });
            }
            else
            {
                views.Add(new ShelfItemView { Model = item, Details = details[i] });
            }
        }
        return views;
    }

    private static Task<ImageSource?> LoadThumbnailAsync(string path, int pixelWidth) => Task.Run<ImageSource?>(() =>
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            // Decode straight to display size: the shelf may hold a folder's worth of camera
            // images, and decoding those at full resolution to draw them at 84px would cost
            // hundreds of megabytes for pixels nobody sees.
            bitmap.DecodePixelWidth = pixelWidth;
            // OnLoad reads the file during EndInit and closes it, so the shelf never keeps a
            // handle on a file the user may want to move, rename or delete.
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            // Frozen so it can cross back to the UI thread.
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            // Missing, unreadable, not actually an image despite its name — the item still
            // belongs on the shelf, it just shows the generic glyph.
            return null;
        }
    });

    private static string? Describe(ShelfItem item) => item.Type switch
    {
        ShelfItemType.File when item.FilePath is not null => DescribeFile(item.FilePath),
        ShelfItemType.Text when item.TextContent is not null => $"Văn bản · {item.TextContent.Length:N0} ký tự",
        ShelfItemType.Link when item.TextContent is not null => DescribeLink(item.TextContent),
        _ => null
    };

    private static string? DescribeFile(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                return "Thư mục";
            }

            var kind = Path.GetExtension(path).TrimStart('.').ToUpperInvariant();
            if (kind.Length == 0)
            {
                kind = "Tệp";
            }

            var info = new FileInfo(path);
            // Worth saying out loud rather than leaving as a size that never appears: the shelf
            // holds paths, so a file moved or deleted after it was collected still has a row here,
            // and this is the only place that would tell the user why dragging it back out fails.
            return info.Exists ? $"{kind} · {FormatSize(info.Length)}" : $"{kind} · không tìm thấy";
        }
        catch
        {
            // An unreadable path is still a row on the shelf; it just has nothing to say about
            // itself. A permission error here must not cost the whole list.
            return null;
        }
    }

    private static string DescribeLink(string text) =>
        Uri.TryCreate(text, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? $"Liên kết · {uri.Host}"
            : "Liên kết";

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        // Bytes are whole things; a decimal place on them reads as a mistake.
        return unit == 0 ? $"{bytes} B" : $"{size:0.#} {units[unit]}";
    }

    private static string Truncate(string text, int maxLength)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }
}
