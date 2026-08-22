using WinClipboard.Core.Models;

namespace WinClipboard.Core.Abstractions;

public interface IClipboardRepository
{
    /// <summary>Inserts a new item and returns its assigned id. Callers dedupe via <see cref="FindByHashAsync"/> first.</summary>
    Task<long> AddAsync(ClipboardItem item, CancellationToken ct = default);

    Task<ClipboardItem?> FindByHashAsync(string hashDedup, CancellationToken ct = default);

    /// <summary>Most recent items first, optionally filtered by type and/or a search term matched against text content and source app.</summary>
    Task<IReadOnlyList<ClipboardItem>> QueryAsync(
        ContentType? type = null,
        string? searchText = null,
        int limit = 200,
        CancellationToken ct = default);

    Task SetPinnedAsync(long id, bool isPinned, CancellationToken ct = default);

    Task DeleteAsync(long id, CancellationToken ct = default);

    Task DeleteAllAsync(bool keepPinned, CancellationToken ct = default);

    /// <summary>Trims unpinned history down to <paramref name="maxItems"/>, oldest first.</summary>
    Task TrimAsync(int maxItems, CancellationToken ct = default);

    /// <summary>Removes unpinned items older than <paramref name="olderThan"/> (auto-purge of sensitive data).</summary>
    Task PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken ct = default);
}
