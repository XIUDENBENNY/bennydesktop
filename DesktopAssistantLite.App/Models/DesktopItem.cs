namespace DesktopAssistantLite.App.Models;

internal sealed class DesktopItem
{
    public long Id { get; init; }

    public long SnapshotId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string FullPath { get; init; } = string.Empty;

    public string OriginalPath { get; init; } = string.Empty;

    public string Category { get; init; } = "其他";

    public string ItemType { get; init; } = string.Empty;

    public string LocationLabel { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public bool CanMove { get; init; } = true;

    public bool CanAutoOrganize { get; init; } = true;

    public DateTime LastWriteTimeUtc { get; init; }
}
