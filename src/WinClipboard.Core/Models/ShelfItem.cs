namespace WinClipboard.Core.Models;

/// <summary>One item dropped into a <see cref="Shelf"/>.</summary>
public sealed class ShelfItem
{
    public long Id { get; set; }
    public required long ShelfId { get; set; }
    public required ShelfItemType Type { get; set; }
    public string? FilePath { get; set; }
    public string? TextContent { get; set; }
    public string? ThumbnailPath { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public int SortOrder { get; set; }
}
