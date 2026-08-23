using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;

namespace WinClipboard.App.Services;

/// <summary>
/// Facade over <see cref="IShelfRepository"/> that adds the session-only shelf behavior from
/// plan 3.5: "Shelf/ShelfItem chỉ ghi xuống SQLite khi IsPersisted = true" — a shelf created
/// without persistence turned on never touches the database at all, it just lives in memory for
/// as long as the app runs. Session shelves/items are tagged with negative ids (DB-assigned ids
/// are always positive/autoincrement) so callers can keep using the same <see cref="IShelfRepository"/>
/// surface — including <see cref="Core.Services.QuickActionsEngine"/> — without knowing which
/// store a given shelf actually lives in.
///
/// Simplification: a shelf's persistence is decided once, at creation time, from the
/// <see cref="Shelf.IsPersisted"/> flag passed to <see cref="CreateShelfAsync"/>. Moving an
/// existing shelf between the two stores later isn't supported — plan section 7 leaves whether
/// persistence should even default to on as an open product decision, so there was nothing to
/// build a toggle against yet.
/// </summary>
public sealed class ShelfSessionManager : IShelfRepository
{
    private readonly IShelfRepository _persistentStore;
    private readonly Dictionary<long, Shelf> _sessionShelves = [];
    private readonly Dictionary<long, List<ShelfItem>> _sessionItems = [];
    private long _nextSessionShelfId = -1;
    private long _nextSessionItemId = -1;

    public event EventHandler? ShelvesChanged;
    public event EventHandler<long>? ShelfItemsChanged;

    public ShelfSessionManager(IShelfRepository persistentStore)
    {
        _persistentStore = persistentStore;
    }

    public async Task<IReadOnlyList<Shelf>> GetShelvesAsync(CancellationToken ct = default)
    {
        var persisted = await _persistentStore.GetShelvesAsync(ct);
        return persisted.Concat(_sessionShelves.Values)
            .OrderBy(s => s.SortOrder)
            .ThenBy(s => s.Id)
            .ToList();
    }

    public Task<Shelf?> GetShelfAsync(long id, CancellationToken ct = default) =>
        id < 0
            ? Task.FromResult(_sessionShelves.GetValueOrDefault(id))
            : _persistentStore.GetShelfAsync(id, ct);

    public async Task<long> CreateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        long id;
        if (shelf.IsPersisted)
        {
            id = await _persistentStore.CreateShelfAsync(shelf, ct);
        }
        else
        {
            id = _nextSessionShelfId--;
            shelf.Id = id;
            _sessionShelves[id] = shelf;
            _sessionItems[id] = [];
        }
        ShelvesChanged?.Invoke(this, EventArgs.Empty);
        return id;
    }

    public async Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        if (shelf.Id < 0)
        {
            _sessionShelves[shelf.Id] = shelf;
        }
        else
        {
            await _persistentStore.UpdateShelfAsync(shelf, ct);
        }
        ShelvesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task DeleteShelfAsync(long id, CancellationToken ct = default)
    {
        if (id < 0)
        {
            _sessionShelves.Remove(id);
            _sessionItems.Remove(id);
        }
        else
        {
            await _persistentStore.DeleteShelfAsync(id, ct);
        }
        ShelvesChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task<IReadOnlyList<ShelfItem>> GetItemsAsync(long shelfId, CancellationToken ct = default) =>
        shelfId < 0
            ? Task.FromResult<IReadOnlyList<ShelfItem>>([.. _sessionItems.GetValueOrDefault(shelfId, [])])
            : _persistentStore.GetItemsAsync(shelfId, ct);

    public async Task<long> AddItemAsync(ShelfItem item, CancellationToken ct = default)
    {
        long id;
        if (item.ShelfId < 0)
        {
            id = _nextSessionItemId--;
            item.Id = id;
            (_sessionItems[item.ShelfId] ??= []).Add(item);
        }
        else
        {
            id = await _persistentStore.AddItemAsync(item, ct);
        }
        ShelfItemsChanged?.Invoke(this, item.ShelfId);
        return id;
    }

    public async Task UpdateItemAsync(ShelfItem item, CancellationToken ct = default)
    {
        if (item.Id < 0)
        {
            // Session items are held by reference, so the caller already mutated the stored
            // instance; this just keeps the contract and notifies listeners.
            var items = _sessionItems.GetValueOrDefault(item.ShelfId);
            var index = items?.FindIndex(i => i.Id == item.Id) ?? -1;
            if (index >= 0)
            {
                items![index] = item;
            }
        }
        else
        {
            await _persistentStore.UpdateItemAsync(item, ct);
        }
        ShelfItemsChanged?.Invoke(this, item.ShelfId);
    }

    public async Task RemoveItemAsync(long itemId, CancellationToken ct = default)
    {
        if (itemId < 0)
        {
            foreach (var (shelfId, items) in _sessionItems)
            {
                if (items.RemoveAll(i => i.Id == itemId) > 0)
                {
                    ShelfItemsChanged?.Invoke(this, shelfId);
                    return;
                }
            }
        }
        else
        {
            await _persistentStore.RemoveItemAsync(itemId, ct);
        }
    }

    public async Task ClearItemsAsync(long shelfId, CancellationToken ct = default)
    {
        if (shelfId < 0)
        {
            _sessionItems[shelfId] = [];
        }
        else
        {
            await _persistentStore.ClearItemsAsync(shelfId, ct);
        }
        ShelfItemsChanged?.Invoke(this, shelfId);
    }

    /// <summary>Ensures at least one shelf exists (a fresh install has none) and returns its id.</summary>
    public async Task<long> EnsureDefaultShelfAsync(CancellationToken ct = default)
    {
        var shelves = await GetShelvesAsync(ct);
        if (shelves.Count > 0)
        {
            return shelves[0].Id;
        }

        return await CreateShelfAsync(new Shelf
        {
            Name = "Shelf 1",
            ColorHex = "#3B82F6",
            SortOrder = 0,
            IsPersisted = false
        }, ct);
    }
}
