using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Storage;

namespace DesktopAssistantLite.App.Services;

internal sealed class TodoService
{
    private readonly DatabaseService _databaseService;
    private readonly LogService _logService;
    private readonly object _syncRoot = new();

    public TodoService(DatabaseService databaseService, LogService logService)
    {
        _databaseService = databaseService;
        _logService = logService;
    }

    public Task<List<TodoItem>> GetAllAsync()
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT id, title, is_completed, is_pinned, due_at_utc, created_at_utc, reminded_at_utc
                    FROM todos
                    ORDER BY is_pinned DESC, is_completed ASC, due_at_utc IS NULL, due_at_utc ASC, created_at_utc DESC;
                    """;
                using var reader = command.ExecuteReader();
                var results = new List<TodoItem>();
                while (reader.Read())
                {
                    results.Add(new TodoItem
                    {
                        Id = reader.GetInt64(0),
                        Title = reader.GetString(1),
                        IsCompleted = reader.GetInt64(2) == 1,
                        IsPinned = reader.GetInt64(3) == 1,
                        DueAtUtc = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
                        CreatedAtUtc = DateTime.Parse(reader.GetString(5)).ToUniversalTime(),
                        RemindedAtUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
                    });
                }

                return results;
            }
        });
    }

    public Task AddAsync(string title, DateTime? dueAtLocal, bool pin)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO todos(title, is_completed, is_pinned, due_at_utc, created_at_utc, reminded_at_utc)
                    VALUES ($title, 0, $isPinned, $dueAtUtc, $createdAtUtc, NULL);
                    """;
                command.Parameters.AddWithValue("$title", title.Trim());
                command.Parameters.AddWithValue("$isPinned", pin ? 1 : 0);
                command.Parameters.AddWithValue("$dueAtUtc", dueAtLocal?.ToUniversalTime().ToString("O") ?? (object)DBNull.Value);
                command.Parameters.AddWithValue("$createdAtUtc", DateTime.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
            }
        });
    }

    public Task ToggleCompletedAsync(long id, bool completed)
    {
        return UpdateFlagAsync(id, "is_completed", completed);
    }

    public Task TogglePinnedAsync(long id, bool pinned)
    {
        return UpdateFlagAsync(id, "is_pinned", pinned);
    }

    public Task DeleteAsync(long id)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM todos WHERE id = $id;";
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        });
    }

    public Task<List<TodoItem>> GetDueReminderItemsAsync(DateTime utcNow)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT id, title, is_completed, is_pinned, due_at_utc, created_at_utc, reminded_at_utc
                    FROM todos
                    WHERE is_completed = 0
                      AND due_at_utc IS NOT NULL
                      AND due_at_utc <= $utcNow
                      AND reminded_at_utc IS NULL
                    ORDER BY due_at_utc ASC;
                    """;
                command.Parameters.AddWithValue("$utcNow", utcNow.ToString("O"));
                using var reader = command.ExecuteReader();
                var dueItems = new List<TodoItem>();
                while (reader.Read())
                {
                    dueItems.Add(new TodoItem
                    {
                        Id = reader.GetInt64(0),
                        Title = reader.GetString(1),
                        IsCompleted = reader.GetInt64(2) == 1,
                        IsPinned = reader.GetInt64(3) == 1,
                        DueAtUtc = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
                        CreatedAtUtc = DateTime.Parse(reader.GetString(5)).ToUniversalTime(),
                        RemindedAtUtc = reader.IsDBNull(6) ? null : DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
                    });
                }

                return dueItems;
            }
        });
    }

    public Task MarkReminderShownAsync(long id, DateTime shownAtUtc)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "UPDATE todos SET reminded_at_utc = $shownAtUtc WHERE id = $id;";
                command.Parameters.AddWithValue("$shownAtUtc", shownAtUtc.ToString("O"));
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        });
    }

    private Task UpdateFlagAsync(long id, string field, bool value)
    {
        return Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = $"UPDATE todos SET {field} = $value WHERE id = $id;";
                command.Parameters.AddWithValue("$value", value ? 1 : 0);
                command.Parameters.AddWithValue("$id", id);
                command.ExecuteNonQuery();
            }
        });
    }
}
