namespace AOB.Core.Entities;

public class Category
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public ICollection<Category> Children { get; set; } = new List<Category>();

    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; } = true;

    public int? LegacyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<Download> Downloads { get; set; } = new List<Download>();
}
