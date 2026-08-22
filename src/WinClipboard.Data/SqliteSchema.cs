using Microsoft.Data.Sqlite;

namespace WinClipboard.Data;

internal static class SqliteSchema
{
    public static void EnsureCreated(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            PRAGMA foreign_keys = ON;

            CREATE TABLE IF NOT EXISTS ClipboardItem (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Type INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL,
                TextContent TEXT NULL,
                FilePath TEXT NULL,
                ThumbnailPath TEXT NULL,
                SourceApp TEXT NULL,
                IsPinned INTEGER NOT NULL DEFAULT 0,
                HashDedup TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_ClipboardItem_HashDedup ON ClipboardItem(HashDedup);
            CREATE INDEX IF NOT EXISTS IX_ClipboardItem_CreatedAt ON ClipboardItem(CreatedAt);

            CREATE TABLE IF NOT EXISTS Shelf (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ColorHex TEXT NOT NULL,
                DefaultActionType INTEGER NULL,
                DefaultTargetPath TEXT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                IsPersisted INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS ShelfItem (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ShelfId INTEGER NOT NULL REFERENCES Shelf(Id) ON DELETE CASCADE,
                Type INTEGER NOT NULL,
                FilePath TEXT NULL,
                TextContent TEXT NULL,
                ThumbnailPath TEXT NULL,
                AddedAt TEXT NOT NULL,
                SortOrder INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS IX_ShelfItem_ShelfId ON ShelfItem(ShelfId);
            """;
        cmd.ExecuteNonQuery();
    }
}
