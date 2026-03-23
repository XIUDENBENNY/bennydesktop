namespace DesktopAssistantLite.App;

internal sealed class AppPaths
{
    public AppPaths()
    {
        BaseDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopAssistantLite");
        LogDirectory = Path.Combine(BaseDirectory, "logs");
        CacheDirectory = Path.Combine(BaseDirectory, "cache");
        ScreenshotDirectory = Path.Combine(BaseDirectory, "screenshots");
        SettingsPath = Path.Combine(BaseDirectory, "settings.json");
        DatabasePath = Path.Combine(BaseDirectory, "app.db");
    }

    public string BaseDirectory { get; }

    public string LogDirectory { get; }

    public string CacheDirectory { get; }

    public string ScreenshotDirectory { get; }

    public string SettingsPath { get; }

    public string DatabasePath { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(BaseDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(ScreenshotDirectory);
    }
}
