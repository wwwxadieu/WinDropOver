using System.IO.Compression;
using WinClipboard.Core.Models;
using WinClipboard.Core.Services;
using WinClipboard.Core.Tests.Fakes;
using Xunit;

namespace WinClipboard.Core.Tests;

public class QuickActionsEngineTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly InMemoryShelfRepository _shelfRepository = new();
    private readonly FakeClipboardWriter _clipboardWriter = new();
    private readonly FakeShellLauncher _shellLauncher = new();
    private readonly QuickActionsEngine _engine;

    public QuickActionsEngineTests()
    {
        _tempRoot = Directory.CreateTempSubdirectory("winclipboard-tests-").FullName;
        _engine = new QuickActionsEngine(_shelfRepository, _clipboardWriter, _shellLauncher);
    }

    public void Dispose() => Directory.Delete(_tempRoot, recursive: true);

    private string NewSourceFile(string name, string content = "content")
    {
        var path = Path.Combine(_tempRoot, "source", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<long> NewShelfWithFileAsync(string filePath)
    {
        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "Test", ColorHex = "#FF0000" });
        await _shelfRepository.AddItemAsync(new ShelfItem
        {
            ShelfId = shelfId,
            Type = ShelfItemType.File,
            FilePath = filePath,
            AddedAt = DateTimeOffset.UtcNow
        });
        return shelfId;
    }

    [Fact]
    public async Task CopyToFolder_CopiesFileAndKeepsOriginal()
    {
        var source = NewSourceFile("a.txt");
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);
        var shelfId = await NewShelfWithFileAsync(source);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.CopyToFolder, destFolder);

        Assert.True(result.AllSucceeded);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destFolder, "a.txt")));
    }

    [Fact]
    public async Task MoveToFolder_MovesFileAndRemovesOriginal()
    {
        var source = NewSourceFile("b.txt");
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);
        var shelfId = await NewShelfWithFileAsync(source);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.MoveToFolder, destFolder);

        Assert.True(result.AllSucceeded);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(Path.Combine(destFolder, "b.txt")));
    }

    [Fact]
    public async Task Zip_PacksAllFilesIntoOneArchive()
    {
        var f1 = NewSourceFile("one.txt", "one");
        var f2 = NewSourceFile("two.txt", "two");
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);

        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "Zip", ColorHex = "#00FF00" });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = f1, AddedAt = DateTimeOffset.UtcNow });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = f2, AddedAt = DateTimeOffset.UtcNow });

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.Zip, destFolder);

        Assert.True(result.AllSucceeded);
        var zipFile = Directory.GetFiles(destFolder, "*.zip").Single();
        using var archive = ZipFile.OpenRead(zipFile);
        Assert.Equal(2, archive.Entries.Count);
        Assert.Contains(archive.Entries, e => e.Name == "one.txt");
        Assert.Contains(archive.Entries, e => e.Name == "two.txt");
    }

    [Fact]
    public async Task BatchAction_OneItemFails_OthersStillSucceed()
    {
        var good = NewSourceFile("good.txt");
        var missing = Path.Combine(_tempRoot, "source", "missing.txt"); // never created
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);

        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "Mixed", ColorHex = "#0000FF" });
        var goodId = await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = good, AddedAt = DateTimeOffset.UtcNow });
        var missingId = await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = missing, AddedAt = DateTimeOffset.UtcNow });

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.CopyToFolder, destFolder);

        Assert.False(result.AllSucceeded);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.True(result.ItemResults.Single(r => r.ShelfItemId == goodId).Succeeded);
        var missingResult = result.ItemResults.Single(r => r.ShelfItemId == missingId);
        Assert.False(missingResult.Succeeded);
        Assert.NotNull(missingResult.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(destFolder, "good.txt")));
    }

    [Fact]
    public async Task ExecuteOnItemsAsync_OnlyTouchesTheGivenItems()
    {
        var dragged = NewSourceFile("dragged.txt");
        var untouched = NewSourceFile("untouched.txt");
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);

        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "S", ColorHex = "#123456" });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = dragged, AddedAt = DateTimeOffset.UtcNow });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = untouched, AddedAt = DateTimeOffset.UtcNow });

        var subset = (await _shelfRepository.GetItemsAsync(shelfId))
            .Where(i => i.FilePath == dragged)
            .ToList();

        var result = await _engine.ExecuteOnItemsAsync(subset, QuickActionType.CopyToFolder, destFolder);

        Assert.True(result.AllSucceeded);
        Assert.Single(result.ItemResults);
        Assert.True(File.Exists(Path.Combine(destFolder, "dragged.txt")));
        Assert.False(File.Exists(Path.Combine(destFolder, "untouched.txt")));
    }

    [Fact]
    public async Task ExecuteOnItemsAsync_Zip_PacksOnlyTheGivenItems()
    {
        var a = NewSourceFile("a.txt");
        var b = NewSourceFile("b.txt");
        var destFolder = Path.Combine(_tempRoot, "dest");
        Directory.CreateDirectory(destFolder);

        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "S", ColorHex = "#123456" });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = a, AddedAt = DateTimeOffset.UtcNow });
        await _shelfRepository.AddItemAsync(new ShelfItem { ShelfId = shelfId, Type = ShelfItemType.File, FilePath = b, AddedAt = DateTimeOffset.UtcNow });

        var subset = (await _shelfRepository.GetItemsAsync(shelfId)).Where(i => i.FilePath == a).ToList();
        var result = await _engine.ExecuteOnItemsAsync(subset, QuickActionType.Zip, destFolder);

        Assert.True(result.AllSucceeded);
        using var archive = ZipFile.OpenRead(Directory.GetFiles(destFolder, "*.zip").Single());
        Assert.Equal("a.txt", archive.Entries.Single().Name);
    }

    [Fact]
    public async Task ExecuteOnItemsAsync_EmptySelection_SucceedsWithNothingDone()
    {
        var result = await _engine.ExecuteOnItemsAsync([], QuickActionType.CopyToClipboard);

        Assert.True(result.AllSucceeded);
        Assert.Empty(result.ItemResults);
    }

    [Fact]
    public async Task Zip_ArchiveCannotBeCreated_ReportsFailurePerItemInsteadOfThrowing()
    {
        var source = NewSourceFile("z.txt");
        var shelfId = await NewShelfWithFileAsync(source);
        var missingFolder = Path.Combine(_tempRoot, "no", "such", "folder"); // never created

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.Zip, missingFolder);

        Assert.False(result.AllSucceeded);
        Assert.Equal(1, result.FailureCount);
        Assert.NotNull(result.ItemResults.Single().ErrorMessage);
    }

    [Fact]
    public async Task CopyToClipboard_FileItem_WritesFilePath()
    {
        var source = NewSourceFile("clip.txt");
        var shelfId = await NewShelfWithFileAsync(source);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.CopyToClipboard);

        Assert.True(result.AllSucceeded);
        Assert.Single(_clipboardWriter.WrittenFileBatches);
        Assert.Equal(source, _clipboardWriter.WrittenFileBatches[0][0]);
    }

    [Fact]
    public async Task CopyToClipboard_TextItem_WritesText()
    {
        var shelfId = await _shelfRepository.CreateShelfAsync(new Shelf { Name = "Text", ColorHex = "#FFFFFF" });
        await _shelfRepository.AddItemAsync(new ShelfItem
        {
            ShelfId = shelfId,
            Type = ShelfItemType.Text,
            TextContent = "hello",
            AddedAt = DateTimeOffset.UtcNow
        });

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.CopyToClipboard);

        Assert.True(result.AllSucceeded);
        Assert.Contains("hello", _clipboardWriter.WrittenText);
    }

    [Fact]
    public async Task DeletePermanently_RemovesTheFileFromDisk()
    {
        var source = NewSourceFile("gone.txt");
        var shelfId = await NewShelfWithFileAsync(source);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.DeletePermanently);

        Assert.True(result.ItemResults.Single().Succeeded);
        Assert.False(File.Exists(source));
    }

    [Fact]
    public async Task DeletePermanently_RemovesAFolderAndEverythingInIt()
    {
        // A shelf holds whatever was dragged onto it, and Explorer lets you drag a folder.
        var folder = Path.Combine(_tempRoot, "bundle");
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "inside.txt"), "x");
        var shelfId = await NewShelfWithFileAsync(folder);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.DeletePermanently);

        Assert.True(result.ItemResults.Single().Succeeded);
        Assert.False(Directory.Exists(folder));
    }

    [Fact]
    public async Task DeleteToRecycleBin_GoesThroughTheShellRatherThanDeletingDirectly()
    {
        // The distinction is the whole point: a File.Delete would leave nothing to restore, which
        // is exactly what the recoverable half of the delete tile promises.
        var source = NewSourceFile("recycled.txt");
        var shelfId = await NewShelfWithFileAsync(source);

        var result = await _engine.ExecuteAsync(shelfId, QuickActionType.DeleteToRecycleBin);

        Assert.True(result.ItemResults.Single().Succeeded);
        Assert.Equal([source], _shellLauncher.RecycleCalls);
        Assert.True(File.Exists(source), "the engine must leave the deletion to the shell");
    }

    [Fact]
    public async Task Delete_ReportsAFailedItemWithoutStoppingTheBatch()
    {
        var present = NewSourceFile("here.txt");
        var missing = Path.Combine(_tempRoot, "no-such-folder", "ghost.txt");

        var result = await _engine.ExecuteOnItemsAsync(
            [
                new ShelfItem { ShelfId = 1, Type = ShelfItemType.File, FilePath = missing },
                new ShelfItem { ShelfId = 1, Type = ShelfItemType.File, FilePath = present }
            ],
            QuickActionType.DeletePermanently);

        Assert.False(result.ItemResults[0].Succeeded);
        Assert.True(result.ItemResults[1].Succeeded);
        Assert.False(File.Exists(present));
    }
}
