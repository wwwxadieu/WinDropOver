using System.IO;
using WinClipboard.Core.Models;

namespace WinClipboard.App.ViewModels;

/// <summary>Display-ready wrapper around a <see cref="ClipboardItem"/> for the history overlay's ItemTemplate bindings.</summary>
public sealed class ClipboardItemView
{
    public required ClipboardItem Model { get; init; }

    public string Glyph => Model.Type switch
    {
        ContentType.Text => "\U0001F4DD",
        ContentType.Image => "\U0001F5BC",
        ContentType.File => "\U0001F4C4",
        ContentType.Link => "\U0001F517",
        _ => ""
    };

    public string Preview => Model.Type switch
    {
        ContentType.Text or ContentType.Link => Truncate(Model.TextContent ?? string.Empty, 140),
        ContentType.File => Path.GetFileName(Model.FilePath ?? string.Empty),
        ContentType.Image => "Hình ảnh",
        _ => string.Empty
    };

    public string SourceAndTime => $"{Model.SourceApp ?? "?"} · {RelativeTime(Model.CreatedAt)}";

    public bool IsPinned => Model.IsPinned;

    public static ClipboardItemView From(ClipboardItem item) => new() { Model = item };

    private static string Truncate(string text, int maxLength)
    {
        var singleLine = text.Replace('\n', ' ').Replace('\r', ' ');
        return singleLine.Length <= maxLength ? singleLine : singleLine[..maxLength] + "…";
    }

    private static string RelativeTime(DateTimeOffset createdAt)
    {
        var elapsed = DateTimeOffset.UtcNow - createdAt;
        if (elapsed < TimeSpan.FromMinutes(1)) return "vừa xong";
        if (elapsed < TimeSpan.FromHours(1)) return $"{(int)elapsed.TotalMinutes} phút trước";
        if (elapsed < TimeSpan.FromDays(1)) return $"{(int)elapsed.TotalHours} giờ trước";
        if (elapsed < TimeSpan.FromDays(2)) return "hôm qua";
        return createdAt.LocalDateTime.ToString("dd/MM/yyyy");
    }
}
