using System.IO;
using WinClipboard.Core.Models;

namespace WinClipboard.App.ViewModels;

/// <summary>Display-ready wrapper around a <see cref="ShelfItem"/> for the shelf panel's item list.</summary>
public sealed class ShelfItemView
{
    public required ShelfItem Model { get; init; }

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

    private static string Truncate(string text, int maxLength)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }
}
