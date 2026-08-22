using Microsoft.Data.Sqlite;
using WinClipboard.Core.Models;
using WinClipboard.Core.Utils;
using WinClipboard.Data;
using Xunit;

namespace WinClipboard.Data.Tests;

public class ClipboardRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ClipboardRepository _repository;

    public ClipboardRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"winclipboard-test-{Guid.NewGuid():N}.db");
        _repository = new ClipboardRepository(new SqliteConnectionFactory(_dbPath));
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections, so disposing one returns it to the pool
        // with the sqlite3 file handle still open. Windows refuses to delete an open file
        // (Linux happily unlinks it), so the temp DB must be released explicitly here.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private static ClipboardItem MakeText(string text, bool pinned = false, DateTimeOffset? createdAt = null) => new()
    {
        Type = ContentType.Text,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        TextContent = text,
        IsPinned = pinned,
        HashDedup = ContentHasher.HashText(text)
    };

    [Fact]
    public async Task AddAsync_ThenQuery_ReturnsItemMostRecentFirst()
    {
        var id1 = await _repository.AddAsync(MakeText("first", createdAt: DateTimeOffset.UtcNow.AddMinutes(-1)));
        var id2 = await _repository.AddAsync(MakeText("second"));

        var items = await _repository.QueryAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal(id2, items[0].Id);
        Assert.Equal(id1, items[1].Id);
    }

    [Fact]
    public async Task FindByHashAsync_ReturnsMostRecentMatch()
    {
        var item = MakeText("dup-me");
        await _repository.AddAsync(item);

        var found = await _repository.FindByHashAsync(item.HashDedup);

        Assert.NotNull(found);
        Assert.Equal("dup-me", found!.TextContent);
    }

    [Fact]
    public async Task FindByHashAsync_NoMatch_ReturnsNull()
    {
        var found = await _repository.FindByHashAsync("nonexistent-hash");
        Assert.Null(found);
    }

    [Fact]
    public async Task QueryAsync_FilterByType_OnlyReturnsMatchingType()
    {
        await _repository.AddAsync(MakeText("a text item"));
        await _repository.AddAsync(new ClipboardItem
        {
            Type = ContentType.File,
            CreatedAt = DateTimeOffset.UtcNow,
            FilePath = @"C:\file.txt",
            HashDedup = ContentHasher.HashPath(ContentType.File, @"C:\file.txt")
        });

        var textItems = await _repository.QueryAsync(type: ContentType.Text);

        Assert.Single(textItems);
        Assert.Equal(ContentType.Text, textItems[0].Type);
    }

    [Fact]
    public async Task QueryAsync_SearchText_MatchesTextContent()
    {
        await _repository.AddAsync(MakeText("the quick brown fox"));
        await _repository.AddAsync(MakeText("something else entirely"));

        var results = await _repository.QueryAsync(searchText: "quick");

        Assert.Single(results);
        Assert.Equal("the quick brown fox", results[0].TextContent);
    }

    [Fact]
    public async Task QueryAsync_PinnedItemsSortFirst()
    {
        await _repository.AddAsync(MakeText("older but pinned", pinned: true, createdAt: DateTimeOffset.UtcNow.AddHours(-1)));
        await _repository.AddAsync(MakeText("newer unpinned"));

        var results = await _repository.QueryAsync();

        Assert.Equal("older but pinned", results[0].TextContent);
    }

    [Fact]
    public async Task SetPinnedAsync_UpdatesFlag()
    {
        var id = await _repository.AddAsync(MakeText("pin me"));

        await _repository.SetPinnedAsync(id, true);
        var items = await _repository.QueryAsync();

        Assert.True(items.Single(i => i.Id == id).IsPinned);
    }

    [Fact]
    public async Task DeleteAsync_RemovesItem()
    {
        var id = await _repository.AddAsync(MakeText("delete me"));

        await _repository.DeleteAsync(id);
        var items = await _repository.QueryAsync();

        Assert.Empty(items);
    }

    [Fact]
    public async Task DeleteAllAsync_KeepPinned_OnlyRemovesUnpinned()
    {
        await _repository.AddAsync(MakeText("pinned", pinned: true));
        await _repository.AddAsync(MakeText("unpinned"));

        await _repository.DeleteAllAsync(keepPinned: true);
        var items = await _repository.QueryAsync();

        Assert.Single(items);
        Assert.Equal("pinned", items[0].TextContent);
    }

    [Fact]
    public async Task TrimAsync_KeepsMostRecentUnpinnedUpToLimit()
    {
        for (var i = 0; i < 5; i++)
        {
            await _repository.AddAsync(MakeText($"item-{i}", createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        await _repository.TrimAsync(maxItems: 2);
        var items = await _repository.QueryAsync();

        Assert.Equal(2, items.Count);
        Assert.Equal("item-0", items[0].TextContent);
        Assert.Equal("item-1", items[1].TextContent);
    }

    [Fact]
    public async Task TrimAsync_NeverRemovesPinnedItems()
    {
        await _repository.AddAsync(MakeText("old pinned", pinned: true, createdAt: DateTimeOffset.UtcNow.AddDays(-1)));
        for (var i = 0; i < 3; i++)
        {
            await _repository.AddAsync(MakeText($"item-{i}"));
        }

        await _repository.TrimAsync(maxItems: 1);
        var items = await _repository.QueryAsync();

        Assert.Contains(items, i => i.TextContent == "old pinned");
    }

    [Fact]
    public async Task PurgeOlderThanAsync_RemovesOldUnpinnedItems()
    {
        await _repository.AddAsync(MakeText("ancient", createdAt: DateTimeOffset.UtcNow.AddDays(-10)));
        await _repository.AddAsync(MakeText("recent", createdAt: DateTimeOffset.UtcNow));

        await _repository.PurgeOlderThanAsync(TimeSpan.FromDays(1));
        var items = await _repository.QueryAsync();

        Assert.Single(items);
        Assert.Equal("recent", items[0].TextContent);
    }
}
