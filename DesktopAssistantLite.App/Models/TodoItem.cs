namespace DesktopAssistantLite.App.Models;

internal sealed class TodoItem
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPinned { get; init; }

    public DateTime? DueAtUtc { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? RemindedAtUtc { get; init; }
}
