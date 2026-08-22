using WinClipboard.Core.Models;

namespace WinClipboard.Core.Abstractions;

public interface IShelfRepository
{
    Task<IReadOnlyList<Shelf>> GetShelvesAsync(CancellationToken ct = default);

    Task<Shelf?> GetShelfAsync(long id, CancellationToken ct = default);

    Task<long> CreateShelfAsync(Shelf shelf, CancellationToken ct = default);

    Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default);

    Task DeleteShelfAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<ShelfItem>> GetItemsAsync(long shelfId, CancellationToken ct = default);

    Task<long> AddItemAsync(ShelfItem item, CancellationToken ct = default);

    Task RemoveItemAsync(long itemId, CancellationToken ct = default);

    Task ClearItemsAsync(long shelfId, CancellationToken ct = default);
}
