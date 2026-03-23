namespace DesktopAssistantLite.App.Models;

internal sealed class MemoryBoostResult
{
    public int TrimmedProcessCount { get; set; }

    public int DeletedItemCount { get; set; }

    public long EstimatedFreedBytes { get; set; }

    public bool ExplorerRestarted { get; set; }
}
