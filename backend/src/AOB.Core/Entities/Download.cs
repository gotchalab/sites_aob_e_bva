namespace AOB.Core.Entities;

public class Download
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string FileName { get; set; } = "";
    public string StoragePath { get; set; } = "";
    public long FileSize { get; set; }
    public string MimeType { get; set; } = "";

    public int DownloadCount { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }

    public int? LegacyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
