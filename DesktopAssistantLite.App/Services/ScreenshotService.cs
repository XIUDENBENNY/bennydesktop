using DesktopAssistantLite.App.Storage;

namespace DesktopAssistantLite.App.Services;

internal sealed class ScreenshotService
{
    private readonly AppPaths _appPaths;
    private readonly LogService _logService;

    public ScreenshotService(AppPaths appPaths, LogService logService)
    {
        _appPaths = appPaths;
        _logService = logService;
    }

    public string SaveScreenshot(Image image, string targetDirectory)
    {
        var directory = string.IsNullOrWhiteSpace(targetDirectory) ? _appPaths.ScreenshotDirectory : targetDirectory;
        Directory.CreateDirectory(directory);
        var filePath = Path.Combine(directory, $"Capture_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        image.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
        _logService.Info($"Screenshot saved to {filePath}");
        return filePath;
    }
}
