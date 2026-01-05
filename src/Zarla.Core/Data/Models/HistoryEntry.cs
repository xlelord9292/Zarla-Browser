namespace Zarla.Core.Data.Models;

public record HistoryEntry
{
    public long Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public DateTime VisitedAt { get; init; } = DateTime.UtcNow;
    public int VisitCount { get; init; } = 1;
    public string? Favicon { get; init; }
}
