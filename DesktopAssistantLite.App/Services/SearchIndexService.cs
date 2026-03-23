using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Storage;
using Microsoft.Data.Sqlite;

namespace DesktopAssistantLite.App.Services;

internal sealed class SearchIndexService : IDisposable
{
    private readonly DatabaseService _databaseService;
    private readonly LogService _logService;
    private readonly object _syncRoot = new();
    private readonly List<FileSystemWatcher> _watchers = [];
    private CancellationTokenSource? _rebuildCts;

    public SearchIndexService(DatabaseService databaseService, LogService logService)
    {
        _databaseService = databaseService;
        _logService = logService;
    }

    public async Task InitializeAsync(IEnumerable<string> paths)
    {
        var normalizedPaths = NormalizePaths(paths);
        RestartWatchers(normalizedPaths);
        await RebuildAsync(normalizedPaths);
    }

    public async Task RebuildAsync(IEnumerable<string> paths)
    {
        var normalizedPaths = NormalizePaths(paths);
        CancelPendingRebuild();
        var rebuildCts = new CancellationTokenSource();
        _rebuildCts = rebuildCts;
        var token = rebuildCts.Token;

        await Task.Run(() =>
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();

                using var deleteCommand = connection.CreateCommand();
                deleteCommand.Transaction = transaction;
                deleteCommand.CommandText = "DELETE FROM file_index;";
                deleteCommand.ExecuteNonQuery();

                foreach (var root in normalizedPaths)
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    foreach (var path in EnumeratePathsSafe(root))
                    {
                        if (token.IsCancellationRequested)
                        {
                            return;
                        }

                        try
                        {
                            UpsertInternal(connection, transaction, root, path);
                        }
                        catch (Exception ex)
                        {
                            _logService.Error($"Failed to index path: {path}", ex);
                        }
                    }
                }

                transaction.Commit();
            }
        }, token);

        _logService.Info("Search index rebuild completed.");
    }

    public Task<List<SearchResult>> SearchAsync(string query, int limit = 200)
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
                    SELECT name, full_path, directory_path, is_directory, last_write_time_utc
                    FROM file_index
                    WHERE lower(name) LIKE $query OR lower(full_path) LIKE $query
                    ORDER BY is_directory DESC, name ASC
                    LIMIT $limit;
                    """;
                command.Parameters.AddWithValue("$query", $"%{query.Trim().ToLowerInvariant()}%");
                command.Parameters.AddWithValue("$limit", limit);

                using var reader = command.ExecuteReader();
                var results = new List<SearchResult>();
                while (reader.Read())
                {
                    results.Add(new SearchResult
                    {
                        Name = reader.GetString(0),
                        FullPath = reader.GetString(1),
                        DirectoryPath = reader.GetString(2),
                        IsDirectory = reader.GetInt64(3) == 1,
                        LastWriteTimeUtc = DateTime.Parse(reader.GetString(4)).ToUniversalTime(),
                    });
                }

                return results;
            }
        });
    }

    public void Dispose()
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();
        CancelPendingRebuild();
    }

    private void CancelPendingRebuild()
    {
        var cts = Interlocked.Exchange(ref _rebuildCts, null);
        if (cts is null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore disposed token sources during shutdown.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private void RestartWatchers(List<string> paths)
    {
        foreach (var watcher in _watchers)
        {
            watcher.Dispose();
        }

        _watchers.Clear();

        foreach (var root in paths)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            var watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
                EnableRaisingEvents = true,
            };

            watcher.Created += (_, args) => UpsertPath(root, args.FullPath);
            watcher.Changed += (_, args) => UpsertPath(root, args.FullPath);
            watcher.Deleted += (_, args) => DeletePath(args.FullPath);
            watcher.Renamed += (_, args) =>
            {
                DeletePath(args.OldFullPath);
                UpsertPath(root, args.FullPath);
            };
            watcher.Error += (_, args) => _logService.Error($"Search watcher error on {root}", args.GetException());

            _watchers.Add(watcher);
        }
    }

    private void UpsertPath(string root, string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        try
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var transaction = connection.BeginTransaction();
                UpsertInternal(connection, transaction, root, path);
                transaction.Commit();
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to upsert search index path: {path}", ex);
        }
    }

    private void DeletePath(string path)
    {
        try
        {
            lock (_syncRoot)
            {
                using var connection = _databaseService.CreateConnection();
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "DELETE FROM file_index WHERE full_path = $path OR full_path LIKE $prefix;";
                command.Parameters.AddWithValue("$path", path);
                command.Parameters.AddWithValue("$prefix", $"{path}{Path.DirectorySeparatorChar}%");
                command.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to delete search index path: {path}", ex);
        }
    }

    private static void UpsertInternal(SqliteConnection connection, SqliteTransaction transaction, string root, string path)
    {
        var isDirectory = Directory.Exists(path);
        FileSystemInfo info = isDirectory ? new DirectoryInfo(path) : new FileInfo(path);

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO file_index(full_path, root_path, name, directory_path, is_directory, last_write_time_utc)
            VALUES ($fullPath, $rootPath, $name, $directoryPath, $isDirectory, $lastWriteTimeUtc)
            ON CONFLICT(full_path) DO UPDATE SET
                root_path = excluded.root_path,
                name = excluded.name,
                directory_path = excluded.directory_path,
                is_directory = excluded.is_directory,
                last_write_time_utc = excluded.last_write_time_utc;
            """;
        command.Parameters.AddWithValue("$fullPath", path);
        command.Parameters.AddWithValue("$rootPath", root);
        command.Parameters.AddWithValue("$name", Path.GetFileName(path));
        command.Parameters.AddWithValue("$directoryPath", Path.GetDirectoryName(path) ?? root);
        command.Parameters.AddWithValue("$isDirectory", isDirectory ? 1 : 0);
        command.Parameters.AddWithValue("$lastWriteTimeUtc", info.LastWriteTimeUtc.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static IEnumerable<string> EnumeratePathsSafe(string root)
    {
        var queue = new Queue<string>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            yield return current;

            IEnumerable<string> directories = [];
            IEnumerable<string> files = [];

            try
            {
                directories = Directory.EnumerateDirectories(current);
            }
            catch
            {
                // Ignore inaccessible directories.
            }

            try
            {
                files = Directory.EnumerateFiles(current);
            }
            catch
            {
                // Ignore inaccessible files.
            }

            foreach (var directory in directories)
            {
                queue.Enqueue(directory);
            }

            foreach (var file in files)
            {
                yield return file;
            }
        }
    }

    private static List<string> NormalizePaths(IEnumerable<string> paths)
    {
        return paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Where(path => Directory.Exists(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
