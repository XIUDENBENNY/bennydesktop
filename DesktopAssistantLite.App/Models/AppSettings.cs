using System.Text.Json.Serialization;

namespace DesktopAssistantLite.App.Models;

internal sealed class AppSettings
{
    public bool StartupEnabled { get; set; }

    public bool ScreenshotHotkeyEnabled { get; set; } = true;

    public bool ScreenshotHotkeyYieldToQq { get; set; } = true;

    public bool FloatingBallVisible { get; set; }

    public bool FloatingBallAutoHide { get; set; } = true;

    public List<string> SearchPaths { get; set; } = [];

    public string ScreenshotSaveDir { get; set; } = string.Empty;

    public Dictionary<string, List<string>> CategoryRules { get; set; } = [];

    public int MemoryRefreshIntervalSeconds { get; set; } = 5;

    public bool BoostExplorerRestartEnabled { get; set; }

    [JsonIgnore]
    public IReadOnlyList<string> DefaultCategoryOrder =>
    [
        "文档",
        "图片",
        "压缩包",
        "安装包",
        "文件夹",
        "其他",
        "桌面保留",
    ];

    public AppSettings Clone()
    {
        return new AppSettings
        {
            StartupEnabled = StartupEnabled,
            ScreenshotHotkeyEnabled = ScreenshotHotkeyEnabled,
            ScreenshotHotkeyYieldToQq = ScreenshotHotkeyYieldToQq,
            FloatingBallVisible = FloatingBallVisible,
            FloatingBallAutoHide = FloatingBallAutoHide,
            SearchPaths = [.. SearchPaths],
            ScreenshotSaveDir = ScreenshotSaveDir,
            CategoryRules = CategoryRules.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.OrdinalIgnoreCase),
            MemoryRefreshIntervalSeconds = MemoryRefreshIntervalSeconds,
            BoostExplorerRestartEnabled = BoostExplorerRestartEnabled,
        };
    }

    public static AppSettings CreateDefault(AppPaths paths)
    {
        return new AppSettings
        {
            StartupEnabled = false,
            ScreenshotHotkeyEnabled = true,
            ScreenshotHotkeyYieldToQq = true,
            FloatingBallVisible = false,
            FloatingBallAutoHide = true,
            SearchPaths =
            [
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"),
            ],
            ScreenshotSaveDir = paths.ScreenshotDirectory,
            CategoryRules = CreateDefaultCategoryRules(),
            MemoryRefreshIntervalSeconds = 5,
            BoostExplorerRestartEnabled = false,
        };
    }

    public static Dictionary<string, List<string>> CreateDefaultCategoryRules()
    {
        return new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["文档"] = [".txt", ".md", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".pdf", ".csv"],
            ["图片"] = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".webp", ".ico"],
            ["压缩包"] = [".zip", ".7z", ".rar", ".tar", ".gz"],
            ["安装包"] = [".exe", ".msi", ".bat", ".cmd"],
        };
    }
}
