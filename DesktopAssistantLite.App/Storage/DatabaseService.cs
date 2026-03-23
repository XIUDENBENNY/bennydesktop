using Microsoft.Data.Sqlite;

namespace DesktopAssistantLite.App.Storage;

internal sealed class DatabaseService
{
    private readonly string _databasePath;

    public DatabaseService(string databasePath)
    {
        _databasePath = databasePath;
    }

    public void Initialize()
    {
        using var connection = CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS layout_snapshots (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                created_at_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS desktop_items (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                snapshot_id INTEGER NOT NULL,
                name TEXT NOT NULL,
                full_path TEXT NOT NULL,
                category TEXT NOT NULL,
                item_type TEXT NOT NULL,
                last_write_time_utc TEXT NOT NULL,
                FOREIGN KEY(snapshot_id) REFERENCES layout_snapshots(id)
            );

            CREATE TABLE IF NOT EXISTS todos (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                title TEXT NOT NULL,
                is_completed INTEGER NOT NULL DEFAULT 0,
                is_pinned INTEGER NOT NULL DEFAULT 0,
                due_at_utc TEXT NULL,
                created_at_utc TEXT NOT NULL,
                reminded_at_utc TEXT NULL
            );

            CREATE TABLE IF NOT EXISTS file_index (
                full_path TEXT PRIMARY KEY,
                root_path TEXT NOT NULL,
                name TEXT NOT NULL,
                directory_path TEXT NOT NULL,
                is_directory INTEGER NOT NULL,
                last_write_time_utc TEXT NOT NULL
            );
            """;
        command.ExecuteNonQuery();
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_databasePath}");
    }
}
