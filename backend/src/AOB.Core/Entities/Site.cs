namespace AOB.Core.Entities;

public class Site
{
    public int Id { get; set; }
    public string Slug { get; set; } = "";
    public string Name { get; set; } = "";
    public string Domain { get; set; } = "";
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? PrimaryColor { get; set; }
    public string? ContactEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? Tagline { get; set; }
    public string? HomeConfig { get; set; }
    public string? Announcement { get; set; }

    public ICollection<Category> Categories { get; set; } = new List<Category>();
    public ICollection<Article> Articles { get; set; } = new List<Article>();
    public ICollection<Download> Downloads { get; set; } = new List<Download>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<Sponsor> Sponsors { get; set; } = new List<Sponsor>();
}
