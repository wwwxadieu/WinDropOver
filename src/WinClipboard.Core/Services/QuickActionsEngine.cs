using System.IO.Compression;
using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;

namespace WinClipboard.Core.Services;

/// <summary>
/// Runs a Quick Action over every item in a shelf. Per the plan (3.2 Quick Actions Engine),
/// each item is processed independently: a failure on one item (locked file, missing
/// permission, ejected removable drive, ...) is reported for that item only and never
/// aborts the rest of the batch.
/// </summary>
public sealed class QuickActionsEngine : IQuickActionsEngine
{
    private readonly IShelfRepository _shelfRepository;
    private readonly IClipboardWriter _clipboardWriter;
    private readonly IShellLauncher _shellLauncher;

    public QuickActionsEngine(
        IShelfRepository shelfRepository,
        IClipboardWriter clipboardWriter,
        IShellLauncher shellLauncher)
    {
        _shelfRepository = shelfRepository;
        _clipboardWriter = clipboardWriter;
        _shellLauncher = shellLauncher;
    }

    public async Task<QuickActionResult> ExecuteAsync(
        long shelfId,
        QuickActionType actionType,
        string? targetPath = null,
        CancellationToken ct = default)
    {
        var items = await _shelfRepository.GetItemsAsync(shelfId, ct);
        return await ExecuteOnItemsAsync(items, actionType, targetPath, ct);
    }

    public async Task<QuickActionResult> ExecuteOnItemsAsync(
        IReadOnlyList<ShelfItem> items,
        QuickActionType actionType,
        string? targetPath = null,
        CancellationToken ct = default)
    {
        var itemResults = actionType == QuickActionType.Zip
            ? ExecuteZip(items, targetPath)
            : new List<QuickActionItemResult>();

        if (actionType != QuickActionType.Zip)
        {
            foreach (var item in items)
            {
                ct.ThrowIfCancellationRequested();
                itemResults.Add(await ExecuteSingleAsync(item, actionType, targetPath, ct));
            }
        }

        return new QuickActionResult { ActionType = actionType, ItemResults = itemResults };
    }

    private async Task<QuickActionItemResult> ExecuteSingleAsync(
        ShelfItem item,
        QuickActionType actionType,
        string? targetPath,
        CancellationToken ct)
    {
        try
        {
            switch (actionType)
            {
                case QuickActionType.MoveToFolder:
                    File.Move(RequireFilePath(item), DestinationPath(item, RequireTargetPath(targetPath)), overwrite: false);
                    break;

                case QuickActionType.CopyToFolder:
                    File.Copy(RequireFilePath(item), DestinationPath(item, RequireTargetPath(targetPath)), overwrite: false);
                    break;

                case QuickActionType.CopyToClipboard:
                    if (item.Type == ShelfItemType.File)
                    {
                        await _clipboardWriter.WriteFilesAsync([RequireFilePath(item)], ct);
                    }
                    else
                    {
                        await _clipboardWriter.WriteTextAsync(item.TextContent ?? item.FilePath ?? string.Empty, ct);
                    }
                    break;

                case QuickActionType.OpenWith:
                    await _shellLauncher.OpenWithDialogAsync(RequireFilePath(item), ct);
                    break;

                case QuickActionType.Share:
                    await _shellLauncher.ShareAsync([RequireFilePath(item)], ct);
                    break;

                default:
                    throw new InvalidOperationException($"Unsupported action: {actionType}");
            }

            return new QuickActionItemResult { ShelfItemId = item.Id, Succeeded = true };
        }
        catch (Exception ex)
        {
            return new QuickActionItemResult { ShelfItemId = item.Id, Succeeded = false, ErrorMessage = ex.Message };
        }
    }

    private static List<QuickActionItemResult> ExecuteZip(IReadOnlyList<ShelfItem> items, string? targetPath)
    {
        var destinationFolder = RequireTargetPath(targetPath);
        var zipPath = Path.Combine(destinationFolder, $"WinClipboard-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

        var results = new List<QuickActionItemResult>();

        ZipArchive archive;
        try
        {
            archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        }
        catch (Exception ex)
        {
            // The archive itself could not be created (read-only folder, path too long, disk
            // full, ...). That is not one item's failure, but the engine's contract is that a
            // caller only ever has to read per-item results — so report it against every item
            // rather than throwing out of the batch.
            return [.. items.Select(i => new QuickActionItemResult
            {
                ShelfItemId = i.Id,
                Succeeded = false,
                ErrorMessage = ex.Message
            })];
        }

        using var archiveScope = archive;
        foreach (var item in items)
        {
            try
            {
                var filePath = RequireFilePath(item);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("File no longer exists.", filePath);
                }
                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                results.Add(new QuickActionItemResult { ShelfItemId = item.Id, Succeeded = true });
            }
            catch (Exception ex)
            {
                results.Add(new QuickActionItemResult { ShelfItemId = item.Id, Succeeded = false, ErrorMessage = ex.Message });
            }
        }
        return results;
    }

    private static string DestinationPath(ShelfItem item, string targetFolder) =>
        Path.Combine(targetFolder, Path.GetFileName(RequireFilePath(item)));

    private static string RequireFilePath(ShelfItem item) =>
        item.Type == ShelfItemType.File && !string.IsNullOrEmpty(item.FilePath)
            ? item.FilePath
            : throw new InvalidOperationException("This action requires a file-backed shelf item.");

    private static string RequireTargetPath(string? targetPath) =>
        !string.IsNullOrWhiteSpace(targetPath)
            ? targetPath
            : throw new InvalidOperationException("This action requires a target folder.");
}
