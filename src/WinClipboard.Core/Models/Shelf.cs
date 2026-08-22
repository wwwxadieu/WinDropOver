namespace WinClipboard.Core.Models;

/// <summary>A named, colored bucket of items a user drags together before dropping them somewhere else.</summary>
public sealed class Shelf
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string ColorHex { get; set; }
    public QuickActionType? DefaultActionType { get; set; }
    public string? DefaultTargetPath { get; set; }
    public int SortOrder { get; set; }

    /// <summary>When false, the shelf and its items live only in memory for the current session.</summary>
    public bool IsPersisted { get; set; }
}
