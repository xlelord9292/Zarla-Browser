namespace Zarla.Core.Data.Models;

public record Bookmark
{
    public long Id { get; init; }
    public required string Url { get; init; }
    public required string Title { get; init; }
    public string? Favicon { get; init; }
    public long? FolderId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int SortOrder { get; init; }
}

public record BookmarkFolder
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public long? ParentId { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public int SortOrder { get; init; }
}
