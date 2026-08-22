using Microsoft.Data.Sqlite;
using WinClipboard.Core.Abstractions;
using WinClipboard.Core.Models;

namespace WinClipboard.Data;

public sealed class ClipboardRepository : IClipboardRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public ClipboardRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> AddAsync(ClipboardItem item, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ClipboardItem (Type, CreatedAt, TextContent, FilePath, ThumbnailPath, SourceApp, IsPinned, HashDedup)
            VALUES (@type, @createdAt, @textContent, @filePath, @thumbnailPath, @sourceApp, @isPinned, @hashDedup);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("@type", (int)item.Type);
        cmd.Parameters.AddWithValue("@createdAt", item.CreatedAt.ToString("O"));
        cmd.Parameters.AddWithValue("@textContent", (object?)item.TextContent ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@filePath", (object?)item.FilePath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@thumbnailPath", (object?)item.ThumbnailPath ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@sourceApp", (object?)item.SourceApp ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@isPinned", item.IsPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@hashDedup", item.HashDedup);

        var id = (long)(await cmd.ExecuteScalarAsync(ct))!;
        return id;
    }

    public async Task<ClipboardItem?> FindByHashAsync(string hashDedup, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Type, CreatedAt, TextContent, FilePath, ThumbnailPath, SourceApp, IsPinned, HashDedup
            FROM ClipboardItem
            WHERE HashDedup = @hashDedup
            ORDER BY CreatedAt DESC
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("@hashDedup", hashDedup);

        using var reader = await cmd.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadItem(reader) : null;
    }

    public async Task<IReadOnlyList<ClipboardItem>> QueryAsync(
        ContentType? type = null,
        string? searchText = null,
        int limit = 200,
        CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();

        var whereClauses = new List<string>();
        if (type is not null)
        {
            whereClauses.Add("Type = @type");
            cmd.Parameters.AddWithValue("@type", (int)type.Value);
        }
        if (!string.IsNullOrWhiteSpace(searchText))
        {
            whereClauses.Add("(TextContent LIKE @search OR FilePath LIKE @search OR SourceApp LIKE @search)");
            cmd.Parameters.AddWithValue("@search", $"%{searchText}%");
        }

        var where = whereClauses.Count > 0 ? "WHERE " + string.Join(" AND ", whereClauses) : "";
        cmd.CommandText = $"""
            SELECT Id, Type, CreatedAt, TextContent, FilePath, ThumbnailPath, SourceApp, IsPinned, HashDedup
            FROM ClipboardItem
            {where}
            ORDER BY IsPinned DESC, CreatedAt DESC
            LIMIT @limit
            """;
        cmd.Parameters.AddWithValue("@limit", limit);

        var results = new List<ClipboardItem>();
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(ReadItem(reader));
        }
        return results;
    }

    public async Task SetPinnedAsync(long id, bool isPinned, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE ClipboardItem SET IsPinned = @isPinned WHERE Id = @id";
        cmd.Parameters.AddWithValue("@isPinned", isPinned ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItem WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task DeleteAllAsync(bool keepPinned, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = keepPinned
            ? "DELETE FROM ClipboardItem WHERE IsPinned = 0"
            : "DELETE FROM ClipboardItem";
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task TrimAsync(int maxItems, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM ClipboardItem
            WHERE IsPinned = 0
              AND Id NOT IN (
                  SELECT Id FROM ClipboardItem
                  WHERE IsPinned = 0
                  ORDER BY CreatedAt DESC
                  LIMIT @maxItems
              )
            """;
        cmd.Parameters.AddWithValue("@maxItems", maxItems);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task PurgeOlderThanAsync(TimeSpan olderThan, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.OpenConnection();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM ClipboardItem WHERE IsPinned = 0 AND CreatedAt < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", (DateTimeOffset.UtcNow - olderThan).ToString("O"));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static ClipboardItem ReadItem(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        Type = (ContentType)reader.GetInt32(1),
        CreatedAt = DateTimeOffset.Parse(reader.GetString(2)),
        TextContent = reader.IsDBNull(3) ? null : reader.GetString(3),
        FilePath = reader.IsDBNull(4) ? null : reader.GetString(4),
        ThumbnailPath = reader.IsDBNull(5) ? null : reader.GetString(5),
        SourceApp = reader.IsDBNull(6) ? null : reader.GetString(6),
        IsPinned = reader.GetInt32(7) != 0,
        HashDedup = reader.GetString(8)
    };
}
