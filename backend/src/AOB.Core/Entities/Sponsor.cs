namespace AOB.Core.Entities;

public enum SponsorTier
{
    Principal = 0,
    Institucional = 1,
    Apoio = 2,
    Parceiro = 3,
}

public class Sponsor
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string LogoPath { get; set; } = "";
    public string? ClickUrl { get; set; }

    public SponsorTier Tier { get; set; } = SponsorTier.Parceiro;
    public bool IsPublished { get; set; } = true;
    public int SortOrder { get; set; }

    public int? LegacyId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
