using Microsoft.Data.Sqlite;

namespace WinClipboard.Data;

/// <summary>Owns the path to the WinClipboard SQLite database and hands out ready-to-use connections.</summary>
public sealed class SqliteConnectionFactory
{
    private readonly string _connectionString;

    public SqliteConnectionFactory(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>Default per-user database location: %LOCALAPPDATA%\WinClipboard\winclipboard.db</summary>
    public static string DefaultDatabasePath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "WinClipboard", "winclipboard.db");
    }

    public SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        SqliteSchema.EnsureCreated(connection);
        return connection;
    }
}
