namespace WinClipboard.Core.Models;

/// <summary>One entry in the clipboard history overlay.</summary>
public sealed class ClipboardItem
{
    public long Id { get; set; }
    public required ContentType Type { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string? TextContent { get; set; }
    public string? FilePath { get; set; }
    public string? ThumbnailPath { get; set; }
    public string? SourceApp { get; set; }
    public bool IsPinned { get; set; }

    /// <summary>Content hash used to skip re-inserting a duplicate of the most recent copy.</summary>
    public required string HashDedup { get; set; }
}
