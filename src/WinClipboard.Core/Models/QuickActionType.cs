namespace WinClipboard.Core.Models;

/// <summary>Batch action a Quick Action bar can run over every item in a shelf.</summary>
public enum QuickActionType
{
    MoveToFolder,
    CopyToFolder,
    Zip,
    /// <summary>Removes the file from disk, recoverably. The safe half of the delete tile.</summary>
    DeleteToRecycleBin,
    /// <summary>Removes the file from disk with nothing to undo. Only ever reached by explicitly choosing it.</summary>
    DeletePermanently,
    CopyToClipboard,
    OpenWith,
    Share
}
