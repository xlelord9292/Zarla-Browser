namespace Zarla.Core.Data.Models;

public enum DownloadStatus
{
    Pending,
    InProgress,
    Completed,
    Failed,
    Cancelled,
    Paused
}

public record Download
{
    public long Id { get; init; }
    public required string Url { get; init; }
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public long TotalBytes { get; init; }
    public long ReceivedBytes { get; init; }
    public DownloadStatus Status { get; init; } = DownloadStatus.Pending;
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; init; }
    public string? MimeType { get; init; }
}
