using System.Diagnostics;
using System.Runtime.InteropServices;
using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Storage;
using Microsoft.Data.Sqlite;

namespace DesktopAssistantLite.App.Services;

internal sealed class DesktopOrganizerService
{
    private const string ManagedFolderPrefix = "桌面整理_";
    private const string DesktopReservedCategory = "桌面保留";

    private readonly DatabaseService _databaseService;
    private readonly LogService _logService;
    private readonly object _syncRoot = new();

    public DesktopOrganizerService(DatabaseService databaseService, LogService logService)
    {
        _databaseService = databaseService;
        _logService = logService;
    }

    public async Task<(long SnapshotId, DateTime CreatedAtUtc, IReadOnlyDictionary<string, List<DesktopItem>> Groups)> OrganizeAsync(
        Dictionary<string, List<string>> categoryRules,
        IReadOnlyList<string> categoryOrder)
    {
        var items = ScanDesktop(categoryRules, includeManagedFolders: true);
        var managedItems = items.Where(item => item.CanMove).ToList();
        var createdAtUtc = DateTime.UtcNow;
        var snapshotId = SaveSnapshot(managedItems, createdAtUtc);
        MoveItemsIntoCategoryFolders(managedItems);
        var groups = await LoadCurrentGroupsAsync(categoryRules, categoryOrder);
        _logService.Info($"Desktop organized with snapshot {snapshotId}, movable item count {managedItems.Count}.");
        return (snapshotId, createdAtUtc, groups);
    }

    public bool ArrangeDesktopIcons()
    {
        var sorted = TrySortDesktopIconsByType();
        var listViewHandle = FindDesktopListViewHandle();
        if (listViewHandle == IntPtr.Zero)
        {
            return sorted;
        }

        NativeMethods.SendMessage(listViewHandle, NativeMethods.LvmArrange, (IntPtr)NativeMethods.LvaDefault, IntPtr.Zero);
        return true;
    }

    public Task<(long SnapshotId, DateTime CreatedAtUtc, IReadOnlyDictionary<string, List<DesktopItem>> Groups)?> RestoreLatestSnapshotAsync(
        IReadOnlyList<string> categoryOrder)
    {
        long snapshotId;
        DateTime createdAtUtc;
        List<DesktopItem> items;

        lock (_syncRoot)
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();

            using var snapshotCommand = connection.CreateCommand();
            snapshotCommand.CommandText =
                """
                SELECT id, created_at_utc
                FROM layout_snapshots
                ORDER BY id DESC
                LIMIT 1;
                """;

            using var snapshotReader = snapshotCommand.ExecuteReader();
            if (!snapshotReader.Read())
            {
                return Task.FromResult<(long SnapshotId, DateTime CreatedAtUtc, IReadOnlyDictionary<string, List<DesktopItem>> Groups)?>(null);
            }

            snapshotId = snapshotReader.GetInt64(0);
            createdAtUtc = DateTime.Parse(snapshotReader.GetString(1)).ToUniversalTime();
            items = LoadSnapshotItems(connection, snapshotId);
        }

