namespace WinClipboard.Core.Models;

/// <summary>Outcome of a Quick Action applied to a single shelf item. Failures are isolated per item.</summary>
public sealed class QuickActionItemResult
{
    public required long ShelfItemId { get; set; }
    public required bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>Aggregate outcome of running a Quick Action across every item in a shelf.</summary>
public sealed class QuickActionResult
{
    public required QuickActionType ActionType { get; set; }
    public required IReadOnlyList<QuickActionItemResult> ItemResults { get; set; }

    public int SuccessCount => ItemResults.Count(r => r.Succeeded);
    public int FailureCount => ItemResults.Count(r => !r.Succeeded);
    public bool AllSucceeded => FailureCount == 0;
}
