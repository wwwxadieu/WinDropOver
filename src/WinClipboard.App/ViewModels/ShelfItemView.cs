using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

    public bool IsImage =>
        Model.Type == ShelfItemType.File &&
        Model.FilePath is not null &&
        ImageExtensions.Contains(Path.GetExtension(Model.FilePath));

    public Visibility ThumbnailVisibility => Thumbnail is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility GlyphVisibility => Thumbnail is null ? Visibility.Visible : Visibility.Collapsed;

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
        var views = new List<ShelfItemView>(items.Count);
        foreach (var item in items)
        {
            var view = From(item);
            views.Add(view.IsImage
                ? new ShelfItemView { Model = item, Thumbnail = await LoadThumbnailAsync(item.FilePath!, thumbnailPixelWidth) }
                : view);
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

    private static string Truncate(string text, int maxLength)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }
}
