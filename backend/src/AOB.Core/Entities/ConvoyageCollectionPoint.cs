namespace AOB.Core.Entities;

public class ConvoyageCollectionPoint
{
    public int Id { get; set; }
    public int ConvoyageYearId { get; set; }
    public ConvoyageYear ConvoyageYear { get; set; } = null!;

    public string Name { get; set; } = "";
    public string? Location { get; set; }
    public int SortOrder { get; set; }
}
