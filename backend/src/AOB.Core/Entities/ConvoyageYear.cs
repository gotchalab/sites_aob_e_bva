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

    // Data/hora (UTC) de fecho das inscrições. Se estiver definida e já passou,
    // o formulário público bloqueia novas submissões.
    public DateTime? RegistrationClosesAt { get; set; }

    // Planeamento de transporte (F2)
    public int NumCargasAlvo { get; set; } = 23;
    public int CapacidadePorCarga { get; set; } = 20;
    public int MinPorCarga { get; set; } = 16;
    // JSON: { "ZONA": ["Nome1","Nome2"], ... } — round-robin por zona.
    public string TransportadorasJson { get; set; } = "{}";

    public ICollection<ConvoyageCollectionPoint> CollectionPoints { get; set; } = [];
    public ICollection<FormSubmission> Submissions { get; set; } = [];
    public ICollection<NomenclatureGroup> NomenclatureGroups { get; set; } = [];
    public ICollection<TransportCarga> TransportCargas { get; set; } = [];
}
