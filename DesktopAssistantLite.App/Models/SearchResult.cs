namespace DesktopAssistantLite.App.Models;

internal sealed class SearchResult
{
    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public string DirectoryPath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public DateTime LastWriteTimeUtc { get; init; }
}
