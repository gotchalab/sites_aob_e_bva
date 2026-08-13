namespace AOB.Core.Entities;

public class ConvoyageYear
{
    public int Id { get; set; }
    public int SiteId { get; set; }
    public Site Site { get; set; } = null!;

    public int Year { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ConvoyageCollectionPoint> CollectionPoints { get; set; } = [];
    public ICollection<FormSubmission> Submissions { get; set; } = [];
}
