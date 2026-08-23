using System.IO;
using System.Text;
using System.Windows;
using WinClipboard.Core.Models;
using WinClipboard.Core.Utils;

namespace WinClipboard.App.Services;

/// <summary>
/// Turns a dropped <see cref="IDataObject"/> into shelf items.
///
/// Dropover accepts "files, folders, documents, images, URLs, and text snippets"; this app
/// originally only read <see cref="DataFormats.FileDrop"/>, so a dragged link or selected text
/// was silently discarded even though the data model already had ShelfItemType.Text and .Link.
/// </summary>
internal static class ShelfDropReader
{
    // Browsers advertise a dragged link under these shell formats rather than as plain text.
    private const string UrlFormatUnicode = "UniformResourceLocatorW";
    private const string UrlFormatAnsi = "UniformResourceLocator";

    /// <summary>True if this drop carries anything the shelf can hold — used to show the right drop cursor.</summary>
    public static bool CanRead(IDataObject data) =>
        data.GetDataPresent(DataFormats.FileDrop)
        || data.GetDataPresent(UrlFormatUnicode)
        || data.GetDataPresent(UrlFormatAnsi)
        || data.GetDataPresent(DataFormats.UnicodeText)
        || data.GetDataPresent(DataFormats.Text);

    /// <summary>
    /// Reads every item the drop carries. <paramref name="firstSortOrder"/> is where numbering
    /// starts, so items dropped later sit after the ones already on the shelf instead of
    /// restarting at zero and scrambling the order.
    /// </summary>
    public static List<ShelfItem> Read(IDataObject data, long shelfId, int firstSortOrder)
    {
        var items = new List<ShelfItem>();
        var now = DateTimeOffset.UtcNow;
        var order = firstSortOrder;

        void Add(ShelfItemType type, string? filePath, string? text)
        {
            items.Add(new ShelfItem
            {
                ShelfId = shelfId,
                Type = type,
                FilePath = filePath,
                TextContent = text,
                AddedAt = now,
                SortOrder = order++
            });
        }

        // Files and folders alike arrive as FileDrop paths.
        if (data.GetDataPresent(DataFormats.FileDrop) &&
            data.GetData(DataFormats.FileDrop) is string[] paths)
        {
            foreach (var path in paths.Where(p => !string.IsNullOrWhiteSpace(p)))
            {
                Add(ShelfItemType.File, path, null);
            }
        }

        // A link dragged out of a browser. Checked before plain text because browsers offer both,
        // and the URL format is the more specific answer.
        var url = ReadUrl(data);
        if (url is not null)
        {
            Add(ShelfItemType.Link, null, url);
        }
        else if (data.GetDataPresent(DataFormats.UnicodeText) || data.GetDataPresent(DataFormats.Text))
        {
            var text = data.GetData(DataFormats.UnicodeText) as string
                       ?? data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(text))
            {
                Add(LinkDetector.IsLink(text) ? ShelfItemType.Link : ShelfItemType.Text, null, text);
            }
        }

        return items;
    }

    private static string? ReadUrl(IDataObject data)
    {
        foreach (var (format, encoding) in new[]
                 {
                     (UrlFormatUnicode, Encoding.Unicode),
                     (UrlFormatAnsi, Encoding.Default)
                 })
        {
            if (!data.GetDataPresent(format))
            {
                continue;
            }

            var raw = data.GetData(format);
            var value = raw switch
            {
                string s => s,
                // The shell hands these over as a null-terminated byte stream, not a string.
                MemoryStream stream => encoding.GetString(stream.ToArray()).TrimEnd('\0'),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}
