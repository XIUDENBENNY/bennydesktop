using System.Text;

namespace DesktopAssistantLite.App.Storage;

internal sealed class LogService
{
    private readonly object _syncRoot = new();
    private readonly string _logDirectory;

    public LogService(string logDirectory)
    {
        _logDirectory = logDirectory;
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception? exception = null)
    {
        var builder = new StringBuilder(message);
        if (exception is not null)
        {
            builder.AppendLine();
            builder.AppendLine(exception.ToString());
        }

        Write("ERROR", builder.ToString());
    }

    private void Write(string level, string message)
    {
        lock (_syncRoot)
        {
            Directory.CreateDirectory(_logDirectory);
            CleanupOldLogs();

            var filePath = Path.Combine(_logDirectory, $"{DateTime.Now:yyyy-MM-dd}.log");
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}{Environment.NewLine}";
            File.AppendAllText(filePath, line, Encoding.UTF8);
        }
    }

    private void CleanupOldLogs()
    {
        foreach (var file in Directory.EnumerateFiles(_logDirectory, "*.log"))
        {
            try
            {
                var info = new FileInfo(file);
                if (info.CreationTimeUtc < DateTime.UtcNow.AddDays(-7))
                {
                    info.Delete();
                }
            }
            catch
            {
                // Ignore log cleanup failures.
            }
        }
    }
}
