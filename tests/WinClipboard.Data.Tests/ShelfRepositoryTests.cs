using WinClipboard.Core.Models;
using WinClipboard.Data;
using Xunit;

namespace WinClipboard.Data.Tests;

public class ShelfRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ShelfRepository _repository;

    public ShelfRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"winclipboard-shelf-test-{Guid.NewGuid():N}.db");
        _repository = new ShelfRepository(new SqliteConnectionFactory(_dbPath));
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public async Task CreateShelfAsync_ThenGetShelfAsync_RoundTrips()
    {
        var shelf = new Shelf { Name = "Công việc", ColorHex = "#3399FF", SortOrder = 0 };
        var id = await _repository.CreateShelfAsync(shelf);

        var loaded = await _repository.GetShelfAsync(id);

        Assert.NotNull(loaded);
        Assert.Equal("Công việc", loaded!.Name);
        Assert.Equal("#3399FF", loaded.ColorHex);
    }

    [Fact]
    public async Task GetShelvesAsync_OrdersBySortOrder()
    {
        await _repository.CreateShelfAsync(new Shelf { Name = "Second", ColorHex = "#000000", SortOrder = 1 });
        await _repository.CreateShelfAsync(new Shelf { Name = "First", ColorHex = "#FFFFFF", SortOrder = 0 });

        var shelves = await _repository.GetShelvesAsync();

        Assert.Equal("First", shelves[0].Name);
        Assert.Equal("Second", shelves[1].Name);
    }

    [Fact]
    public async Task UpdateShelfAsync_PersistsChanges()
    {
        var id = await _repository.CreateShelfAsync(new Shelf { Name = "Old Name", ColorHex = "#111111" });
        var shelf = (await _repository.GetShelfAsync(id))!;
        shelf.Name = "New Name";
        shelf.DefaultActionType = QuickActionType.Zip;
        shelf.DefaultTargetPath = @"C:\Exports";

        await _repository.UpdateShelfAsync(shelf);
        var reloaded = await _repository.GetShelfAsync(id);

        Assert.Equal("New Name", reloaded!.Name);
        Assert.Equal(QuickActionType.Zip, reloaded.DefaultActionType);
        Assert.Equal(@"C:\Exports", reloaded.DefaultTargetPath);
    }

    [Fact]
    public async Task DeleteShelfAsync_RemovesShelf()
    {
        var id = await _repository.CreateShelfAsync(new Shelf { Name = "Temp", ColorHex = "#222222" });

        await _repository.DeleteShelfAsync(id);
        var loaded = await _repository.GetShelfAsync(id);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task AddItemAsync_ThenGetItemsAsync_ReturnsInSortOrder()
    {
        var shelfId = await _repository.CreateShelfAsync(new Shelf { Name = "Ảnh", ColorHex = "#FF00FF" });
        await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = @"C:\b.png", AddedAt = DateTimeOffset.UtcNow, SortOrder = 1 });
        await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = @"C:\a.png", AddedAt = DateTimeOffset.UtcNow, SortOrder = 0 });

        var items = await _repository.GetItemsAsync(shelfId);

        Assert.Equal(2, items.Count);
        Assert.Equal(@"C:\a.png", items[0].FilePath);
        Assert.Equal(@"C:\b.png", items[1].FilePath);
    }

    [Fact]
    public async Task RemoveItemAsync_RemovesOnlyThatItem()
    {
        var shelfId = await _repository.CreateShelfAsync(new Shelf { Name = "S", ColorHex = "#ABCDEF" });
        var keepId = await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.Text, TextContent = "keep", AddedAt = DateTimeOffset.UtcNow });
        var removeId = await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.Text, TextContent = "remove", AddedAt = DateTimeOffset.UtcNow });

        await _repository.RemoveItemAsync(removeId);
        var items = await _repository.GetItemsAsync(shelfId);

        Assert.Single(items);
        Assert.Equal(keepId, items[0].Id);
    }

    [Fact]
    public async Task ClearItemsAsync_RemovesAllItemsInShelfOnly()
    {
        var shelfA = await _repository.CreateShelfAsync(new Shelf { Name = "A", ColorHex = "#111111" });
        var shelfB = await _repository.CreateShelfAsync(new Shelf { Name = "B", ColorHex = "#222222" });
        await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfA, Type = ShelfItemType.Text, TextContent = "a1", AddedAt = DateTimeOffset.UtcNow });
        await _repository.AddItemAsync(new ShelfItem { ShelfId = shelfB, Type = ShelfItemType.Text, TextContent = "b1", AddedAt = DateTimeOffset.UtcNow });

        await _repository.ClearItemsAsync(shelfA);

        Assert.Empty(await _repository.GetItemsAsync(shelfA));
        Assert.Single(await _repository.GetItemsAsync(shelfB));
    }
}
