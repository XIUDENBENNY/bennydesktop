using System.Text.Json;
using DesktopAssistantLite.App.Models;

namespace DesktopAssistantLite.App.Storage;

internal sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _settingsPath;
    private readonly AppPaths _appPaths;

    public SettingsService(string settingsPath, AppPaths appPaths)
    {
        _settingsPath = settingsPath;
        _appPaths = appPaths;
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
        {
            var defaults = AppSettings.CreateDefault(_appPaths);
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? AppSettings.CreateDefault(_appPaths);
            return Normalize(loaded);
        }
        catch
        {
            var defaults = AppSettings.CreateDefault(_appPaths);
            Save(defaults);
            return defaults;
        }
    }

    public void Save(AppSettings settings)
    {
        var normalized = Normalize(settings);
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private AppSettings Normalize(AppSettings settings)
    {
        var normalized = settings.Clone();
        normalized.SearchPaths = normalized.SearchPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.SearchPaths.Count == 0)
        {
            normalized.SearchPaths = AppSettings.CreateDefault(_appPaths).SearchPaths;
        }

        if (string.IsNullOrWhiteSpace(normalized.ScreenshotSaveDir))
        {
            normalized.ScreenshotSaveDir = _appPaths.ScreenshotDirectory;
        }

        Directory.CreateDirectory(normalized.ScreenshotSaveDir);

        if (normalized.MemoryRefreshIntervalSeconds < 2)
        {
            normalized.MemoryRefreshIntervalSeconds = 5;
        }

        if (normalized.CategoryRules.Count == 0)
        {
            normalized.CategoryRules = AppSettings.CreateDefaultCategoryRules();
        }

        return normalized;
    }
}
