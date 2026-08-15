namespace AOB.Core.Entities;

public class NomenclatureClass
{
    public int Id { get; set; }

    public int NomenclatureGroupId { get; set; }
    public NomenclatureGroup NomenclatureGroup { get; set; } = null!;

    // Full BVA code including group prefix: "012/01", "055/03", "451/11".
    // Multiple rows may share the same Code (one per mutation variant, e.g.
    // "012/01 pallid green" + "012/01 pallid D green" + "012/01 pallid DD green").
    public string Code { get; set; } = "";

    // Single mutation variant text ("pallid green", "opaline-cinnamon violet",
    // "birds in pastel", "A. roseicollis marbled (group 6)").
    public string Mutation { get; set; } = "";

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Optional annotation ("male", "female", "fischeri-only", etc).
    public string? Notes { get; set; }

    public ICollection<ConvoyageBirdEntry> BirdEntries { get; set; } = [];
}
