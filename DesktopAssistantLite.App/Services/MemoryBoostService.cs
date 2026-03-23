using System.Diagnostics;
using DesktopAssistantLite.App.Models;
using DesktopAssistantLite.App.Storage;

namespace DesktopAssistantLite.App.Services;

internal sealed class MemoryBoostService
{
    private readonly AppPaths _appPaths;
    private readonly LogService _logService;

    public MemoryBoostService(AppPaths appPaths, LogService logService)
    {
        _appPaths = appPaths;
        _logService = logService;
    }

    public Task<MemoryBoostResult> RunSafeBoostAsync(bool restartExplorer)
    {
        return Task.Run(() =>
        {
            var result = new MemoryBoostResult();
            var currentSessionId = Process.GetCurrentProcess().SessionId;

            foreach (var process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Environment.ProcessId || process.SessionId != currentSessionId)
                    {
                        continue;
                    }

                    if (process.WorkingSet64 < 40 * 1024 * 1024)
                    {
                        continue;
                    }

                    if (IsProtectedProcess(process.ProcessName))
                    {
                        continue;
                    }

                    var before = process.WorkingSet64;
                    var handle = NativeMethods.OpenProcess(
                        NativeMethods.ProcessQueryInformation | NativeMethods.ProcessSetQuota,
                        bInheritHandle: false,
                        process.Id);
                    if (handle == IntPtr.Zero)
                    {
                        continue;
                    }

                    try
                    {
                        if (NativeMethods.EmptyWorkingSet(handle))
                        {
                            result.TrimmedProcessCount++;
                            result.EstimatedFreedBytes += before / 20;
                        }
                    }
                    finally
                    {
                        NativeMethods.CloseHandle(handle);
                    }
                }
                catch
                {
                    // Ignore individual process failures.
                }
                finally
                {
                    process.Dispose();
                }
            }

            var deletedBytes = 0L;
            result.DeletedItemCount += CleanupDirectory(Path.GetTempPath(), ref deletedBytes);
            result.DeletedItemCount += CleanupDirectory(_appPaths.CacheDirectory, ref deletedBytes);
            result.EstimatedFreedBytes += deletedBytes;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            if (restartExplorer)
            {
                RestartExplorer();
                result.ExplorerRestarted = true;
            }

            _logService.Info(
                $"Memory boost finished. Trimmed={result.TrimmedProcessCount}, Deleted={result.DeletedItemCount}, EstimatedFreedBytes={result.EstimatedFreedBytes}");
            return result;
        });
    }

    private static bool IsProtectedProcess(string processName)
    {
        var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Idle",
            "System",
            "wininit",
            "csrss",
            "services",
            "lsass",
            "dwm",
            "smss",
            "svchost",
            "fontdrvhost",
            "audiodg",
        };

        return protectedNames.Contains(processName);
    }

    private static int CleanupDirectory(string root, ref long estimatedFreedBytes)
    {
        if (!Directory.Exists(root))
        {
            return 0;
        }

        var deletedCount = 0;
        var cutoff = DateTime.UtcNow.AddDays(-1);

        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.LastWriteTimeUtc > cutoff)
                {
                    continue;
                }

                estimatedFreedBytes += info.Length;
                info.Delete();
                deletedCount++;
            }
            catch
            {
                // Ignore temp cleanup failures.
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(path => path.Length))
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
                // Ignore directory cleanup failures.
            }
        }

        return deletedCount;
    }

    private static void RestartExplorer()
    {
        foreach (var explorer in Process.GetProcessesByName("explorer"))
        {
            try
            {
                explorer.Kill();
                explorer.WaitForExit(5000);
            }
            catch
            {
                // Ignore explorer restart failures.
            }
            finally
            {
                explorer.Dispose();
            }
        }

        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        }
        catch
        {
            // Ignore restart failures.
        }
    }
}
