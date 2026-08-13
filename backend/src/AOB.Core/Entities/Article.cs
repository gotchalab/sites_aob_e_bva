namespace AOB.Core.Entities;

public class Article
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Excerpt { get; set; }
    public string Content { get; set; } = "";
    public string? CoverImagePath { get; set; }

    public string? AuthorId { get; set; }
    public ApplicationUser? Author { get; set; }

    public bool IsPublished { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime? PublishedAt { get; set; }

    public List<string> Tags { get; set; } = new();
    public int ViewCount { get; set; }

    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }

    public int? LegacyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
