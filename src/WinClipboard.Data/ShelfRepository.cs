using Microsoft.Data.Sqlite;
using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;

namespace WinClipboard.Data;

public sealed class ShelfRepository : IShelfRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ShelfRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Shelf>> GetShelvesAsync(CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, ColorHex, DefaultActionType, DefaultTargetPath, SortOrder, IsPersisted
            FROM Shelf
            ORDER BY SortOrder ASC, Id ASC
            """;

        var results = new List<Shelf>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadShelf(reader));
        }
        return results;
    }

    public async Task<Shelf?> GetShelfAsync(long id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Name, ColorHex, DefaultActionType, DefaultTargetPath, SortOrder, IsPersisted
            FROM Shelf
            WHERE Id = @id
            """;
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadShelf(reader) : null;
    }

    public async Task<long> CreateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO Shelf (Name, ColorHex, DefaultActionType, DefaultTargetPath, SortOrder, IsPersisted)
            VALUES (@name, @colorHex, @defaultActionType, @defaultTargetPath, @sortOrder, @isPersisted);
            SELECT last_insert_rowid();
            """;
        BindShelf(cmd, shelf);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task UpdateShelfAsync(Shelf shelf, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE Shelf
            SET Name = @name,
                ColorHex = @colorHex,
                DefaultActionType = @defaultActionType,
                DefaultTargetPath = @defaultTargetPath,
                SortOrder = @sortOrder,
                IsPersisted = @isPersisted
            WHERE Id = @id
            """;
        BindShelf(cmd, shelf);
        cmd.Parameters.AddWithValue("@id", shelf.Id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteShelfAsync(long id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM Shelf WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<ShelfItem>> GetItemsAsync(long shelfId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, ShelfId, Type, FilePath, TextContent, ThumbnailPath, AddedAt, SortOrder
            FROM ShelfItem
            WHERE ShelfId = @shelfId
            ORDER BY SortOrder ASC, Id ASC
            """;
        cmd.Parameters.AddWithValue("@shelfId", shelfId);

        var results = new List<ShelfItem>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadShelfItem(reader));
        }
        return results;
    }

    public async Task<long> AddItemAsync(ShelfItem item, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ShelfItem (ShelfId, Type, FilePath, TextContent, ThumbnailPath, AddedAt, SortOrder)
            VALUES (@shelfId, @type, @filePath, @textContent, @thumbnailPath, @addedAt, @sortOrder);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@shelfId", item.ShelfId);
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@filePath", (object?)item.FilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@textContent", (object?)item.TextContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@thumbnailPath", (object?)item.ThumbnailPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@addedAt", item.AddedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@sortOrder", item.SortOrder);
        return (long)(await cmd.ExecuteScalarAsync(ct))!;
    }

    public async Task RemoveItemAsync(long itemId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ShelfItem WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", itemId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ClearItemsAsync(long shelfId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ShelfItem WHERE ShelfId = @shelfId";
        cmd.Parameters.AddWithValue("@shelfId", shelfId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static void BindShelf(SqliteCommand cmd, Shelf shelf)
    {
        cmd.Parameters.AddWithValue("@name", shelf.Name);
        cmd.Parameters.AddWithValue("@colorHex", shelf.ColorHex);
        cmd.Parameters.AddWithValue("@defaultActionType", (object?)(int?)shelf.DefaultActionType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@defaultTargetPath", (object?)shelf.DefaultTargetPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sortOrder", shelf.SortOrder);
        cmd.Parameters.AddWithValue("@isPersisted", shelf.IsPersisted ? 1 : 0);
    }

    private static Shelf ReadShelf(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Name = reader.GetString(1),
        ColorHex = reader.GetString(2),
        DefaultActionType = reader.IsDBNull(3) ? null : (QuickActionType)reader.GetInt32(3),
        DefaultTargetPath = reader.IsDBNull(4) ? null : reader.GetString(4),
        SortOrder = reader.GetInt32(5),
        IsPersisted = reader.GetInt32(6) != 0
    };

    private static ShelfItem ReadShelfItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ShelfId = reader.GetInt64(1),
        Type = (ShelfItemType)reader.GetInt32(2),
        FilePath = reader.IsDBNull(3) ? null : reader.GetString(3),
        TextContent = reader.IsDBNull(4) ? null : reader.GetString(4),
        ThumbnailPath = reader.IsDBNull(5) ? null : reader.GetString(5),
        AddedAt = DateTimeOffset.Parse(reader.GetString(6)),
        SortOrder = reader.GetInt32(7)
    };
}
