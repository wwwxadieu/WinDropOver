using WinClipboard.Core.Models;

namespace WinClipboard.Core.Abstractions;

public interface IQuickActionsEngine
{
    /// <summary>
    /// Runs <paramref name="actionType"/> over every item in the shelf. Each item is processed
    /// independently: one item failing (locked file, missing permission, removed drive, ...) is
    /// reported per-item and never aborts the rest of the batch.
    /// </summary>
    Task<QuickActionResult> ExecuteAsync(
        long shelfId,
        QuickActionType actionType,
        string? targetPath = null,
        CancellationToken ct = default);
}
