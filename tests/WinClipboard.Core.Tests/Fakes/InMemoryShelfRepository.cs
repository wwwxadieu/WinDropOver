using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;

namespace WinClipboard.Core.Tests.Fakes;

/// <summary>Minimal in-memory stand-in for the SQLite-backed repository, used to keep engine tests off disk I/O for the DB.</summary>
public sealed class InMemoryShelfRepository : IShelfRepository
{
    private readonly Dictionary<long, Shelf> _shelves = new();
    private readonly Dictionary<long, ShelfItem> _items = new();
    private long _nextShelfId = 1;
    private long _nextItemId = 1;

    public Task<IReadOnlyList<Shelf>> GetShelvesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Shelf>>(_shelves.Values.OrderBy(s => s.SortOrder).ToList());

    public Task<Shelf?> GetShelfAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(_shelves.GetValueOrDefault(id));

    public Task<long> CreateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        shelf.Id = _nextShelfId++;
        _shelves[shelf.Id] = shelf;
        return Task.FromResult(shelf.Id);
    }

    public Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        _shelves[shelf.Id] = shelf;
        return Task.CompletedTask;
    }

    public Task DeleteShelfAsync(long id, CancellationToken ct = default)
    {
        _shelves.Remove(id);
        foreach (var itemId in _items.Where(kv => kv.Value.ShelfId == id).Select(kv => kv.Key).ToList())
        {
            _items.Remove(itemId);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ShelfItem>> GetItemsAsync(long shelfId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ShelfItem>>(
            _items.Values.Where(i => i.ShelfId == shelfId).OrderBy(i => i.SortOrder).ToList());

    public Task<long> AddItemAsync(ShelfItem item, CancellationToken ct = default)
    {
        item.Id = _nextItemId++;
        _items[item.Id] = item;
        return Task.FromResult(item.Id);
    }

    public Task RemoveItemAsync(long itemId, CancellationToken ct = default)
    {
        _items.Remove(itemId);
        return Task.CompletedTask;
    }

    public Task ClearItemsAsync(long shelfId, CancellationToken ct = default)
    {
        foreach (var itemId in _items.Where(kv => kv.Value.ShelfId == shelfId).Select(kv => kv.Key).ToList())
        {
            _items.Remove(itemId);
        }
        return Task.CompletedTask;
    }
}