        RestoreItemsToDesktop(items);
        CleanupManagedFolders();
        var groups = GroupItems(items, categoryOrder);
        _logService.Info($"Desktop snapshot {snapshotId} restored.");
        return Task.FromResult<(long SnapshotId, DateTime CreatedAtUtc, IReadOnlyDictionary<string, List<DesktopItem>> Groups)?>((snapshotId, createdAtUtc, groups));
    }

    public Task<IReadOnlyDictionary<string, List<DesktopItem>>> LoadCurrentGroupsAsync(
        Dictionary<string, List<string>> categoryRules,
        IReadOnlyList<string> categoryOrder)
    {
        return Task.Run<IReadOnlyDictionary<string, List<DesktopItem>>>(() =>
        {
            var items = ScanDesktop(categoryRules, includeManagedFolders: true);
            return GroupItems(items, categoryOrder);
        });
    }

    public Task MoveItemToCategoryAsync(DesktopItem item, string targetCategory)
    {
        return Task.Run(() =>
        {
            if (!item.CanMove)
            {
                throw new InvalidOperationException("当前项目不参与整理，不能移动分类。");
            }

            var sourcePath = ResolveExistingPath(item);
            if (sourcePath is null)
            {
                throw new FileNotFoundException("未找到要移动的桌面项目。", item.FullPath);
            }

            var targetPath = BuildManagedPath(targetCategory, item.Name);
            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (Path.Exists(targetPath))
            {
                throw new IOException($"目标位置已存在同名项目：{targetPath}");
            }

            MovePath(sourcePath, targetPath);
            _logService.Info($"Desktop item moved from {sourcePath} to {targetPath}");
        });
    }

    public void OpenItem(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to open desktop item: {path}", ex);
        }
    }

    public void OpenContainingFolder(string path)
    {
        try
        {
            var target = File.Exists(path) || Directory.Exists(path)
                ? $"/select,\"{path}\""
                : $"\"{Path.GetDirectoryName(path)}\"";
            Process.Start(new ProcessStartInfo("explorer.exe", target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logService.Error($"Failed to open containing folder: {path}", ex);
        }
    }

    private List<DesktopItem> ScanDesktop(Dictionary<string, List<string>> categoryRules, bool includeManagedFolders)
    {
        var desktopPath = GetDesktopPath();
        var items = new List<DesktopItem>();

        foreach (var entry in Directory.EnumerateFileSystemEntries(desktopPath))
        {
            try
            {
                var name = Path.GetFileName(entry);
                if (includeManagedFolders && IsManagedCategoryDirectory(entry, name))
                {
                    var category = name[ManagedFolderPrefix.Length..];
                    foreach (var managedChild in Directory.EnumerateFileSystemEntries(entry))
                    {
                        TryAppendItem(items, managedChild, categoryRules, category, isManagedFolderItem: true);
                    }

                    continue;
                }

                if (IsManagedCategoryDirectory(entry, name))
                {
                    continue;
                }

                TryAppendItem(items, entry, categoryRules, explicitCategory: null, isManagedFolderItem: false);
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to scan desktop entry: {entry}", ex);
            }
        }

        return items
            .OrderBy(item => item.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private void TryAppendItem(
        List<DesktopItem> items,
        string path,
        Dictionary<string, List<string>> categoryRules,
        string? explicitCategory,
        bool isManagedFolderItem)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden ||
            (attributes & FileAttributes.System) == FileAttributes.System)
        {
            return;
        }

        var name = Path.GetFileName(path);
        var isDirectory = Directory.Exists(path);
        var isShortcut = !isDirectory && IsShortcut(name);
        var info = isDirectory ? new DirectoryInfo(path) as FileSystemInfo : new FileInfo(path);

        var category = explicitCategory ?? DetermineCategory(path, isDirectory, categoryRules, isShortcut);
        var isReserved = string.Equals(category, DesktopReservedCategory, StringComparison.OrdinalIgnoreCase);
        var originalPath = isReserved ? path : Path.Combine(GetDesktopPath(), name);
        var locationLabel = isManagedFolderItem ? $"分类目录/{category}" : "桌面";
        var statusLabel = isReserved ? "桌面保留，不参与整理" : isManagedFolderItem ? "已整理" : "未整理";

        items.Add(new DesktopItem
        {
            Name = name,
            FullPath = path,
            OriginalPath = originalPath,
            Category = category,
            ItemType = isDirectory ? "文件夹" : InferItemType(path),
            LocationLabel = locationLabel,
            StatusLabel = statusLabel,
            CanMove = !isReserved,
            LastWriteTimeUtc = info.LastWriteTimeUtc,
        });
    }

    private long SaveSnapshot(List<DesktopItem> items, DateTime createdAtUtc)
    {
        lock (_syncRoot)
        {
            using var connection = _databaseService.CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var snapshotCommand = connection.CreateCommand();
            snapshotCommand.Transaction = transaction;
            snapshotCommand.CommandText =
                """
                INSERT INTO layout_snapshots(created_at_utc)
                VALUES ($createdAtUtc);
                SELECT last_insert_rowid();
                """;
            snapshotCommand.Parameters.AddWithValue("$createdAtUtc", createdAtUtc.ToString("O"));
            var snapshotId = (long)(snapshotCommand.ExecuteScalar() ?? 0L);

            foreach (var item in items)
            {
                using var itemCommand = connection.CreateCommand();
                itemCommand.Transaction = transaction;
                itemCommand.CommandText =
                    """
                    INSERT INTO desktop_items(snapshot_id, name, full_path, category, item_type, last_write_time_utc)
                    VALUES ($snapshotId, $name, $fullPath, $category, $itemType, $lastWriteTimeUtc);
                    """;
                itemCommand.Parameters.AddWithValue("$snapshotId", snapshotId);
                itemCommand.Parameters.AddWithValue("$name", item.Name);
                itemCommand.Parameters.AddWithValue("$fullPath", item.OriginalPath);
                itemCommand.Parameters.AddWithValue("$category", item.Category);
                itemCommand.Parameters.AddWithValue("$itemType", item.ItemType);
                itemCommand.Parameters.AddWithValue("$lastWriteTimeUtc", item.LastWriteTimeUtc.ToString("O"));
                itemCommand.ExecuteNonQuery();
            }

            transaction.Commit();
            return snapshotId;
        }
    }

    private void MoveItemsIntoCategoryFolders(IEnumerable<DesktopItem> items)
    {
        foreach (var item in items)
        {
            var sourcePath = ResolveExistingPath(item);
            if (sourcePath is null)
            {
                continue;
            }

            var targetPath = BuildManagedPath(item.Category, item.Name);
            if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                if (Path.Exists(targetPath))
                {
                    _logService.Info($"Skip move because destination already exists: {targetPath}");
                    continue;
                }

                MovePath(sourcePath, targetPath);
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to move desktop item into category folder: {sourcePath}", ex);
            }
        }
    }

    private void RestoreItemsToDesktop(IEnumerable<DesktopItem> items)
    {
        foreach (var item in items)
        {
            var targetPath = item.OriginalPath;
            var sourcePath = ResolveExistingPath(item) ?? BuildManagedPath(item.Category, item.Name);

            try
            {
                if (!Path.Exists(sourcePath))
                {
                    continue;
                }

                if (string.Equals(sourcePath, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Path.Exists(targetPath))
                {
                    _logService.Info($"Skip restore because target already exists: {targetPath}");
                    continue;
                }

                MovePath(sourcePath, targetPath);
            }
            catch (Exception ex)
            {
                _logService.Error($"Failed to restore desktop item: {sourcePath}", ex);
            }
        }
    }

    private static IReadOnlyDictionary<string, List<DesktopItem>> GroupItems(IEnumerable<DesktopItem> items, IReadOnlyList<string> categoryOrder)
    {
        var grouped = categoryOrder.ToDictionary(name => name, _ => new List<DesktopItem>(), StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!grouped.TryGetValue(item.Category, out var list))
            {
                list = [];
                grouped[item.Category] = list;
            }

            list.Add(item);
        }

        return grouped;
    }

    private static List<DesktopItem> LoadSnapshotItems(SqliteConnection connection, long snapshotId)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, snapshot_id, name, full_path, category, item_type, last_write_time_utc
            FROM desktop_items
            WHERE snapshot_id = $snapshotId
            ORDER BY category, name;
            """;
        command.Parameters.AddWithValue("$snapshotId", snapshotId);

        using var reader = command.ExecuteReader();
        var items = new List<DesktopItem>();
        while (reader.Read())
        {
            var originalPath = reader.GetString(3);
            items.Add(new DesktopItem
            {
                Id = reader.GetInt64(0),
                SnapshotId = reader.GetInt64(1),
                Name = reader.GetString(2),
                FullPath = originalPath,
                OriginalPath = originalPath,
                Category = reader.GetString(4),
                ItemType = reader.GetString(5),
                LocationLabel = "桌面",
                StatusLabel = "恢复后在桌面",
                CanMove = true,
                LastWriteTimeUtc = DateTime.Parse(reader.GetString(6)).ToUniversalTime(),
            });
        }

        return items;
    }

    private static string DetermineCategory(string path, bool isDirectory, Dictionary<string, List<string>> categoryRules, bool isShortcut)
    {
        if (isShortcut)
        {
            return DesktopReservedCategory;
        }

        if (isDirectory)
        {
            return "文件夹";
        }

        var extension = Path.GetExtension(path);
        foreach (var pair in categoryRules)
        {
            if (pair.Value.Any(rule => string.Equals(rule, extension, StringComparison.OrdinalIgnoreCase)))
            {
                return pair.Key;
            }
        }

        return "其他";
    }

    private static string InferItemType(string path)
    {
        var extension = Path.GetExtension(path);
        return string.IsNullOrWhiteSpace(extension) ? "文件" : extension.TrimStart('.').ToUpperInvariant();
    }

    private static bool IsShortcut(string name)
    {
        var extension = Path.GetExtension(name);
        return extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
    }

    private bool TrySortDesktopIconsByType()
    {
        object? shell = null;
        object? windows = null;
        object? desktopWindow = null;
        object? document = null;

        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType is null)
            {
                return false;
            }

            shell = Activator.CreateInstance(shellType);
            if (shell is null)
            {
                return false;
            }

            dynamic shellApp = shell;
            windows = shellApp.Windows();
            if (windows is null)
            {
                return false;
            }

            dynamic shellWindows = windows;
            object location = Type.Missing;
            object root = Type.Missing;
            object hwnd = 0;
            desktopWindow = shellWindows.FindWindowSW(location, root, 8, ref hwnd, 1);
            if (desktopWindow is null)
            {
                return false;
            }

            dynamic desktop = desktopWindow;
            document = desktop.Document;
            if (document is null)
            {
                return false;
            }

            dynamic folderView = document;
            folderView.SortColumns = "prop:System.ItemTypeText;System.ItemNameDisplay;";
            _logService.Info("Desktop icons sorted by item type.");
            return true;
        }
        catch (Exception ex)
        {
            _logService.Error("Failed to sort desktop icons by item type.", ex);
            return false;
        }
        finally
        {
            ReleaseComObject(document);
            ReleaseComObject(desktopWindow);
            ReleaseComObject(windows);
            ReleaseComObject(shell);
        }
    }

    private static void MovePath(string sourcePath, string targetPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        if (Directory.Exists(sourcePath))
        {
            Directory.Move(sourcePath, targetPath);
        }
        else
        {
            File.Move(sourcePath, targetPath);
        }
    }

    private static bool IsManagedCategoryDirectory(string path, string name)
    {
        return Directory.Exists(path) && name.StartsWith(ManagedFolderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDesktopPath()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    }

    private static string BuildManagedPath(string category, string name)
    {
        return Path.Combine(GetDesktopPath(), $"{ManagedFolderPrefix}{category}", name);
    }

    private static void CleanupManagedFolders()
    {
        var desktopPath = GetDesktopPath();
        foreach (var directory in Directory.EnumerateDirectories(desktopPath, $"{ManagedFolderPrefix}*"))
        {
            try
            {
                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory, recursive: false);
                }
            }
            catch
            {
                // Ignore cleanup failures.
            }
        }
    }

    private static string? ResolveExistingPath(DesktopItem item)
    {
        if (Path.Exists(item.FullPath))
        {
            return item.FullPath;
        }

        if (Path.Exists(item.OriginalPath))
        {
            return item.OriginalPath;
        }

        var managedPath = BuildManagedPath(item.Category, item.Name);
        return Path.Exists(managedPath) ? managedPath : null;
    }

    private static void ReleaseComObject(object? value)
    {
        if (value is not null && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }

    private static IntPtr FindDesktopListViewHandle()
    {
        var progman = NativeMethods.FindWindow("Progman", "Program Manager");
        var shellView = NativeMethods.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);

        if (shellView == IntPtr.Zero)
        {
            var worker = IntPtr.Zero;
            while ((worker = NativeMethods.FindWindowEx(IntPtr.Zero, worker, "WorkerW", null)) != IntPtr.Zero)
            {
                shellView = NativeMethods.FindWindowEx(worker, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (shellView != IntPtr.Zero)
                {
                    break;
                }
            }
        }

        if (shellView == IntPtr.Zero)
        {
            return IntPtr.Zero;
        }

        return NativeMethods.FindWindowEx(shellView, IntPtr.Zero, "SysListView32", "FolderView");
    }
}
